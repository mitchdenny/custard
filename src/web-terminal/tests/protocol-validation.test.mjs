import assert from "node:assert/strict";
import { test } from "node:test";
import { decodeFrame } from "../dist/protocol.js";

function metadata() {
  return {
    version: 1, revision: 1, baseRevision: 0, full: true,
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
    peer: { id: null, primaryId: null, isPrimary: true },
    cursor: { x: 0, y: 0, visible: false, shape: "SteadyBlock" }, history: null,
    images: [], retainedImages: [], placements: [], warnings: [], hyperlinks: [],
    stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }
  };
}

function frame(state) {
  const json = new TextEncoder().encode(JSON.stringify(state));
  const buffer = new ArrayBuffer(8 + json.byteLength + 4 + 23);
  const view = new DataView(buffer);
  view.setUint32(0, 0x31545748, true);
  view.setUint32(4, json.byteLength, true);
  new Uint8Array(buffer, 8, json.byteLength).set(json);
  const offset = 8 + json.byteLength;
  view.setUint32(offset, 1, true);
  view.setUint8(offset + 4 + 18, 1);
  view.setUint16(offset + 4 + 20, 1, true);
  view.setUint8(offset + 4 + 22, 65);
  return buffer;
}

test("Typed protocol decoding preserves cursor normalization and binary cells", () => {
  const decoded = decodeFrame(frame(metadata()));
  assert.equal(decoded.metadata.cursor.shape, 2);
  assert.equal(decoded.cells.length, 1);
  assert.equal(decoded.cells[0].text, "A");
  assert.equal(decoded.cells[0].width, 1);
});

test("Typed protocol boundary still rejects malformed metadata before use", () => {
  for (const value of [null, [], false, "metadata"]) {
    assert.throws(() => decodeFrame(frame(value)));
  }
  for (const [field, invalid] of [
    ["columns", "1"], ["rows", -1], ["cellWidth", 11], ["mouseTracking", 1],
    ["cursor", null], ["peer", []], ["stats", {}], ["history", {}], ["warnings", [1]],
    ["images", [null]], ["retainedImages", ["duplicate", "duplicate"]], ["placements", [null]]
  ]) {
    const state = metadata();
    state[field] = invalid;
    assert.throws(() => decodeFrame(frame(state)), field);
  }
});

test("Typed protocol boundary rejects every truncated frame and trailing payload", () => {
  const buffer = frame(metadata());
  for (let length = 0; length < buffer.byteLength; length++) {
    assert.throws(() => decodeFrame(buffer.slice(0, length)), `length ${length}`);
  }
  const trailing = new Uint8Array(buffer.byteLength + 1);
  trailing.set(new Uint8Array(buffer));
  assert.throws(() => decodeFrame(trailing.buffer), /Image payload length mismatch/u);
});

test("Hyperlink metadata preserves URI data and rejects malformed or overlapping ranges", () => {
  const link = { row: 0, startColumn: 0, endColumn: 1, uri: "https://example.com" };
  assert.deepEqual(decodeFrame(frame({ ...metadata(), hyperlinks: [link] })).metadata.hyperlinks, [link]);
  for (const hyperlinks of [undefined, null, {}, [null], [link, link],
    [{ ...link, row: 1 }], [{ ...link, row: -1 }], [{ ...link, row: .5 }],
    [{ ...link, startColumn: -1 }], [{ ...link, startColumn: "0" }],
    [{ ...link, endColumn: 0 }], [{ ...link, endColumn: 2 }],
    [{ ...link, uri: "" }], [{ ...link, uri: null }], [{ ...link, uri: 42 }]]) {
    assert.throws(() => decodeFrame(frame({ ...metadata(), hyperlinks })));
  }
});
