import { WebTerminal } from "./web-terminal.js";
import { MIN_FONT_SIZE, MAX_FONT_SIZE } from "./terminal-sizing.js";

const byId = id => document.getElementById(id);
const workspace = byId("workspace");
const instancesSelect = byId("instances");
const views = new Map();
let instances = [];
let selected;
let nextView = 0;
let zIndex = 0;
let refreshing;
let shuttingDown = false;
const gridPresets = ["80x24", "80x25", "100x30", "120x40", "132x43", "160x50", "200x60", "240x80"];

// Playground diagnostics only; the mounted component has no window-global state.
window.webTerminalViews = views;
window.webTerminalStats = {};
window.webTerminalScreenText = "";

function report(message, level = "info") {
  byId("status").textContent = message;
  byId("status").dataset.level = level;
}

async function api(path, method = "GET", body) {
  const response = await fetch(path, {
    method,
    ...(body === undefined ? {} : { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) })
  });
  if (!response.ok) throw new Error(`${response.status}: ${await response.text() || response.statusText}`);
  return response.status === 204 ? null : response.json();
}

function action(button, operation) {
  button.addEventListener("click", async () => {
    button.disabled = true;
    try { await operation(); }
    catch (error) { report(error.message, "error"); }
    finally { button.disabled = false; }
  });
}

function updateInstanceControls() {
  const instance = instances.find(item => item.id === instancesSelect.value);
  byId("attach").disabled = !instance;
  byId("terminate").disabled = !instance;
  for (const id of ["pause", "rate", "batch", "apply-rate"]) byId(id).disabled = !instance || instance.scene === "shell";
  byId("pause").textContent = instance?.paused ? "Resume" : "Pause";
  byId("pause").setAttribute("aria-pressed", String(instance?.paused ?? false));
  for (const id of ["rate", "batch"]) {
    if (document.activeElement !== byId(id)) byId(id).value = instance?.[id] ?? (id === "rate" ? 30 : 20);
  }
}

async function refreshInstances(preferred) {
  if (shuttingDown) return;
  if (refreshing) {
    await refreshing;
    return refreshInstances(preferred);
  }
  refreshing = loadInstances(preferred);
  try {
    await refreshing;
  } finally {
    refreshing = undefined;
  }
}

async function loadInstances(preferred) {
  instances = await api("/api/terminals");
  const selectedId = preferred || instancesSelect.value;
  instancesSelect.replaceChildren(...instances.map(instance => {
    const option = document.createElement("option");
    option.value = instance.id;
    option.textContent = `${instance.name} - ${instance.columns}x${instance.rows}, ${instance.peerCount} peers`;
    return option;
  }));
  if (instances.some(instance => instance.id === selectedId)) instancesSelect.value = selectedId;
  updateInstanceControls();
}

function metrics(view, force = false) {
  if (!force && selected !== view) return;
  const stats = view.stats || {};
  window.webTerminalStats = stats;
  window.webTerminalScreenText = view.text || "";
  const number = (value, digits = 1) => Number(value || 0).toFixed(digits);
  byId("metric-fps").textContent = number(stats.fps);
  byId("metric-received").textContent = number(stats.receivedKBps);
  byId("metric-workload").textContent = number(stats.workloadMBps, 2);
  byId("metric-projection").textContent = number(stats.captureMs, 2);
  byId("metric-cpu").textContent = number(stats.rendererCpuMs, 2);
  byId("metric-cells").textContent = number(stats.lastChangedCells, 0);
  byId("metric-images").textContent = `${stats.imageCount || 0} / ${number(stats.textureBytes / 1048576, 2)}`;
  byId("metric-atlas").textContent = `${stats.atlasGlyphs || 0} / ${number(stats.atlasBytes / 1048576, 1)}`;
  byId("metric-uploads").textContent = number(stats.imageUploadBytes / 1048576, 2);
  byId("metric-revision").textContent = `${stats.revision || 0} / ${stats.fullFrames || 0}`;
  byId("warnings").textContent = (stats.warnings || []).join("\n");
  byId("screen-mirror").textContent = view.text || "";
  byId("selected-view").textContent = `${view.instance.name} / view ${view.id}`;
}

function selectView(view) {
  selected?.element.classList.remove("selected");
  selected = view;
  view.element.classList.add("selected");
  view.element.style.zIndex = String(++zIndex);
  if (instances.some(instance => instance.id === view.instance.id)) {
    instancesSelect.value = view.instance.id;
    updateInstanceControls();
  }
  metrics(view);
}

