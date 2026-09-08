import assert from "node:assert/strict";
import { test } from "node:test";
import { Worker as NodeWorker } from "node:worker_threads";
import { setImmediate as nextTurn } from "node:timers/promises";
import { WebTerminal } from "../dist/web-terminal.js";

class Target {
  listeners = new Map();
  addEventListener(type, callback) {
    const handlers = this.listeners.get(type) ?? [];
    handlers.push(callback);
    this.listeners.set(type, handlers);
  }
  dispatchEvent(event) {
    for (const callback of this.listeners.get(event.type) ?? []) callback(event);
    return !event.defaultPrevented;
  }
}

class Element extends Target {
  style = {};
  dataset = {};
  attributes = new Map();
  children = [];
  selectors = new Map();
  textContent = "";
  title = "";
  setAttribute(name, value) { this.attributes.set(name, value); }
  getAttribute(name) { return this.attributes.get(name); }
  attachShadow() { return this.shadowRoot = new Element(); }
  querySelector(selector) {
    if (!this.selectors.has(selector)) this.selectors.set(selector, new Element());
    return this.selectors.get(selector);
  }
  append(...elements) {
    this.children.push(...elements);
    for (const element of elements) element.parent = this;
  }
  replaceChildren(...elements) { this.children = []; this.append(...elements); }
  remove() {
    if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this);
  }
  transferControlToOffscreen() { return {}; }
  getBoundingClientRect() { return { width: 100, height: 100, left: 0, top: 0 }; }
}

class WorkerBridge extends Target {
  worker = new NodeWorker(new URL("./fixtures/title-worker.mjs", import.meta.url));
  pending = new Map();
  outputs = [];
  commands = [];
  errors = [];
  serial = 0;
  constructor() {
    super();
    this.worker.on("message", envelope => {
      if (envelope.type === "output") {
        this.outputs.push(envelope.message);
        // Browser listener errors are reported to the host, not back into the worker.
        try { this.deliver(envelope.message); }
        catch (error) { this.errors.push(error); }
      } else if (envelope.type === "sent") {
        this.commands.push(envelope.command);
      } else {
        const pending = this.pending.get(envelope.id);
        if (!pending) return;
        this.pending.delete(envelope.id);
        if (envelope.type === "failure") pending.reject(new Error(envelope.error));
        else pending.resolve();
      }
    });
    this.worker.on("error", error => {
      for (const pending of this.pending.values()) pending.reject(error);
      this.pending.clear();
    });
    this.worker.on("exit", () => {
      for (const pending of this.pending.values()) pending.reject(new Error("Worker terminated"));
      this.pending.clear();
    });
  }
  deliver(message) { this.dispatchEvent({ type: "message", data: message }); }
  request(action, details = {}) {
    const id = ++this.serial;
    const pending = Promise.withResolvers();
    this.pending.set(id, pending);
    this.worker.postMessage({ id, action, ...details });
    return pending.promise;
  }
  postMessage(message) { this.request("input", { message }).catch(() => {}); }
  terminate() { return this.worker.terminate(); }
}

function browser(t) {
  const workers = [];
  const globals = {
    Element, HTMLElement: Element, HTMLDivElement: Element, HTMLCanvasElement: Element,
    HTMLTextAreaElement: Element, HTMLButtonElement: Element, HTMLSpanElement: Element,
    ResizeObserver: class { observe() {} disconnect() {} },
    OffscreenCanvas: class {},
    Worker: class extends WorkerBridge {
      constructor() { super(); workers.push(this); }
    },
    document: { createElement: () => new Element(), title: "Host document", activeElement: null },
    location: { href: "https://example.test/terminal" },
    getComputedStyle: element => ({ width: element.style.width ?? "0", height: element.style.height ?? "0" })
  };
  globals.window = Object.assign(new Target(), {
    Worker: globals.Worker, ResizeObserver: globals.ResizeObserver,
    OffscreenCanvas: globals.OffscreenCanvas, devicePixelRatio: 1
  });
  const saved = new Map(Object.keys(globals).map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  for (const [key, value] of Object.entries(globals))
    Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  t.after(async () => {
    for (const worker of workers) worker.disposeView?.();
    await Promise.all(workers.map(worker => worker.terminate()));
    for (const [key, descriptor] of saved) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else delete globalThis[key];
    }
  });
  return workers;
}

