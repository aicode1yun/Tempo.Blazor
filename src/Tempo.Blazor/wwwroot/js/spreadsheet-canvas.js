window.tmSpreadsheetCanvas = window.tmSpreadsheetCanvas || {};

(function () {
    const stateKey = "__tmSpreadsheetCanvas";
    const imageCache = new Map();
    const maxTextMeasureCacheSize = 5000;
    const maxStyleCacheSize = 1200;
    const maxDisplayValueCacheSize = 10000;
    const defaultCellFontFamily = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";

    function read(obj, name, fallback) {
        if (!obj) return fallback;
        const camel = name.charAt(0).toLowerCase() + name.slice(1);
        return obj[camel] ?? obj[name] ?? fallback;
    }

    function write(obj, name, value) {
        if (!obj) return;
        const camel = name.charAt(0).toLowerCase() + name.slice(1);
        if (camel in obj || !(name in obj)) obj[camel] = value;
        if (name in obj) obj[name] = value;
    }

    function css(root, name, fallback) {
        return getComputedStyle(root).getPropertyValue(name).trim() || fallback;
    }

    function getState(root) {
        return root ? root[stateKey] : null;
    }

    function safeSetPointerCapture(root, pointerId) {
        try { root?.setPointerCapture?.(pointerId); } catch { }
    }

    function safeReleasePointerCapture(root, pointerId) {
        try { root?.releasePointerCapture?.(pointerId); } catch { }
    }

    function limitCache(cache, maxSize) {
        if (!cache || cache.size <= maxSize) return;
        while (cache.size > maxSize) {
            const firstKey = cache.keys().next().value;
            cache.delete(firstKey);
        }
    }

    function syncModelViewport(root, model) {
        write(model, "ScrollLeft", root.scrollLeft || 0);
        write(model, "ScrollTop", root.scrollTop || 0);
        write(model, "ViewportWidth", root.clientWidth || read(model, "ViewportWidth", 1));
        write(model, "ViewportHeight", root.clientHeight || read(model, "ViewportHeight", 1));
    }

    function requestCanvasRedraw(root, reason, kind) {
        const s = getState(root);
        if (!s || !s.model) return;
        s.pendingRedrawKind = mergeRedrawKind(s.pendingRedrawKind, kind || "full");
        s.pendingRedrawReason = reason || s.pendingRedrawReason || "unknown";
        if (s.localFrame) return;
        s.localFrame = requestAnimationFrame(() => {
            const redrawReason = s.pendingRedrawReason || "unknown";
            const redrawKind = s.pendingRedrawKind || "full";
            const oldScrollLeft = read(s.model, "ScrollLeft", root.scrollLeft || 0);
            const oldScrollTop = read(s.model, "ScrollTop", root.scrollTop || 0);
            s.localFrame = 0;
            s.pendingRedrawReason = "";
            s.pendingRedrawKind = "";
            if (s.metrics) {
                s.metrics.lastRedrawSource = redrawReason;
                s.metrics.lastRedrawKind = redrawKind;
            }
            if (redrawKind === "selection") {
                syncModelViewport(root, s.model);
                renderSelectionOverlay(root, s.model);
            } else if (redrawReason === "scroll" && tryBitmapScrollRedraw(root, s.model, oldScrollLeft, oldScrollTop)) {
                return;
            } else {
                syncModelViewport(root, s.model);
                renderModel(root, s.canvas, s.model);
            }
        });
    }

    function mergeRedrawKind(current, next) {
        const rank = { selection: 1, headers: 2, content: 3, full: 4 };
        if (!current) return next || "full";
        return (rank[next] || 4) > (rank[current] || 4) ? next : current;
    }

    function scheduleLocalRender(root) {
        requestCanvasRedraw(root, "scroll", "full");
    }

    function markLocalInteraction(root, source) {
        const s = getState(root);
        if (!s) return 0;
        s.interactionVersion += 1;
        s.lastInteractionSource = source || "local";
        if (s.metrics) {
            if (source === "keyboard") s.metrics.keyboardInteractions += 1;
            else if (source === "scroll") s.metrics.scrollInteractions += 1;
            else if (source === "pointer") s.metrics.pointerInteractions += 1;
        }
        return s.interactionVersion;
    }

    function currentInteractionVersion(root) {
        return getState(root)?.interactionVersion || 0;
    }

    function scheduleForcedViewportSync(root) {
        const s = getState(root);
        if (!s || s.forcedViewportFrame) return;
        s.forcedViewportFrame = requestAnimationFrame(() => {
            s.forcedViewportFrame = 0;
            sendViewport(root, true);
        });
    }

    function sendViewport(root, force) {
        const s = getState(root);
        if (!s) return;
        if (s.viewportTimer) {
            clearTimeout(s.viewportTimer);
            s.viewportTimer = 0;
        }

        if (s.viewportInFlight) {
            s.viewportPending = true;
            s.viewportPendingForce = s.viewportPendingForce || !!force;
            return;
        }

        s.viewportInFlight = true;
        s.viewportPending = false;
        s.viewportPendingForce = false;
        s.syncedScrollLeft = root.scrollLeft || 0;
        s.syncedScrollTop = root.scrollTop || 0;
        s.lastViewportSync = performance.now();
        const selection = getSelectionSnapshot(root);
        if (s.metrics) s.metrics.viewportCallbackCount += 1;

        s.dotNet.invokeMethodAsync(
            "OnCanvasViewportChanged",
            root.scrollLeft || 0,
            root.scrollTop || 0,
            root.clientWidth || 0,
            root.clientHeight || 0,
            selection.row,
            selection.col,
            selection.startRow,
            selection.startCol,
            selection.endRow,
            selection.endCol,
            currentInteractionVersion(root)
        ).catch(() => {}).finally(() => {
            s.viewportInFlight = false;
            if (s.viewportPending) {
                const pendingForce = !!s.viewportPendingForce;
                s.viewportPending = false;
                s.viewportPendingForce = false;
                sendViewport(root, pendingForce);
            }
        });
    }

    function notifyViewport(root, force) {
        const s = getState(root);
        if (!s) return;

        const now = performance.now();
        const movedX = Math.abs((root.scrollLeft || 0) - (s.syncedScrollLeft || 0));
        const movedY = Math.abs((root.scrollTop || 0) - (s.syncedScrollTop || 0));
        const shouldSyncNow = force
            || movedX > Math.max(160, (root.clientWidth || 0) * 0.35)
            || movedY > Math.max(120, (root.clientHeight || 0) * 0.35)
            || now - (s.lastViewportSync || 0) > 140;

        if (shouldSyncNow) {
            if (s.viewportFrame) return;
            s.viewportFrame = requestAnimationFrame(() => {
                s.viewportFrame = 0;
                sendViewport(root, !!force);
            });
            return;
        }

        if (!s.viewportTimer) {
            s.viewportTimer = setTimeout(() => sendViewport(root, false), 120);
        }
    }

    function getSelectionSnapshot(root) {
        const model = getState(root)?.model;
        const active = parseCellRef(read(model, "ActiveCellRef", "A1"));
        const selection = read(model, "Selection", {});
        return {
            row: active.row,
            col: active.col,
            startRow: read(selection, "StartRow", active.row),
            startCol: read(selection, "StartCol", active.col),
            endRow: read(selection, "EndRow", active.row),
            endCol: read(selection, "EndCol", active.col)
        };
    }

    function applySelectionSnapshot(model, snapshot) {
        if (!model || !snapshot) return;
        const selection = read(model, "Selection", {});
        const activeRef = toCellRef(snapshot.row, snapshot.col);
        const startRow = Number(snapshot.startRow ?? snapshot.row) || 0;
        const startCol = Number(snapshot.startCol ?? snapshot.col) || 0;
        const endRow = Number(snapshot.endRow ?? snapshot.row) || 0;
        const endCol = Number(snapshot.endCol ?? snapshot.col) || 0;
        const minRow = Math.min(startRow, endRow);
        const maxRow = Math.max(startRow, endRow);
        const minCol = Math.min(startCol, endCol);
        const maxCol = Math.max(startCol, endCol);

        write(model, "ActiveCellRef", activeRef);
        write(selection, "StartRow", startRow);
        write(selection, "StartCol", startCol);
        write(selection, "EndRow", endRow);
        write(selection, "EndCol", endCol);
        write(model, "Selection", selection);

        for (const cell of read(model, "Cells", [])) {
            const cellRow = read(cell, "Row", -1);
            const cellCol = read(cell, "Col", -1);
            const selected = cellRow >= minRow && cellRow <= maxRow && cellCol >= minCol && cellCol <= maxCol;
            write(cell, "Active", cellRow === snapshot.row && cellCol === snapshot.col);
            write(cell, "Selected", selected);
            write(cell, "SelectionEnd", cellRow === endRow && cellCol === endCol);
        }
    }

    function preserveLocalInteraction(root, model) {
        const s = getState(root);
        if (!s || !s.model || !model) return model;

        const frameVersion = Number(read(model, "InteractionVersion", 0)) || 0;
        if (frameVersion >= s.interactionVersion) return model;

        if (s.metrics) s.metrics.staleFramesIgnored += 1;
        write(model, "ScrollLeft", root.scrollLeft || 0);
        write(model, "ScrollTop", root.scrollTop || 0);
        applySelectionSnapshot(model, getSelectionSnapshot(root));
        write(model, "InteractionVersion", s.interactionVersion);
        return model;
    }

    function buildPalette(root) {
        return {
            surface: css(root, "--tm-color-surface", "#ffffff"),
            elevated: css(root, "--tm-color-surface-elevated", "#f8fafc"),
            hover: css(root, "--tm-color-surface-hover", "#f1f5f9"),
            border: css(root, "--tm-color-border", "#cbd5e1"),
            subtle: css(root, "--tm-color-border-subtle", "#e2e8f0"),
            text: css(root, "--tm-color-text", "#0f172a"),
            muted: css(root, "--tm-color-text-muted", "#64748b"),
            primary: css(root, "--tm-color-primary", "#2563eb"),
            primarySubtle: css(root, "--tm-color-primary-subtle", "rgba(37, 99, 235, 0.12)"),
            selectionFill: "rgba(37, 99, 235, 0.12)",
            formulaRefs: [
                { stroke: "#1e8fe0", fill: "rgba(30, 143, 224, 0.08)" },
                { stroke: "#e04030", fill: "rgba(224, 64, 48, 0.08)" },
                { stroke: "#20a040", fill: "rgba(32, 160, 64, 0.08)" },
                { stroke: "#a040c0", fill: "rgba(160, 64, 192, 0.08)" },
                { stroke: "#e08020", fill: "rgba(224, 128, 32, 0.08)" },
                { stroke: "#208080", fill: "rgba(32, 128, 128, 0.08)" }
            ]
        };
    }

    function createDebugMetrics() {
        return {
            redrawCount: 0,
            selectionRedrawCount: 0,
            bitmapShiftCount: 0,
            bitmapShiftFallbackCount: 0,
            dragAutoscrollFrames: 0,
            viewportCallbackCount: 0,
            selectionCallbackCount: 0,
            keyboardInteractions: 0,
            scrollInteractions: 0,
            pointerInteractions: 0,
            staleFramesIgnored: 0,
            slowFramesOver16: 0,
            slowFramesOver33: 0,
            lastRedrawSource: "",
            lastRedrawKind: "",
            lastDrawMs: 0,
            lastSelectionDrawMs: 0,
            lastBitmapShiftDx: 0,
            lastBitmapShiftDy: 0,
            lastBitmapShiftReason: "",
            lastTextDrawMs: 0,
            lastVisibleCellCount: 0,
            lastTextCount: 0,
            textMeasureCacheSize: 0,
            fontStringCacheSize: 0,
            paintStyleCacheSize: 0,
            displayValueCacheSize: 0,
            visibleRowCount: 0,
            visibleColumnCount: 0,
            visibleLayoutCacheHits: 0,
            visibleLayoutCacheMisses: 0
        };
    }

    function toContentPoint(root, ev) {
        const s = getState(root);
        const model = s?.model || {};
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const rect = root.getBoundingClientRect();
        return {
            x: ev.clientX - rect.left + root.scrollLeft - rowHeaderWidth,
            y: ev.clientY - rect.top + root.scrollTop - columnHeaderHeight
        };
    }

    function screenX(model, col) {
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const scrollLeft = read(model, "ScrollLeft", 0);
        return rowHeaderWidth + read(col, "Left", 0) - (read(col, "Frozen", false) ? 0 : scrollLeft);
    }

    function screenY(model, row) {
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const scrollTop = read(model, "ScrollTop", 0);
        return columnHeaderHeight + read(row, "Top", 0) - (read(row, "Frozen", false) ? 0 : scrollTop);
    }

    function hitResize(root, ev) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return null;

        const rect = root.getBoundingClientRect();
        const x = ev.clientX - rect.left;
        const y = ev.clientY - rect.top;
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);

        if (y <= columnHeaderHeight && x >= rowHeaderWidth) {
            for (const col of read(model, "Columns", [])) {
                const right = screenX(model, col) + read(col, "Width", 0);
                if (Math.abs(x - right) <= 4) {
                    return { kind: "column", index: read(col, "Index", 0), start: ev.clientX, size: read(col, "Width", 64) };
                }
            }
        }

        if (x <= rowHeaderWidth && y >= columnHeaderHeight) {
            for (const row of read(model, "Rows", [])) {
                const bottom = screenY(model, row) + read(row, "Height", 0);
                if (Math.abs(y - bottom) <= 4) {
                    return { kind: "row", index: read(row, "Index", 0), start: ev.clientY, size: read(row, "Height", 20) };
                }
            }
        }

        return null;
    }

    function hitCell(root, point) {
        const s = getState(root);
        const model = s?.model;
        if (!model || point.x < 0 || point.y < 0) return null;

        const row = findFrameAt(read(model, "Rows", []), point.y, "Top", "Height");
        const col = findFrameAt(read(model, "Columns", []), point.x, "Left", "Width");
        if (!row || !col) return null;

        const rowIndex = read(row, "Index", -1);
        const colIndex = read(col, "Index", -1);
        if (rowIndex < 0 || colIndex < 0) return null;

        return {
            row: rowIndex,
            col: colIndex,
            cell: findCell(model, rowIndex, colIndex)
        };
    }

    function sameCell(a, b) {
        if (!a && !b) return true;
        if (!a || !b) return false;
        return a.row === b.row && a.col === b.col;
    }

    function parseCellRef(cellRef) {
        const match = /^([A-Z]+)(\d+)$/i.exec(String(cellRef || "A1"));
        if (!match) return { row: 0, col: 0 };

        let col = 0;
        for (const ch of match[1].toUpperCase()) {
            col = col * 26 + ch.charCodeAt(0) - 64;
        }

        return { row: Math.max(0, Number(match[2]) - 1), col: Math.max(0, col - 1) };
    }

    function findFrameAt(frames, offset, startName, sizeName) {
        for (const frame of frames) {
            const start = read(frame, startName, 0);
            const size = read(frame, sizeName, 0);
            if (size > 0 && offset >= start && offset < start + size) {
                return frame;
            }
        }

        return null;
    }

    function getFrameByIndex(frames, index) {
        for (const frame of frames) {
            if (read(frame, "Index", -1) === index) return frame;
        }

        return null;
    }

    function findIndexAtContentOffset(frames, offset, startName, sizeName, indexName, minIndex, maxIndex) {
        const frame = findFrameAt(frames, offset, startName, sizeName);
        if (frame) return Math.max(minIndex, Math.min(maxIndex, read(frame, indexName, minIndex)));

        if (!frames || frames.length === 0) {
            return Math.max(minIndex, Math.min(maxIndex, Math.floor(offset)));
        }

        let first = null;
        let last = null;
        let totalSize = 0;
        let sizeCount = 0;
        for (const item of frames) {
            const size = read(item, sizeName, 0);
            if (size <= 0) continue;
            if (!first || read(item, startName, 0) < read(first, startName, 0)) first = item;
            if (!last || read(item, startName, 0) > read(last, startName, 0)) last = item;
            totalSize += size;
            sizeCount += 1;
        }

        if (!first || !last || sizeCount === 0) return minIndex;

        const averageSize = Math.max(1, totalSize / sizeCount);
        const firstStart = read(first, startName, 0);
        const firstIndex = read(first, indexName, minIndex);
        const lastStart = read(last, startName, 0);
        const lastSize = read(last, sizeName, averageSize);
        const lastIndex = read(last, indexName, minIndex);
        let index;
        if (offset < firstStart) {
            index = firstIndex - Math.ceil((firstStart - offset) / averageSize);
        } else {
            index = lastIndex + Math.floor(Math.max(0, offset - (lastStart + lastSize)) / averageSize) + 1;
        }

        return Math.max(minIndex, Math.min(maxIndex, index));
    }

    function findCell(model, row, col) {
        for (const cell of read(model, "Cells", [])) {
            if (read(cell, "Row", -1) === row && read(cell, "Col", -1) === col) {
                return cell;
            }
        }

        return null;
    }

    function updateLocalActiveCell(root, row, col, extendSelection, source) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return;
        markLocalInteraction(root, source || "pointer");

        const selection = read(model, "Selection", {});
        const startRow = extendSelection ? read(selection, "StartRow", row) : row;
        const startCol = extendSelection ? read(selection, "StartCol", col) : col;
        const minRow = Math.min(startRow, row);
        const maxRow = Math.max(startRow, row);
        const minCol = Math.min(startCol, col);
        const maxCol = Math.max(startCol, col);

        write(model, "ActiveCellRef", toCellRef(row, col));
        write(selection, "StartRow", startRow);
        write(selection, "StartCol", startCol);
        write(selection, "EndRow", row);
        write(selection, "EndCol", col);

        for (const cell of read(model, "Cells", [])) {
            const cellRow = read(cell, "Row", -1);
            const cellCol = read(cell, "Col", -1);
            const selected = cellRow >= minRow && cellRow <= maxRow && cellCol >= minCol && cellCol <= maxCol;
            write(cell, "Active", cellRow === row && cellCol === col);
            write(cell, "Selected", selected);
            write(cell, "SelectionEnd", cellRow === row && cellCol === col);
        }

        requestCanvasRedraw(root, source || "selection", "selection");
    }

    function getLocalCellFromClientPoint(root, clientX, clientY) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return null;

        const rect = root.getBoundingClientRect();
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const bodyWidth = Math.max(1, root.clientWidth - rowHeaderWidth);
        const bodyHeight = Math.max(1, root.clientHeight - columnHeaderHeight);
        const localX = Math.max(0, Math.min(bodyWidth - 1, clientX - rect.left - rowHeaderWidth));
        const localY = Math.max(0, Math.min(bodyHeight - 1, clientY - rect.top - columnHeaderHeight));
        const contentX = (root.scrollLeft || 0) + localX;
        const contentY = (root.scrollTop || 0) + localY;
        const rowCount = read(model, "RowCount", 1);
        const colCount = read(model, "ColumnCount", 1);
        const row = findIndexAtContentOffset(read(model, "Rows", []), contentY, "Top", "Height", "Index", 0, Math.max(0, rowCount - 1));
        const col = findIndexAtContentOffset(read(model, "Columns", []), contentX, "Left", "Width", "Index", 0, Math.max(0, colCount - 1));

        return { row, col, cell: findCell(model, row, col) };
    }

    function updateDragSelectionTarget(root, clientX, clientY) {
        const s = getState(root);
        if (!s?.selectionDrag) return false;

        const hit = hitCell(root, toContentPoint(root, { clientX, clientY })) || getLocalCellFromClientPoint(root, clientX, clientY);
        if (!hit) return false;

        if (hit.row !== s.selectionDrag.row || hit.col !== s.selectionDrag.col) {
            s.selectionDrag.row = hit.row;
            s.selectionDrag.col = hit.col;
            updateLocalActiveCell(root, hit.row, hit.col, true, "pointer");
            scheduleSelectionSync(root);
            return true;
        }

        return false;
    }

    function updateDragAutoscroll(root, clientX, clientY, pointerId) {
        const s = getState(root);
        if (!s) return;
        s.dragAutoscrollPointer = { clientX, clientY, pointerId };
        if (s.dragAutoscrollFrame) return;
        s.dragAutoscrollFrame = requestAnimationFrame(() => runDragAutoscroll(root));
    }

    function stopDragAutoscroll(root) {
        const s = getState(root);
        if (!s) return;
        if (s.dragAutoscrollFrame) {
            cancelAnimationFrame(s.dragAutoscrollFrame);
            s.dragAutoscrollFrame = 0;
        }
        s.dragAutoscrollPointer = null;
    }

    function runDragAutoscroll(root) {
        const s = getState(root);
        if (!s) return;
        s.dragAutoscrollFrame = 0;
        if (!s.selectionDrag || !s.dragAutoscrollPointer) return;

        const point = s.dragAutoscrollPointer;
        const model = s.model;
        const rect = root.getBoundingClientRect();
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const edge = 36;
        const maxStep = 28;
        const bodyLeft = rect.left + rowHeaderWidth;
        const bodyTop = rect.top + columnHeaderHeight;
        const bodyRight = rect.right;
        const bodyBottom = rect.bottom;

        const leftPower = Math.max(0, edge - (point.clientX - bodyLeft)) / edge;
        const rightPower = Math.max(0, edge - (bodyRight - point.clientX)) / edge;
        const upPower = Math.max(0, edge - (point.clientY - bodyTop)) / edge;
        const downPower = Math.max(0, edge - (bodyBottom - point.clientY)) / edge;
        const dx = Math.round((rightPower - leftPower) * maxStep);
        const dy = Math.round((downPower - upPower) * maxStep);
        const beforeLeft = root.scrollLeft || 0;
        const beforeTop = root.scrollTop || 0;

        if (dx || dy) {
            root.scrollTo({
                left: Math.max(0, beforeLeft + dx),
                top: Math.max(0, beforeTop + dy),
                behavior: "auto"
            });
            syncModelViewport(root, model);
            updateDragSelectionTarget(root, point.clientX, point.clientY);
            requestCanvasRedraw(root, "drag-autoscroll", "selection");
            notifyViewport(root, false);
            if (s.metrics) s.metrics.dragAutoscrollFrames += 1;
        } else {
            updateDragSelectionTarget(root, point.clientX, point.clientY);
        }

        if (s.selectionDrag) {
            s.dragAutoscrollFrame = requestAnimationFrame(() => runDragAutoscroll(root));
        }
    }

    function sendSelection(root) {
        const s = getState(root);
        const model = s?.model;
        if (!s || !model) return;
        if (s.selectionInFlight) {
            s.selectionPending = true;
            return;
        }

        const active = parseCellRef(read(model, "ActiveCellRef", "A1"));
        const selection = read(model, "Selection", {});
        s.selectionInFlight = true;
        s.selectionPending = false;
        if (s.metrics) s.metrics.selectionCallbackCount += 1;
        s.dotNet.invokeMethodAsync(
            "OnCanvasSelectionChanged",
            active.row,
            active.col,
            read(selection, "StartRow", active.row),
            read(selection, "StartCol", active.col),
            read(selection, "EndRow", active.row),
            read(selection, "EndCol", active.col),
            currentInteractionVersion(root)
        ).catch(() => {}).finally(() => {
            s.selectionInFlight = false;
            if (s.selectionPending) {
                s.selectionPending = false;
                scheduleSelectionSync(root);
            }
        });
    }

    function scheduleSelectionSync(root) {
        const s = getState(root);
        if (!s || !s.model || s.selectionSyncFrame) return;
        s.selectionSyncFrame = requestAnimationFrame(() => {
            s.selectionSyncFrame = 0;
            sendSelection(root);
        });
    }

    function ensureCellVisibleLocal(root, row, col) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return false;

        const rows = read(model, "Rows", []);
        const columns = read(model, "Columns", []);
        const rowFrame = getFrameByIndex(rows, row);
        const colFrame = getFrameByIndex(columns, col);
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const rowHeight = rowFrame ? read(rowFrame, "Height", 20) : 20;
        const colWidth = colFrame ? read(colFrame, "Width", 64) : 64;
        const top = rowFrame ? read(rowFrame, "Top", row * rowHeight) : row * rowHeight;
        const left = colFrame ? read(colFrame, "Left", col * colWidth) : col * colWidth;
        const bottom = top + rowHeight;
        const right = left + colWidth;
        const bodyWidth = Math.max(0, root.clientWidth - rowHeaderWidth);
        const bodyHeight = Math.max(0, root.clientHeight - columnHeaderHeight);
        let nextLeft = root.scrollLeft || 0;
        let nextTop = root.scrollTop || 0;

        if (left < nextLeft) nextLeft = left;
        else if (right > nextLeft + bodyWidth) nextLeft = right - bodyWidth;

        if (top < nextTop) nextTop = top;
        else if (bottom > nextTop + bodyHeight) nextTop = bottom - bodyHeight;

        nextLeft = Math.max(0, nextLeft);
        nextTop = Math.max(0, nextTop);
        if (Math.abs(nextLeft - root.scrollLeft) > 0.5 || Math.abs(nextTop - root.scrollTop) > 0.5) {
            root.scrollTo({ left: nextLeft, top: nextTop, behavior: "auto" });
            return true;
        }

        return false;
    }

    function navigateLocal(root, dRow, dCol, extendSelection) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return false;

        const active = parseCellRef(read(model, "ActiveCellRef", "A1"));
        const rowCount = read(model, "RowCount", active.row + 1);
        const colCount = read(model, "ColumnCount", active.col + 1);
        const row = Math.max(0, Math.min(rowCount - 1, active.row + dRow));
        const col = Math.max(0, Math.min(colCount - 1, active.col + dCol));
        if (row === active.row && col === active.col) return true;

        updateLocalActiveCell(root, row, col, extendSelection, "keyboard");
        const scrolled = ensureCellVisibleLocal(root, row, col);
        scheduleSelectionSync(root);
        if (scrolled) scheduleForcedViewportSync(root);
        else notifyViewport(root, false);
        return true;
    }

    function moveLocalTo(root, row, col, extendSelection) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return false;

        const rowCount = read(model, "RowCount", row + 1);
        const colCount = read(model, "ColumnCount", col + 1);
        row = Math.max(0, Math.min(rowCount - 1, row));
        col = Math.max(0, Math.min(colCount - 1, col));
        updateLocalActiveCell(root, row, col, extendSelection, "keyboard");
        const scrolled = ensureCellVisibleLocal(root, row, col);
        scheduleSelectionSync(root);
        if (scrolled) scheduleForcedViewportSync(root);
        else notifyViewport(root, false);
        return true;
    }

    function handleNavigationKey(root, ev) {
        if (ev.altKey || ev.metaKey || ev.ctrlKey) return false;
        const s = getState(root);
        const model = s?.model;
        if (!model) return false;

        const active = parseCellRef(read(model, "ActiveCellRef", "A1"));
        let handled = false;
        switch (ev.key) {
            case "ArrowUp": handled = navigateLocal(root, -1, 0, ev.shiftKey); break;
            case "ArrowDown": handled = navigateLocal(root, 1, 0, ev.shiftKey); break;
            case "ArrowLeft": handled = navigateLocal(root, 0, -1, ev.shiftKey); break;
            case "ArrowRight": handled = navigateLocal(root, 0, 1, ev.shiftKey); break;
            case "Tab": handled = navigateLocal(root, 0, ev.shiftKey ? -1 : 1, false); break;
            case "Home": handled = moveLocalTo(root, active.row, 0, ev.shiftKey); break;
            case "End": handled = moveLocalTo(root, active.row, read(model, "ColumnCount", active.col + 1) - 1, ev.shiftKey); break;
        }

        if (handled) {
            ev.preventDefault();
            ev.stopImmediatePropagation();
        }

        return handled;
    }

    function closeLocalEditor(root, commit) {
        const s = getState(root);
        const editor = s?.editor;
        if (!s || !editor) return;

        s.editor = null;
        if (commit && s.model) {
            const value = editor.input.value;
            const cell = findCell(s.model, editor.row, editor.col);
            const previousValue = cell ? (read(cell, "Value", "") || "") : "";
            const changed = value !== previousValue;
            if (changed && cell) write(cell, "Value", value);
            s.dotNet.invokeMethodAsync("OnCanvasCellEditCommitted", editor.row, editor.col, value).catch(() => {});
            if (changed) requestCanvasRedraw(root, "edit", "content");
        }

        editor.input.remove();
        root.focus?.({ preventScroll: true });
    }

    function openLocalEditor(root, hit) {
        const s = getState(root);
        const model = s?.model;
        if (!s || !model || !hit) return;

        closeLocalEditor(root, false);

        const cell = hit.cell || findCell(model, hit.row, hit.col);
        const row = findFrameAt(read(model, "Rows", []), cell ? read(cell, "Top", 0) : hit.row, "Top", "Height");
        const col = findFrameAt(read(model, "Columns", []), cell ? read(cell, "Left", 0) : hit.col, "Left", "Width");
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const left = rowHeaderWidth + (cell ? read(cell, "Left", 0) : read(col, "Left", 0));
        const top = columnHeaderHeight + (cell ? read(cell, "Top", 0) : read(row, "Top", 0));
        const width = cell ? read(cell, "Width", read(col, "Width", 64)) : read(col, "Width", 64);
        const height = cell ? read(cell, "Height", read(row, "Height", 20)) : read(row, "Height", 20);

        const input = document.createElement("input");
        input.className = "tm-spreadsheet-canvas-grid__editor";
        input.value = cell ? read(cell, "Value", "") || "" : "";
        input.style.left = `${left}px`;
        input.style.top = `${top}px`;
        input.style.width = `${Math.max(16, width)}px`;
        input.style.height = `${Math.max(8, height)}px`;
        input.addEventListener("click", ev => ev.stopPropagation());
        input.addEventListener("dblclick", ev => ev.stopPropagation());
        input.addEventListener("keydown", ev => {
            if (ev.key === "Enter" || ev.key === "Tab") {
                ev.preventDefault();
                closeLocalEditor(root, true);
            } else if (ev.key === "Escape") {
                ev.preventDefault();
                closeLocalEditor(root, false);
            }
            ev.stopPropagation();
        });
        input.addEventListener("blur", () => closeLocalEditor(root, true));

        root.appendChild(input);
        s.editor = { input, row: hit.row, col: hit.col };
        input.focus({ preventScroll: true });
        input.select();
    }

    function toCellRef(row, col) {
        let n = col + 1;
        let letters = "";
        while (n > 0) {
            const rem = (n - 1) % 26;
            letters = String.fromCharCode(65 + rem) + letters;
            n = Math.floor((n - 1) / 26);
        }
        return letters + String(row + 1);
    }

    window.tmSpreadsheetCanvas.register = function (root, canvas, headerCanvas, selectionCanvas, dotNet) {
        if (!dotNet) {
            dotNet = selectionCanvas;
            selectionCanvas = null;
            if (headerCanvas && !headerCanvas.getContext) {
                selectionCanvas = null;
            } else if (headerCanvas && headerCanvas.getContext) {
                selectionCanvas = headerCanvas;
            }
            headerCanvas = null;
        }
        if (!root || !canvas || !dotNet) return;
        window.tmSpreadsheetCanvas.dispose(root);

        const s = {
            canvas,
            headerCanvas,
            selectionCanvas,
            dotNet,
            model: null,
            interactionVersion: 0,
            lastInteractionSource: "",
            localFrame: 0,
            pendingRedrawKind: "",
            pendingRedrawReason: "",
            viewportFrame: 0,
            forcedViewportFrame: 0,
            viewportTimer: 0,
            viewportInFlight: false,
            viewportPending: false,
            viewportPendingForce: false,
            pointerFrame: 0,
            pointerPoint: null,
            hoverCell: null,
            possibleDrag: null,
            selectionDrag: null,
            dragAutoscrollFrame: 0,
            dragAutoscrollPointer: null,
            suppressClick: false,
            selectionSyncFrame: 0,
            selectionInFlight: false,
            selectionPending: false,
            lastViewportSync: 0,
            syncedScrollLeft: root.scrollLeft || 0,
            syncedScrollTop: root.scrollTop || 0,
            editor: null,
            palette: buildPalette(root),
            textMetricsCache: new Map(),
            fontStringCache: new Map(),
            paintStyleCache: new Map(),
            displayValueCache: new Map(),
            fontSignature: "",
            visibleLayoutCache: null,
            metrics: createDebugMetrics(),
            resize: null,
            listeners: []
        };

        const onScroll = () => {
            closeLocalEditor(root, true);
            if (s.metrics) s.metrics.scrollInteractions += 1;
            scheduleLocalRender(root);
            notifyViewport(root, false);
        };
        const updatePointerCursor = () => {
            s.pointerFrame = 0;
            const point = s.pointerPoint;
            if (!point) return;
            if (s.resize) {
                root.style.cursor = s.resize.kind === "column" ? "col-resize" : "row-resize";
                return;
            }

            const hit = hitResize(root, point);
            root.style.cursor = hit ? (hit.kind === "column" ? "col-resize" : "row-resize") : "";
            if (!hit && s.model && !s.selectionDrag) {
                const cellHit = hitCell(root, toContentPoint(root, point));
                const nextHover = cellHit ? { row: cellHit.row, col: cellHit.col } : null;
                if (!sameCell(nextHover, s.hoverCell)) {
                    s.hoverCell = nextHover;
                    requestCanvasRedraw(root, "pointer", "selection");
                }
            }
        };
        const onPointerMove = ev => {
            s.pointerPoint = { clientX: ev.clientX, clientY: ev.clientY };
            if (s.selectionDrag) {
                updateDragSelectionTarget(root, ev.clientX, ev.clientY);
                updateDragAutoscroll(root, ev.clientX, ev.clientY, ev.pointerId);
                ev.preventDefault();
                return;
            }

            if (s.possibleDrag) {
                const dx = ev.clientX - s.possibleDrag.clientX;
                const dy = ev.clientY - s.possibleDrag.clientY;
                if (Math.abs(dx) + Math.abs(dy) > 4) {
                    s.selectionDrag = { row: s.possibleDrag.row, col: s.possibleDrag.col };
                    s.suppressClick = true;
                    safeSetPointerCapture(root, ev.pointerId);
                    updateLocalActiveCell(root, s.possibleDrag.row, s.possibleDrag.col, false, "pointer");
                    updateDragSelectionTarget(root, ev.clientX, ev.clientY);
                    updateDragAutoscroll(root, ev.clientX, ev.clientY, ev.pointerId);
                    ev.preventDefault();
                    return;
                }
            }

            if (!s.pointerFrame) {
                s.pointerFrame = requestAnimationFrame(updatePointerCursor);
            }
        };
        const onPointerDown = ev => {
            const hit = hitResize(root, ev);
            if (!hit) return;
            s.resize = hit;
            safeSetPointerCapture(root, ev.pointerId);
            ev.preventDefault();
        };
        const onPointerDownWrapper = ev => {
            if (ev.button !== 0) {
                onPointerDown(ev);
                return;
            }

            const resizeHit = hitResize(root, ev);
            if (resizeHit) {
                onPointerDown(ev);
                return;
            }

            if (s.model && !read(s.model, "IsFormulaPointMode", false) && !read(s.model, "IsFormatPainterActive", false)) {
                const hit = hitCell(root, toContentPoint(root, ev));
                s.possibleDrag = hit ? { row: hit.row, col: hit.col, clientX: ev.clientX, clientY: ev.clientY } : null;
            }
        };
        const onPointerUp = ev => {
            if (s.selectionDrag) {
                s.selectionDrag = null;
                s.possibleDrag = null;
                stopDragAutoscroll(root);
                safeReleasePointerCapture(root, ev.pointerId);
                scheduleSelectionSync(root);
                ev.preventDefault();
                return;
            }

            s.possibleDrag = null;
            if (!s.resize) return;
            const resize = s.resize;
            s.resize = null;
            safeReleasePointerCapture(root, ev.pointerId);
            const delta = resize.kind === "column" ? ev.clientX - resize.start : ev.clientY - resize.start;
            const next = Math.max(resize.kind === "column" ? 16 : 8, resize.size + delta);
            const method = resize.kind === "column" ? "OnCanvasColumnResize" : "OnCanvasRowResize";
            dotNet.invokeMethodAsync(method, resize.index, next).catch(() => {});
            ev.preventDefault();
        };
        const onKeyDown = ev => {
            if (s.editor) return;
            handleNavigationKey(root, ev);
        };
        const onClick = ev => {
            if (s.suppressClick) {
                s.suppressClick = false;
                ev.preventDefault();
                return;
            }
            if (s.resize) return;
            const p = toContentPoint(root, ev);
            const hit = hitCell(root, p);
            if (hit) {
                closeLocalEditor(root, true);
                if (!read(s.model, "IsFormulaPointMode", false)) {
                    updateLocalActiveCell(root, hit.row, hit.col, !!ev.shiftKey, "pointer");
                }
                dotNet.invokeMethodAsync("OnCanvasCellPointer", hit.row, hit.col, !!ev.shiftKey, !!ev.ctrlKey).catch(() => {});
                return;
            }

            dotNet.invokeMethodAsync("OnCanvasPointer", p.x, p.y, !!ev.shiftKey, !!ev.ctrlKey).catch(() => {});
        };
        const onDblClick = ev => {
            const p = toContentPoint(root, ev);
            const hit = hitCell(root, p);
            if (hit) {
                updateLocalActiveCell(root, hit.row, hit.col, false, "pointer");
                openLocalEditor(root, hit);
                return;
            }

            dotNet.invokeMethodAsync("OnCanvasDoubleClick", p.x, p.y).catch(() => {});
        };
        const onContextMenu = ev => {
            ev.preventDefault();
            const p = toContentPoint(root, ev);
            dotNet.invokeMethodAsync("OnCanvasContextMenu", p.x, p.y, ev.clientX, ev.clientY).catch(() => {});
        };

        root.addEventListener("scroll", onScroll, { passive: true });
        root.addEventListener("pointermove", onPointerMove);
        root.addEventListener("pointerdown", onPointerDownWrapper);
        root.addEventListener("pointerup", onPointerUp);
        root.addEventListener("keydown", onKeyDown);
        root.addEventListener("click", onClick);
        root.addEventListener("dblclick", onDblClick);
        root.addEventListener("contextmenu", onContextMenu);
        s.listeners.push(
            ["scroll", onScroll],
            ["pointermove", onPointerMove],
            ["pointerdown", onPointerDownWrapper],
            ["pointerup", onPointerUp],
            ["keydown", onKeyDown],
            ["click", onClick],
            ["dblclick", onDblClick],
            ["contextmenu", onContextMenu]
        );

        if (typeof ResizeObserver !== "undefined") {
            s.resizeObserver = new ResizeObserver(() => {
                s.palette = buildPalette(root);
                notifyViewport(root, true);
                if (s.model) requestCanvasRedraw(root, "resize", "full");
            });
            s.resizeObserver.observe(root);
        }

        root[stateKey] = s;
        notifyViewport(root, true);
    };

    window.tmSpreadsheetCanvas.dispose = function (root) {
        const s = getState(root);
        if (!root || !s) return;
        for (const [event, listener] of s.listeners || []) {
            root.removeEventListener(event, listener);
        }
        if (s.resizeObserver) s.resizeObserver.disconnect();
        if (s.localFrame) cancelAnimationFrame(s.localFrame);
        if (s.viewportFrame) cancelAnimationFrame(s.viewportFrame);
        if (s.forcedViewportFrame) cancelAnimationFrame(s.forcedViewportFrame);
        if (s.pointerFrame) cancelAnimationFrame(s.pointerFrame);
        if (s.dragAutoscrollFrame) cancelAnimationFrame(s.dragAutoscrollFrame);
        if (s.selectionSyncFrame) cancelAnimationFrame(s.selectionSyncFrame);
        if (s.viewportTimer) clearTimeout(s.viewportTimer);
        closeLocalEditor(root, false);
        root.style.cursor = "";
        delete root[stateKey];
    };

    window.tmSpreadsheetCanvas.render = function (root, canvas, model) {
        if (!root || !canvas || !model) return;
        const s = getState(root);
        model = preserveLocalInteraction(root, model);
        syncModelViewport(root, model);
        if (s) {
            s.model = model;
            s.palette = buildPalette(root);
            s.syncedScrollLeft = root.scrollLeft || 0;
            s.syncedScrollTop = root.scrollTop || 0;
            const frameVersion = Number(read(model, "InteractionVersion", 0)) || 0;
            if (frameVersion > s.interactionVersion) s.interactionVersion = frameVersion;
        }
        renderModel(root, canvas, model);
    };

    window.tmSpreadsheetCanvas.getDebugMetrics = function (root) {
        const s = getState(root);
        return s?.metrics ? { ...s.metrics } : null;
    };

    window.tmSpreadsheetCanvas.ensureCellVisible = function (root, cell, options) {
        if (!root || !cell) return;
        const rowHeaderWidth = read(options, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(options, "ColumnHeaderHeight", 20);
        const frozenRow = read(cell, "FrozenRow", false);
        const frozenColumn = read(cell, "FrozenColumn", false);
        const left = read(cell, "Left", 0);
        const right = read(cell, "Right", left);
        const top = read(cell, "Top", 0);
        const bottom = read(cell, "Bottom", top);
        let nextLeft = root.scrollLeft;
        let nextTop = root.scrollTop;

        if (!frozenColumn) {
            const visibleLeft = root.scrollLeft + rowHeaderWidth;
            const visibleRight = root.scrollLeft + root.clientWidth;
            if (left < visibleLeft) nextLeft = left - rowHeaderWidth;
            else if (right > visibleRight) nextLeft = right - root.clientWidth;
        }

        if (!frozenRow) {
            const visibleTop = root.scrollTop + columnHeaderHeight;
            const visibleBottom = root.scrollTop + root.clientHeight;
            if (top < visibleTop) nextTop = top - columnHeaderHeight;
            else if (bottom > visibleBottom) nextTop = bottom - root.clientHeight;
        }

        root.scrollTo({ left: Math.max(0, nextLeft), top: Math.max(0, nextTop), behavior: "auto" });
    };

    function prepareRenderCaches(root) {
        const s = getState(root);
        if (!s) return;

        const nextSignature = getFontSignature(root);
        if (s.fontSignature && s.fontSignature !== nextSignature) {
            s.textMetricsCache?.clear();
            s.fontStringCache?.clear();
        }

        s.fontSignature = nextSignature;
    }

    function getFontSignature(root) {
        const computed = getComputedStyle(root);
        return [
            computed.fontFamily,
            computed.fontSize,
            css(root, "--tm-font-sans", ""),
            css(root, "--tm-font-mono", "")
        ].join("|");
    }

    function updateCacheMetrics(root, layout) {
        const s = getState(root);
        const metrics = s?.metrics;
        if (!metrics) return;

        metrics.textMeasureCacheSize = s.textMetricsCache?.size || 0;
        metrics.fontStringCacheSize = s.fontStringCache?.size || 0;
        metrics.paintStyleCacheSize = s.paintStyleCache?.size || 0;
        metrics.displayValueCacheSize = s.displayValueCache?.size || 0;
        if (layout) {
            metrics.visibleRowCount = layout.rows.length;
            metrics.visibleColumnCount = layout.columns.length;
        }
    }

    function getVisibleLayout(root, model, width, height) {
        const s = getState(root);
        const rows = read(model, "Rows", []);
        const columns = read(model, "Columns", []);
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const key = [
            Math.round(read(model, "ScrollLeft", 0) * 100) / 100,
            Math.round(read(model, "ScrollTop", 0) * 100) / 100,
            Math.round(width),
            Math.round(height),
            rowHeaderWidth,
            columnHeaderHeight,
            read(model, "FreezeRowCount", 0),
            read(model, "FreezeColumnCount", 0),
            frameSignature(rows, "Index", "Top", "Height"),
            frameSignature(columns, "Index", "Left", "Width")
        ].join(";");

        if (s?.visibleLayoutCache?.key === key) {
            if (s.metrics) s.metrics.visibleLayoutCacheHits += 1;
            return s.visibleLayoutCache.layout;
        }

        const layout = {
            rows: [],
            columns: [],
            rowHeaderWidth,
            columnHeaderHeight,
            width,
            height
        };

        for (const row of rows) {
            const y = screenY(model, row);
            const rowHeight = read(row, "Height", 0);
            if (rowHeight <= 0 || y + rowHeight < columnHeaderHeight || y > height) continue;
            layout.rows.push({
                source: row,
                index: read(row, "Index", 0),
                y,
                height: rowHeight
            });
        }

        for (const col of columns) {
            const x = screenX(model, col);
            const colWidth = read(col, "Width", 0);
            if (colWidth <= 0 || x + colWidth < rowHeaderWidth || x > width) continue;
            layout.columns.push({
                source: col,
                index: read(col, "Index", 0),
                label: read(col, "Label", ""),
                x,
                width: colWidth
            });
        }

        if (s) {
            s.visibleLayoutCache = { key, layout };
            if (s.metrics) s.metrics.visibleLayoutCacheMisses += 1;
        }

        return layout;
    }

    function frameSignature(items, indexName, offsetName, sizeName) {
        if (!items || items.length === 0) return "0";
        let signature = String(items.length);
        for (const item of items) {
            signature += "|" + read(item, indexName, 0)
                + ":" + Math.round(read(item, offsetName, 0) * 100) / 100
                + ":" + Math.round(read(item, sizeName, 0) * 100) / 100
                + ":" + (read(item, "Frozen", false) ? 1 : 0);
        }
        return signature;
    }

    function renderModel(root, canvas, model) {
        const started = performance.now();
        const s = getState(root);
        const metrics = s?.metrics;
        prepareRenderCaches(root);
        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, root.clientWidth || read(model, "ViewportWidth", 1));
        const height = Math.max(1, root.clientHeight || read(model, "ViewportHeight", 1));
        resizeCanvasSurface(root, canvas, width, height, dpr);
        if (s?.headerCanvas) resizeCanvasSurface(root, s.headerCanvas, width, height, dpr);
        if (s?.selectionCanvas) resizeCanvasSurface(root, s.selectionCanvas, width, height, dpr);

        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);

        const palette = s?.palette || buildPalette(root);

        ctx.fillStyle = palette.surface;
        ctx.fillRect(0, 0, width, height);

        const layout = getVisibleLayout(root, model, width, height);
        const cellMetrics = drawCells(ctx, root, model, palette);
        if (s?.headerCanvas) renderHeaderLayer(root, model);
        else drawHeaders(ctx, root, model, palette, width, height);
        if (s?.selectionCanvas) renderSelectionOverlay(root, model);
        else drawSelection(ctx, root, model, palette, width, height);
        if (metrics) {
            metrics.redrawCount += 1;
            metrics.lastDrawMs = performance.now() - started;
            metrics.lastTextDrawMs = cellMetrics.textMs;
            metrics.lastVisibleCellCount = cellMetrics.cells;
            metrics.lastTextCount = cellMetrics.texts;
            updateCacheMetrics(root, layout);
            if (metrics.lastDrawMs > 33) metrics.slowFramesOver33 += 1;
            if (metrics.lastDrawMs > 16) metrics.slowFramesOver16 += 1;
        }
    }

    function tryBitmapScrollRedraw(root, model, oldScrollLeft, oldScrollTop) {
        const s = getState(root);
        const canvas = s?.canvas;
        if (!s || !canvas || !model) return false;

        const started = performance.now();
        const metrics = s.metrics;
        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, root.clientWidth || read(model, "ViewportWidth", 1));
        const height = Math.max(1, root.clientHeight || read(model, "ViewportHeight", 1));
        const pixelWidth = Math.round(width * dpr);
        const pixelHeight = Math.round(height * dpr);
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const bodyWidth = Math.max(0, width - rowHeaderWidth);
        const bodyHeight = Math.max(0, height - columnHeaderHeight);

        const fallback = reason => {
            if (metrics) {
                metrics.bitmapShiftFallbackCount += 1;
                metrics.lastBitmapShiftReason = reason;
            }
            return false;
        };

        if (read(model, "FreezeRowCount", 0) > 0 || read(model, "FreezeColumnCount", 0) > 0) {
            return fallback("frozen");
        }

        if (bodyWidth <= 0 || bodyHeight <= 0) return fallback("empty-body");
        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) return fallback("resize-or-dpr");

        const newScrollLeft = root.scrollLeft || 0;
        const newScrollTop = root.scrollTop || 0;
        const rawDx = oldScrollLeft - newScrollLeft;
        const rawDy = oldScrollTop - newScrollTop;
        const dx = Math.round(rawDx);
        const dy = Math.round(rawDy);
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) return fallback("no-delta");
        if (Math.abs(dx) >= bodyWidth || Math.abs(dy) >= bodyHeight) return fallback("full-viewport");
        if (Math.abs(dx) > Math.max(96, bodyWidth * 0.35) || Math.abs(dy) > Math.max(96, bodyHeight * 0.35)) {
            return fallback("large-delta");
        }

        syncModelViewport(root, model);
        prepareRenderCaches(root);

        const palette = s.palette || buildPalette(root);
        resizeCanvasSurface(root, canvas, width, height, dpr);
        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.save();
        ctx.beginPath();
        ctx.rect(rowHeaderWidth, columnHeaderHeight, bodyWidth, bodyHeight);
        ctx.clip();
        ctx.drawImage(
            canvas,
            rowHeaderWidth,
            columnHeaderHeight,
            bodyWidth,
            bodyHeight,
            rowHeaderWidth + dx,
            columnHeaderHeight + dy,
            bodyWidth,
            bodyHeight);

        const strips = getExposedScrollStrips(rowHeaderWidth, columnHeaderHeight, bodyWidth, bodyHeight, dx, dy);
        let cellMetrics = { cells: 0, texts: 0, textMs: 0 };
        for (const strip of strips) {
            ctx.save();
            ctx.beginPath();
            ctx.rect(strip.x, strip.y, strip.width, strip.height);
            ctx.clip();
            ctx.fillStyle = palette.surface;
            ctx.fillRect(strip.x, strip.y, strip.width, strip.height);
            cellMetrics = addCellMetrics(cellMetrics, drawCells(ctx, root, model, palette));
            ctx.restore();
        }

        ctx.restore();
        if (s.headerCanvas) renderHeaderLayer(root, model);
        if (s.selectionCanvas) renderSelectionOverlay(root, model);

        if (metrics) {
            metrics.bitmapShiftCount += 1;
            metrics.lastBitmapShiftDx = dx;
            metrics.lastBitmapShiftDy = dy;
            metrics.lastBitmapShiftReason = "bitmap-shift";
            metrics.lastDrawMs = performance.now() - started;
            metrics.lastTextDrawMs = cellMetrics.textMs;
            metrics.lastVisibleCellCount = cellMetrics.cells;
            metrics.lastTextCount = cellMetrics.texts;
            updateCacheMetrics(root, getVisibleLayout(root, model, width, height));
            if (metrics.lastDrawMs > 33) metrics.slowFramesOver33 += 1;
            if (metrics.lastDrawMs > 16) metrics.slowFramesOver16 += 1;
        }

        return true;
    }

    function getExposedScrollStrips(x, y, width, height, dx, dy) {
        const strips = [];
        if (dy > 0) {
            strips.push({ x, y, width, height: Math.min(height, dy) });
        } else if (dy < 0) {
            const h = Math.min(height, -dy);
            strips.push({ x, y: y + height - h, width, height: h });
        }

        if (dx > 0) {
            strips.push({ x, y, width: Math.min(width, dx), height });
        } else if (dx < 0) {
            const w = Math.min(width, -dx);
            strips.push({ x: x + width - w, y, width: w, height });
        }

        return strips;
    }

    function addCellMetrics(left, right) {
        return {
            cells: left.cells + right.cells,
            texts: left.texts + right.texts,
            textMs: left.textMs + right.textMs
        };
    }

    function renderHeaderLayer(root, model) {
        const s = getState(root);
        const canvas = s?.headerCanvas;
        if (!s || !canvas || !model) return;

        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, root.clientWidth || read(model, "ViewportWidth", 1));
        const height = Math.max(1, root.clientHeight || read(model, "ViewportHeight", 1));
        resizeCanvasSurface(root, canvas, width, height, dpr);

        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);
        drawHeaders(ctx, root, model, s.palette || buildPalette(root), width, height);
    }

    function resizeCanvasSurface(root, canvas, width, height, dpr) {
        canvas.style.width = width + "px";
        canvas.style.height = height + "px";
        const layer = canvas.parentElement;
        if (layer && layer.classList.contains("tm-spreadsheet-canvas-grid__layer")) {
            layer.style.width = width + "px";
            layer.style.height = height + "px";
        }

        const pixelWidth = Math.round(width * dpr);
        const pixelHeight = Math.round(height * dpr);
        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }
    }

    function renderSelectionOverlay(root, model) {
        const s = getState(root);
        const canvas = s?.selectionCanvas;
        if (!s || !canvas || !model) return;

        const started = performance.now();
        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, root.clientWidth || read(model, "ViewportWidth", 1));
        const height = Math.max(1, root.clientHeight || read(model, "ViewportHeight", 1));
        resizeCanvasSurface(root, canvas, width, height, dpr);

        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);

        const palette = s.palette || buildPalette(root);
        drawSelection(ctx, root, model, palette, width, height);
        if (s.metrics) {
            s.metrics.selectionRedrawCount += 1;
            s.metrics.lastSelectionDrawMs = performance.now() - started;
            updateCacheMetrics(root, getVisibleLayout(root, model, width, height));
        }
    }

    function drawHeaders(ctx, root, model, palette, width, height) {
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const layout = getVisibleLayout(root, model, width, height);

        ctx.fillStyle = palette.elevated;
        ctx.fillRect(0, 0, width, columnHeaderHeight);
        ctx.fillRect(0, 0, rowHeaderWidth, height);
        ctx.strokeStyle = palette.border;
        ctx.strokeRect(0.5, 0.5, rowHeaderWidth - 0.5, columnHeaderHeight - 0.5);

        ctx.font = "500 11px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillStyle = palette.muted;

        for (const col of layout.columns) {
            const x = col.x;
            const w = col.width;
            ctx.fillStyle = palette.elevated;
            ctx.fillRect(x, 0, w, columnHeaderHeight);
            ctx.strokeStyle = palette.border;
            ctx.strokeRect(Math.floor(x) + 0.5, 0.5, w, columnHeaderHeight - 0.5);
            ctx.fillStyle = palette.muted;
            ctx.fillText(col.label, x + w / 2, columnHeaderHeight / 2);
        }

        for (const row of layout.rows) {
            const y = row.y;
            const h = row.height;
            ctx.fillStyle = palette.elevated;
            ctx.fillRect(0, y, rowHeaderWidth, h);
            ctx.strokeStyle = palette.border;
            ctx.strokeRect(0.5, Math.floor(y) + 0.5, rowHeaderWidth - 0.5, h);
            ctx.fillStyle = palette.muted;
            ctx.fillText(String(row.index + 1), rowHeaderWidth / 2, y + h / 2);
        }
    }

    function drawCells(ctx, root, model, palette) {
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const width = root.clientWidth;
        const height = root.clientHeight;
        const showGridLines = read(model, "ShowGridLines", true);
        const scrollLeft = read(model, "ScrollLeft", 0);
        const scrollTop = read(model, "ScrollTop", 0);
        const freezeRows = read(model, "FreezeRowCount", 0);
        const freezeCols = read(model, "FreezeColumnCount", 0);
        const cells = read(model, "Cells", []);
        const metrics = { cells: 0, texts: 0, textMs: 0 };
        const layout = getVisibleLayout(root, model, width, height);

        for (const cell of cells) {
            const row = read(cell, "Row", 0);
            const col = read(cell, "Col", 0);
            const frozenCol = col < freezeCols;
            const frozenRow = row < freezeRows;
            const x = rowHeaderWidth + read(cell, "Left", 0) - (frozenCol ? 0 : scrollLeft);
            const y = columnHeaderHeight + read(cell, "Top", 0) - (frozenRow ? 0 : scrollTop);
            const w = read(cell, "Width", 0);
            const h = read(cell, "Height", 0);
            if (x + w < rowHeaderWidth || y + h < columnHeaderHeight || x > width || y > height || w <= 0 || h <= 0) continue;
            metrics.cells += 1;

            const style = read(cell, "Style", {});
            const paint = getCellPaint(root, cell, style, palette);
            if (paint.backgroundColor) {
                ctx.fillStyle = paint.backgroundColor;
                ctx.fillRect(x, y, w, h);
            }

            const textStarted = performance.now();
            if (drawCellContent(ctx, root, cell, style, paint, palette, x, y, w, h)) {
                metrics.texts += 1;
                metrics.textMs += performance.now() - textStarted;
            }
        }

        if (showGridLines) {
            drawGridLines(ctx, root, model, palette, width, height, layout);
        }

        for (const cell of cells) {
            const row = read(cell, "Row", 0);
            const col = read(cell, "Col", 0);
            const frozenCol = col < freezeCols;
            const frozenRow = row < freezeRows;
            const x = rowHeaderWidth + read(cell, "Left", 0) - (frozenCol ? 0 : scrollLeft);
            const y = columnHeaderHeight + read(cell, "Top", 0) - (frozenRow ? 0 : scrollTop);
            const w = read(cell, "Width", 0);
            const h = read(cell, "Height", 0);
            if (x + w < rowHeaderWidth || y + h < columnHeaderHeight || x > width || y > height || w <= 0 || h <= 0) continue;

            const style = read(cell, "Style", {});

            drawBorders(ctx, root, style, x, y, w, h, palette);
        }

        return metrics;
    }

    function drawSelection(ctx, root, model, palette, width, height) {
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const scrollLeft = read(model, "ScrollLeft", 0);
        const scrollTop = read(model, "ScrollTop", 0);
        const freezeRows = read(model, "FreezeRowCount", 0);
        const freezeCols = read(model, "FreezeColumnCount", 0);
        const hover = getState(root)?.hoverCell;
        const formulaPoint = read(model, "IsFormulaPointMode", false);

        for (const cell of read(model, "Cells", [])) {
            const row = read(cell, "Row", 0);
            const col = read(cell, "Col", 0);
            const frozenCol = col < freezeCols;
            const frozenRow = row < freezeRows;
            const x = rowHeaderWidth + read(cell, "Left", 0) - (frozenCol ? 0 : scrollLeft);
            const y = columnHeaderHeight + read(cell, "Top", 0) - (frozenRow ? 0 : scrollTop);
            const w = read(cell, "Width", 0);
            const h = read(cell, "Height", 0);
            if (x + w < rowHeaderWidth || y + h < columnHeaderHeight || x > width || y > height || w <= 0 || h <= 0) continue;

            const formulaColorIndex = Number(read(cell, "FormulaRefColorIndex", -1));
            if (formulaColorIndex >= 0) {
                const refColor = palette.formulaRefs[formulaColorIndex % palette.formulaRefs.length];
                ctx.fillStyle = refColor.fill;
                ctx.fillRect(x, y, w, h);
                ctx.strokeStyle = refColor.stroke;
                ctx.lineWidth = 2;
                ctx.strokeRect(Math.floor(x) + 1, Math.floor(y) + 1, Math.max(0, w - 2), Math.max(0, h - 2));
            }

            if (hover && hover.row === row && hover.col === col && !read(cell, "Selected", false)) {
                ctx.fillStyle = "rgba(148, 163, 184, 0.12)";
                ctx.fillRect(x, y, w, h);
            }

            if (read(cell, "Selected", false) && !formulaPoint) {
                ctx.fillStyle = palette.selectionFill;
                ctx.fillRect(x, y, w, h);
            }

            if (read(cell, "Active", false)) {
                ctx.strokeStyle = palette.primary;
                ctx.lineWidth = 2;
                ctx.strokeRect(Math.floor(x) + 1, Math.floor(y) + 1, Math.max(0, w - 2), Math.max(0, h - 2));
            }

            if (read(cell, "SelectionEnd", false)) {
                ctx.fillStyle = palette.primary;
                ctx.fillRect(x + w - 4, y + h - 4, 6, 6);
            }
        }
    }

    function drawGridLines(ctx, root, model, palette, width, height, layout) {
        layout = layout || getVisibleLayout(root, model, width, height);
        const rowHeaderWidth = layout.rowHeaderWidth;
        const columnHeaderHeight = layout.columnHeaderHeight;

        ctx.strokeStyle = palette.subtle;
        ctx.lineWidth = 1;
        ctx.beginPath();
        const verticalLines = new Set();
        const horizontalLines = new Set();

        const columns = layout?.columns || [];
        const rows = layout?.rows || [];

        for (const col of columns) {
            const x = col.x;
            const w = col.width;
            const left = Math.floor(Math.max(rowHeaderWidth, x)) + 0.5;
            const right = Math.floor(x + w) + 0.5;
            if (left >= rowHeaderWidth && left <= width) verticalLines.add(left);
            if (right >= rowHeaderWidth && right <= width) verticalLines.add(right);
        }

        for (const row of rows) {
            const y = row.y;
            const h = row.height;
            const top = Math.floor(Math.max(columnHeaderHeight, y)) + 0.5;
            const bottom = Math.floor(y + h) + 0.5;
            if (top >= columnHeaderHeight && top <= height) horizontalLines.add(top);
            if (bottom >= columnHeaderHeight && bottom <= height) horizontalLines.add(bottom);
        }

        for (const x of verticalLines) {
            ctx.moveTo(x, columnHeaderHeight);
            ctx.lineTo(x, height);
        }

        for (const y of horizontalLines) {
            ctx.moveTo(rowHeaderWidth, y);
            ctx.lineTo(width, y);
        }

        ctx.stroke();
    }

    function drawCellContent(ctx, root, cell, style, paint, palette, x, y, w, h) {
        const imageUrl = read(cell, "ImageUrl", null);
        if (imageUrl) {
            drawImage(ctx, root, imageUrl, x, y, w, h);
            return false;
        }

        const value = getDisplayValue(root, cell);
        if (value == null || value === "") return false;

        const font = getCanvasFont(root, style);
        const fontSize = Number(read(style, "FontSize", 10)) || 10;
        ctx.font = font;
        ctx.fillStyle = paint.foreColor || palette.text;
        ctx.textBaseline = paint.verticalBaseline;
        ctx.textAlign = paint.horizontalAlign;

        const padding = 4;
        const textX = textAnchorX(paint.horizontalAlignValue, x, w, padding);
        const textY = textAnchorY(paint.verticalAlignValue, y, h, padding);
        ctx.save();
        ctx.beginPath();
        ctx.rect(x + 1, y + 1, Math.max(0, w - 2), Math.max(0, h - 2));
        ctx.clip();
        ctx.fillText(value, textX, textY);

        if (paint.underline || paint.doubleUnderline || paint.strikeThrough || paint.hyperlink) {
            const textWidth = measureTextWidth(ctx, root, font, value);
            const lineY = paint.strikeThrough ? textY - fontSize * 0.25 : textY + 2;
            const startX = textXForDecoration(ctx.textAlign, textX, textWidth);
            ctx.strokeStyle = ctx.fillStyle;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(startX, lineY);
            ctx.lineTo(startX + textWidth, lineY);
            if (paint.doubleUnderline && !paint.strikeThrough) {
                ctx.moveTo(startX, lineY + 2);
                ctx.lineTo(startX + textWidth, lineY + 2);
            }
            ctx.stroke();
        }
        ctx.restore();
        return true;
    }

    function getDisplayValue(root, cell) {
        const rawValue = read(cell, "Value", "");
        if (rawValue == null || rawValue === "") return "";

        const style = read(cell, "Style", {});
        const key = [
            read(cell, "Ref", ""),
            read(style, "NumberFormat", ""),
            rawValue
        ].join("\n");
        const s = getState(root);
        const cache = s?.displayValueCache;
        if (!cache) return String(rawValue);

        const cached = cache.get(key);
        if (cached != null) return cached;

        const value = String(rawValue);
        cache.set(key, value);
        limitCache(cache, maxDisplayValueCacheSize);
        return value;
    }

    function getCanvasFont(root, style) {
        const key = [
            read(style, "FontFamily", null) || "",
            read(style, "FontSize", 10),
            read(style, "Bold", false) ? 1 : 0,
            read(style, "Italic", false) ? 1 : 0
        ].join("|");
        const s = getState(root);
        const cache = s?.fontStringCache;
        if (cache) {
            const cached = cache.get(key);
            if (cached) return cached;
        }

        const fontSize = Number(read(style, "FontSize", 10)) || 10;
        const fontFamily = read(style, "FontFamily", null) || defaultCellFontFamily;
        const weight = read(style, "Bold", false) ? "700" : "400";
        const italic = read(style, "Italic", false) ? "italic " : "";
        const font = `${italic}${weight} ${fontSize}pt ${fontFamily}`;
        if (cache) {
            cache.set(key, font);
            limitCache(cache, maxStyleCacheSize);
        }
        return font;
    }

    function getCellPaint(root, cell, style, palette) {
        const key = [
            "cell",
            read(style, "BackgroundColor", null) || "",
            read(style, "ForeColor", null) || palette.text,
            read(style, "HorizontalAlign", "left"),
            read(style, "VerticalAlign", "bottom"),
            read(style, "Underline", false) ? 1 : 0,
            read(style, "DoubleUnderline", false) ? 1 : 0,
            read(style, "StrikeThrough", false) ? 1 : 0,
            read(cell, "Hyperlink", null) ? 1 : 0
        ].join("|");
        const s = getState(root);
        const cache = s?.paintStyleCache;
        if (cache) {
            const cached = cache.get(key);
            if (cached) return cached;
        }

        const horizontalAlignValue = read(style, "HorizontalAlign", "left");
        const verticalAlignValue = read(style, "VerticalAlign", "bottom");
        const paint = {
            backgroundColor: read(style, "BackgroundColor", null),
            foreColor: read(style, "ForeColor", null) || palette.text,
            horizontalAlignValue,
            verticalAlignValue,
            horizontalAlign: horizontalAlign(horizontalAlignValue),
            verticalBaseline: verticalBaseline(verticalAlignValue),
            underline: !!read(style, "Underline", false),
            doubleUnderline: !!read(style, "DoubleUnderline", false),
            strikeThrough: !!read(style, "StrikeThrough", false),
            hyperlink: !!read(cell, "Hyperlink", null)
        };

        if (cache) {
            cache.set(key, paint);
            limitCache(cache, maxStyleCacheSize);
        }
        return paint;
    }

    function measureTextWidth(ctx, root, font, value) {
        const s = getState(root);
        const cache = s?.textMetricsCache;
        if (!cache) return ctx.measureText(value).width;

        const key = font + "\n" + value;
        const cached = cache.get(key);
        if (cached != null) return cached;

        const width = ctx.measureText(value).width;
        cache.set(key, width);
        limitCache(cache, maxTextMeasureCacheSize);
        return width;
    }

    function drawImage(ctx, root, imageUrl, x, y, w, h) {
        let image = imageCache.get(imageUrl);
        if (!image) {
            image = new Image();
            image.onload = () => {
                const s = getState(root);
                if (s?.model) requestCanvasRedraw(root, "image", "content");
            };
            image.src = imageUrl;
            imageCache.set(imageUrl, image);
        }

        if (image.complete && image.naturalWidth > 0) {
            const scale = Math.min(w / image.naturalWidth, h / image.naturalHeight);
            const dw = image.naturalWidth * scale;
            const dh = image.naturalHeight * scale;
            ctx.drawImage(image, x + (w - dw) / 2, y + (h - dh) / 2, dw, dh);
        }
    }

    function drawBorders(ctx, root, style, x, y, w, h, palette) {
        drawBorder(ctx, root, read(style, "BorderTop", null), x, y, x + w, y, palette);
        drawBorder(ctx, root, read(style, "BorderRight", null), x + w, y, x + w, y + h, palette);
        drawBorder(ctx, root, read(style, "BorderBottom", null), x, y + h, x + w, y + h, palette);
        drawBorder(ctx, root, read(style, "BorderLeft", null), x, y, x, y + h, palette);
    }

    function drawBorder(ctx, root, border, x1, y1, x2, y2, palette) {
        const paint = getBorderPaint(root, border, palette);
        if (!paint) return;
        ctx.strokeStyle = paint.color;
        ctx.lineWidth = paint.width;
        ctx.setLineDash(paint.dash);
        ctx.beginPath();
        if (paint.double) {
            const horizontal = Math.abs(y1 - y2) < 0.5;
            const offset = 1.5;
            if (horizontal) {
                ctx.moveTo(x1, y1 - offset);
                ctx.lineTo(x2, y2 - offset);
                ctx.moveTo(x1, y1 + offset);
                ctx.lineTo(x2, y2 + offset);
            } else {
                ctx.moveTo(x1 - offset, y1);
                ctx.lineTo(x2 - offset, y2);
                ctx.moveTo(x1 + offset, y1);
                ctx.lineTo(x2 + offset, y2);
            }
        } else {
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
        }
        ctx.stroke();
        ctx.setLineDash([]);
    }

    function getBorderPaint(root, border, palette) {
        const style = read(border, "Style", "none");
        if (!border || style === "none") return null;

        const key = [
            "border",
            style,
            read(border, "Color", null) || palette.border
        ].join("|");
        const s = getState(root);
        const cache = s?.paintStyleCache;
        if (cache) {
            const cached = cache.get(key);
            if (cached) return cached;
        }

        const paint = {
            color: read(border, "Color", null) || palette.border,
            width: style === "medium" ? 2 : style === "thick" ? 3 : 1,
            dash: style === "dashed" ? [4, 3] : style === "dotted" ? [1, 2] : [],
            double: style === "double"
        };
        if (cache) {
            cache.set(key, paint);
            limitCache(cache, maxStyleCacheSize);
        }
        return paint;
    }

    function horizontalAlign(value) {
        if (value === "right") return "right";
        if (value === "center") return "center";
        return "left";
    }

    function textAnchorX(value, x, w, padding) {
        if (value === "right") return x + w - padding;
        if (value === "center") return x + w / 2;
        return x + padding;
    }

    function verticalBaseline(value) {
        if (value === "top") return "top";
        if (value === "middle") return "middle";
        return "bottom";
    }

    function textAnchorY(value, y, h, padding) {
        if (value === "top") return y + padding;
        if (value === "middle") return y + h / 2;
        return y + h - padding;
    }

    function textXForDecoration(align, x, width) {
        if (align === "right") return x - width;
        if (align === "center") return x - width / 2;
        return x;
    }
})();
