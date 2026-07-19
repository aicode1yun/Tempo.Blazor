// C#↔engine command contract: every command id that TmDocumentEditor routes into
// the canvas engine (extracted from the C# sources by
// scripts/extract-canvas-command-ids.mjs) must be handled by the engine — either
// registered in the command runtime dispatcher, or matched by an entry-level
// special command in entry.mjs. A miss means the invoking UI is a silent no-op
// (the setFullscreen/insertTable class of regressions).
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCanvasCommandRuntime } from './commands/dispatcher.mjs';
import { extractCanvasCommandIds, findRepoRoot } from '../../../../../scripts/extract-canvas-command-ids.mjs';

// Commands known to be routed by C# but NOT yet implemented in the engine.
// Phases 3–9 of the command-layer plan implement them one by one — every phase
// MUST remove its ids here, and the allowlist ends empty. Keys are
// dispatcher-normalized (trim + lowercase).
const KNOWN_MISSING = new Set([
  // Audit 2026-07-19 (plan fáze 3–9): toolbar/context-menu commands that are
  // currently silent no-ops in the engine.
  'inserttable',
  'deletetable',
  'toggletableheaderrow',
  'settableproperties',
  'setcellproperties',
  'deletepagebreak',
  'setprotectionmode',
  'opentokenmenu',
  // Discovered by this contract test beyond the manual audit: ribbon registry
  // commands routed through ExecuteTableRuntimeCommandAsync/ExecuteImageRuntimeCommandAsync
  // that the engine never registered (reported to the plan as remaining work).
  'tableproperties',
  'cellproperties',
  'replaceimage',
  'setimagelink',
]);

const dispatcherNormalize = id => String(id).trim().toLowerCase();
const entryNormalize = id => String(id).replace(/[\s_-]/g, '').toLowerCase();

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = findRepoRoot(here);

function buildRuntimeCommandSet() {
  const runtime = createCanvasCommandRuntime({
    getModel: () => ({ blocks: [] }),
    getSelection: () => null,
    commit: () => {},
  });
  return new Set(runtime.listCommands());
}

/** Entry-level special command ids, harvested from entry.mjs's own normalization
 * comparisons (`normalized === '…'` and `['…','…'].includes(normalized)`). */
function buildEntryCommandSet() {
  const entrySource = readFileSync(path.join(here, 'entry.mjs'), 'utf8');
  const ids = new Set();
  for (const match of entrySource.matchAll(/normalized\s*===\s*'([^']+)'/g)) {
    ids.add(match[1]);
  }
  for (const match of entrySource.matchAll(/\[((?:\s*'[^']+'\s*,?)+)\]\.includes\(normalized\)/g)) {
    for (const literal of match[1].matchAll(/'([^']+)'/g)) {
      ids.add(literal[1]);
    }
  }
  return ids;
}

function isHandled(id, runtimeCommands, entryCommands) {
  return runtimeCommands.has(dispatcherNormalize(id)) || entryCommands.has(entryNormalize(id));
}

test('every canvas command id routed from C# is registered in the engine', () => {
  const routedIds = extractCanvasCommandIds(repoRoot);
  assert.ok(routedIds.length > 50, `Extractor sanity check: expected >50 routed ids, got ${routedIds.length}.`);

  const runtimeCommands = buildRuntimeCommandSet();
  assert.ok(runtimeCommands.size > 200, `Runtime sanity check: expected >200 registered commands, got ${runtimeCommands.size}.`);
  const entryCommands = buildEntryCommandSet();
  assert.ok(entryCommands.size > 5, `Entry sanity check: expected >5 entry-level ids, got ${entryCommands.size}.`);

  const missing = routedIds.filter(id =>
    !isHandled(id, runtimeCommands, entryCommands)
    && !KNOWN_MISSING.has(dispatcherNormalize(id)));

  assert.deepEqual(
    missing,
    [],
    `C# routes command ids the canvas engine does not handle (silent UI no-ops):\n`
    + missing.map(id => `  ${id}`).join('\n')
    + '\nRegister the command in the engine, or (only during the command-layer plan) add it to KNOWN_MISSING.');
});

test('KNOWN_MISSING allowlist contains only ids that are still actually missing', () => {
  const runtimeCommands = buildRuntimeCommandSet();
  const entryCommands = buildEntryCommandSet();
  const stale = [...KNOWN_MISSING].filter(id => isHandled(id, runtimeCommands, entryCommands));
  assert.deepEqual(
    stale,
    [],
    `These KNOWN_MISSING entries are now handled by the engine — remove them from the allowlist:\n`
    + stale.map(id => `  ${id}`).join('\n'));
});