function updateSizingControls(view) {
  const terminal = view.terminal;
  const primary = terminal?.connected && terminal.peer.isPrimary;
  const sizing = terminal?.sizing;
  const auto = primary && sizing.mode === "auto";
  view.element.querySelector(".font-smaller").disabled = !auto || sizing.fontSize <= MIN_FONT_SIZE;
  view.element.querySelector(".font-larger").disabled = !auto || sizing.fontSize >= MAX_FONT_SIZE;
  view.element.querySelector(".font-size").textContent = auto ? `${sizing.fontSize}px` : "Fit";
  const resolution = view.element.querySelector(".view-resolution");
  resolution.disabled = !primary;
  const fixed = sizing?.mode === "fixed" ? `${sizing.columns}x${sizing.rows}` : undefined;
  const custom = resolution.querySelector('[value="custom"]');
  custom.textContent = fixed ? `Custom ${fixed}` : "Custom";
  resolution.value = !primary ? "follow" : auto ? "auto" : gridPresets.includes(fixed) ? fixed : "custom";
}

function changeSizing(view, sizing) {
  try { view.terminal.setSizing(sizing); }
  catch (error) { report(error.message, "error"); }
  finally { updateSizingControls(view); }
}

function moveAndResize(view) {
  const signal = view.controller.signal;
  for (const [handle, resize] of [[view.element.querySelector(".view-titlebar"), false], [view.element.querySelector(".resize-handle"), true]]) {
    let gesture;
    handle.addEventListener("pointerdown", event => {
      if (event.button !== 0 || event.target.closest("button")) return;
      event.preventDefault();
      selectView(view);
      gesture = {
        pointer: event.pointerId, x: event.clientX, y: event.clientY,
        left: view.element.offsetLeft, top: view.element.offsetTop,
        width: view.element.offsetWidth, height: view.element.offsetHeight
      };
      handle.setPointerCapture(event.pointerId);
    }, { signal });
    handle.addEventListener("pointermove", event => {
      if (!gesture || gesture.pointer !== event.pointerId) return;
      const dx = event.clientX - gesture.x;
      const dy = event.clientY - gesture.y;
      if (resize) {
        view.element.style.width = `${Math.max(240, Math.min(3200, gesture.width + dx))}px`;
        view.element.style.height = `${Math.max(180, Math.min(2200, gesture.height + dy))}px`;
      } else {
        view.element.style.left = `${Math.max(0, gesture.left + dx)}px`;
        view.element.style.top = `${Math.max(0, gesture.top + dy)}px`;
      }
    }, { signal });
    const end = event => {
      if (!gesture || gesture.pointer !== event.pointerId) return;
      gesture = undefined;
      if (handle.hasPointerCapture(event.pointerId)) handle.releasePointerCapture(event.pointerId);
    };
    handle.addEventListener("pointerup", end, { signal });
    handle.addEventListener("pointercancel", end, { signal });
    handle.addEventListener("lostpointercapture", () => { gesture = undefined; }, { signal });
  }
}

function closeView(view) {
  view.controller.abort();
  view.terminal?.dispose();
  view.element.remove();
  views.delete(view.id);
  if (selected === view) {
    selected = undefined;
    const remaining = [...views.values()].at(-1);
    if (remaining) selectView(remaining);
    else {
      metrics({ instance: { name: "" }, id: "", stats: {}, text: "" }, true);
      byId("selected-view").textContent = "No view selected";
      window.webTerminalStats = {};
      window.webTerminalScreenText = "";
    }
  }
  workspace.classList.toggle("empty", views.size === 0);
  refreshInstances().catch(error => report(error.message, "error"));
}

