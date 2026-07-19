#!/usr/bin/env node
// Extracts every canvas-engine command id literal that C# routes into the canvas
// engine, for the C#↔engine command contract test (command-contract.test.mjs).
//
// Sources scanned: src/Tempo.Blazor.DocumentEditor/**/*.cs + *.razor (bin/obj
// excluded). A command id is any string literal inside the FIRST argument of a
// call to one of the routing wrappers below — including ternaries like
// RouteToCanvasEngineAsync(x ? "insertEndnote" : "insertFootnote", …).
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

// Every method whose first argument is a canvas command id. RegisterTableRuntimeCommand
// and RunTableContextCommandAsync carry the table ribbon/context-menu commands;
// the image/content-control wrappers forward their literal straight to ExecCommandAsync.
export const COMMAND_ROUTING_METHODS = [
  'RouteToCanvasEngineAsync',
  'ExecCommandAsync',
  'ExecuteTableRuntimeCommandAsync',
  'RunTableContextCommandAsync',
  'RegisterTableRuntimeCommand',
  'ExecuteImageRuntimeCommandAsync',
  'RunCanvasContentControlCommandAsync',
];

const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules']);

export function collectSourceFiles(documentEditorRoot) {
  const files = [];
  const walk = dir => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (!SKIP_DIRS.has(entry.name)) {
          walk(full);
        }
      } else if (entry.isFile() && (entry.name.endsWith('.cs') || entry.name.endsWith('.razor'))) {
        files.push(full);
      }
    }
  };
  walk(documentEditorRoot);
  return files.sort();
}

/** Returns the source slice of the FIRST argument of the call starting at openParenIndex. */
function firstArgumentSlice(source, openParenIndex) {
  let depth = 0;
  let inString = false;
  for (let index = openParenIndex; index < source.length; index++) {
    const char = source[index];
    if (inString) {
      if (char === '\\') {
        index++;
      } else if (char === '"') {
        inString = false;
      }
      continue;
    }
    if (char === '"') {
      inString = true;
    } else if (char === '(') {
      depth++;
    } else if (char === ')') {
      depth--;
      if (depth === 0) {
        return source.slice(openParenIndex + 1, index);
      }
    } else if (char === ',' && depth === 1) {
      return source.slice(openParenIndex + 1, index);
    }
  }
  return '';
}

/** All command-id string literals routed by the wrappers in one source text. */
export function extractCommandIdsFromSource(source) {
  const ids = new Set();
  for (const method of COMMAND_ROUTING_METHODS) {
    let searchFrom = 0;
    for (;;) {
      const callIndex = source.indexOf(`${method}(`, searchFrom);
      if (callIndex < 0) {
        break;
      }
      // Skip the method's own declaration ("Task ExecCommandAsync(string commandId…") —
      // its first argument is a parameter list, which contains no string literal anyway.
      const openParen = callIndex + method.length;
      const slice = firstArgumentSlice(source, openParen);
      for (const literal of slice.matchAll(/"([^"\\]+)"/g)) {
        if (literal[1].trim().length > 0) {
          ids.add(literal[1]);
        }
      }
      searchFrom = openParen;
    }
  }
  return ids;
}

export function extractCanvasCommandIds(repoRoot) {
  const documentEditorRoot = path.join(repoRoot, 'src', 'Tempo.Blazor.DocumentEditor');
  const ids = new Set();
  for (const file of collectSourceFiles(documentEditorRoot)) {
    for (const id of extractCommandIdsFromSource(readFileSync(file, 'utf8'))) {
      ids.add(id);
    }
  }
  return [...ids].sort();
}

export function findRepoRoot(startDir) {
  let current = startDir;
  for (;;) {
    try {
      readFileSync(path.join(current, 'TempoBlazor.slnx'));
      return current;
    } catch {
      const parent = path.dirname(current);
      if (parent === current) {
        throw new Error(`Could not locate TempoBlazor.slnx above ${startDir}.`);
      }
      current = parent;
    }
  }
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  const repoRoot = findRepoRoot(path.dirname(fileURLToPath(import.meta.url)));
  const ids = extractCanvasCommandIds(repoRoot);
  console.log(`extract-canvas-command-ids: ${ids.length} routed command id(s).`);
  for (const id of ids) {
    console.log(`  ${id}`);
  }
}
