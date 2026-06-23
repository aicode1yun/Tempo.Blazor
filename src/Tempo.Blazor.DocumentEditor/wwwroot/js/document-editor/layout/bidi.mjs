// Phase R.4.5 — layout/bidi.mjs
// A clean-room implementation of the Unicode Bidirectional Algorithm (UAX #9), enough to
// lay out mixed left-to-right / right-to-left text on a single paragraph line: resolve an
// embedding level per character, then reorder logical → visual (rule L2). Implemented
// from the published Unicode spec — NOT derived from any other engine's source.
//
// Scope: one isolating run sequence per paragraph (the whole line). Explicit formatting
// codes (LRE/RLE/LRO/RLO/PDF and the isolates LRI/RLI/FSI/PDI) are treated as removed
// (BN) — they are rare in editor content and add large complexity; documented as a
// follow-up. The implemented rules cover P2–P3, W1–W7, N1–N2, I1–I2 and L2, which is what
// real Arabic/Hebrew/Latin/number/neutral text needs.
//
//   bidiClass(codePoint)            → 'L' | 'R' | 'AL' | 'EN' | … (the UAX #9 type)
//   baseDirection(text, dir?)       → 'ltr' | 'rtl'  (P2/P3 first-strong, or forced)
//   resolveLevels(text, dir?)       → { baseLevel, levels: number[] }  (per code unit)
//   reorderVisual(levels)           → number[] mapping visual position → logical index
//   hasRtl(levels)                  → true if any odd level (line needs reordering)

// --- character classification (compact range table, derived from Unicode blocks) -------

function inRange(cp, lo, hi) { return cp >= lo && cp <= hi; }

