// Phase R.4.6e — core-engine/header-footer.mjs
// Page headers / footers + fields for the model-owned surface. The layout engine already
// lays out `model.headers` / `model.footers` regions on every page and resolves field
// runs per page (cloneBlockWithResolvedFields → page number / page count), and the atomic
// renderer paints them. This module just builds region/run content and sets it.
//
//   textRun(text) / pageNumberField() / pageCountField()   → run builders
//   normalizeRegionRuns(content, idBase)                   → string | run[] → run[]
//   setRegion(model, 'header'|'footer', content)           → region id
//   clearRegion(model, 'header'|'footer')

import { asArray } from '../core/helpers.mjs';

let seq = 0;

export function textRun(text, id) {
    return { id: id || ('hf-t-' + (++seq)), kind: 'text', text: String(text == null ? '' : text) };
}
export function pageNumberField(id) {
    return { id: id || ('hf-pn-' + (++seq)), kind: 'field', fieldType: 'pageNumber', text: '1' };
}
export function pageCountField(id) {
    return { id: id || ('hf-pc-' + (++seq)), kind: 'field', fieldType: 'pageCount', text: '1' };
}
// R.5.13 — a date field. The formatted date is frozen into `text` at insert (deterministic,
// like Word's "insert date as text"); an explicit value can be supplied (e.g. for tests).
export function dateField(value, id) {
    const text = value != null ? String(value) : new Date().toISOString().slice(0, 10);
    return { id: id || ('hf-dt-' + (++seq)), kind: 'field', fieldType: 'date', text: text };
}

// R.5.13 — which pages a header/footer region applies to (mirrors render/header-footer-region).
function normalizeScope(scope) {
    const v = String(scope || '').toLowerCase();
    if (v.indexOf('first') >= 0) return 'FirstPage';
    if (v.indexOf('even') >= 0) return 'EvenPage';
    return 'Primary';
}

export function normalizeRegionRuns(content, idBase) {
    let runs;
    if (typeof content === 'string') {
        runs = [textRun(content, idBase + '-t0')];
    } else {
        runs = asArray(content).map(function (r, i) {
            return Object.assign({ kind: 'text' }, r, { id: (r && r.id) || (idBase + '-r' + i) });
        });
    }
    if (!runs.length) runs = [textRun('', idBase + '-empty')];
    return runs;
}

// R.5.13 — `scope` selects which pages this region applies to: undefined/'primary' (default,
// odd + fallback), 'first' (page 1), 'even'. The layout's resolveHeaderFooterRegion picks the
// right region per page. Setting a scope replaces only that scope's region (others kept).
export function setRegion(model, which, content, scope) {
    if (!model) return null;
    const isFooter = which === 'footer';
    const key = isFooter ? 'footers' : 'headers';
    const normScope = normalizeScope(scope);
    const id = (isFooter ? 'footer' : 'header') + '-' + normScope.toLowerCase();
    const runs = normalizeRegionRuns(content, id);
    const region = { id: id, scope: normScope, blocks: [{ id: id + '-p', type: 'paragraph', content: { type: 'paragraph', runs: runs } }] };
    const list = asArray(model[key]).filter(function (r) { return normalizeScope(r.scope) !== normScope; });
    list.push(region);
    model[key] = list;
    return id;
}

// `scope` omitted clears every region; otherwise only that scope's region is removed.
export function clearRegion(model, which, scope) {
    if (!model) return;
    const key = which === 'footer' ? 'footers' : 'headers';
    if (scope == null) { model[key] = []; return; }
    const normScope = normalizeScope(scope);
    model[key] = asArray(model[key]).filter(function (r) { return normalizeScope(r.scope) !== normScope; });
}
