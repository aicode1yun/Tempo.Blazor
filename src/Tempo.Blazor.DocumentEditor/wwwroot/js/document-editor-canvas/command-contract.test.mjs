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

// The command-layer plan (phases 3–10, completed 2026-07-19) drove the temporary
// KNOWN_MISSING allowlist to empty and removed it: the contract is now HARD.
// Every id C# routes into the engine must be handled — fix the engine or stop
// routing the id; do not reintroduce an allowlist.

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

  const missing = routedIds.filter(id => !isHandled(id, runtimeCommands, entryCommands));

  assert.deepEqual(
    missing,
    [],
    `C# routes command ids the canvas engine does not handle (silent UI no-ops):\n`
    + missing.map(id => `  ${id}`).join('\n')
    + '\nRegister the command in the engine, or stop routing the id from C#.');
});