export function bidiClass(cp) {
    // Strong R (Hebrew + related RTL scripts without joining).
    if (inRange(cp, 0x0590, 0x05FF) || inRange(cp, 0x07C0, 0x089F)
        || inRange(cp, 0xFB1D, 0xFB4F) || inRange(cp, 0x10800, 0x10FFF)) {
        // Hebrew points / Arabic-context marks inside these blocks are NSM (handled below).
        if (inRange(cp, 0x0591, 0x05BD) || cp === 0x05BF || cp === 0x05C1 || cp === 0x05C2
            || cp === 0x05C4 || cp === 0x05C5 || cp === 0x05C7) return 'NSM';
        return 'R';
    }
    // Arabic letters (AL) + Arabic marks (NSM) + Arabic-Indic digits (AN).
    if (inRange(cp, 0x0600, 0x06FF) || inRange(cp, 0x0750, 0x077F)
        || inRange(cp, 0x08A0, 0x08FF) || inRange(cp, 0xFB50, 0xFDFF)
        || inRange(cp, 0xFE70, 0xFEFF) || inRange(cp, 0x0700, 0x074F) /* Syriac */
        || inRange(cp, 0x0780, 0x07BF) /* Thaana */) {
        if (inRange(cp, 0x0660, 0x0669) || inRange(cp, 0x066B, 0x066C)
            || inRange(cp, 0x06F0, 0x06F9)) return 'AN';
        if (inRange(cp, 0x0610, 0x061A) || inRange(cp, 0x064B, 0x065F) || cp === 0x0670
            || inRange(cp, 0x06D6, 0x06DC) || inRange(cp, 0x06DF, 0x06E4)
            || inRange(cp, 0x06E7, 0x06E8) || inRange(cp, 0x06EA, 0x06ED)
            || inRange(cp, 0x08E3, 0x08FF)) return 'NSM';
        return 'AL';
    }
    // European numbers + separators.
    if (inRange(cp, 0x0030, 0x0039)) return 'EN';
    if (cp === 0x002B || cp === 0x002D) return 'ES';
    if (cp === 0x0023 || cp === 0x0024 || cp === 0x0025 || inRange(cp, 0x00A2, 0x00A5)
        || cp === 0x0025 || cp === 0x00B0 || cp === 0x066A) return 'ET';
    if (cp === 0x002C || cp === 0x002E || cp === 0x002F || cp === 0x003A || cp === 0x00A0) return 'CS';
    // Combining marks (generic).
    if (inRange(cp, 0x0300, 0x036F) || inRange(cp, 0x1AB0, 0x1AFF)
        || inRange(cp, 0x1DC0, 0x1DFF) || inRange(cp, 0x20D0, 0x20FF)
        || inRange(cp, 0xFE20, 0xFE2F)) return 'NSM';
    // Paragraph / segment separators + whitespace.
    if (cp === 0x000A || cp === 0x000D || cp === 0x001C || cp === 0x001D || cp === 0x001E
        || cp === 0x0085 || cp === 0x2029) return 'B';
    if (cp === 0x0009 || cp === 0x000B || cp === 0x001F) return 'S';
    if (cp === 0x0020 || cp === 0x000C || cp === 0x1680 || inRange(cp, 0x2000, 0x200A)
        || cp === 0x2028 || cp === 0x205F || cp === 0x3000) return 'WS';
    // R.5.16 — implicit directional MARKS are strong (zero-width): LRM=L, RLM=R, ALM=AL.
    if (cp === 0x200E) return 'L';
    if (cp === 0x200F) return 'R';
    if (cp === 0x061C) return 'AL';
    // Explicit formatting characters (embeddings/overrides/isolates) — kept as their own classes
    // so resolveLevels' X-pass can act on them, then stripped to BN.
    if (cp === 0x202A) return 'LRE';
    if (cp === 0x202B) return 'RLE';
    if (cp === 0x202D) return 'LRO';
    if (cp === 0x202E) return 'RLO';
    if (cp === 0x202C) return 'PDF';
    if (cp === 0x2066) return 'LRI';
    if (cp === 0x2067) return 'RLI';
    if (cp === 0x2068) return 'FSI';
    if (cp === 0x2069) return 'PDI';
    // Boundary-neutral controls + zero-width formatting.
    if (inRange(cp, 0x0000, 0x0008) || inRange(cp, 0x000E, 0x001B)
        || inRange(cp, 0x007F, 0x0084) || inRange(cp, 0x0086, 0x009F)
        || inRange(cp, 0x200B, 0x200F) || cp === 0x2060 || cp === 0xFEFF
        || inRange(cp, 0x202A, 0x202E) /* explicit embeds → removed */
        || inRange(cp, 0x2066, 0x2069) /* isolates → removed */) return 'BN';
    // Other neutrals: brackets / punctuation / symbols / emoji (resolved by N rules).
    if (inRange(cp, 0x0021, 0x002A) || inRange(cp, 0x003B, 0x0040)
        || inRange(cp, 0x005B, 0x0060) || inRange(cp, 0x007B, 0x007E)
        || inRange(cp, 0x2010, 0x2027) || inRange(cp, 0x2030, 0x205E)
        || inRange(cp, 0x2190, 0x2BFF) /* arrows/symbols/dingbats */
        || inRange(cp, 0x1F000, 0x1FAFF) /* emoji */) return 'ON';
    // Everything else (Latin, Greek, Cyrillic, CJK, Hangul, …) is strong L.
    return 'L';
}

// Maps each UTF-16 code unit to the code point that owns it (a trailing surrogate inherits
// the leading surrogate's code point), so levels are produced per code unit and line up
// with the engine's code-unit caret offsets.
function codeUnitCodePoints(str) {
    const cps = new Array(str.length);
    for (let i = 0; i < str.length; i++) {
        const code = str.charCodeAt(i);
        if (code >= 0xDC00 && code <= 0xDFFF && i > 0) {
            cps[i] = cps[i - 1]; // low surrogate inherits the astral code point
        } else {
            cps[i] = str.codePointAt(i);
        }
    }
    return cps;
}

export function baseDirection(text, dir) {
    if (dir === 'ltr' || dir === 'rtl') return dir;
    const str = String(text == null ? '' : text);
    const cps = codeUnitCodePoints(str);
    for (let i = 0; i < str.length; i++) {
        const c = bidiClass(cps[i]);
        if (c === 'L') return 'ltr';
        if (c === 'R' || c === 'AL') return 'rtl';
    }
    return 'ltr';
}

