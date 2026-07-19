// Headless document runtime — advance-table text measurement.
//
// Measures text from precomputed Skia glyph advance tables (font units) extracted server-side
// from the SAME ReportPdfFontFace bytes the PDF renderer embeds
// (TempoFontAdvanceTableExtractor in Tempo.Blazor.DocumentFormats): measurement and drawing
// parity by construction, no .NET↔JS callbacks per glyph.
//
// Wiring options:
//   - `createAdvanceFontMetricsService(tableOrJson)` — a full font-metrics service (drop-in for
//     `createFontMetricsService`): the advance tables act as the "real" measure context, and any
//     unknown family/glyph falls back to the deterministic synthetic metrics with diagnostics
//     (`service.getAdvanceDiagnostics()`).
//   - the returned service (or a `{ measureRun }` partial over it) plugs into pagination's
//     `ensureMeasurementService` seam via `options.fontMetrics`.
//
// Face resolution mirrors the PDF renderer's ReportPdfFontCatalog exactly, so the face that
// measures a run is the face that draws it: (1) exact family+weight+style, (2) family + weight
// 400 + same style, (3) any style of the family by nearest weight, (4) nothing → throw (the
// service catches and measures synthetically, recording a diagnostic).

import { createFontMetricsService } from './font-metrics.mjs';

const GENERIC_FAMILIES = new Set(['serif', 'sans-serif', 'monospace', 'cursive', 'fantasy', 'system-ui', 'ui-serif', 'ui-sans-serif', 'ui-monospace', 'ui-rounded', 'math', 'emoji', 'fangsong']);

// Parses the advance-table JSON (or an already-parsed object) into an indexed table:
// `{ faces: [...], byFamily: Map<lowercased family, face[]> }` with per-face advance Maps.
export function parseFontAdvanceTable(source) {
    const raw = typeof source === 'string' ? JSON.parse(source) : (source || {});
    const faces = (Array.isArray(raw.faces) ? raw.faces : []).map(face => {
        const advances = new Map();
        for (const [key, value] of Object.entries(face?.advances || {})) {
            const codePoint = Number(key);
            const advance = Number(value);
            if (Number.isFinite(codePoint) && Number.isFinite(advance)) {
                advances.set(codePoint, advance);
            }
        }

        return {
            family: String(face?.family || '').trim(),
            weight: Number(face?.weight) || 400,
            style: String(face?.style || 'normal').trim().toLowerCase() || 'normal',
            unitsPerEm: Number(face?.unitsPerEm) > 0 ? Number(face.unitsPerEm) : 1000,
            ascent: Number(face?.ascent) || 0,
            descent: Number(face?.descent) || 0,
            lineGap: Number(face?.lineGap) || 0,
            missingGlyphAdvance: Number(face?.missingGlyphAdvance) || 0,
            advances,
        };
    }).filter(face => face.family.length > 0);

    const byFamily = new Map();
    for (const face of faces) {
        const key = face.family.toLowerCase();
        if (!byFamily.has(key)) {
            byFamily.set(key, []);
        }

        byFamily.get(key).push(face);
    }

    return { schemaVersion: Number(raw.schemaVersion) || 1, faces, byFamily };
}

