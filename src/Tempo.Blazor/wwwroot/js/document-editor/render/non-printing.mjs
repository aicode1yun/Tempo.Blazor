// Phase D — render/non-printing.mjs
// `formatNonPrintingText` renders whitespace as visible glyphs when the editor
// is in "show non-printing characters" mode:
//   space  →  · (U+00B7 middle dot)
//   tab    →  → (U+2192 rightwards arrow)
//   newline → ¶ (U+00B6 pilcrow) followed by an actual newline so layout still wraps

import { asText } from '../core/helpers.mjs';

export function formatNonPrintingText(text) {
    return asText(text)
        .replace(/ /g, '·')
        .replace(/\t/g, '→')
        .replace(/\n/g, '¶\n');
}