function frame({ title = "", revision = 1, full = revision === 1, baseRevision = full ? 0 : revision - 1,
  peer = { id: null, primaryId: null, isPrimary: true }, ...overrides } = {}) {
  const metadata = {
    version: 1, title, revision, full, baseRevision, peer,
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
    cursor: { visible: true, x: 0, y: 0, shape: 1 },
    history: null, images: [], retainedImages: [], placements: [], warnings: [], hyperlinks: [],
    stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }, ...overrides
  };
  const json = new TextEncoder().encode(JSON.stringify(metadata));
  const bytes = new Uint8Array(12 + json.length + (full ? 23 : 0));
  const view = new DataView(bytes.buffer);
  view.setUint32(0, 0x31545748, true);
  view.setUint32(4, json.length, true);
  bytes.set(json, 8);
  if (full) {
    const offset = 12 + json.length;
    view.setUint32(offset - 4, 1, true);
    view.setUint8(offset + 18, 1);
    view.setUint16(offset + 20, 1, true);
    view.setUint8(offset + 22, 65);
  }
  return bytes.buffer;
}

async function mounting(t, workers, options = {}) {
  const container = new Element();
  const notices = [];
  let handle;
  let settled = false;
  const promise = WebTerminal.mount(container, {
    url: "/ws", renderer: "webgl2", label: "Host input label", ...options,
    onSelectionUI(event) {
      void event.detail.runAction(context => { handle = context.terminal; });
    },
    onTitleChange(title) {
      assert.ok(handle, "public input action provides the in-flight handle");
      assert.equal(handle.title, title, "getter must update before the callback");
      notices.push({ title, settled });
      options.onTitleChange?.(title);
    }
  });
  promise.then(() => { settled = true; }, () => { settled = true; });
  await nextTurn();
  const worker = workers.at(-1);
  await worker.request("flush");
  worker.disposeView = () => handle?.dispose();
  return { worker, promise, notices, container, get handle() { return handle; }, get settled() { return settled; } };
}

async function present(view, state) {
  await view.worker.request("frame", { buffer: frame(state) });
  await view.worker.request("draw");
}

for (const initial of ["", "shell; 世界 😀"]) {
  test(`Mount publishes initial title ${JSON.stringify(initial)} only after presentation, then distinct changes`, async t => {
    const workers = browser(t);
    const view = await mounting(t, workers);
    await view.worker.request("open");
    await view.worker.request("hold");
    await view.worker.request("frame", { buffer: frame({ title: initial }) });
    assert.deepEqual(view.notices, []);
    await view.worker.request("draw");
    assert.deepEqual(view.notices, []);
    assert.equal(view.settled, false);
    await view.worker.request("release");
    const terminal = await view.promise;
    assert.deepEqual(view.notices, [{ title: initial, settled: false }]);
    assert.equal(terminal.title, initial);
    assert.throws(() => { terminal.title = "not writable"; }, TypeError);

    await present(view, { title: initial, revision: 2 });
    terminal.resync();
    await present(view, { title: initial, revision: 3, full: true });
    await view.worker.request("pulse");
    await view.worker.request("input", { message: { type: "viewport", width: 100, height: 100 } });
    await view.worker.request("draw");
    assert.equal(view.notices.length, 1);
    assert.equal(view.worker.outputs.filter(message => message.type === "geometry").length, 3);

    const literal = '<img src=x onerror="alert(1)">\u202e title';
    await present(view, { title: literal, revision: 4 });
    await present(view, { title: "", revision: 5 });
    await present(view, { title: "", revision: 6 });
    assert.deepEqual(view.notices.map(notice => notice.title), [initial, literal, ""]);
    assert.equal(terminal.title, "");
    assert.equal(document.title, "Host document");
    assert.equal(terminal.element.shadowRoot.querySelector("textarea").getAttribute("aria-label"), "Host input label");
    assert.equal(view.container.textContent, "");
    assert.deepEqual(view.worker.errors, []);
    terminal.dispose();
  });
}

