import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  auditCssTokens,
  findRepoRoot,
  parseDefinitions,
  parseUsages
} from './audit-css-tokens.mjs';

test('parseDefinitions collects --tm-* custom-property declarations and ignores comments', () => {
  const css = `
    :root {
      --tm-color-primary: #2563eb;
      --tm-space-2: 0.5rem; /* --tm-commented-out: red; */
    }
    /* --tm-also-commented: blue; */
    .scoped { --tm-document-z-text: 10; }
  `;
  const definitions = parseDefinitions(css);
  assert.deepEqual(
    [...definitions].sort(),
    ['--tm-color-primary', '--tm-document-z-text', '--tm-space-2']);
});

test('parseUsages distinguishes fallback-less var() references from guarded ones', () => {
  const css = [
    '.a { color: var(--tm-color-primary); }',
    '.b { background: var(--tm-surface, #fff); border: 1px solid var( --tm-color-border ); }',
    '/* var(--tm-in-comment) */',
    '.c { background: linear-gradient(180deg, var(--tm-color-surface) 0, var(--tm-color-surface-secondary) 100%); }'
  ].join('\n');
  const usages = parseUsages(css);
  assert.deepEqual(usages, [
    { token: '--tm-color-primary', line: 1, hasFallback: false },
    { token: '--tm-surface', line: 2, hasFallback: true },
    { token: '--tm-color-border', line: 2, hasFallback: false },
    { token: '--tm-color-surface', line: 4, hasFallback: false },
    { token: '--tm-color-surface-secondary', line: 4, hasFallback: false }
  ]);
});

// The CI drift gate: every var(--tm-*) usage without a fallback must resolve to a
// token defined somewhere in the scanned stylesheets. A failure here is the exact
// error class that made the fullscreen editor root transparent
// (--tm-color-surface-secondary was referenced by ~20 rules but never defined).
test('repository stylesheets reference no undefined --tm-* tokens without fallback', () => {
  const repoRoot = findRepoRoot(path.dirname(fileURLToPath(import.meta.url)));
  const { files, missing } = auditCssTokens(repoRoot);
  assert.ok(files.length > 10, `Expected the audit to scan the repo stylesheets, got ${files.length} file(s).`);
  assert.deepEqual(
    missing,
    [],
    `Undefined --tm-* tokens without fallback:\n${missing.map(m => `  ${m.token}  ${m.file}:${m.line}`).join('\n')}`);
});
