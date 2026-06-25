import * as esbuild from 'esbuild';

const common = {
    bundle: true,
    format: 'esm',
    target: ['es2022'],
    sourcemap: false,
    legalComments: 'none',
};

await Promise.all([
    esbuild.build({
        ...common,
        entryPoints: ['src/Tempo.Blazor/wwwroot/js/reporting/reporting-painter.mjs'],
        outfile: 'src/Tempo.Blazor/wwwroot/js/reporting/reporting-painter.bundle.js',
    }),
    esbuild.build({
        ...common,
        entryPoints: ['src/Tempo.Blazor.Reporting/wwwroot/js/reporting/tm-report-viewer.mjs'],
        outfile: 'src/Tempo.Blazor.Reporting/wwwroot/js/reporting/tm-report-viewer.bundle.js',
    }),
]);