async function openView(instance, { primary = false, thumbnail = false } = {}) {
  if (views.size >= 8) throw new Error("Close a view before opening another (eight views per playground)");
  const id = String(++nextView);
  const element = document.createElement("section");
  element.className = "terminal-window";
  element.tabIndex = -1;
  element.dataset.view = id;
  element.dataset.instance = instance.id;
  element.setAttribute("aria-label", `${instance.name}, view ${id}`);
  element.innerHTML = `
    <header class="view-titlebar">
      <span class="view-title"></span><span class="view-role">Joining</span>
      <button class="close-view" title="Close this view; keep the terminal running" aria-label="Close view">Close</button>
    </header>
    <div class="view-tools">
      <button class="take-primary" disabled>Take primary</button>
      <button class="thumbnail">Thumbnail</button>
      <button class="resync" disabled>Resync</button>
      <span class="view-grid"></span>
    </div>
    <div class="terminal-mount"></div>
    <footer class="view-footer">
      <span class="view-status">Initializing WebGPU...</span>
      <button class="font-smaller" disabled title="Smaller text; more cells (Auto mode)" aria-label="Decrease terminal font size">-</button>
      <span class="font-size" title="Requested font size in Auto mode; fixed grids scale to fit">Fit</span>
      <button class="font-larger" disabled title="Larger text; fewer cells (Auto mode)" aria-label="Increase terminal font size">+</button>
      <select class="view-resolution" disabled aria-label="Terminal resolution" title="Auto uses the font size; presets hold the grid and scale to fit">
        <option value="follow" hidden>Follow primary</option>
        <option value="auto">Auto</option>
        ${gridPresets.map(grid => `<option value="${grid}">${grid}</option>`).join("")}
        <option value="custom" hidden>Custom</option>
      </select>
    </footer>
    <span class="resize-handle" title="Drag to resize view" aria-hidden="true"></span>`;
  element.querySelector(".view-title").textContent = `${instance.name} / ${id}`;
  const width = thumbnail ? 320 : Math.min(1040, Math.max(300, workspace.clientWidth - 64));
  const height = thumbnail ? 240 : Math.min(660, Math.max(300, workspace.clientHeight - 64));
  element.style.width = `${width}px`;
  element.style.height = `${height}px`;
  element.style.left = `${thumbnail ? Math.max(0, workspace.clientWidth - width - 24) : 24 + (views.size % 5) * 32}px`;
  element.style.top = `${24 + (views.size % 5) * (thumbnail ? 48 : 32)}px`;
  workspace.append(element);
  workspace.classList.remove("empty");
  const view = { id, instance, element, controller: new AbortController(), stats: {}, text: "" };
  views.set(id, view);
  selectView(view);
  moveAndResize(view);
  element.addEventListener("pointerdown", event => {
    if (selected !== view) selectView(view);
    if (!event.target.closest("button, select, input")) {
      if (view.terminal) view.terminal.focus();
      else element.focus({ preventScroll: true });
    }
  }, { capture: true, signal: view.controller.signal });
  element.addEventListener("focusin", () => {
    if (selected !== view) selectView(view);
  }, { signal: view.controller.signal });
  element.querySelector(".close-view").addEventListener("click", () => closeView(view), { signal: view.controller.signal });
  action(element.querySelector(".thumbnail"), () => openView(instance, { thumbnail: true }));
  action(element.querySelector(".take-primary"), () => view.terminal.requestPrimary());
  action(element.querySelector(".resync"), () => view.terminal.resync());
  element.querySelector(".font-smaller").addEventListener("click", () =>
    changeSizing(view, { mode: "auto", fontSize: view.terminal.sizing.fontSize - 1 }), { signal: view.controller.signal });
  element.querySelector(".font-larger").addEventListener("click", () =>
    changeSizing(view, { mode: "auto", fontSize: view.terminal.sizing.fontSize + 1 }), { signal: view.controller.signal });
  element.querySelector(".view-resolution").addEventListener("change", event => {
    if (event.target.value === "auto") changeSizing(view, { mode: "auto" });
    else {
      const [columns, rows] = event.target.value.split("x").map(Number);
      changeSizing(view, { mode: "fixed", columns, rows });
    }
  }, { signal: view.controller.signal });
  const url = new URL("/ws", location.href);
  url.search = new URLSearchParams({ instance: instance.id, name: `Web view ${id}` }).toString();
  try {
    view.terminal = await WebTerminal.mount(element.querySelector(".terminal-mount"), {
      url, signal: view.controller.signal,
      scale: byId("scale").value === "auto" ? "auto" : Number(byId("scale").value),
      font: byId("font").value === "monospace" ? { family: "monospace" } : undefined,
      label: `${instance.name}, view ${id}, terminal input`,
      onStatus(message, level) {
        const status = element.querySelector(".view-status");
        status.textContent = message;
        status.title = message;
        status.dataset.level = level;
        if (level === "error") {
          element.dataset.primary = "false";
          element.querySelector(".take-primary").disabled = true;
          element.querySelector(".resync").disabled = true;
          element.querySelector(".view-role").textContent = "Disconnected";
        }
        updateSizingControls(view);
      },
      onGeometry(geometry) {
        element.querySelector(".view-grid").textContent = `${geometry.columns}x${geometry.rows}`;
      },
      onRoleChange(peer) {
        element.dataset.primary = String(peer.isPrimary);
        element.dataset.peer = peer.id ?? "";
        element.dataset.primaryPeer = peer.primaryId ?? "";
        element.querySelector(".view-role").textContent = peer.isPrimary ? "Primary" : peer.id === null ? "Joining" : "Secondary";
        element.querySelector(".view-role").title = `Peer: ${peer.id ?? "local"}; primary: ${peer.primaryId ?? "unassigned"}`;
        element.querySelector(".take-primary").disabled = peer.isPrimary || peer.id === null || !view.terminal?.connected;
        updateSizingControls(view);
      },
      onSizingChange() { updateSizingControls(view); },
      onViewportChange(viewport) {
        view.viewport = viewport;
        element.dataset.following = String(viewport.following);
      },
      onSelectionChange(selection) {
        view.selection = selection;
        element.dataset.selection = selection.status;
      },
      onStats(stats, text) {
        view.stats = stats;
        if (text !== undefined) view.text = text;
        metrics(view);
      }
    });
    if (view.controller.signal.aborted) {
      view.terminal.dispose();
      return;
    }
    element.querySelector(".resync").disabled = false;
    element.querySelector(".take-primary").disabled = view.terminal.peer.isPrimary || view.terminal.peer.id === null;
    updateSizingControls(view);
    if (primary) view.terminal.requestPrimary();
    if (selected === view && (!thumbnail || element.contains(document.activeElement))) view.terminal.focus();
    report("Use -/+ in Auto mode to change text size, or choose a fixed grid. Secondary views follow the primary.");
    await refreshInstances(instance.id);
  } catch (error) {
    if (view.controller.signal.aborted) return;
    element.querySelector(".view-status").textContent = error.message;
    element.querySelector(".view-status").dataset.level = "error";
    throw error;
  }
}

