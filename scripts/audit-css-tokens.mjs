#!/usr/bin/env node
// CSS design-token drift audit: finds var(--tm-*) usages that reference a token
// which is defined NOWHERE in the scanned stylesheets and carries no fallback.
// Such a usage computes to the property's initial/inherited value at runtime
// (the --tm-color-surface-secondary regression: the editor root rendered
// transparent because the token was referenced by ~20 rules but never defined).
//
// Definitions are collected from ALL scanned files, not just tokens.css /
// tokens-dark.css: component stylesheets legitimately define scoped custom
// properties (e.g. --tm-document-z-text on .tm-document-editor) and those must
// not be reported as drift. Runtime-set tokens are recognized too: .razor/.cs
// inline styles ($"--tm-x:{value}") and JS style builders ('--tm-x:' + value)
// all contain the same `--tm-name:` shape, so non-CSS sources under src/ are
// scanned as definition-only inputs.
//
// CLI: node scripts/audit-css-tokens.mjs  → exit 1 when undefined tokens exist.
// Test: scripts/audit-css-tokens.test.mjs runs the audit as a CI gate.
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

// bin/obj hold build copies of the same files; the vendor dirs ship third-party
// CSS that does not participate in the --tm-* token system.
const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules', 'bootstrap', 'open-iconic', 'leaflet']);

export function collectCssFiles(repoRoot) {
  const files = [];
  const walk = dir => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (!SKIP_DIRS.has(entry.name)) {
          walk(full);
        }
        continue;
      }
      if (!entry.isFile()) {
        continue;
      }
      if (entry.name.endsWith('.razor.css')) {
        files.push(full);
      } else if (entry.name.endsWith('.css')) {
        const segments = full.split(path.sep);
        if (segments.includes('wwwroot') && segments.includes('css')) {
          files.push(full);
        }
      }
    }
  };
  walk(path.join(repoRoot, 'src'));
  return files.sort();
}

const RUNTIME_SOURCE_EXTENSIONS = ['.razor', '.cs', '.mjs', '.js'];

/** Non-CSS sources that can DEFINE tokens at runtime (inline styles, JS style builders). */
export function collectRuntimeSourceFiles(repoRoot) {
  const files = [];
  const walk = dir => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (!SKIP_DIRS.has(entry.name) && entry.name !== '__tests__') {
          walk(full);
        }
        continue;
      }
      if (!entry.isFile() || entry.name.includes('.test.')) {
        continue;
      }
      if (RUNTIME_SOURCE_EXTENSIONS.some(extension => entry.name.endsWith(extension))) {
        files.push(full);
      }
    }
  };
  walk(path.join(repoRoot, 'src'));
  return files.sort();
}

export function stripCssComments(css) {
  // Replace comment characters with spaces so line numbers stay stable.
  return css.replace(/\/\*[\s\S]*?\*\//g, match => match.replace(/[^\n]/g, ' '));
}

/** All `--tm-…:` custom-property declarations in a stylesheet. */
export function parseDefinitions(css) {
  const definitions = new Set();
  const pattern = /(--tm-[a-zA-Z0-9-]+)\s*:/g;
  for (const match of stripCssComments(css).matchAll(pattern)) {
    definitions.add(match[1]);
  }
  return definitions;
}

/** All `var(--tm-…)` references with token name, 1-based line, and fallback flag. */
export function parseUsages(css) {
  const usages = [];
  const lines = stripCssComments(css).split('\n');
  const pattern = /var\(\s*(--tm-[a-zA-Z0-9-]+)\s*([,)])/g;
  for (let index = 0; index < lines.length; index++) {
    for (const match of lines[index].matchAll(pattern)) {
      usages.push({ token: match[1], line: index + 1, hasFallback: match[2] === ',' });
    }
  }
  return usages;
}

export function auditCssTokens(repoRoot) {
  const files = collectCssFiles(repoRoot);
  const definitions = new Set();
  const usagesByFile = [];

  for (const file of files) {
    const css = readFileSync(file, 'utf8');
    for (const definition of parseDefinitions(css)) {
      definitions.add(definition);
    }
    const usages = parseUsages(css);
    if (usages.length > 0) {
      usagesByFile.push({ file: path.relative(repoRoot, file), usages });
    }
  }

  for (const file of collectRuntimeSourceFiles(repoRoot)) {
    for (const definition of parseDefinitions(readFileSync(file, 'utf8'))) {
      definitions.add(definition);
    }
  }

  const missing = [];
  for (const { file, usages } of usagesByFile) {
    for (const usage of usages) {
      if (!usage.hasFallback && !definitions.has(usage.token)) {
        missing.push({ token: usage.token, file, line: usage.line });
      }
    }
  }

  missing.sort((a, b) => a.token.localeCompare(b.token) || a.file.localeCompare(b.file) || a.line - b.line);
  return { files, definitions, missing };
}

export function findRepoRoot(startDir) {
  let current = startDir;
  while (current) {
    try {
      readFileSync(path.join(current, 'TempoBlazor.slnx'));
      return current;
    } catch {
      const parent = path.dirname(current);
      if (parent === current) {
        break;
      }
      current = parent;
    }
  }
  throw new Error(`Could not locate TempoBlazor.slnx above ${startDir}.`);
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  const repoRoot = findRepoRoot(path.dirname(fileURLToPath(import.meta.url)));
  const { files, definitions, missing } = auditCssTokens(repoRoot);
  console.log(`audit-css-tokens: scanned ${files.length} stylesheet(s), ${definitions.size} --tm-* definition(s).`);
  if (missing.length === 0) {
    console.log('audit-css-tokens: no undefined --tm-* tokens without fallback.');
    process.exit(0);
  }
  console.error(`audit-css-tokens: ${missing.length} usage(s) of undefined --tm-* tokens WITHOUT fallback:`);
  for (const { token, file, line } of missing) {
    console.error(`  ${token}  ${file}:${line}`);
  }
  console.error('Define the token in tokens.css/tokens-dark.css (or the owning component stylesheet), or add a var() fallback.');
  process.exit(1);
}
