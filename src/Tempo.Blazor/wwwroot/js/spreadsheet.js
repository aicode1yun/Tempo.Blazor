window.tmSpreadsheetGrid = window.tmSpreadsheetGrid || {};
window.tmSpreadsheetFormulaBar = window.tmSpreadsheetFormulaBar || {};

window.tmSpreadsheetGrid.observeViewport = function (grid, dotNetRef) {
    if (!grid || !dotNetRef) return;

    if (typeof grid.__tmSpreadsheetViewportCleanup === "function") {
        grid.__tmSpreadsheetViewportCleanup();
    }

    let frame = 0;
    const notify = () => {
        if (frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            dotNetRef.invokeMethodAsync(
                "OnSpreadsheetViewportChanged",
                grid.scrollLeft || 0,
                grid.clientWidth || 0
            ).catch(() => {
                // Component was disposed before the queued viewport update ran.
            });
        });
    };

    const resizeObserver = typeof ResizeObserver !== "undefined"
        ? new ResizeObserver(notify)
        : null;

    grid.addEventListener("scroll", notify, { passive: true });
    if (resizeObserver) {
        resizeObserver.observe(grid);
    }

    grid.__tmSpreadsheetViewportCleanup = () => {
        grid.removeEventListener("scroll", notify);
        if (resizeObserver) {
            resizeObserver.disconnect();
        }
        if (frame) {
            cancelAnimationFrame(frame);
            frame = 0;
        }
        delete grid.__tmSpreadsheetViewportCleanup;
    };

    notify();
};

window.tmSpreadsheetGrid.disposeViewportObserver = function (grid) {
    if (!grid || typeof grid.__tmSpreadsheetViewportCleanup !== "function") return;
    grid.__tmSpreadsheetViewportCleanup();
};

window.tmSpreadsheetGrid.ensureCellVisible = function (grid, cell, options) {
    if (!grid || !cell) return;

    const rowHeaderWidth = options?.rowHeaderWidth ?? options?.RowHeaderWidth ?? 40;
    const columnHeaderHeight = options?.columnHeaderHeight ?? options?.ColumnHeaderHeight ?? 20;

    const left = cell.left ?? cell.Left ?? 0;
    const top = cell.top ?? cell.Top ?? 0;
    const right = cell.right ?? cell.Right ?? left;
    const bottom = cell.bottom ?? cell.Bottom ?? top;
    const frozenRow = cell.frozenRow ?? cell.FrozenRow ?? false;
    const frozenColumn = cell.frozenColumn ?? cell.FrozenColumn ?? false;

    let nextScrollLeft = grid.scrollLeft;
    let nextScrollTop = grid.scrollTop;

    if (!frozenColumn) {
        const visibleLeft = grid.scrollLeft + rowHeaderWidth;
        const visibleRight = grid.scrollLeft + grid.clientWidth;

        if (left < visibleLeft) {
            nextScrollLeft = left - rowHeaderWidth;
        } else if (right > visibleRight) {
            nextScrollLeft = right - grid.clientWidth;
        }
    }

    if (!frozenRow) {
        const visibleTop = grid.scrollTop + columnHeaderHeight;
        const visibleBottom = grid.scrollTop + grid.clientHeight;

        if (top < visibleTop) {
            nextScrollTop = top - columnHeaderHeight;
        } else if (bottom > visibleBottom) {
            nextScrollTop = bottom - grid.clientHeight;
        }
    }

    nextScrollLeft = Math.max(0, nextScrollLeft);
    nextScrollTop = Math.max(0, nextScrollTop);

    if (nextScrollLeft !== grid.scrollLeft || nextScrollTop !== grid.scrollTop) {
        grid.scrollTo({
            left: nextScrollLeft,
            top: nextScrollTop,
            behavior: "auto"
        });
    }
};

