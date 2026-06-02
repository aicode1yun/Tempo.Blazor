// Phase D — render/revision-color.mjs
// Revision author color helpers used to tint tracked-change markup.
//
// `clamp01(value)` — clamps a number to [0,1]; NaN → 0.
// `describeRevisionColor(hex)` — parse a 6-digit hex (with or without `#`) into
//   `{r, g, b}`; null for anything else.
// `blendRevisionColor(color, ratio)` — lighten `color` toward white by `ratio`
//   (0..1), returning a `#rrggbb` string; null when the color can't be parsed.
// `revisionColorForAuthor(seed)` — deterministic `hsl(...)` color from a string
//   seed (stable per author).
// `applyRevisionColorVars(target, color)` — sets `--tm-revision-color` +
//   `--tm-revision-color-soft` CSS vars on a DOM target; no-op when
//   target/style/color missing or color unparseable.

import { asText } from '../core/helpers.mjs';

export function clamp01(value) {
    const number = Number(value);
    return number < 0 ? 0 : number > 1 ? 1 : (number === number ? number : 0);
}

export function describeRevisionColor(hex) {
    const normalized = asText(hex).trim();
    const match = /^#?([0-9a-f]{6})$/i.exec(normalized);
    if (!match) return null;
    const int = parseInt(match[1], 16);
    return { r: (int >> 16) & 255, g: (int >> 8) & 255, b: int & 255 };
}

export function blendRevisionColor(color, ratio) {
    const base = describeRevisionColor(color);
    if (!base) return null;
    const amount = clamp01(ratio);
    const mix = function (channel) {
        return Math.round(channel + (255 - channel) * amount);
    };
    return '#' + [mix(base.r), mix(base.g), mix(base.b)].map(function (channel) {
        return ('0' + channel.toString(16)).slice(-2);
    }).join('');
}

export function revisionColorForAuthor(seed) {
    const text = asText(seed);
    let hash = 0;
    for (let i = 0; i < text.length; i++) {
        hash = (hash * 31 + text.charCodeAt(i)) >>> 0;
    }
    const hue = hash % 360;
    return 'hsl(' + hue + ', 70%, 45%)';
}

export function applyRevisionColorVars(target, color) {
    if (!target || !target.style || !color) return;
    const rgb = describeRevisionColor(color);
    if (!rgb) return;
    target.style.setProperty('--tm-revision-color', color);
    target.style.setProperty('--tm-revision-color-soft', blendRevisionColor(color, 0.85));
}