async function createInstance() {
  const instance = await api("/api/terminals", "POST", { scene: byId("scene").value, columns: 100, rows: 30 });
  await refreshInstances(instance.id);
  await openView(instance, { primary: true });
}

action(byId("create"), createInstance);
action(byId("attach"), () => {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance) throw new Error("Choose an existing terminal instance");
  return openView(instance);
});
action(byId("terminate"), async () => {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance || !window.confirm(`End ${instance.name}? This stops its workload and disconnects every attached view.`)) return;
  await api(`/api/terminals/${encodeURIComponent(instance.id)}`, "DELETE");
  for (const view of [...views.values()]) if (view.instance.id === instance.id) closeView(view);
  await refreshInstances();
});
action(byId("pause"), async () => {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance) return;
  await api(`/api/terminals/${encodeURIComponent(instance.id)}/controls`, "POST", { paused: !instance.paused });
  await refreshInstances();
});
action(byId("apply-rate"), async () => {
  if (!byId("rate").reportValidity() || !byId("batch").reportValidity()) return;
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance || instance.scene === "shell") return;
  await api(`/api/terminals/${encodeURIComponent(instance.id)}/controls`, "POST", {
    rate: Number(byId("rate").value), batch: Number(byId("batch").value)
  });
  await refreshInstances();
});
instancesSelect.addEventListener("change", updateInstanceControls);
const refreshTimer = setInterval(() => {
  if (!refreshing) refreshInstances().catch(error => report(error.message, "error"));
}, 2000);
window.addEventListener("pagehide", () => {
  shuttingDown = true;
  clearInterval(refreshTimer);
  for (const view of views.values()) {
    view.controller.abort();
    view.terminal?.dispose();
  }
});

try {
  if (!window.isSecureContext || !navigator.gpu) {
    byId("create").disabled = true;
    throw new Error("WebTerminal requires a WebGPU-enabled browser over HTTPS or localhost");
  }
  const parameters = new URLSearchParams(location.search);
  const scene = parameters.get("scene");
  const requestedScene = [...byId("scene").options].some(option => option.value === scene);
  if (requestedScene) byId("scene").value = scene;
  const scale = parameters.get("scale");
  if ([...byId("scale").options].some(option => option.value === scale)) byId("scale").value = scale;
  await refreshInstances();
  if (parameters.get("empty") !== "1") {
    if (instances.length && !requestedScene) await openView(instances[0]);
    else await createInstance();
  }
} catch (error) {
  report(error.message, "error");
}
