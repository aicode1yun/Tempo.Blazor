#!/usr/bin/env node
// Phase D — esbuild bundler config for the document editor modules.
//
// Inputs: src/Tempo.Blazor/wwwroot/js/document-editor/runtime/entry.mjs (the entry point
// that will eventually wire all submodules into the global window.tmDocumentEditorEngine).
//
// Output: src/Tempo.Blazor/wwwroot/js/document-editor.dist.js (bundled IIFE).
//
// Until the full monolith is extracted, the bundler only handles the modules that have been
// migrated. The current bundle is therefore a small "verification artifact" — it proves the
// build chain works end-to-end without affecting the production document-editor-wysiwyg.js
// that still ships as a single IIFE.
//
// Usage:
//   node tests/Tempo.Blazor.Tests/jsbuild/esbuild.mjs [--watch]
//
// Requires esbuild to be installed (npm i). If esbuild is not present the script prints a
// clear message and exits 0 so CI without npm install does not fail.

import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const repoRoot = resolve(__dirname, '..', '..', '..');

const entryPoint = resolve(repoRoot, 'src/Tempo.Blazor/wwwroot/js/document-editor/runtime/entry.mjs');
const outFile = resolve(repoRoot, 'src/Tempo.Blazor/wwwroot/js/document-editor.dist.js');

const watch = process.argv.includes('--watch');

if (!existsSync(entryPoint)) {
    console.error(`[document-editor build] entry point not found: ${entryPoint}`);
    process.exit(1);
}

let esbuild;
try {
    esbuild = await import('esbuild');
} catch (err) {
    console.warn(`[document-editor build] esbuild is not installed (run 'npm install' in repo root).`);
    console.warn(`[document-editor build] skipping bundle build — exit 0 (treated as no-op for CI).`);
    console.warn(`[document-editor build] details: ${err.message}`);
    process.exit(0);
}

const buildOptions = {
    entryPoints: [entryPoint],
    bundle: true,
    format: 'iife',
    globalName: 'tmDocumentEditorModules',
    outfile: outFile,
    target: ['es2018'],
    sourcemap: true,
    legalComments: 'inline',
    logLevel: 'info',
    minify: false,
    metafile: true,
};

async function runOnce() {
    const result = await esbuild.build(buildOptions);
    if (result.metafile) {
        const totalBytes = Object.values(result.metafile.outputs)
            .reduce((sum, o) => sum + (o.bytes || 0), 0);
        console.log(`[document-editor build] bundle size: ${(totalBytes / 1024).toFixed(1)} KB`);
    }
}

if (watch) {
    const ctx = await esbuild.context(buildOptions);
    await ctx.watch();
    console.log('[document-editor build] watching for changes…');
} else {
    await runOnce();
}