export function resolveLevels(text, dir) {
    const str = String(text == null ? '' : text);
    const n = str.length;
    if (!n) return { baseLevel: dir === 'rtl' ? 1 : 0, levels: [] };

    const cps = codeUnitCodePoints(str);
    const types = new Array(n);
    for (let i = 0; i < n; i++) types[i] = bidiClass(cps[i]);

    // P2/P3 — base paragraph level from the first strong type (unless forced).
    let baseLevel;
    if (dir === 'ltr') baseLevel = 0;
    else if (dir === 'rtl') baseLevel = 1;
    else {
        baseLevel = 0;
        for (let i = 0; i < n; i++) {
            if (types[i] === 'L') { baseLevel = 0; break; }
            if (types[i] === 'R' || types[i] === 'AL') { baseLevel = 1; break; }
        }
    }
    const sos = (baseLevel % 2 === 0) ? 'L' : 'R';
    const eos = sos; // single run sequence spanning the whole paragraph

    // R.5.16 — X1–X9 (compact): explicit embedding levels + directional overrides (LRE/RLE/LRO/
    // RLO/PDF). Isolates (LRI/RLI/FSI/PDI) are approximated as embeddings (their fully independent
    // resolution is a documented simplification). Format chars become BN; overrides force L/R.
    const MAX_DEPTH = 125;
    const explicitLevels = new Array(n);
    const dirStack = [{ level: baseLevel, override: 'neutral' }];
    const lgOdd = function (l) { return (l % 2 === 0) ? l + 1 : l + 2; };
    const lgEven = function (l) { return (l % 2 === 0) ? l + 2 : l + 1; };
    const firstStrong = function (from) { for (let k = from; k < n; k++) { const ty = types[k]; if (ty === 'L' || ty === 'LRI' || ty === 'LRE') return 'L'; if (ty === 'R' || ty === 'AL' || ty === 'RLI' || ty === 'RLE') return 'R'; if (ty === 'PDI') break; } return 'L'; };
    for (let i = 0; i < n; i++) {
        const ty = types[i];
        const top = dirStack[dirStack.length - 1];
        if (ty === 'RLE' || ty === 'RLI') { explicitLevels[i] = top.level; if (dirStack.length < MAX_DEPTH) dirStack.push({ level: lgOdd(top.level), override: 'neutral' }); types[i] = 'BN'; }
        else if (ty === 'LRE' || ty === 'LRI') { explicitLevels[i] = top.level; if (dirStack.length < MAX_DEPTH) dirStack.push({ level: lgEven(top.level), override: 'neutral' }); types[i] = 'BN'; }
        else if (ty === 'RLO') { explicitLevels[i] = top.level; if (dirStack.length < MAX_DEPTH) dirStack.push({ level: lgOdd(top.level), override: 'R' }); types[i] = 'BN'; }
        else if (ty === 'LRO') { explicitLevels[i] = top.level; if (dirStack.length < MAX_DEPTH) dirStack.push({ level: lgEven(top.level), override: 'L' }); types[i] = 'BN'; }
        else if (ty === 'FSI') { explicitLevels[i] = top.level; const d = firstStrong(i + 1); if (dirStack.length < MAX_DEPTH) dirStack.push({ level: d === 'R' ? lgOdd(top.level) : lgEven(top.level), override: 'neutral' }); types[i] = 'BN'; }
        else if (ty === 'PDF' || ty === 'PDI') { if (dirStack.length > 1) dirStack.pop(); explicitLevels[i] = dirStack[dirStack.length - 1].level; types[i] = 'BN'; }
        else { explicitLevels[i] = top.level; if (top.override === 'L') types[i] = 'L'; else if (top.override === 'R') types[i] = 'R'; }
    }

    const t = types.slice();

    // W1 — NSM takes the type of the previous character (sos at the start).
    for (let i = 0; i < n; i++) {
        if (t[i] === 'NSM') t[i] = (i === 0) ? sos : t[i - 1];
    }
    // W2 — EN → AN when the last strong type is AL.
    let strong = sos;
    for (let i = 0; i < n; i++) {
        if (t[i] === 'L' || t[i] === 'R' || t[i] === 'AL') strong = t[i];
        else if (t[i] === 'EN' && strong === 'AL') t[i] = 'AN';
    }
    // W3 — AL → R.
    for (let i = 0; i < n; i++) if (t[i] === 'AL') t[i] = 'R';
    // W4 — a single ES between two EN → EN; a single CS between two EN (or two AN) → that.
    for (let i = 1; i < n - 1; i++) {
        if (t[i] === 'ES' && t[i - 1] === 'EN' && t[i + 1] === 'EN') t[i] = 'EN';
        else if (t[i] === 'CS' && t[i - 1] === 'EN' && t[i + 1] === 'EN') t[i] = 'EN';
        else if (t[i] === 'CS' && t[i - 1] === 'AN' && t[i + 1] === 'AN') t[i] = 'AN';
    }
    // W5 — a run of ET adjacent to EN becomes EN.
    for (let i = 0; i < n; i++) {
        if (t[i] === 'ET') {
            let j = i; while (j < n && t[j] === 'ET') j++;
            const before = (i > 0) ? t[i - 1] : null;
            const after = (j < n) ? t[j] : null;
            if (before === 'EN' || after === 'EN') for (let k = i; k < j; k++) t[k] = 'EN';
            i = j - 1;
        }
    }
    // W6 — remaining ES/ET/CS → ON.
    for (let i = 0; i < n; i++) if (t[i] === 'ES' || t[i] === 'ET' || t[i] === 'CS') t[i] = 'ON';
    // W7 — EN → L when the last strong type is L.
    strong = sos;
    for (let i = 0; i < n; i++) {
        if (t[i] === 'L' || t[i] === 'R') strong = t[i];
        else if (t[i] === 'EN' && strong === 'L') t[i] = 'L';
    }

    // N1/N2 — resolve neutrals (and BN, treated as neutral here). EN/AN count as R.
    const isNeutral = (x) => x === 'B' || x === 'S' || x === 'WS' || x === 'ON' || x === 'BN';
    const dirOf = (x) => (x === 'L') ? 'L' : ((x === 'R' || x === 'EN' || x === 'AN') ? 'R' : null);
    const embeddingDir = (baseLevel % 2 === 0) ? 'L' : 'R';
    for (let i = 0; i < n; i++) {
        if (isNeutral(t[i])) {
            let j = i; while (j < n && isNeutral(t[j])) j++;
            const before = (i > 0) ? dirOf(t[i - 1]) : sos;
            const after = (j < n) ? dirOf(t[j]) : eos;
            const resolved = (before && after && before === after) ? before : embeddingDir; // N1 else N2
            for (let k = i; k < j; k++) t[k] = resolved;
            i = j - 1;
        }
    }

    // I1/I2 — implicit levels, relative to each char's explicit embedding level (R.5.16).
    const levels = new Array(n);
    for (let i = 0; i < n; i++) {
        const lvl = explicitLevels[i] != null ? explicitLevels[i] : baseLevel;
        if (lvl % 2 === 0) { // even (LTR) embedding
            if (t[i] === 'R') levels[i] = lvl + 1;
            else if (t[i] === 'AN' || t[i] === 'EN') levels[i] = lvl + 2;
            else levels[i] = lvl; // L
        } else { // odd (RTL) embedding
            if (t[i] === 'L' || t[i] === 'EN' || t[i] === 'AN') levels[i] = lvl + 1;
            else levels[i] = lvl; // R
        }
    }
    return { baseLevel, levels };
}