test("Relay waits for authoritative peer state, independent views and remount deliver their own initial titles", async t => {
  const workers = browser(t);
  const relay = await mounting(t, workers);
  await relay.worker.request("open");
  const disconnected = { id: null, primaryId: null, isPrimary: false };
  await present(relay, { title: "", peer: disconnected });
  await relay.worker.request("pulse");
  assert.deepEqual(relay.notices, []);
  assert.equal(relay.settled, false);
  const peer = { id: "relay", primaryId: "producer", isPrimary: false };
  await present(relay, { title: "already running", revision: 2, peer });
  const terminal = await relay.promise;
  assert.deepEqual(relay.notices, [{ title: "already running", settled: false }]);

  const independent = await mounting(t, workers);
  await independent.worker.request("open");
  await present(independent, { title: "different workload" });
  const other = await independent.promise;
  assert.equal(other.title, "different workload");
  assert.equal(terminal.title, "already running");

  await relay.worker.request("disconnect");
  assert.equal(terminal.connected, false);
  assert.equal(terminal.title, "already running");
  terminal.dispose();
  assert.equal(terminal.title, "already running");
  assert.equal(relay.notices.length, 1);
  const remount = await mounting(t, workers);
  await remount.worker.request("open");
  await present(remount, { title: "already running", peer });
  await remount.promise;
  assert.deepEqual(remount.notices, [{ title: "already running", settled: false }]);
});

test("Dropped deltas and invalid frames never publish their titles or clear the last known value", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, { title: "accepted" });
  const terminal = await view.promise;
  await present(view, { title: "discarded", revision: 2, baseRevision: 99 });
  assert.equal(terminal.title, "accepted");
  assert.deepEqual(view.worker.commands.slice(-2), [{ type: "ack", revision: 2 }, { type: "resync" }]);
  await present(view, { title: "accepted", revision: 3, full: true });
  await present(view, { title: "invalid\u0007", revision: 4 });
  assert.equal(terminal.title, "accepted");
  assert.equal(terminal.connected, false);
  assert.deepEqual(view.notices.map(notice => notice.title), ["accepted"]);
  assert.equal(view.worker.outputs.filter(message => message.type === "geometry").length, 2);
  assert.ok(view.worker.outputs.some(message => message.type === "status" && message.level === "error"));
});

test("Disposal and abort retain titles and ignore already queued geometry messages", async t => {
  const workers = browser(t);
  for (const abort of [false, true]) {
    const controller = new AbortController();
    const view = await mounting(t, workers, { signal: controller.signal });
    await view.worker.request("open");
    await present(view, { title: "retained" });
    const terminal = await view.promise;
    await view.worker.request("hold");
    await view.worker.request("frame", { buffer: frame({ title: "never presented", revision: 2 }) });
    await view.worker.request("draw");
    if (abort) controller.abort();
    else terminal.dispose();
    const geometry = view.worker.outputs.find(message => message.type === "geometry");
    view.worker.deliver({ ...geometry, title: "queued after disposal", revision: 3 });
    assert.equal(terminal.title, "retained");
    assert.deepEqual(view.notices.map(notice => notice.title), ["retained"]);
    assert.equal(view.container.children.length, 0);
  }
});

test("Aborting before initial presentation rejects mount without a title notification", async t => {
  const workers = browser(t);
  const controller = new AbortController();
  const view = await mounting(t, workers, { signal: controller.signal });
  await view.worker.request("open");
  await view.worker.request("frame", { buffer: frame({ title: "not presented" }) });
  controller.abort();
  await assert.rejects(view.promise, { name: "AbortError" });
  assert.deepEqual(view.notices, []);
  assert.equal(view.handle.title, "");
});

test("A host callback can abort during geometry delivery without a subsequent title callback", async t => {
  const workers = browser(t);
  const controller = new AbortController();
  const view = await mounting(t, workers, {
    signal: controller.signal, onGeometry() { controller.abort(); }
  });
  await view.worker.request("open");
  // Deliver the same geometry message shape the real worker emits, reentrantly aborting mounting.
  view.worker.deliver({
    type: "geometry", title: "not notified", revision: 1, text: "", history: null, hyperlinks: [],
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
    peer: { id: null, primaryId: null, isPrimary: true }
  });
  await assert.rejects(view.promise, { name: "AbortError" });
  assert.deepEqual(view.notices, []);
});

test("Title callback errors reach the host without retrying a delivered value", async t => {
  const workers = browser(t);
  const failure = new Error("Host callback failed");
  const view = await mounting(t, workers, { onTitleChange() { throw failure; } });
  await view.worker.request("open");
  await present(view, { title: "delivered" });
  const terminal = await view.promise;
  assert.equal(terminal.title, "delivered");
  assert.deepEqual(view.worker.errors, [failure]);
  await present(view, { title: "delivered", revision: 2 });
  assert.equal(view.notices.length, 1);
  assert.deepEqual(view.worker.errors, [failure]);
});