// Splits a CSS font-family list into cleaned family names (quotes stripped, generics dropped).
function splitFamilies(familyList) {
    return String(familyList || '')
        .split(',')
        .map(entry => entry.trim().replace(/^['"]|['"]$/g, '').trim())
        .filter(entry => entry.length > 0 && !GENERIC_FAMILIES.has(entry.toLowerCase()));
}

// Parses the CSS font shorthand produced by fontStringFromStyle
// (e.g. `italic small-caps 700 12px Arial, sans-serif`).
function parseFontShorthand(font) {
    const text = String(font || '');
    const match = text.match(/(\d+(?:\.\d+)?)px\s+(.+)$/);
    if (!match) {
        return null;
    }

    let weight = 400;
    let style = 'normal';
    for (const token of text.slice(0, match.index).trim().split(/\s+/).filter(Boolean)) {
        const lower = token.toLowerCase();
        if (lower === 'italic' || lower === 'oblique') {
            style = 'italic';
        } else if (lower === 'bold') {
            weight = 700;
        } else if (/^\d+$/.test(lower)) {
            weight = Number(lower);
        }
        // 'normal' and 'small-caps' do not affect face resolution.
    }

    return { size: Number(match[1]), families: splitFamilies(match[2]), weight, style };
}

// ReportPdfFontCatalog.Resolve, step for step (see the module header).
function resolveFace(table, families, weight, style) {
    for (const family of families) {
        const candidates = table.byFamily.get(family.toLowerCase());
        if (!candidates || candidates.length === 0) {
            continue;
        }

        const exact = candidates.find(face => face.weight === weight && face.style === style);
        if (exact) {
            return exact;
        }

        const regular = candidates.find(face => face.weight === 400 && face.style === style);
        if (regular) {
            return regular;
        }

        let nearest = candidates[0];
        for (const face of candidates) {
            if (Math.abs(face.weight - weight) < Math.abs(nearest.weight - weight)) {
                nearest = face;
            }
        }

        return nearest;
    }

    return null;
}

// A minimal canvas-2D-context stand-in over advance tables: `font` setter + `measureText`.
// Unknown families and unmapped glyphs throw — createFontMetricsService's measureReal catches
// and falls back to the deterministic synthetic metrics; the miss is recorded in diagnostics.
export function createFontAdvanceMeasureContext(table, options = {}) {
    const parsed = table && table.byFamily instanceof Map ? table : parseFontAdvanceTable(table);
    const unknownFamilies = new Set();
    const missingGlyphs = new Map();
    const onDiagnostic = typeof options.onDiagnostic === 'function' ? options.onDiagnostic : null;

    let current = null;
    let currentFontString = '';

    const context = {
        get font() {
            return currentFontString;
        },
        set font(value) {
            currentFontString = String(value || '');
            current = parseFontShorthand(currentFontString);
        },
        measureText(text) {
            const value = String(text ?? '');
            if (!current) {
                throw new Error(`Advance-table context received an unparseable font: "${currentFontString}"`);
            }

            const face = resolveFace(parsed, current.families, current.weight, current.style);
            if (!face) {
                const familyList = current.families.join(', ') || currentFontString;
                if (!unknownFamilies.has(familyList)) {
                    unknownFamilies.add(familyList);
                    onDiagnostic?.({ kind: 'unknownFamily', family: familyList });
                }

                throw new Error(`Advance table has no face for font family "${familyList}".`);
            }

            let units = 0;
            for (const ch of value) {
                const codePoint = ch.codePointAt(0);
                const advance = face.advances.get(codePoint);
                if (advance === undefined) {
                    const missKey = `${face.family}@${codePoint}`;
                    if (!missingGlyphs.has(missKey)) {
                        missingGlyphs.set(missKey, { family: face.family, codePoint });
                        onDiagnostic?.({ kind: 'missingGlyph', family: face.family, codePoint });
                    }

                    throw new Error(
                        `Advance table for "${face.family}" has no glyph for U+${codePoint.toString(16).toUpperCase()}.`);
                }

                units += advance;
            }

            return {
                width: units * current.size / face.unitsPerEm,
                fontBoundingBoxAscent: face.ascent * current.size / face.unitsPerEm,
                fontBoundingBoxDescent: face.descent * current.size / face.unitsPerEm,
            };
        },
        getDiagnostics() {
            return {
                unknownFamilies: [...unknownFamilies],
                missingGlyphs: [...missingGlyphs.values()],
            };
        },
    };

    return context;
}

// Full font-metrics service measuring from advance tables: reuses the production
// createFontMetricsService (LRU cache, style normalization, letter spacing, character scale,
// zoom, caret advances) with the advance-table context as its "real" canvas. Adds
// `getAdvanceDiagnostics()` reporting families/glyphs that fell back to synthetic metrics.
export function createAdvanceFontMetricsService(tableOrJson, options = {}) {
    const context = createFontAdvanceMeasureContext(tableOrJson, options);
    const service = createFontMetricsService({
        ...options,
        createMeasureContext: () => context,
    });

    return Object.freeze(Object.assign(Object.create(service), {
        getAdvanceDiagnostics: () => context.getDiagnostics(),
    }));
}
