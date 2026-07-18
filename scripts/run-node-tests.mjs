#!/usr/bin/env node
// Deterministic Node test runner: enumerates *.test.mjs files explicitly via
// the filesystem instead of relying on `node --test` glob expansion, which
// varies across Node versions and shells (a stale/unsupported glob silently
// runs a subset). Fails loudly when a root is missing or matches no files.
import { spawnSync } from 'node:child_process';
import { readdirSync, existsSync } from 'node:fs';
import path from 'node:path';

const roots = process.argv.slice(2);
if (roots.length === 0) {
  console.error('Usage: node scripts/run-node-tests.mjs <dir> [<dir> ...]');
  process.exit(2);
}

const files = [];
function walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full);
    } else if (entry.isFile() && entry.name.endsWith('.test.mjs')) {
      files.push(full);
    }
  }
}

for (const root of roots) {
  if (!existsSync(root)) {
    console.error(`Test root does not exist: ${root}`);
    process.exit(2);
  }
  walk(root);
}

files.sort();
if (files.length === 0) {
  console.error(`No *.test.mjs files found under: ${roots.join(', ')}`);
  process.exit(2);
}

console.log(`run-node-tests: ${files.length} test file(s) under ${roots.join(', ')}`);
const result = spawnSync(process.execPath, ['--test', ...files], { stdio: 'inherit' });
process.exit(result.status ?? 1);