(function () {
    const cellRefPattern = /^\$?[A-Za-z]{1,3}\$?\d{1,7}$/;
    const functionBoundaryPattern = /[=+\-*/^&(,;<>:\s]/;

    function clampPosition(value, text) {
        const length = String(text || "").length;
        const numeric = Number(value);
        if (!Number.isFinite(numeric)) return length;
        return Math.max(0, Math.min(length, Math.floor(numeric)));
    }

    function normalizeCellRef(token) {
        return String(token || "").replace(/\$/g, "").toUpperCase();
    }

    function columnLettersToIndex(letters) {
        let result = 0;
        const value = String(letters || "").toUpperCase();
        for (let index = 0; index < value.length; index += 1) {
            result = result * 26 + (value.charCodeAt(index) - 64);
        }
        return Math.max(0, result - 1);
    }

    function parseCellRef(ref) {
        const match = /^([A-Z]{1,3})(\d{1,7})$/.exec(normalizeCellRef(ref));
        if (!match) return null;
        return {
            row: Math.max(0, Number(match[2]) - 1),
            col: columnLettersToIndex(match[1])
        };
    }

    function parseReferenceToken(token) {
        const raw = normalizeCellRef(token);
        const parts = raw.split(":");
        if (parts.length < 1 || parts.length > 2) return null;
        const start = parseCellRef(parts[0]);
        const end = parseCellRef(parts[1] || parts[0]);
        if (!start || !end) return null;
        return {
            raw,
            startRow: Math.min(start.row, end.row),
            startCol: Math.min(start.col, end.col),
            endRow: Math.max(start.row, end.row),
            endCol: Math.max(start.col, end.col)
        };
    }

    function isFormulaIdentifierBoundary(ch) {
        return !ch || !/[A-Za-z0-9_$]/.test(ch);
    }

    function readFormulaCellReference(value, start) {
        let index = start;
        if (value[index] === "$") index += 1;

        const lettersStart = index;
        while (index < value.length && /[A-Za-z]/.test(value[index]) && index - lettersStart < 3) {
            index += 1;
        }

        const lettersLength = index - lettersStart;
        if (lettersLength < 1 || lettersLength > 3) return null;

        if (value[index] === "$") index += 1;

        const digitsStart = index;
        while (index < value.length && /\d/.test(value[index]) && index - digitsStart < 7) {
            index += 1;
        }

        const digitsLength = index - digitsStart;
        if (digitsLength < 1 || digitsLength > 7) return null;

        return {
            text: value.slice(start, index),
            start,
            end: index
        };
    }

    function readFormulaReferenceToken(value, start) {
        const first = readFormulaCellReference(value, start);
        if (!first) return null;

        let end = first.end;
        let text = first.text;
        let type = "reference";
        if (value[end] === ":") {
            const second = readFormulaCellReference(value, end + 1);
            if (second) {
                end = second.end;
                text = value.slice(start, end);
                type = "range";
            }
        }

        const before = value[start - 1] || "";
        const after = value[end] || "";
        if (!isFormulaIdentifierBoundary(before) || !isFormulaIdentifierBoundary(after)) {
            return null;
        }

        return { type, text, start, end };
    }

    function tokenizeFormula(text) {
        const value = String(text || "");
        const tokens = [];
        let index = 0;

        while (index < value.length) {
            const ch = value[index];

            if (/\s/.test(ch)) {
                const start = index;
                index += 1;
                while (index < value.length && /\s/.test(value[index])) index += 1;
                tokens.push({ type: "whitespace", text: value.slice(start, index), start, end: index });
                continue;
            }

            if (ch === "\"") {
                const start = index;
                index += 1;
                while (index < value.length) {
                    if (value[index] === "\"") {
                        if (value[index + 1] === "\"") {
                            index += 2;
                            continue;
                        }
                        index += 1;
                        break;
                    }
                    index += 1;
                }
                tokens.push({ type: "string", text: value.slice(start, index), start, end: index });
                continue;
            }

            if (ch === "," || ch === ";") {
                tokens.push({ type: "separator", text: ch, start: index, end: index + 1 });
                index += 1;
                continue;
            }

            if (ch === "(" || ch === ")") {
                tokens.push({ type: "paren", text: ch, start: index, end: index + 1 });
                index += 1;
                continue;
            }

            if ((ch === "<" || ch === ">") && value[index + 1] === "=") {
                tokens.push({ type: "operator", text: value.slice(index, index + 2), start: index, end: index + 2 });
                index += 2;
                continue;
            }

            if (ch === "<" && value[index + 1] === ">") {
                tokens.push({ type: "operator", text: "<>", start: index, end: index + 2 });
                index += 2;
                continue;
            }

            if (/[=+\-*/^&<>:%]/.test(ch)) {
                tokens.push({ type: "operator", text: ch, start: index, end: index + 1 });
                index += 1;
                continue;
            }

            if (/\d/.test(ch) || (ch === "." && /\d/.test(value[index + 1] || ""))) {
                const start = index;
                index += 1;
                while (index < value.length && /[\d.]/.test(value[index])) index += 1;
                tokens.push({ type: "number", text: value.slice(start, index), start, end: index });
                continue;
            }

            const referenceToken = (ch === "$" || /[A-Za-z]/.test(ch))
                ? readFormulaReferenceToken(value, index)
                : null;
            if (referenceToken) {
                tokens.push(referenceToken);
                index = referenceToken.end;
                continue;
            }

            if (/[A-Za-z_]/.test(ch)) {
                const start = index;
                index += 1;
                while (index < value.length && /[A-Za-z0-9_.]/.test(value[index])) index += 1;
                const end = index;
                let probe = index;
                while (probe < value.length && /\s/.test(value[probe])) probe += 1;
                tokens.push({
                    type: probe < value.length && value[probe] === "(" ? "function" : "identifier",
                    text: value.slice(start, end),
                    start,
                    end
                });
                continue;
            }

            tokens.push({ type: "unknown", text: ch, start: index, end: index + 1 });
            index += 1;
        }

        return tokens;
    }

    function parseFormulaReferences(text) {
        const refs = [];
        for (const token of tokenizeFormula(text)) {
            if (token.type !== "reference" && token.type !== "range") continue;
            const parsed = parseReferenceToken(token.text);
            if (!parsed) continue;
            refs.push({
                text: token.text,
                start: token.start,
                end: token.end,
                colorIndex: refs.length % 6,
                startRow: parsed.startRow,
                startCol: parsed.startCol,
                endRow: parsed.endRow,
                endCol: parsed.endCol
            });
        }
        return refs;
    }

    function getReferenceAtCaret(refs, caret) {
        const position = Math.max(0, Number(caret) || 0);
        let previous = null;
        for (const ref of refs || []) {
            if (position >= ref.start && position <= ref.end) return ref;
            if (ref.end <= position) previous = ref;
        }
        return previous && previous.end === position ? previous : null;
    }

    function getReferenceSelection(refs, selectionStart, selectionEnd, text) {
        const normalizedText = String(text || "");
        const start = clampPosition(selectionStart, normalizedText);
        const end = clampPosition(selectionEnd, normalizedText);
        const caretToken = getReferenceAtCaret(refs, start);
        let selectionToken = null;
        if (end > start) {
            selectionToken = (refs || []).find(ref => start >= ref.start && start <= ref.end && end >= ref.start && end <= ref.end)
                || (refs || []).find(ref => start < ref.end && end > ref.start)
                || (refs || []).find(ref => start >= ref.start && start <= ref.end)
                || null;
        }
        const activeToken = selectionToken || caretToken;
        const activeTokenIndex = activeToken
            ? (refs || []).findIndex(ref => ref.start === activeToken.start && ref.end === activeToken.end)
            : -1;
        return { selectionStart: start, selectionEnd: end, activeToken, activeTokenIndex };
    }

    function cycleSingleAbsoluteReference(cellRef) {
        const match = /^(\$?)([A-Za-z]{1,3})(\$?)(\d{1,7})$/i.exec(String(cellRef || ""));
        if (!match) return String(cellRef || "");
        const colAbs = match[1] === "$";
        const col = match[2].toUpperCase();
        const rowAbs = match[3] === "$";
        const row = match[4];
        let nextColAbs;
        let nextRowAbs;
        if (!colAbs && !rowAbs) {
            nextColAbs = true;
            nextRowAbs = true;
        } else if (colAbs && rowAbs) {
            nextColAbs = false;
            nextRowAbs = true;
        } else if (!colAbs && rowAbs) {
            nextColAbs = true;
            nextRowAbs = false;
        } else {
            nextColAbs = false;
            nextRowAbs = false;
        }
        return `${nextColAbs ? "$" : ""}${col}${nextRowAbs ? "$" : ""}${row}`;
    }

    function cycleAbsoluteReferenceToken(token) {
        const value = String(token || "");
        const parts = value.split(":");
        if (parts.length === 2) {
            return `${cycleSingleAbsoluteReference(parts[0])}:${cycleSingleAbsoluteReference(parts[1])}`;
        }
        return cycleSingleAbsoluteReference(value);
    }

    function isCellReferenceLike(token) {
        return cellRefPattern.test(String(token || "").replace(/\$/g, ""));
    }

    function findFunctionPrefix(text, caret) {
        const value = String(text || "");
        const position = clampPosition(caret, value);
        let start = position;
        while (start > 0 && /[A-Za-z]/.test(value[start - 1])) {
            start -= 1;
        }
        if (start === position) return null;
        const prefix = value.slice(start, position).toUpperCase();
        if (!prefix) return null;
        if (isCellReferenceLike(prefix)) return null;
        const boundary = start > 0 ? value[start - 1] : "=";
        if (start > 0 && !functionBoundaryPattern.test(boundary)) return null;
        if (value[position] === "(") return null;
        return { prefix, start, end: position };
    }

    function findActiveFunctionContext(text, caret) {
        const value = String(text || "");
        const limit = clampPosition(caret, value);
        const stack = [];
        let index = 0;
        while (index < limit) {
            const ch = value[index];
            if (ch === "\"") {
                index += 1;
                while (index < limit) {
                    if (value[index] === "\"") {
                        if (index + 1 < limit && value[index + 1] === "\"") {
                            index += 2;
                            continue;
                        }
                        index += 1;
                        break;
                    }
                    index += 1;
                }
                continue;
            }

            if (/[A-Za-z_]/.test(ch)) {
                const start = index;
                index += 1;
                while (index < limit && /[A-Za-z0-9_]/.test(value[index])) {
                    index += 1;
                }
                const identifier = value.slice(start, index).toUpperCase();
                if (!isCellReferenceLike(identifier)) {
                    let probe = index;
                    while (probe < limit && /\s/.test(value[probe])) {
                        probe += 1;
                    }
                    if (probe < limit && value[probe] === "(") {
                        stack.push({ name: identifier, argIndex: 0 });
                        index = probe + 1;
                        continue;
                    }
                }
                continue;
            }

            if ((ch === "," || ch === ";") && stack.length > 0) {
                stack[stack.length - 1].argIndex += 1;
                index += 1;
                continue;
            }

            if (ch === ")" && stack.length > 0) {
                stack.pop();
                index += 1;
                continue;
            }

            index += 1;
        }

        return stack.length > 0 ? stack[stack.length - 1] : null;
    }

    window.tmSpreadsheetFormulaBar.getSelection = function (input) {
        if (!input) {
            return { selectionStart: 0, selectionEnd: 0 };
        }
        return {
            selectionStart: Number(input.selectionStart || 0),
            selectionEnd: Number(input.selectionEnd || 0)
        };
    };

    function setHostFormulaPointMode(scope, active, value) {
        const host = scope?.closest?.(".tm-spreadsheet");
        if (!host) return;
        const formulaActive = !!active && String(value || "").startsWith("=");
        if (formulaActive) {
            host.dataset.formulaPointMode = "true";
        } else {
            delete host.dataset.formulaPointMode;
        }
        const grid = host.querySelector?.(".tm-spreadsheet-canvas-grid");
        if (grid && window.tmSpreadsheetCanvas?.setExternalFormulaPointMode) {
            window.tmSpreadsheetCanvas.setExternalFormulaPointMode(grid, formulaActive);
        }
    }

    window.tmSpreadsheetFormulaBar.setHostFormulaPointMode = function (scope, active, value) {
        setHostFormulaPointMode(scope, !!active, value);
    };

    window.tmSpreadsheetFormulaBar.syncHostFormulaPointModeFromInput = function (input) {
        if (!input) return;
        setHostFormulaPointMode(input, true, input.value || "");
    };

    window.tmSpreadsheetFormulaBar.bindHostFormulaPointMode = function (scope, input) {
        if (!scope || !input) return;
        if (typeof input.__tmSpreadsheetFormulaHostSyncCleanup === "function") {
            input.__tmSpreadsheetFormulaHostSyncCleanup();
        }

        const sync = () => setHostFormulaPointMode(scope, true, input.value || "");
        input.addEventListener("input", sync);
        input.addEventListener("change", sync);
        input.__tmSpreadsheetFormulaHostSyncCleanup = () => {
            input.removeEventListener("input", sync);
            input.removeEventListener("change", sync);
            delete input.__tmSpreadsheetFormulaHostSyncCleanup;
        };
        sync();
    };

    window.tmSpreadsheetFormulaBar.setValueAndSelection = function (input, value, selectionStart, selectionEnd) {
        if (!input) return;
        input.value = String(value || "");
        const start = clampPosition(selectionStart, input.value);
        const end = clampPosition(selectionEnd, input.value);
        input.setSelectionRange(start, end);
        input.focus({ preventScroll: true });
    };

    window.tmSpreadsheetFormulaBar.analyzeSession = function (text, selectionStart, selectionEnd) {
        const value = String(text || "");
        const refs = value.startsWith("=") ? parseFormulaReferences(value) : [];
        const selection = getReferenceSelection(refs, selectionStart, selectionEnd, value);
        const prefix = value.startsWith("=") && !selection.activeToken
            ? findFunctionPrefix(value, selection.selectionStart)
            : null;
        const activeFunction = value.startsWith("=") ? findActiveFunctionContext(value, selection.selectionStart) : null;
        return {
            text: value,
            selectionStart: selection.selectionStart,
            selectionEnd: selection.selectionEnd,
            isFormula: value.startsWith("="),
            isReferencePickingMode: value.startsWith("="),
            activeReferenceToken: selection.activeToken || null,
            activeReferenceTokenIndex: selection.activeTokenIndex,
            referenceTokens: refs,
            functionPrefix: prefix ? prefix.prefix : null,
            functionPrefixStart: prefix ? prefix.start : -1,
            functionPrefixEnd: prefix ? prefix.end : -1,
            activeFunctionName: activeFunction ? activeFunction.name : null,
            activeFunctionArgumentIndex: activeFunction ? activeFunction.argIndex : -1
        };
    };

    window.tmSpreadsheetFormulaBar.replaceReferenceAtSelection = function (text, selectionStart, selectionEnd, refText) {
        const value = String(text || "=");
        const refs = parseFormulaReferences(value);
        const selection = getReferenceSelection(refs, selectionStart, selectionEnd, value);
        let start = selection.activeToken ? selection.activeToken.start : selection.selectionStart;
        let end = selection.activeToken ? selection.activeToken.end : selection.selectionEnd;
        if (start < 1) {
            start = value.length <= 1 ? 1 : Math.max(1, selection.selectionStart);
        }
        if (end < start) {
            end = start;
        }
        const replacement = String(refText || "");
        const nextValue = value.slice(0, start) + replacement + value.slice(end);
        const nextCaret = start + replacement.length;
        return {
            value: nextValue,
            selectionStart: nextCaret,
            selectionEnd: nextCaret
        };
    };

    window.tmSpreadsheetFormulaBar.cycleReferenceAtSelection = function (text, selectionStart, selectionEnd) {
        const value = String(text || "");
        if (!value.startsWith("=")) {
            return {
                value,
                selectionStart: clampPosition(selectionStart, value),
                selectionEnd: clampPosition(selectionEnd, value),
                changed: false
            };
        }

        const refs = parseFormulaReferences(value);
        const selection = getReferenceSelection(refs, selectionStart, selectionEnd, value);
        const token = selection.activeToken;
        if (!token) {
            return {
                value,
                selectionStart: selection.selectionStart,
                selectionEnd: selection.selectionEnd,
                changed: false
            };
        }

        const replacement = cycleAbsoluteReferenceToken(token.text);
        const nextValue = value.slice(0, token.start) + replacement + value.slice(token.end);
        const offsetWithinToken = Math.max(0, Math.min(token.text.length, selection.selectionStart - token.start));
        const nextCaret = token.start + Math.min(replacement.length, offsetWithinToken);
        return {
            value: nextValue,
            selectionStart: nextCaret,
            selectionEnd: nextCaret,
            changed: true
        };
    };

    window.tmSpreadsheetFormulaBar.acceptFunctionSuggestion = function (text, selectionStart, selectionEnd, functionName) {
        const value = String(text || "=");
        const replacement = `${String(functionName || "").toUpperCase()}(`;
        const prefix = findFunctionPrefix(value, selectionStart);
        const start = prefix ? prefix.start : clampPosition(selectionStart, value);
        const end = prefix ? prefix.end : clampPosition(selectionEnd, value);
        const nextValue = value.slice(0, start) + replacement + value.slice(end);
        const nextCaret = start + replacement.length;
        return {
            value: nextValue,
            selectionStart: nextCaret,
            selectionEnd: nextCaret
        };
    };
})();
