import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { terminalThemeCss } from "../wwwroot/terminal-theme.js";
import { WebTerminal } from "../wwwroot/web-terminal.js";

test("Standalone component palette defaults match the playground Clawpilot colors", () => {
  const playground = readFileSync(new URL("../wwwroot/index.html", import.meta.url), "utf8");
  const colors = ["surface", "text", "text-muted", "border-strong", "accent", "accent-soft", "danger"];
  for (const name of colors) {
    const values = [...playground.matchAll(new RegExp(`--cp-${name}: ([^;]+);`, "gu"))].map(match => match[1]);
    assert.equal(values.length, 2, `${name} must have light and dark palettes`);
    for (const value of values) assert.ok(terminalThemeCss.includes(`--cp-terminal-${name}: ${value};`));
    assert.ok(terminalThemeCss.includes(`--cp-view-${name}: var(--cp-${name}, var(--cp-terminal-${name}));`));
    assert.ok(!terminalThemeCss.includes(`--cp-${name}:`), "Do not shadow inherited embedding theme tokens");
  }
  assert.match(terminalThemeCss, /@media \(prefers-color-scheme: dark\)/u);
  assert.match(terminalThemeCss, /:host-context\(\[data-theme="light"\]\)/u);
  assert.match(terminalThemeCss, /:host-context\(\[data-theme="dark"\]\)/u);
});

test("Inspection font has an embedding override and the Clawpilot default stack", () => {
  assert.ok(terminalThemeCss.includes('--cp-terminal-font-family: "Segoe UI", Aptos, Calibri, -apple-system, BlinkMacSystemFont, sans-serif;'));
  assert.ok(terminalThemeCss.includes("--cp-view-font-family: var(--cp-font-family, var(--cp-terminal-font-family));"));
});

test("A newly constructed view follows live before any worker history response", () => {
  const original = globalThis.document;
  globalThis.document = { createElement: () => ({ style: {} }) };
  try {
    const terminal = new WebTerminal({});
    assert.deepEqual(terminal.viewport, {
      available: false, following: true, pending: false, followTail: true, offset: 0
    });
    assert.equal(terminal.selection.active, false);
    assert.equal(terminal.selection.pending, false);
    assert.equal(terminal.selection.status, "unavailable");
  } finally {
    if (original === undefined) delete globalThis.document;
    else globalThis.document = original;
  }
});
