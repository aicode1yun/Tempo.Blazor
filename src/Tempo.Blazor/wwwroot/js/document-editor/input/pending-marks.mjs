// Phase D — input/pending-marks.mjs
// `pendingMarkForCommand(pendingTypingMarks, commandId)` — the most-recent pending
// typing mark that matches the given toolbar command. Returns `null` when no
// pending mark applies. Used to render toolbar state before any text is typed.

import { asArray } from '../core/helpers.mjs';
import { markMatchesCommand } from './command-classifiers.mjs';

export function pendingMarkForCommand(pendingTypingMarks, id) {
    return asArray(pendingTypingMarks).slice().reverse().find(function (mark) {
        return markMatchesCommand(mark, id);
    }) || null;
}