function reverseRange(arr, from, to) {
    while (from < to) { const tmp = arr[from]; arr[from] = arr[to]; arr[to] = tmp; from++; to--; }
}

// L2 — from the highest level down to the lowest odd level, reverse every contiguous run
// of characters whose level is ≥ that level. Returns visual order: out[v] = logical index.
export function reorderVisual(levels) {
    const n = levels.length;
    const order = new Array(n);
    for (let i = 0; i < n; i++) order[i] = i;
    if (!n) return order;
    let max = 0; let minOdd = Infinity;
    for (let i = 0; i < n; i++) {
        if (levels[i] > max) max = levels[i];
        if ((levels[i] & 1) && levels[i] < minOdd) minOdd = levels[i];
    }
    if (minOdd === Infinity) return order; // all even → already in visual order
    for (let lvl = max; lvl >= minOdd; lvl--) {
        let start = -1;
        for (let i = 0; i <= n; i++) {
            const lev = (i < n) ? levels[order[i]] : -1;
            if (lev >= lvl) { if (start < 0) start = i; }
            else if (start >= 0) { reverseRange(order, start, i - 1); start = -1; }
        }
    }
    return order;
}

export function hasRtl(levels) {
    for (let i = 0; i < levels.length; i++) if (levels[i] & 1) return true;
    return false;
}
