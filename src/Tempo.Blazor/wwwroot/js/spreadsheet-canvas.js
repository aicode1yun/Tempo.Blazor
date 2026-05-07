window.tmSpreadsheetCanvas = window.tmSpreadsheetCanvas || {};

(function () {
    const stateKey = "__tmSpreadsheetCanvas";
    const imageCache = new Map();
    const maxTextMeasureCacheSize = 5000;
    const maxStyleCacheSize = 1200;
    const maxDisplayValueCacheSize = 10000;
    const defaultCellFontFamily = "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";
    const keyboardRepeatPauseMs = 500;
    const keyboardRepeatAccelerationEnabledDefault = true;

    if (window.tmSpreadsheetCanvas.keyboardRepeatAccelerationEnabled == null) {
        window.tmSpreadsheetCanvas.keyboardRepeatAccelerationEnabled = keyboardRepeatAccelerationEnabledDefault;
    }

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

    function maxLogicalScrollLeft(root) {
        return Math.max(0, (root?.scrollWidth || 0) - (root?.clientWidth || 0));
    }

    function maxLogicalScrollTop(root) {
        return Math.max(0, (root?.scrollHeight || 0) - (root?.clientHeight || 0));
    }

    function clampLogicalScrollLeft(root, value) {
        return Math.max(0, Math.min(maxLogicalScrollLeft(root), Number(value) || 0));
    }

    function clampLogicalScrollTop(root, value) {
        return Math.max(0, Math.min(maxLogicalScrollTop(root), Number(value) || 0));
    }

    function getLogicalScrollLeft(root) {
        const s = getState(root);
        return s ? s.logicalScrollLeft || 0 : root?.scrollLeft || 0;
    }

    function getLogicalScrollTop(root) {
        const s = getState(root);
        return s ? s.logicalScrollTop || 0 : root?.scrollTop || 0;
    }

    function setLogicalScroll(root, left, top, source) {
        const s = getState(root);
        if (!s) return false;
        const nextLeft = clampLogicalScrollLeft(root, left);
        const nextTop = clampLogicalScrollTop(root, top);
        const changed = Math.abs(nextLeft - (s.logicalScrollLeft || 0)) > 0.5
            || Math.abs(nextTop - (s.logicalScrollTop || 0)) > 0.5;
        s.logicalScrollLeft = nextLeft;
        s.logicalScrollTop = nextTop;
        if (s.model) {
            write(s.model, "ScrollLeft", nextLeft);
            write(s.model, "ScrollTop", nextTop);
        }
        if (changed) updateLocalEditorPosition(root);

        if (changed && source === "keyboard") {
            s.lastLogicalKeyboardScrollAt = performance.now();
        }

        if (s.metrics) {
            s.metrics.logicalScrollLeft = nextLeft;
            s.metrics.logicalScrollTop = nextTop;
            s.metrics.nativeScrollLeft = root.scrollLeft || 0;
            s.metrics.nativeScrollTop = root.scrollTop || 0;
            if (changed) {
                if (source === "keyboard") {
                    s.metrics.logicalKeyboardScrollCount += 1;
                } else if (source === "native") {
                    s.metrics.logicalNativeScrollCount += 1;
                } else if (source === "programmatic") {
                    s.metrics.logicalProgrammaticScrollCount += 1;
                } else if (source === "wheel") {
                    s.metrics.logicalWheelScrollCount += 1;
                } else if (source === "pointer") {
                    s.metrics.logicalPointerScrollCount += 1;
                }
            }
        }

        return changed;
    }

    function syncLogicalScrollFromNative(root, source) {
        const s = getState(root);
        if (!s) return false;
        return setLogicalScroll(root, root.scrollLeft || 0, root.scrollTop || 0, source || "native");
    }

    function syncModelViewport(root, model) {
        write(model, "ScrollLeft", getLogicalScrollLeft(root));
        write(model, "ScrollTop", getLogicalScrollTop(root));
        write(model, "ViewportWidth", root.clientWidth || read(model, "ViewportWidth", 1));
        write(model, "ViewportHeight", root.clientHeight || read(model, "ViewportHeight", 1));
    }

    function syncNativeScrollFromLogical(root) {
        const s = getState(root);
        if (!s) return;
        const left = clampLogicalScrollLeft(root, s.logicalScrollLeft || 0);
        const top = clampLogicalScrollTop(root, s.logicalScrollTop || 0);
        if (Math.abs((root.scrollLeft || 0) - left) <= 0.5 && Math.abs((root.scrollTop || 0) - top) <= 0.5) {
            return;
        }

        s.ownScrollUntil = performance.now() + 250;
        if (s.metrics) {
            s.metrics.scrollToCount += 1;
            s.metrics.logicalNativeSyncCount += 1;
        }
        root.scrollTo({ left, top, behavior: "auto" });
    }

    function scheduleNativeScrollSync(root, delay) {
        const s = getState(root);
        if (!s) return;
        if (s.nativeScrollSyncTimer) clearTimeout(s.nativeScrollSyncTimer);
        s.nativeScrollSyncTimer = setTimeout(() => {
            s.nativeScrollSyncTimer = 0;
            syncNativeScrollFromLogical(root);
            requestViewportSync(root, true);
        }, delay ?? 90);
    }

    function dirtyRank(kind) {
        return ({ selection: 1, headers: 2, content: 3, full: 4 })[kind] || 4;
    }

    function mergeRedrawKind(current, next) {
        if (!current) return next || "full";
        return dirtyRank(next) > dirtyRank(current) ? next : current;
    }

    function requestPaint(root, reason, kind) {
        const s = getState(root);
        if (!s || !s.model) return;
        const nextKind = kind || "full";
        const previousKind = s.dirtyKind || "";
        const mergedKind = mergeRedrawKind(previousKind, nextKind);
        const isMergedRequest = !!s.paintRequested;

        if (s.metrics) {
            s.metrics.paintRequestCount += 1;
            if (isMergedRequest) {
                s.metrics.mergedPaintRequestCount += 1;
                s.metrics.discardedIntermediatePaintCount += 1;
            }
        }

        s.pendingPaintRequestCount = (s.pendingPaintRequestCount || 0) + 1;
        s.dirtyKind = mergedKind;
        if (!previousKind || dirtyRank(nextKind) >= dirtyRank(previousKind)) {
            s.dirtyReason = reason || "unknown";
        } else if (!s.dirtyReason) {
            s.dirtyReason = reason || "unknown";
        }

        if (s.paintRequested) return;
        s.paintRequested = true;
        s.paintFrame = requestAnimationFrame(() => {
            const redrawReason = s.dirtyReason || "unknown";
            const redrawKind = s.dirtyKind || "full";
            const oldScrollLeft = Number.isFinite(s.paintedScrollLeft) ? s.paintedScrollLeft : read(s.model, "ScrollLeft", getLogicalScrollLeft(root));
            const oldScrollTop = Number.isFinite(s.paintedScrollTop) ? s.paintedScrollTop : read(s.model, "ScrollTop", getLogicalScrollTop(root));
            const requestCount = s.pendingPaintRequestCount || 1;
            s.paintFrame = 0;
            s.paintRequested = false;
            s.pendingPaintRequestCount = 0;
            s.dirtyReason = "";
            s.dirtyKind = "";
            if (s.metrics) {
                s.metrics.paintFrameCount += 1;
                s.metrics.maxMergedPaintRequestsPerFrame = Math.max(s.metrics.maxMergedPaintRequestsPerFrame || 0, requestCount);
                s.metrics.lastRedrawSource = redrawReason;
                s.metrics.lastRedrawKind = redrawKind;
            }
            if (redrawKind === "selection") {
                syncModelViewport(root, s.model);
                renderSelectionOverlay(root, s.model);
                if (s.metrics) s.metrics.selectionPaintFrameCount += 1;
            } else if (redrawReason === "scroll" && tryBitmapScrollRedraw(root, s.model, oldScrollLeft, oldScrollTop)) {
                s.paintedScrollLeft = getLogicalScrollLeft(root);
                s.paintedScrollTop = getLogicalScrollTop(root);
                if (s.metrics) s.metrics.contentPaintFrameCount += 1;
                return;
            } else {
                syncModelViewport(root, s.model);
                renderModel(root, s.canvas, s.model);
                s.paintedScrollLeft = getLogicalScrollLeft(root);
                s.paintedScrollTop = getLogicalScrollTop(root);
                if (s.metrics) s.metrics.contentPaintFrameCount += 1;
            }
        });
    }

    function requestCanvasRedraw(root, reason, kind) {
        requestPaint(root, reason, kind);
    }

    function scheduleLocalRender(root) {
        requestPaint(root, "scroll", "full");
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
        requestViewportSync(root, true);
    }

    function requestViewportSync(root, force) {
        const s = getState(root);
        if (!s) return;
        s.syncRequested = true;
        s.syncForce = s.syncForce || !!force;
        if (s.metrics) s.metrics.syncRequestCount += 1;
        if (s.syncFrame) return;
        s.syncFrame = requestAnimationFrame(() => {
            const syncForce = !!s.syncForce;
            s.syncFrame = 0;
            s.syncRequested = false;
            s.syncForce = false;
            sendViewport(root, syncForce);
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
        const scrollLeft = getLogicalScrollLeft(root);
        const scrollTop = getLogicalScrollTop(root);
        s.syncedScrollLeft = scrollLeft;
        s.syncedScrollTop = scrollTop;
        s.lastViewportSync = performance.now();
        const selection = getSelectionSnapshot(root);
        if (s.metrics) s.metrics.viewportCallbackCount += 1;

        s.dotNet.invokeMethodAsync(
            "OnCanvasViewportChanged",
            scrollLeft,
            scrollTop,
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
        const movedX = Math.abs(getLogicalScrollLeft(root) - (s.syncedScrollLeft || 0));
        const movedY = Math.abs(getLogicalScrollTop(root) - (s.syncedScrollTop || 0));
        const shouldSyncNow = force
            || movedX > Math.max(160, (root.clientWidth || 0) * 0.35)
            || movedY > Math.max(120, (root.clientHeight || 0) * 0.35)
            || now - (s.lastViewportSync || 0) > 140;

        if (shouldSyncNow) {
            requestViewportSync(root, !!force);
            return;
        }

        if (!s.viewportTimer) {
            s.viewportTimer = setTimeout(() => sendViewport(root, false), 120);
        }
    }

    function debounceViewportSyncAfterPaint(root, force, delay) {
        const s = getState(root);
        if (!s) return;
        s.postPaintDebouncedViewportForce = s.postPaintDebouncedViewportForce || !!force;
        if (s.postPaintDebouncedViewportFrame) return;
        s.postPaintDebouncedViewportFrame = requestAnimationFrame(() => {
            const nextForce = !!s.postPaintDebouncedViewportForce;
            s.postPaintDebouncedViewportFrame = 0;
            s.postPaintDebouncedViewportForce = false;

            if (nextForce) {
                requestViewportSync(root, true);
                return;
            }

            if (s.viewportTimer) clearTimeout(s.viewportTimer);
            s.viewportTimer = setTimeout(() => sendViewport(root, false), delay ?? 120);
        });
    }

    function normalizeWheelDelta(ev, root) {
        const lineSize = 32;
        const pageHeight = Math.max(1, root.clientHeight || 1);
        const pageWidth = Math.max(1, root.clientWidth || 1);
        let dx = Number(ev.deltaX) || 0;
        let dy = Number(ev.deltaY) || 0;

        if (ev.deltaMode === WheelEvent.DOM_DELTA_LINE) {
            dx *= lineSize;
            dy *= lineSize;
        } else if (ev.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
            dx *= pageWidth;
            dy *= pageHeight;
        }

        if (ev.shiftKey && !dx && dy) {
            dx = dy;
            dy = 0;
        }

        return { dx, dy };
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
        write(model, "ScrollLeft", getLogicalScrollLeft(root));
        write(model, "ScrollTop", getLogicalScrollTop(root));
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
            keyCommandCallbackCount: 0,
            paintRequestCount: 0,
            paintFrameCount: 0,
            selectionPaintFrameCount: 0,
            contentPaintFrameCount: 0,
            mergedPaintRequestCount: 0,
            discardedIntermediatePaintCount: 0,
            maxMergedPaintRequestsPerFrame: 0,
            syncRequestCount: 0,
            nativeScrollEventCount: 0,
            scrollToCount: 0,
            keyboardScrollToCount: 0,
            programmaticEnsureScrollToCount: 0,
            logicalScrollLeft: 0,
            logicalScrollTop: 0,
            nativeScrollLeft: 0,
            nativeScrollTop: 0,
            logicalKeyboardScrollCount: 0,
            logicalNativeScrollCount: 0,
            logicalProgrammaticScrollCount: 0,
            logicalWheelScrollCount: 0,
            logicalPointerScrollCount: 0,
            wheelEventCount: 0,
            wheelPreventedCount: 0,
            logicalNativeSyncCount: 0,
            ignoredOwnNativeScrollEventCount: 0,
            ownNativeScrollEventCount: 0,
            userNativeScrollEventCount: 0,
            wheelNativeScrollEventCount: 0,
            scrollbarNativeScrollEventCount: 0,
            staleNativeScrollEventCount: 0,
            keyboardInteractions: 0,
            keyboardRepeatAccelerationEnabled: keyboardRepeatAccelerationEnabledDefault,
            keyboardRepeatSequenceCount: 0,
            keyboardRepeatResetCount: 0,
            keyboardRepeatEventCount: 0,
            keyboardRepeatAcceleratedEventCount: 0,
            keyboardRepeatLastKey: "",
            keyboardRepeatLastStep: 1,
            keyboardRepeatMaxStep: 1,
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
            lastDrawCellsMs: 0,
            lastDrawCellContentMs: 0,
            lastVisibleCellCount: 0,
            lastTextCount: 0,
            lastContextSaveClipRestoreCount: 0,
            lastContextSaveMs: 0,
            lastContextClipMs: 0,
            lastContextRestoreMs: 0,
            ensureCellVisibleCount: 0,
            ensureCellVisibleTotalMs: 0,
            drawCellsTotalMs: 0,
            drawCellContentTotalMs: 0,
            contextSaveClipRestoreTotalCount: 0,
            contextSaveTotalMs: 0,
            contextClipTotalMs: 0,
            contextRestoreTotalMs: 0,
            fastCellPathCount: 0,
            slowCellPathCount: 0,
            skippedEmptyCellContentCount: 0,
            unclippedTextCount: 0,
            clippedTextCount: 0,
            contextStateSetCount: 0,
            contextStateSkipCount: 0,
            cellSnapshotHitCount: 0,
            cellSnapshotMissCount: 0,
            cellSnapshotStoreCount: 0,
            cellSnapshotInvalidationCount: 0,
            cellSnapshotCacheSize: 0,
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
            x: ev.clientX - rect.left + getLogicalScrollLeft(root) - rowHeaderWidth,
            y: ev.clientY - rect.top + getLogicalScrollTop(root) - columnHeaderHeight
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

    function getFormulaReferenceCells(root, model) {
        const s = getState(root);
        const cells = read(model, "Cells", []);
        if (s
            && s.formulaReferenceSource === cells
            && s.formulaReferenceRevision === s.modelRevision
            && Array.isArray(s.formulaReferenceCells)) {
            return s.formulaReferenceCells;
        }

        const references = [];
        for (const cell of cells) {
            if (Number(read(cell, "FormulaRefColorIndex", -1)) >= 0) {
                references.push(cell);
            }
        }

        if (s) {
            s.formulaReferenceSource = cells;
            s.formulaReferenceRevision = s.modelRevision;
            s.formulaReferenceCells = references;
        }

        return references;
    }

    function updateCellSelectionFlags(model, row, col, active, selected, selectionEnd) {
        const cell = findCell(model, row, col);
        if (!cell) return;
        write(cell, "Active", !!active);
        write(cell, "Selected", !!selected);
        write(cell, "SelectionEnd", !!selectionEnd);
    }

    function updateLocalActiveCell(root, row, col, extendSelection, source) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return;
        markLocalInteraction(root, source || "pointer");

        const previousActive = parseCellRef(read(model, "ActiveCellRef", "A1"));
        const selection = read(model, "Selection", {});
        const startRow = extendSelection ? read(selection, "StartRow", row) : row;
        const startCol = extendSelection ? read(selection, "StartCol", col) : col;

        write(model, "ActiveCellRef", toCellRef(row, col));
        write(selection, "StartRow", startRow);
        write(selection, "StartCol", startCol);
        write(selection, "EndRow", row);
        write(selection, "EndCol", col);

        if (previousActive.row !== row || previousActive.col !== col) {
            updateCellSelectionFlags(model, previousActive.row, previousActive.col, false, false, false);
        }
        updateCellSelectionFlags(model, row, col, true, true, true);

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
        const contentX = getLogicalScrollLeft(root) + localX;
        const contentY = getLogicalScrollTop(root) + localY;
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
        const beforeLeft = getLogicalScrollLeft(root);
        const beforeTop = getLogicalScrollTop(root);

        if (dx || dy) {
            const scrolled = setLogicalScroll(root, beforeLeft + dx, beforeTop + dy, "pointer");
            updateDragSelectionTarget(root, point.clientX, point.clientY);
            if (scrolled) {
                requestCanvasRedraw(root, "scroll", "full");
                scheduleNativeScrollSync(root, 120);
                debounceViewportSyncAfterPaint(root, false, 120);
            } else {
                requestCanvasRedraw(root, "drag-autoscroll", "selection");
            }
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

    function debounceSelectionSync(root, delay) {
        const s = getState(root);
        if (!s || !s.model) return;
        if (s.selectionSyncTimer) clearTimeout(s.selectionSyncTimer);
        s.selectionSyncTimer = setTimeout(() => {
            s.selectionSyncTimer = 0;
            sendSelection(root);
        }, delay ?? 90);
    }

    function ensureCellVisibleLocal(root, row, col) {
        const started = performance.now();
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
        let nextLeft = getLogicalScrollLeft(root);
        let nextTop = getLogicalScrollTop(root);

        if (left < nextLeft) nextLeft = left;
        else if (right > nextLeft + bodyWidth) nextLeft = right - bodyWidth;

        if (top < nextTop) nextTop = top;
        else if (bottom > nextTop + bodyHeight) nextTop = bottom - bodyHeight;

        nextLeft = Math.max(0, nextLeft);
        nextTop = Math.max(0, nextTop);
        if (setLogicalScroll(root, nextLeft, nextTop, "keyboard")) {
            if (s.metrics) {
                s.metrics.ensureCellVisibleCount += 1;
                s.metrics.ensureCellVisibleTotalMs += performance.now() - started;
            }
            return true;
        }

        if (s.metrics) {
            s.metrics.ensureCellVisibleCount += 1;
            s.metrics.ensureCellVisibleTotalMs += performance.now() - started;
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
        debounceSelectionSync(root, 90);
        if (scrolled) {
            requestPaint(root, "scroll", "full");
            scheduleNativeScrollSync(root, 90);
            debounceViewportSyncAfterPaint(root, false, 90);
        } else {
            notifyViewport(root, false);
        }
        return true;
    }

    function resetKeyboardRepeatState(root) {
        const s = getState(root);
        if (!s) return;
        if (s.keyboardRepeatKey && s.metrics) s.metrics.keyboardRepeatResetCount += 1;
        s.keyboardRepeatKey = "";
        s.keyboardRepeatStartedAt = 0;
        s.keyboardRepeatLastAt = 0;
        s.keyboardRepeatCount = 0;
    }

    function keyboardRepeatAccelerationEnabled(root) {
        const configured = window.tmSpreadsheetCanvas.keyboardRepeatAccelerationEnabled;
        return configured == null
            ? keyboardRepeatAccelerationEnabledDefault
            : configured !== false;
    }

    function keyboardRepeatStepForCount(count, elapsedMs) {
        if (count >= 45 || elapsedMs >= 1200) return 8;
        if (count >= 25 || elapsedMs >= 700) return 4;
        if (count >= 10 || elapsedMs >= 300) return 2;
        return 1;
    }

    function getNavigationStep(root, ev, dRow, dCol) {
        const s = getState(root);
        if (!s) return 1;

        const now = performance.now();
        const verticalArrow = ev.key === "ArrowDown" || ev.key === "ArrowUp";
        const canAccelerate = verticalArrow
            && dRow !== 0
            && dCol === 0
            && !ev.shiftKey
            && !read(s.model, "IsFormulaPointMode", false)
            && !s.editor
            && keyboardRepeatAccelerationEnabled(root);

        if (!canAccelerate || !ev.repeat) {
            resetKeyboardRepeatState(root);
            if (s.metrics) {
                s.metrics.keyboardRepeatAccelerationEnabled = keyboardRepeatAccelerationEnabled(root);
                s.metrics.keyboardRepeatLastKey = "";
                s.metrics.keyboardRepeatLastStep = 1;
            }
            return 1;
        }

        const sameSequence = s.keyboardRepeatKey === ev.key
            && now - (s.keyboardRepeatLastAt || 0) <= keyboardRepeatPauseMs;
        if (!sameSequence) {
            if (s.keyboardRepeatKey && s.metrics) s.metrics.keyboardRepeatResetCount += 1;
            s.keyboardRepeatKey = ev.key;
            s.keyboardRepeatStartedAt = now;
            s.keyboardRepeatCount = 0;
            if (s.metrics) s.metrics.keyboardRepeatSequenceCount += 1;
        }

        s.keyboardRepeatLastAt = now;
        s.keyboardRepeatCount += 1;
        const elapsed = Math.max(0, now - (s.keyboardRepeatStartedAt || now));
        const step = keyboardRepeatStepForCount(s.keyboardRepeatCount, elapsed);
        if (s.metrics) {
            s.metrics.keyboardRepeatAccelerationEnabled = true;
            s.metrics.keyboardRepeatEventCount += 1;
            s.metrics.keyboardRepeatLastKey = ev.key;
            s.metrics.keyboardRepeatLastStep = step;
            s.metrics.keyboardRepeatMaxStep = Math.max(s.metrics.keyboardRepeatMaxStep || 1, step);
            if (step > 1) s.metrics.keyboardRepeatAcceleratedEventCount += 1;
        }

        return step;
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
        if (scrolled) {
            requestPaint(root, "scroll", "full");
            scheduleNativeScrollSync(root, 90);
            scheduleForcedViewportSync(root);
        } else {
            notifyViewport(root, false);
        }
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
            case "ArrowUp": handled = navigateLocal(root, -getNavigationStep(root, ev, -1, 0), 0, ev.shiftKey); break;
            case "ArrowDown": handled = navigateLocal(root, getNavigationStep(root, ev, 1, 0), 0, ev.shiftKey); break;
            case "ArrowLeft": resetKeyboardRepeatState(root); handled = navigateLocal(root, 0, -1, ev.shiftKey); break;
            case "ArrowRight": resetKeyboardRepeatState(root); handled = navigateLocal(root, 0, 1, ev.shiftKey); break;
            case "Tab": resetKeyboardRepeatState(root); handled = navigateLocal(root, 0, ev.shiftKey ? -1 : 1, false); break;
            case "Home": resetKeyboardRepeatState(root); handled = moveLocalTo(root, active.row, 0, ev.shiftKey); break;
            case "End": resetKeyboardRepeatState(root); handled = moveLocalTo(root, active.row, read(model, "ColumnCount", active.col + 1) - 1, ev.shiftKey); break;
        }

        if (handled) {
            ev.preventDefault();
            ev.stopImmediatePropagation();
        }

        return handled;
    }

    function handleCommandKey(root, ev) {
        const s = getState(root);
        if (!s?.dotNet || !s.model) return false;

        const key = ev.key || "";
        const shortcutKey = key.length === 1 ? key.toLowerCase() : key;
        const isShortcut = ev.ctrlKey || ev.metaKey;
        const isShortcutCommand = isShortcut && (
            ["c", "v", "x", "z", "y", "b", "i", "u", "a", "1", "5"].includes(shortcutKey) ||
            key === "Home" ||
            key === "End"
        );
        const isEditCommand = key === "Enter" || key === "F2" || key === "Escape" || key === "Delete";
        const isTextCommand = key.length === 1 && !ev.altKey && !ev.ctrlKey && !ev.metaKey;
        if (!isShortcutCommand && !isEditCommand && !isTextCommand) return false;

        ev.preventDefault();
        ev.stopImmediatePropagation();
        if ((key === "Enter" || key === "F2")
            && !read(s.model, "IsFormulaPointMode", false)
            && !read(s.model, "IsFormatPainterActive", false)) {
            openLocalEditorAtActive(root);
            return true;
        }

        if (isTextCommand && key !== "=" && !read(s.model, "IsFormulaPointMode", false) && !read(s.model, "IsFormatPainterActive", false)) {
            openLocalEditorAtActive(root, key);
            return true;
        }

        if (s.metrics) s.metrics.keyCommandCallbackCount += 1;
        s.dotNet.invokeMethodAsync(
            "OnCanvasKeyCommand",
            key,
            !!ev.shiftKey,
            !!ev.ctrlKey,
            !!ev.altKey,
            !!ev.metaKey
        ).catch(() => {});
        return true;
    }

    function openLocalEditorAtActive(root, initialValue) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return;

        const active = parseCellRef(read(model, "ActiveCellRef", "A1"));
        openLocalEditor(root, {
            row: active.row,
            col: active.col,
            cell: findCell(model, active.row, active.col)
        }, initialValue);
    }

    function isEditableKeyTarget(target) {
        if (!(target instanceof Element)) return false;
        return !!target.closest("input, textarea, select, [contenteditable=''], [contenteditable='true'], .tm-spreadsheet-canvas-grid__editor");
    }

    function closeLocalEditor(root, commit) {
        const s = getState(root);
        const editor = s?.editor;
        if (!s || !editor) return;

        s.editor = null;
        if (editor.input.__tmClosing) return;
        editor.input.__tmClosing = true;
        if (commit && s.model) {
            const value = editor.input.value;
            const cell = findCell(s.model, editor.row, editor.col);
            const previousValue = editor.initialValue ?? (cell ? (read(cell, "Value", "") || "") : "");
            const changed = value !== previousValue;
            if (changed) {
                if (cell) write(cell, "Value", value);
                invalidateCellSnapshot(root, editor.row, editor.col);
                requestCanvasRedraw(root, "edit", "content");
                setTimeout(() => {
                    s.dotNet.invokeMethodAsync("OnCanvasCellEditCommitted", editor.row, editor.col, value).catch(() => {});
                }, 0);
            }
        }

        editor.input.remove();
        root.focus?.({ preventScroll: true });
    }

    function commitLocalEditorAndNavigate(root, dRow, dCol, extendSelection) {
        closeLocalEditor(root, true);
        navigateLocal(root, dRow, dCol, !!extendSelection);
    }

    function getEditorCellRect(root, editor) {
        const s = getState(root);
        const model = s?.model;
        if (!model || !editor) return null;

        const row = getFrameByIndex(read(model, "Rows", []), editor.row);
        const col = getFrameByIndex(read(model, "Columns", []), editor.col);
        if (!row || !col) return null;

        const x = screenX(model, col);
        const y = screenY(model, row);
        const w = read(col, "Width", 64);
        const h = read(row, "Height", 20);
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const visible = w > 0
            && h > 0
            && x + w >= rowHeaderWidth
            && y + h >= columnHeaderHeight
            && x <= (root.clientWidth || 0)
            && y <= (root.clientHeight || 0);

        return { x, y, w, h, visible };
    }

    function updateLocalEditorPosition(root) {
        const s = getState(root);
        const editor = s?.editor;
        if (!editor) return;

        const rect = getEditorCellRect(root, editor);
        if (!rect || !rect.visible) {
            editor.suppressBlur = true;
            editor.input.style.visibility = "hidden";
            setTimeout(() => {
                if (editor) editor.suppressBlur = false;
            }, 0);
            return;
        }

        editor.input.style.display = "";
        editor.input.style.visibility = "";
        editor.input.style.left = `${(root.scrollLeft || 0) + rect.x}px`;
        editor.input.style.top = `${(root.scrollTop || 0) + rect.y}px`;
        editor.input.style.width = `${Math.max(16, rect.w)}px`;
        editor.input.style.height = `${Math.max(8, rect.h)}px`;
    }

    function openLocalEditor(root, hit, initialValue) {
        const s = getState(root);
        const model = s?.model;
        if (!s || !model || !hit) return;

        closeLocalEditor(root, false);

        const cell = hit.cell || findCell(model, hit.row, hit.col);
        const value = initialValue ?? (cell ? read(cell, "Value", "") || "" : "");

        const input = document.createElement("input");
        input.className = "tm-spreadsheet-canvas-grid__editor";
        input.value = value;
        input.addEventListener("click", ev => ev.stopPropagation());
        input.addEventListener("dblclick", ev => ev.stopPropagation());
        input.addEventListener("keydown", ev => {
            if (ev.key === "Enter") {
                ev.preventDefault();
                ev.stopPropagation();
                commitLocalEditorAndNavigate(root, ev.shiftKey ? -1 : 1, 0, false);
            } else if (ev.key === "Tab") {
                ev.preventDefault();
                ev.stopPropagation();
                commitLocalEditorAndNavigate(root, 0, ev.shiftKey ? -1 : 1, false);
            } else if (ev.key === "ArrowUp") {
                ev.preventDefault();
                ev.stopPropagation();
                commitLocalEditorAndNavigate(root, -1, 0, false);
            } else if (ev.key === "ArrowDown") {
                ev.preventDefault();
                ev.stopPropagation();
                commitLocalEditorAndNavigate(root, 1, 0, false);
            } else if (ev.key === "ArrowLeft") {
                ev.preventDefault();
                ev.stopPropagation();
                commitLocalEditorAndNavigate(root, 0, -1, false);
            } else if (ev.key === "ArrowRight") {
                ev.preventDefault();
                ev.stopPropagation();
                commitLocalEditorAndNavigate(root, 0, 1, false);
            } else if (ev.key === "Escape") {
                ev.preventDefault();
                ev.stopPropagation();
                closeLocalEditor(root, false);
            } else {
                ev.stopPropagation();
            }
        });
        input.addEventListener("blur", () => {
            if (s.editor?.suppressBlur) return;
            closeLocalEditor(root, true);
        });

        root.appendChild(input);
        s.editor = { input, row: hit.row, col: hit.col, initialValue: value };
        updateLocalEditorPosition(root);
        input.focus({ preventScroll: true });
        if (initialValue === undefined || initialValue === null) {
            input.select();
        } else {
            input.setSelectionRange(input.value.length, input.value.length);
        }
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
            paintFrame: 0,
            paintRequested: false,
            pendingPaintRequestCount: 0,
            dirtyKind: "",
            dirtyReason: "",
            syncFrame: 0,
            syncRequested: false,
            syncForce: false,
            viewportTimer: 0,
            viewportInFlight: false,
            viewportPending: false,
            viewportPendingForce: false,
            logicalScrollLeft: root.scrollLeft || 0,
            logicalScrollTop: root.scrollTop || 0,
            paintedScrollLeft: root.scrollLeft || 0,
            paintedScrollTop: root.scrollTop || 0,
            nativeScrollSyncTimer: 0,
            ownScrollUntil: 0,
            lastLogicalKeyboardScrollAt: 0,
            lastNativeScrollInput: "",
            keyboardRepeatKey: "",
            keyboardRepeatStartedAt: 0,
            keyboardRepeatLastAt: 0,
            keyboardRepeatCount: 0,
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
            cellSnapshotCache: new Map(),
            fontSignature: "",
            visibleLayoutCache: null,
            metrics: createDebugMetrics(),
            resize: null,
            listeners: []
        };

        const onScroll = () => {
            const now = performance.now();
            const ownScroll = now < (s.ownScrollUntil || 0);
            if (ownScroll) {
                if (s.metrics) {
                    s.metrics.nativeScrollEventCount += 1;
                    s.metrics.ignoredOwnNativeScrollEventCount += 1;
                    s.metrics.ownNativeScrollEventCount += 1;
                    s.metrics.nativeScrollLeft = root.scrollLeft || 0;
                    s.metrics.nativeScrollTop = root.scrollTop || 0;
                }
                updateLocalEditorPosition(root);
                return;
            }

            const nativeBehindLogical = s.lastInteractionSource === "keyboard"
                && now - (s.lastLogicalKeyboardScrollAt || 0) < 180
                && (Math.abs((root.scrollLeft || 0) - getLogicalScrollLeft(root)) > 0.5
                    || Math.abs((root.scrollTop || 0) - getLogicalScrollTop(root)) > 0.5);
            if (nativeBehindLogical) {
                if (s.metrics) {
                    s.metrics.nativeScrollEventCount += 1;
                    s.metrics.staleNativeScrollEventCount += 1;
                    s.metrics.nativeScrollLeft = root.scrollLeft || 0;
                    s.metrics.nativeScrollTop = root.scrollTop || 0;
                }
                scheduleNativeScrollSync(root, 30);
                updateLocalEditorPosition(root);
                return;
            }

            syncLogicalScrollFromNative(root, "native");
            updateLocalEditorPosition(root);
            if (s.metrics) {
                s.metrics.nativeScrollEventCount += 1;
                s.metrics.userNativeScrollEventCount += 1;
                if (s.lastNativeScrollInput === "wheel") s.metrics.wheelNativeScrollEventCount += 1;
                else s.metrics.scrollbarNativeScrollEventCount += 1;
                s.metrics.scrollInteractions += 1;
            }
            s.lastNativeScrollInput = "";
            scheduleLocalRender(root);
            notifyViewport(root, false);
        };
        const onWheel = ev => {
            if (ev.ctrlKey || !s.model) return;

            const delta = normalizeWheelDelta(ev, root);
            if (!delta.dx && !delta.dy) return;

            s.lastNativeScrollInput = "wheel";
            if (s.metrics) {
                s.metrics.wheelEventCount += 1;
                s.metrics.wheelPreventedCount += 1;
            }

            ev.preventDefault();
            markLocalInteraction(root, "scroll");
            const changed = setLogicalScroll(
                root,
                getLogicalScrollLeft(root) + delta.dx,
                getLogicalScrollTop(root) + delta.dy,
                "wheel");

            if (!changed) return;

            requestPaint(root, "scroll", "full");
            scheduleNativeScrollSync(root, 120);
            debounceViewportSyncAfterPaint(root, false, 120);
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
            if (resize.kind === "column") invalidateColumnSnapshots(root, resize.index);
            else invalidateRowSnapshots(root, resize.index);
            dotNet.invokeMethodAsync(method, resize.index, next).catch(() => {});
            ev.preventDefault();
        };
        const onKeyDown = ev => {
            if (isEditableKeyTarget(ev.target)) return;
            if (s.editor) return;
            if (handleNavigationKey(root, ev)) return;
            handleCommandKey(root, ev);
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
        const onFocusOut = () => {
            syncNativeScrollFromLogical(root);
            requestViewportSync(root, true);
        };

        root.addEventListener("scroll", onScroll, { passive: true });
        root.addEventListener("wheel", onWheel, { passive: false });
        root.addEventListener("pointermove", onPointerMove);
        root.addEventListener("pointerdown", onPointerDownWrapper);
        root.addEventListener("pointerup", onPointerUp);
        root.addEventListener("keydown", onKeyDown, true);
        root.addEventListener("click", onClick);
        root.addEventListener("dblclick", onDblClick);
        root.addEventListener("contextmenu", onContextMenu);
        root.addEventListener("focusout", onFocusOut);
        s.listeners.push(
            ["scroll", onScroll],
            ["wheel", onWheel, { passive: false }],
            ["pointermove", onPointerMove],
            ["pointerdown", onPointerDownWrapper],
            ["pointerup", onPointerUp],
            ["keydown", onKeyDown, true],
            ["click", onClick],
            ["dblclick", onDblClick],
            ["contextmenu", onContextMenu],
            ["focusout", onFocusOut]
        );

        if (typeof ResizeObserver !== "undefined") {
            s.resizeObserver = new ResizeObserver(() => {
                s.palette = buildPalette(root);
                clearCellSnapshots(root, "resize");
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
        for (const [event, listener, options] of s.listeners || []) {
            root.removeEventListener(event, listener, options);
        }
        if (s.resizeObserver) s.resizeObserver.disconnect();
        if (s.paintFrame) cancelAnimationFrame(s.paintFrame);
        if (s.syncFrame) cancelAnimationFrame(s.syncFrame);
        if (s.pointerFrame) cancelAnimationFrame(s.pointerFrame);
        if (s.dragAutoscrollFrame) cancelAnimationFrame(s.dragAutoscrollFrame);
        if (s.selectionSyncFrame) cancelAnimationFrame(s.selectionSyncFrame);
        if (s.postPaintDebouncedViewportFrame) cancelAnimationFrame(s.postPaintDebouncedViewportFrame);
        if (s.viewportTimer) clearTimeout(s.viewportTimer);
        if (s.nativeScrollSyncTimer) clearTimeout(s.nativeScrollSyncTimer);
        if (s.selectionSyncTimer) clearTimeout(s.selectionSyncTimer);
        closeLocalEditor(root, false);
        root.style.cursor = "";
        delete root[stateKey];
    };

    window.tmSpreadsheetCanvas.invalidateCellSnapshots = function (root, payload) {
        const s = getState(root);
        if (!s) return;

        if (read(payload, "Clear", false)) {
            clearCellSnapshots(root, "external");
        }

        invalidateCellSnapshotRefs(root, read(payload, "Cells", []));
        invalidateCellSnapshotRows(root, read(payload, "Rows", []));
        invalidateCellSnapshotColumns(root, read(payload, "Columns", []));
        updateCacheMetrics(root);
    };

    window.tmSpreadsheetCanvas.render = function (root, canvas, model) {
        if (!root || !canvas || !model) return;
        const s = getState(root);
        model = preserveLocalInteraction(root, model);
        syncModelViewport(root, model);
        if (s) {
            s.model = model;
            s.palette = buildPalette(root);
            s.modelRevision = (s.modelRevision || 0) + 1;
            updateLocalEditorPosition(root);
            s.syncedScrollLeft = getLogicalScrollLeft(root);
            s.syncedScrollTop = getLogicalScrollTop(root);
            const frameVersion = Number(read(model, "InteractionVersion", 0)) || 0;
            if (frameVersion > s.interactionVersion) s.interactionVersion = frameVersion;
            requestPaint(root, "dotnet-frame", "full");
            return;
        }
        renderModel(root, canvas, model);
    };

    window.tmSpreadsheetCanvas.getDebugMetrics = function (root) {
        const s = getState(root);
        if (s?.metrics) {
            s.metrics.keyboardRepeatAccelerationEnabled = keyboardRepeatAccelerationEnabled(root);
            s.metrics.logicalScrollLeft = getLogicalScrollLeft(root);
            s.metrics.logicalScrollTop = getLogicalScrollTop(root);
            s.metrics.nativeScrollLeft = root.scrollLeft || 0;
            s.metrics.nativeScrollTop = root.scrollTop || 0;
        }
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
        let nextLeft = getLogicalScrollLeft(root);
        let nextTop = getLogicalScrollTop(root);

        if (!frozenColumn) {
            const visibleLeft = nextLeft + rowHeaderWidth;
            const visibleRight = nextLeft + root.clientWidth;
            if (left < visibleLeft) nextLeft = left - rowHeaderWidth;
            else if (right > visibleRight) nextLeft = right - root.clientWidth;
        }

        if (!frozenRow) {
            const visibleTop = nextTop + columnHeaderHeight;
            const visibleBottom = nextTop + root.clientHeight;
            if (top < visibleTop) nextTop = top - columnHeaderHeight;
            else if (bottom > visibleBottom) nextTop = bottom - root.clientHeight;
        }

        const s = getState(root);
        if (s?.metrics) {
            s.metrics.scrollToCount += 1;
            s.metrics.programmaticEnsureScrollToCount += 1;
        }
        if (s) {
            setLogicalScroll(root, nextLeft, nextTop, "programmatic");
            syncModelViewport(root, s.model || {});
            requestPaint(root, "programmatic-scroll", "full");
            syncNativeScrollFromLogical(root);
            requestViewportSync(root, true);
        } else {
            root.scrollTo({ left: Math.max(0, nextLeft), top: Math.max(0, nextTop), behavior: "auto" });
        }
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
        metrics.cellSnapshotCacheSize = s.cellSnapshotCache?.size || 0;
        if (layout) {
            metrics.visibleRowCount = layout.rows.length;
            metrics.visibleColumnCount = layout.columns.length;
        }
    }

    function resetFrameInstrumentation(metrics) {
        if (!metrics) return;
        metrics.lastDrawCellsMs = 0;
        metrics.lastDrawCellContentMs = 0;
        metrics.lastContextSaveClipRestoreCount = 0;
        metrics.lastContextSaveMs = 0;
        metrics.lastContextClipMs = 0;
        metrics.lastContextRestoreMs = 0;
    }

    function resetContextState(ctx) {
        ctx.__tmState = {
            font: null,
            fillStyle: null,
            strokeStyle: null,
            lineWidth: null,
            textBaseline: null,
            textAlign: null
        };
    }

    function getContextState(ctx) {
        return ctx.__tmState || (ctx.__tmState = {});
    }

    function setContextProperty(ctx, property, value, metrics) {
        const state = getContextState(ctx);
        if (state[property] === value) {
            if (metrics) metrics.contextStateSkipCount += 1;
            return;
        }

        ctx[property] = value;
        state[property] = value;
        if (metrics) metrics.contextStateSetCount += 1;
    }

    function setContextFont(ctx, value, metrics) {
        setContextProperty(ctx, "font", value, metrics);
    }

    function setContextFillStyle(ctx, value, metrics) {
        setContextProperty(ctx, "fillStyle", value, metrics);
    }

    function setContextStrokeStyle(ctx, value, metrics) {
        setContextProperty(ctx, "strokeStyle", value, metrics);
    }

    function setContextLineWidth(ctx, value, metrics) {
        setContextProperty(ctx, "lineWidth", value, metrics);
    }

    function setContextTextBaseline(ctx, value, metrics) {
        setContextProperty(ctx, "textBaseline", value, metrics);
    }

    function setContextTextAlign(ctx, value, metrics) {
        setContextProperty(ctx, "textAlign", value, metrics);
    }

    function contextSave(ctx, metrics) {
        const started = performance.now();
        ctx.save();
        if (metrics) {
            const elapsed = performance.now() - started;
            metrics.lastContextSaveClipRestoreCount += 1;
            metrics.contextSaveClipRestoreTotalCount += 1;
            metrics.lastContextSaveMs += elapsed;
            metrics.contextSaveTotalMs += elapsed;
        }
    }

    function contextClip(ctx, metrics) {
        const started = performance.now();
        ctx.clip();
        if (metrics) {
            const elapsed = performance.now() - started;
            metrics.lastContextSaveClipRestoreCount += 1;
            metrics.contextSaveClipRestoreTotalCount += 1;
            metrics.lastContextClipMs += elapsed;
            metrics.contextClipTotalMs += elapsed;
        }
    }

    function contextRestore(ctx, metrics) {
        const started = performance.now();
        ctx.restore();
        resetContextState(ctx);
        if (metrics) {
            const elapsed = performance.now() - started;
            metrics.lastContextSaveClipRestoreCount += 1;
            metrics.contextSaveClipRestoreTotalCount += 1;
            metrics.lastContextRestoreMs += elapsed;
            metrics.contextRestoreTotalMs += elapsed;
        }
    }

    function cellSnapshotId(row, col) {
        return row + ":" + col;
    }

    function cellSnapshotKey(root, cell, style, paint, displayValue, imageUrl) {
        const borders = [
            borderSnapshotKey(read(style, "BorderTop", null)),
            borderSnapshotKey(read(style, "BorderRight", null)),
            borderSnapshotKey(read(style, "BorderBottom", null)),
            borderSnapshotKey(read(style, "BorderLeft", null))
        ].join("/");

        return [
            read(cell, "Value", ""),
            displayValue ?? "",
            imageUrl || "",
            getCanvasFont(root, style),
            paint.foreColor || "",
            paint.backgroundColor || "",
            paint.horizontalAlignValue || "",
            paint.verticalAlignValue || "",
            paint.underline ? 1 : 0,
            paint.doubleUnderline ? 1 : 0,
            paint.strikeThrough ? 1 : 0,
            paint.hyperlink ? 1 : 0,
            borders
        ].join("\u001f");
    }

    function borderSnapshotKey(border) {
        if (!border) return "";
        return [
            read(border, "Style", "none"),
            read(border, "Color", "")
        ].join(":");
    }

    function clearCellSnapshots(root, reason) {
        const s = getState(root);
        if (!s?.cellSnapshotCache) return;
        const removed = s.cellSnapshotCache.size;
        if (!removed) return;
        s.cellSnapshotCache.clear();
        if (s.metrics) s.metrics.cellSnapshotInvalidationCount += removed;
    }

    function invalidateCellSnapshot(root, row, col) {
        const s = getState(root);
        if (!s?.cellSnapshotCache) return;
        if (s.cellSnapshotCache.delete(cellSnapshotId(row, col)) && s.metrics) {
            s.metrics.cellSnapshotInvalidationCount += 1;
        }
    }

    function invalidateColumnSnapshots(root, col) {
        const s = getState(root);
        if (!s?.cellSnapshotCache) return;
        let removed = 0;
        for (const key of [...s.cellSnapshotCache.keys()]) {
            if (Number(key.split(":")[1]) === col) {
                s.cellSnapshotCache.delete(key);
                removed++;
            }
        }

        if (removed && s.metrics) s.metrics.cellSnapshotInvalidationCount += removed;
    }

    function invalidateRowSnapshots(root, row) {
        const s = getState(root);
        if (!s?.cellSnapshotCache) return;
        let removed = 0;
        for (const key of [...s.cellSnapshotCache.keys()]) {
            if (Number(key.split(":")[0]) === row) {
                s.cellSnapshotCache.delete(key);
                removed++;
            }
        }

        if (removed && s.metrics) s.metrics.cellSnapshotInvalidationCount += removed;
    }

    function pruneCellSnapshots(root, visibleIds) {
        const s = getState(root);
        if (!s?.cellSnapshotCache || !visibleIds) return;
        let removed = 0;
        for (const key of [...s.cellSnapshotCache.keys()]) {
            if (!visibleIds.has(key)) {
                s.cellSnapshotCache.delete(key);
                removed++;
            }
        }

        if (removed && s.metrics) s.metrics.cellSnapshotInvalidationCount += removed;
    }

    function invalidateCellSnapshotRefs(root, refs) {
        if (!Array.isArray(refs)) return;
        for (const cellRef of refs) {
            const cell = parseCellRef(cellRef);
            invalidateCellSnapshot(root, cell.row, cell.col);
        }
    }

    function invalidateCellSnapshotRows(root, rows) {
        if (!Array.isArray(rows)) return;
        for (const row of rows) {
            const rowIndex = Number(row);
            if (Number.isFinite(rowIndex) && rowIndex >= 0) invalidateRowSnapshots(root, rowIndex);
        }
    }

    function invalidateCellSnapshotColumns(root, columns) {
        if (!Array.isArray(columns)) return;
        for (const col of columns) {
            const colIndex = Number(col);
            if (Number.isFinite(colIndex) && colIndex >= 0) invalidateColumnSnapshots(root, colIndex);
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
        resetFrameInstrumentation(metrics);

        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        resetContextState(ctx);
        ctx.clearRect(0, 0, width, height);

        const palette = s?.palette || buildPalette(root);

        setContextFillStyle(ctx, palette.surface, metrics);
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
        resetFrameInstrumentation(metrics);

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

        const newScrollLeft = getLogicalScrollLeft(root);
        const newScrollTop = getLogicalScrollTop(root);
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
        resetContextState(ctx);
        contextSave(ctx, metrics);
        ctx.beginPath();
        ctx.rect(rowHeaderWidth, columnHeaderHeight, bodyWidth, bodyHeight);
        contextClip(ctx, metrics);
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
            contextSave(ctx, metrics);
            ctx.beginPath();
            ctx.rect(strip.x, strip.y, strip.width, strip.height);
            contextClip(ctx, metrics);
            setContextFillStyle(ctx, palette.surface, metrics);
            ctx.fillRect(strip.x, strip.y, strip.width, strip.height);
            cellMetrics = addCellMetrics(cellMetrics, drawCells(ctx, root, model, palette, { allowSnapshotSkip: false, storeSnapshots: false }));
            contextRestore(ctx, metrics);
        }

        contextRestore(ctx, metrics);
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
        resetContextState(ctx);
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
        resetContextState(ctx);
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
        const metrics = getState(root)?.metrics;
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const layout = getVisibleLayout(root, model, width, height);

        setContextFillStyle(ctx, palette.elevated, metrics);
        ctx.fillRect(0, 0, width, columnHeaderHeight);
        ctx.fillRect(0, 0, rowHeaderWidth, height);
        setContextStrokeStyle(ctx, palette.border, metrics);
        ctx.strokeRect(0.5, 0.5, rowHeaderWidth - 0.5, columnHeaderHeight - 0.5);

        setContextFont(ctx, "500 11px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace", metrics);
        setContextTextAlign(ctx, "center", metrics);
        setContextTextBaseline(ctx, "middle", metrics);
        setContextFillStyle(ctx, palette.muted, metrics);

        for (const col of layout.columns) {
            const x = col.x;
            const w = col.width;
            setContextFillStyle(ctx, palette.elevated, metrics);
            ctx.fillRect(x, 0, w, columnHeaderHeight);
            setContextStrokeStyle(ctx, palette.border, metrics);
            ctx.strokeRect(Math.floor(x) + 0.5, 0.5, w, columnHeaderHeight - 0.5);
            setContextFillStyle(ctx, palette.muted, metrics);
            ctx.fillText(col.label, x + w / 2, columnHeaderHeight / 2);
        }

        for (const row of layout.rows) {
            const y = row.y;
            const h = row.height;
            setContextFillStyle(ctx, palette.elevated, metrics);
            ctx.fillRect(0, y, rowHeaderWidth, h);
            setContextStrokeStyle(ctx, palette.border, metrics);
            ctx.strokeRect(0.5, Math.floor(y) + 0.5, rowHeaderWidth - 0.5, h);
            setContextFillStyle(ctx, palette.muted, metrics);
            ctx.fillText(String(row.index + 1), rowHeaderWidth / 2, y + h / 2);
        }
    }

    function drawCells(ctx, root, model, palette, options) {
        const drawStarted = performance.now();
        const s = getState(root);
        const debugMetrics = s?.metrics;
        const settings = options || {};
        const allowSnapshotSkip = settings.allowSnapshotSkip !== false;
        const storeSnapshots = settings.storeSnapshots !== false;
        const snapshotCache = s?.cellSnapshotCache;
        const visibleSnapshotIds = snapshotCache && storeSnapshots ? new Set() : null;
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
            const imageUrl = read(cell, "ImageUrl", null);
            const value = imageUrl ? null : getDisplayValue(root, cell);
            const hasBackground = !!paint.backgroundColor && paint.backgroundColor !== palette.surface;
            const hasContent = !!imageUrl || (value != null && value !== "");
            if (!hasBackground && !hasContent) {
                if (debugMetrics) debugMetrics.skippedEmptyCellContentCount += 1;
                continue;
            }

            const snapshotId = cellSnapshotId(row, col);
            const snapshotKey = cellSnapshotKey(root, cell, style, paint, value, imageUrl);
            if (visibleSnapshotIds) visibleSnapshotIds.add(snapshotId);

            const textStarted = performance.now();
            let textDrawn = false;
            const snapshot = snapshotCache?.get(snapshotId);
            if (allowSnapshotSkip && canUseCellSnapshot(snapshot, snapshotKey, x, y, w, h)) {
                ctx.drawImage(snapshot.canvas, x, y, w, h);
                textDrawn = snapshot.textDrawn;
                if (debugMetrics) debugMetrics.cellSnapshotHitCount += 1;
            } else {
                if (debugMetrics) debugMetrics.cellSnapshotMissCount += 1;
                if (snapshotCache && storeSnapshots && !imageUrl) {
                    const nextSnapshot = renderCellSnapshot(root, cell, style, paint, palette, w, h, value, imageUrl);
                    if (nextSnapshot) {
                        nextSnapshot.key = snapshotKey;
                        nextSnapshot.x = x;
                        nextSnapshot.y = y;
                        nextSnapshot.w = w;
                        nextSnapshot.h = h;
                        snapshotCache.set(snapshotId, nextSnapshot);
                        ctx.drawImage(nextSnapshot.canvas, x, y, w, h);
                        textDrawn = nextSnapshot.textDrawn;
                        if (debugMetrics) debugMetrics.cellSnapshotStoreCount += 1;
                    } else {
                        textDrawn = drawCellBody(ctx, root, cell, style, paint, palette, x, y, w, h, value, imageUrl);
                    }
                } else {
                    textDrawn = drawCellBody(ctx, root, cell, style, paint, palette, x, y, w, h, value, imageUrl);
                }
            }

            if (textDrawn) {
                metrics.texts += 1;
            }
            metrics.textMs += performance.now() - textStarted;
        }

        pruneCellSnapshots(root, visibleSnapshotIds);

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
            if (!hasBorders(style)) continue;

            drawBorders(ctx, root, style, x, y, w, h, palette);
        }

        const drawMs = performance.now() - drawStarted;
        if (debugMetrics) {
            debugMetrics.lastDrawCellsMs += drawMs;
            debugMetrics.drawCellsTotalMs += drawMs;
        }
        return metrics;
    }

    function drawSelection(ctx, root, model, palette, width, height) {
        const metrics = getState(root)?.metrics;
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);
        const scrollLeft = read(model, "ScrollLeft", 0);
        const scrollTop = read(model, "ScrollTop", 0);
        const freezeRows = read(model, "FreezeRowCount", 0);
        const freezeCols = read(model, "FreezeColumnCount", 0);
        const hover = getState(root)?.hoverCell;
        const formulaPoint = read(model, "IsFormulaPointMode", false);
        const rows = read(model, "Rows", []);
        const columns = read(model, "Columns", []);
        const active = parseCellRef(read(model, "ActiveCellRef", "A1"));
        const selection = read(model, "Selection", {});
        const startRow = read(selection, "StartRow", active.row);
        const startCol = read(selection, "StartCol", active.col);
        const endRow = read(selection, "EndRow", active.row);
        const endCol = read(selection, "EndCol", active.col);
        const minRow = Math.min(startRow, endRow);
        const maxRow = Math.max(startRow, endRow);
        const minCol = Math.min(startCol, endCol);
        const maxCol = Math.max(startCol, endCol);

        const drawCellOverlay = (rowFrame, colFrame, drawFill, drawActive, drawHandle) => {
            const row = read(rowFrame, "Index", 0);
            const col = read(colFrame, "Index", 0);
            const frozenCol = col < freezeCols;
            const frozenRow = row < freezeRows;
            const x = rowHeaderWidth + read(colFrame, "Left", 0) - (frozenCol ? 0 : scrollLeft);
            const y = columnHeaderHeight + read(rowFrame, "Top", 0) - (frozenRow ? 0 : scrollTop);
            const w = read(colFrame, "Width", 0);
            const h = read(rowFrame, "Height", 0);
            if (x + w < rowHeaderWidth || y + h < columnHeaderHeight || x > width || y > height || w <= 0 || h <= 0) return;

            if (drawFill && !formulaPoint) {
                setContextFillStyle(ctx, palette.selectionFill, metrics);
                ctx.fillRect(x, y, w, h);
            }

            if (drawActive) {
                setContextStrokeStyle(ctx, palette.primary, metrics);
                setContextLineWidth(ctx, 2, metrics);
                ctx.strokeRect(Math.floor(x) + 1, Math.floor(y) + 1, Math.max(0, w - 2), Math.max(0, h - 2));
            }

            if (drawHandle) {
                setContextFillStyle(ctx, palette.primary, metrics);
                ctx.fillRect(x + w - 4, y + h - 4, 6, 6);
            }
        };

        for (const rowFrame of rows) {
            const row = read(rowFrame, "Index", -1);
            if (row < minRow || row > maxRow) continue;
            for (const colFrame of columns) {
                const col = read(colFrame, "Index", -1);
                if (col < minCol || col > maxCol) continue;
                drawCellOverlay(rowFrame, colFrame, true, row === active.row && col === active.col, row === endRow && col === endCol);
            }
        }

        if (hover) {
            const hoverRow = getFrameByIndex(rows, hover.row);
            const hoverCol = getFrameByIndex(columns, hover.col);
            const hoverInSelection = hover.row >= minRow && hover.row <= maxRow && hover.col >= minCol && hover.col <= maxCol;
            if (hoverRow && hoverCol && !hoverInSelection) {
                const frozenCol = hover.col < freezeCols;
                const frozenRow = hover.row < freezeRows;
                const x = rowHeaderWidth + read(hoverCol, "Left", 0) - (frozenCol ? 0 : scrollLeft);
                const y = columnHeaderHeight + read(hoverRow, "Top", 0) - (frozenRow ? 0 : scrollTop);
                const w = read(hoverCol, "Width", 0);
                const h = read(hoverRow, "Height", 0);
                if (x + w >= rowHeaderWidth && y + h >= columnHeaderHeight && x <= width && y <= height && w > 0 && h > 0) {
                    setContextFillStyle(ctx, "rgba(148, 163, 184, 0.12)", metrics);
                    ctx.fillRect(x, y, w, h);
                }
            }
        }

        const formulaReferenceCells = getFormulaReferenceCells(root, model);
        if (formulaReferenceCells.length === 0) return;

        for (const cell of formulaReferenceCells) {
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
                setContextFillStyle(ctx, refColor.fill, metrics);
                ctx.fillRect(x, y, w, h);
                setContextStrokeStyle(ctx, refColor.stroke, metrics);
                setContextLineWidth(ctx, 2, metrics);
                ctx.strokeRect(Math.floor(x) + 1, Math.floor(y) + 1, Math.max(0, w - 2), Math.max(0, h - 2));
            }
        }
    }

    function drawGridLines(ctx, root, model, palette, width, height, layout) {
        const metrics = getState(root)?.metrics;
        layout = layout || getVisibleLayout(root, model, width, height);
        const rowHeaderWidth = layout.rowHeaderWidth;
        const columnHeaderHeight = layout.columnHeaderHeight;

        setContextStrokeStyle(ctx, palette.subtle, metrics);
        setContextLineWidth(ctx, 1, metrics);
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

    function hasBorders(style) {
        return hasVisibleBorder(read(style, "BorderTop", null))
            || hasVisibleBorder(read(style, "BorderRight", null))
            || hasVisibleBorder(read(style, "BorderBottom", null))
            || hasVisibleBorder(read(style, "BorderLeft", null));
    }

    function hasVisibleBorder(border) {
        return !!border && read(border, "Style", "none") !== "none";
    }

    function isSimpleTextCell(cell, style, paint, palette, imageUrl) {
        if (imageUrl) return false;
        const backgroundColor = read(style, "BackgroundColor", null);
        const foreColor = read(style, "ForeColor", null);
        if (backgroundColor && backgroundColor !== palette.surface) return false;
        if (foreColor && foreColor !== palette.text) return false;
        if (paint.horizontalAlign !== "left" || paint.verticalBaseline !== "bottom") return false;
        if (paint.underline || paint.doubleUnderline || paint.strikeThrough || paint.hyperlink) return false;
        if (hasBorders(style)) return false;
        if (read(style, "Bold", false) || read(style, "Italic", false)) return false;
        const fontFamily = read(style, "FontFamily", null);
        return !fontFamily || fontFamily === defaultCellFontFamily;
    }

    function textBoundsForAlign(align, textX, textWidth) {
        if (align === "right" || align === "end") return { start: textX - textWidth, end: textX };
        if (align === "center") return { start: textX - textWidth / 2, end: textX + textWidth / 2 };
        return { start: textX, end: textX + textWidth };
    }

    function finishCellContentMetrics(metrics, started) {
        if (!metrics) return;
        const contentMs = performance.now() - started;
        metrics.lastDrawCellContentMs += contentMs;
        metrics.drawCellContentTotalMs += contentMs;
    }

    function canUseCellSnapshot(snapshot, key, x, y, w, h) {
        if (!snapshot || snapshot.key !== key) return false;
        return Math.abs(snapshot.x - x) <= 0.5
            && Math.abs(snapshot.y - y) <= 0.5
            && Math.abs(snapshot.w - w) <= 0.5
            && Math.abs(snapshot.h - h) <= 0.5;
    }

    function renderCellSnapshot(root, cell, style, paint, palette, w, h, value, imageUrl) {
        if (w <= 0 || h <= 0 || imageUrl) return null;
        const dpr = window.devicePixelRatio || 1;
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.round(w * dpr));
        canvas.height = Math.max(1, Math.round(h * dpr));
        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        resetContextState(ctx);
        const textDrawn = drawCellBody(ctx, root, cell, style, paint, palette, 0, 0, w, h, value, imageUrl);
        return { canvas, textDrawn, key: "", x: 0, y: 0, w, h };
    }

    function drawCellBody(ctx, root, cell, style, paint, palette, x, y, w, h, value, imageUrl) {
        const metrics = getState(root)?.metrics;
        const backgroundColor = paint.backgroundColor;
        if (backgroundColor && backgroundColor !== palette.surface) {
            setContextFillStyle(ctx, backgroundColor, metrics);
            ctx.fillRect(x, y, w, h);
        }

        if (!imageUrl && (value == null || value === "")) {
            if (metrics) metrics.skippedEmptyCellContentCount += 1;
            return false;
        }

        return drawCellContent(ctx, root, cell, style, paint, palette, x, y, w, h, value, imageUrl);
    }

    function drawCellContent(ctx, root, cell, style, paint, palette, x, y, w, h, displayValue, imageUrl) {
        const contentStarted = performance.now();
        const debugMetrics = getState(root)?.metrics;
        imageUrl = imageUrl ?? read(cell, "ImageUrl", null);
        if (imageUrl) {
            if (debugMetrics) debugMetrics.slowCellPathCount += 1;
            drawImage(ctx, root, imageUrl, x, y, w, h);
            finishCellContentMetrics(debugMetrics, contentStarted);
            return false;
        }

        const value = displayValue ?? getDisplayValue(root, cell);
        if (value == null || value === "") {
            if (debugMetrics) debugMetrics.skippedEmptyCellContentCount += 1;
            finishCellContentMetrics(debugMetrics, contentStarted);
            return false;
        }

        const font = getCanvasFont(root, style);
        const fontSize = Number(read(style, "FontSize", 10)) || 10;
        setContextFont(ctx, font, debugMetrics);
        setContextFillStyle(ctx, paint.foreColor || palette.text, debugMetrics);
        setContextTextBaseline(ctx, paint.verticalBaseline, debugMetrics);
        setContextTextAlign(ctx, paint.horizontalAlign, debugMetrics);

        const padding = 4;
        const textX = textAnchorX(paint.horizontalAlignValue, x, w, padding);
        const textY = textAnchorY(paint.verticalAlignValue, y, h, padding);
        const textWidth = measureTextWidth(ctx, root, font, value);
        const bounds = textBoundsForAlign(paint.horizontalAlign, textX, textWidth);
        const needsClip = bounds.start < x + 1
            || bounds.end > x + w - 1
            || fontSize + 4 > h;
        const hasDecoration = paint.underline || paint.doubleUnderline || paint.strikeThrough || paint.hyperlink;
        const simple = isSimpleTextCell(cell, style, paint, palette, imageUrl);

        if (needsClip) {
            if (debugMetrics) {
                debugMetrics.slowCellPathCount += 1;
                debugMetrics.clippedTextCount += 1;
            }
            contextSave(ctx, debugMetrics);
            ctx.beginPath();
            ctx.rect(x + 1, y + 1, Math.max(0, w - 2), Math.max(0, h - 2));
            contextClip(ctx, debugMetrics);
        } else if (debugMetrics) {
            if (simple && !hasDecoration) debugMetrics.fastCellPathCount += 1;
            else debugMetrics.slowCellPathCount += 1;
            debugMetrics.unclippedTextCount += 1;
        }

        ctx.fillText(value, textX, textY);

        if (hasDecoration) {
            const lineY = paint.strikeThrough ? textY - fontSize * 0.25 : textY + 2;
            const startX = textXForDecoration(paint.horizontalAlign, textX, textWidth);
            setContextStrokeStyle(ctx, paint.foreColor || palette.text, debugMetrics);
            setContextLineWidth(ctx, 1, debugMetrics);
            ctx.beginPath();
            ctx.moveTo(startX, lineY);
            ctx.lineTo(startX + textWidth, lineY);
            if (paint.doubleUnderline && !paint.strikeThrough) {
                ctx.moveTo(startX, lineY + 2);
                ctx.lineTo(startX + textWidth, lineY + 2);
            }
            ctx.stroke();
        }

        if (needsClip) contextRestore(ctx, debugMetrics);
        finishCellContentMetrics(debugMetrics, contentStarted);
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
        const metrics = getState(root)?.metrics;
        setContextStrokeStyle(ctx, paint.color, metrics);
        setContextLineWidth(ctx, paint.width, metrics);
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
