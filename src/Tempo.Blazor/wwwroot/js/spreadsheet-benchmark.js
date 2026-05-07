window.tmSpreadsheetBenchmark = (() => {
    const frame = () => new Promise(resolve => requestAnimationFrame(resolve));

    const waitForStableFrame = async () => {
        const start = performance.now();
        await frame();
        await frame();
        return performance.now() - start;
    };

    const measureScroll = async (grid, durationMs) => {
        if (!grid) {
            return { fps: 0, frames: 0, durationMs: 0, maxFrameMs: 0, scrollTop: 0, scrollLeft: 0 };
        }

        const maxTop = Math.max(0, grid.scrollHeight - grid.clientHeight);
        const maxLeft = Math.max(0, grid.scrollWidth - grid.clientWidth);
        const startTop = grid.scrollTop;
        const startLeft = grid.scrollLeft;
        const targetTop = maxTop > 0 ? Math.min(maxTop, startTop + Math.max(240, grid.clientHeight)) : startTop;
        const targetLeft = maxLeft > 0 ? Math.min(maxLeft, startLeft + Math.max(320, grid.clientWidth / 2)) : startLeft;

        let frames = 0;
        let maxFrameMs = 0;
        let previousTime = performance.now();
        const startTime = previousTime;

        await new Promise(resolve => {
            const step = now => {
                frames++;
                maxFrameMs = Math.max(maxFrameMs, now - previousTime);
                previousTime = now;

                const progress = Math.min(1, (now - startTime) / durationMs);
                grid.scrollTop = startTop + (targetTop - startTop) * progress;
                grid.scrollLeft = startLeft + (targetLeft - startLeft) * progress;

                if (progress < 1) {
                    requestAnimationFrame(step);
                } else {
                    resolve();
                }
            };

            requestAnimationFrame(step);
        });

        const measuredDurationMs = performance.now() - startTime;
        return {
            fps: measuredDurationMs > 0 ? frames * 1000 / measuredDurationMs : 0,
            frames,
            durationMs: measuredDurationMs,
            maxFrameMs,
            scrollTop: grid.scrollTop,
            scrollLeft: grid.scrollLeft
        };
    };

    const debugMetrics = grid => {
        if (!grid || !window.tmSpreadsheetCanvas?.getDebugMetrics) {
            return {};
        }

        return window.tmSpreadsheetCanvas.getDebugMetrics(grid) || {};
    };

    const diffDebugMetrics = (before, after) => ({
        nativeScrollEvents: (after.nativeScrollEventCount || 0) - (before.nativeScrollEventCount || 0),
        viewportCallbackCount: (after.viewportCallbackCount || 0) - (before.viewportCallbackCount || 0),
        selectionCallbackCount: (after.selectionCallbackCount || 0) - (before.selectionCallbackCount || 0),
        scrollToCount: (after.scrollToCount || 0) - (before.scrollToCount || 0),
        keyboardScrollToCount: (after.keyboardScrollToCount || 0) - (before.keyboardScrollToCount || 0),
        logicalKeyboardScrollCount: (after.logicalKeyboardScrollCount || 0) - (before.logicalKeyboardScrollCount || 0),
        logicalWheelScrollCount: (after.logicalWheelScrollCount || 0) - (before.logicalWheelScrollCount || 0),
        logicalPointerScrollCount: (after.logicalPointerScrollCount || 0) - (before.logicalPointerScrollCount || 0),
        wheelEventCount: (after.wheelEventCount || 0) - (before.wheelEventCount || 0),
        wheelPreventedCount: (after.wheelPreventedCount || 0) - (before.wheelPreventedCount || 0),
        dragAutoscrollFrames: (after.dragAutoscrollFrames || 0) - (before.dragAutoscrollFrames || 0),
        paintFrameCount: (after.paintFrameCount || 0) - (before.paintFrameCount || 0),
        contentPaintFrameCount: (after.contentPaintFrameCount || 0) - (before.contentPaintFrameCount || 0),
        selectionPaintFrameCount: (after.selectionPaintFrameCount || 0) - (before.selectionPaintFrameCount || 0),
        ensureCellVisibleMs: (after.ensureCellVisibleTotalMs || 0) - (before.ensureCellVisibleTotalMs || 0),
        ensureCellVisibleCount: (after.ensureCellVisibleCount || 0) - (before.ensureCellVisibleCount || 0),
        drawCellsMs: (after.drawCellsTotalMs || 0) - (before.drawCellsTotalMs || 0),
        drawCellContentMs: (after.drawCellContentTotalMs || 0) - (before.drawCellContentTotalMs || 0),
        contextSaveClipRestoreCount: (after.contextSaveClipRestoreTotalCount || 0) - (before.contextSaveClipRestoreTotalCount || 0)
    });

    const dispatchArrow = (grid, key) => {
        grid.dispatchEvent(new KeyboardEvent("keydown", {
            key,
            code: key,
            bubbles: true,
            cancelable: true
        }));
    };

    const settleInteraction = async () => {
        await frame();
        await frame();
    };

    const waitForBenchmarkIdle = async grid => {
        await settleInteraction();
        for (let i = 0; i < 20; i++) {
            const state = grid?.__tmSpreadsheetCanvas;
            if (!state) return;
            const hasPendingTimer = !!state.viewportTimer || !!state.nativeScrollSyncTimer || !!state.selectionSyncTimer;
            const hasPendingFrame = !!state.paintFrame
                || !!state.syncFrame
                || !!state.selectionSyncFrame
                || !!state.postPaintDebouncedViewportFrame
                || !!state.dragAutoscrollFrame;
            const hasInFlight = !!state.viewportInFlight || !!state.selectionInFlight;
            if (!hasPendingTimer && !hasPendingFrame && !hasInFlight) return;
            await frame();
        }
    };

    const resetGridViewport = async grid => {
        if (!grid) return;
        grid.scrollTop = 0;
        grid.scrollLeft = 0;
        grid.focus();
        await settleInteraction();
    };

    const dispatchViewportClick = async (grid, x, y) => {
        const rect = grid.getBoundingClientRect();
        grid.dispatchEvent(new MouseEvent("click", {
            clientX: Math.max(rect.left + 1, Math.min(rect.right - 1, rect.left + x)),
            clientY: Math.max(rect.top + 1, Math.min(rect.bottom - 1, rect.top + y)),
            bubbles: true,
            cancelable: true
        }));
        await settleInteraction();
    };

    const prepareTopLeftCell = grid => dispatchViewportClick(grid, 72, 48);

    const prepareBottomEdgeCell = grid => dispatchViewportClick(grid, 72, grid.clientHeight - 32);

    const prepareTopEdgeCellAfterScroll = async grid => {
        const maxTop = Math.max(0, grid.scrollHeight - grid.clientHeight);
        grid.scrollTop = Math.min(maxTop, Math.max(240, grid.clientHeight));
        grid.dispatchEvent(new Event("scroll", { bubbles: true }));
        await settleInteraction();
        await dispatchViewportClick(grid, 72, 48);
    };

    const prepareRightEdgeCell = grid => dispatchViewportClick(grid, grid.clientWidth - 32, 48);

    const measureKeyNavigation = async (grid, key, count, options) => {
        if (!grid) {
            return { durationMs: 0, totalDurationMs: 0, count: 0, debug: diffDebugMetrics({}, {}) };
        }

        const settings = options || {};
        if (settings.reset !== false) {
            await resetGridViewport(grid);
        }

        grid.focus();
        if (typeof settings.prepare === "function") {
            await settings.prepare(grid);
            grid.focus();
        }
        await waitForBenchmarkIdle(grid);

        const before = debugMetrics(grid);
        const start = performance.now();
        for (let i = 0; i < count; i++) {
            dispatchArrow(grid, key);

            if ((i + 1) % 5 === 0) {
                await frame();
            }
        }

        await settleInteraction();
        const totalDurationMs = performance.now() - start;
        return {
            durationMs: count > 0 ? totalDurationMs / count : 0,
            totalDurationMs,
            count,
            debug: diffDebugMetrics(before, debugMetrics(grid)),
            scrollTop: grid.scrollTop,
            scrollLeft: grid.scrollLeft
        };
    };

    const measureKeyboardScenarios = async (grid, settings) => {
        if (!grid) {
            return {};
        }

        const noScrollCount = settings.keyboardNoScrollCount || 8;
        const edgeCount = settings.keyboardEdgeCount || 80;
        const rightCount = settings.keyboardRightEdgeCount || 45;

        const noScrollDown = await measureKeyNavigation(grid, "ArrowDown", noScrollCount, { reset: true, prepare: prepareTopLeftCell });
        const edgeDown = await measureKeyNavigation(grid, "ArrowDown", edgeCount, { reset: true, prepare: prepareBottomEdgeCell });
        const edgeUp = await measureKeyNavigation(grid, "ArrowUp", edgeCount, { reset: true, prepare: prepareTopEdgeCellAfterScroll });
        const edgeRight = await measureKeyNavigation(grid, "ArrowRight", rightCount, { reset: true, prepare: prepareRightEdgeCell });
        const keyboardDebug = [noScrollDown, edgeDown, edgeUp, edgeRight].reduce((total, item) => ({
            nativeScrollEvents: total.nativeScrollEvents + (item.debug?.nativeScrollEvents || 0),
            scrollToCount: total.scrollToCount + (item.debug?.scrollToCount || 0),
            keyboardScrollToCount: total.keyboardScrollToCount + (item.debug?.keyboardScrollToCount || 0),
            logicalKeyboardScrollCount: total.logicalKeyboardScrollCount + (item.debug?.logicalKeyboardScrollCount || 0),
            ensureCellVisibleMs: total.ensureCellVisibleMs + (item.debug?.ensureCellVisibleMs || 0),
            ensureCellVisibleCount: total.ensureCellVisibleCount + (item.debug?.ensureCellVisibleCount || 0)
        }), {
            nativeScrollEvents: 0,
            scrollToCount: 0,
            keyboardScrollToCount: 0,
            logicalKeyboardScrollCount: 0,
            ensureCellVisibleMs: 0,
            ensureCellVisibleCount: 0
        });

        return {
            noScrollDown,
            edgeDown,
            edgeUp,
            edgeRight,
            keyboardDebug
        };
    };

    const measureWheelScroll = async (grid, durationMs) => {
        if (!grid) {
            return { fps: 0, frames: 0, durationMs: 0, debug: diffDebugMetrics({}, {}) };
        }

        await resetGridViewport(grid);
        const before = debugMetrics(grid);
        const start = performance.now();
        let frames = 0;
        let maxFrameMs = 0;
        let previous = start;
        await new Promise(resolve => {
            const step = now => {
                frames++;
                maxFrameMs = Math.max(maxFrameMs, now - previous);
                previous = now;

                const defaultAllowed = grid.dispatchEvent(new WheelEvent("wheel", {
                    deltaY: 96,
                    deltaX: 24,
                    bubbles: true,
                    cancelable: true
                }));

                if (defaultAllowed) {
                    const topBefore = grid.scrollTop;
                    const leftBefore = grid.scrollLeft;
                    grid.scrollTop = Math.min(grid.scrollHeight - grid.clientHeight, grid.scrollTop + 96);
                    grid.scrollLeft = Math.min(grid.scrollWidth - grid.clientWidth, grid.scrollLeft + 24);
                    if (grid.scrollTop !== topBefore || grid.scrollLeft !== leftBefore) {
                        grid.dispatchEvent(new Event("scroll", { bubbles: true }));
                    }
                }

                if (now - start < durationMs) {
                    requestAnimationFrame(step);
                } else {
                    resolve();
                }
            };
            requestAnimationFrame(step);
        });

        await settleInteraction();
        const totalDurationMs = performance.now() - start;
        return {
            fps: totalDurationMs > 0 ? frames * 1000 / totalDurationMs : 0,
            frames,
            durationMs: totalDurationMs,
            maxFrameMs,
            scrollTop: grid.scrollTop,
            scrollLeft: grid.scrollLeft,
            debug: diffDebugMetrics(before, debugMetrics(grid)),
            finalDebug: debugMetrics(grid)
        };
    };

    const measureDragAutoscroll = async (grid, durationMs) => {
        if (!grid) {
            return { durationMs: 0, dragAutoscrollFrames: 0, debug: diffDebugMetrics({}, {}) };
        }

        await resetGridViewport(grid);
        const rect = grid.getBoundingClientRect();
        const startX = Math.min(rect.right - 80, rect.left + 140);
        const startY = Math.min(rect.bottom - 80, rect.top + 90);
        const dragX = Math.min(rect.right - 8, startX + 40);
        const dragY = rect.bottom + 36;
        const before = debugMetrics(grid);
        const started = performance.now();
        const pointerId = 77;

        const PointerCtor = window.PointerEvent || window.MouseEvent;
        grid.dispatchEvent(new PointerCtor("pointerdown", {
            pointerId,
            pointerType: "mouse",
            clientX: startX,
            clientY: startY,
            bubbles: true,
            cancelable: true
        }));

        grid.dispatchEvent(new PointerCtor("pointermove", {
            pointerId,
            pointerType: "mouse",
            clientX: startX + 10,
            clientY: startY + 10,
            bubbles: true,
            cancelable: true
        }));

        while (performance.now() - started < durationMs) {
            grid.dispatchEvent(new PointerCtor("pointermove", {
                pointerId,
                pointerType: "mouse",
                clientX: dragX,
                clientY: dragY,
                bubbles: true,
                cancelable: true
            }));
            await frame();
        }

        grid.dispatchEvent(new PointerCtor("pointerup", {
            pointerId,
            pointerType: "mouse",
            clientX: dragX,
            clientY: dragY,
            bubbles: true,
            cancelable: true
        }));

        await settleInteraction();
        const after = debugMetrics(grid);
        return {
            durationMs: performance.now() - started,
            dragAutoscrollFrames: (after.dragAutoscrollFrames || 0) - (before.dragAutoscrollFrames || 0),
            scrollTop: grid.scrollTop,
            scrollLeft: grid.scrollLeft,
            debug: diffDebugMetrics(before, after)
        };
    };

    const getMemory = () => {
        if (performance.memory && typeof performance.memory.usedJSHeapSize === "number") {
            return performance.memory.usedJSHeapSize;
        }

        return null;
    };

    return {
        run: async (selector, options) => {
            const settings = options || {};
            const grid = document.querySelector(selector);
            const stableFrameMs = await waitForStableFrame();
            const scroll = await measureScroll(grid, settings.scrollDurationMs || 1200);
            const keyboard = await measureKeyboardScenarios(grid, settings);
            const wheel = await measureWheelScroll(grid, settings.wheelDurationMs || 900);
            const drag = await measureDragAutoscroll(grid, settings.dragAutoscrollDurationMs || 700);
            const finalDebug = debugMetrics(grid);

            return {
                stableFrameMs,
                scrollFps: scroll.fps,
                scrollFrames: scroll.frames,
                scrollDurationMs: scroll.durationMs,
                maxFrameMs: scroll.maxFrameMs,
                scrollTop: scroll.scrollTop,
                scrollLeft: scroll.scrollLeft,
                keyNavigationMs: keyboard.edgeDown?.durationMs || 0,
                keyNavigationTotalMs: keyboard.edgeDown?.totalDurationMs || 0,
                keyNavigationCount: keyboard.edgeDown?.count || 0,
                keyboardNoScrollMs: keyboard.noScrollDown?.durationMs || 0,
                keyboardNoScrollTotalMs: keyboard.noScrollDown?.totalDurationMs || 0,
                keyboardScrollDownMs: keyboard.edgeDown?.durationMs || 0,
                keyboardScrollDownTotalMs: keyboard.edgeDown?.totalDurationMs || 0,
                keyboardScrollUpMs: keyboard.edgeUp?.durationMs || 0,
                keyboardScrollUpTotalMs: keyboard.edgeUp?.totalDurationMs || 0,
                keyboardScrollRightMs: keyboard.edgeRight?.durationMs || 0,
                keyboardScrollRightTotalMs: keyboard.edgeRight?.totalDurationMs || 0,
                keyboardNativeScrollEvents: keyboard.keyboardDebug?.nativeScrollEvents || 0,
                keyboardScrollToCount: keyboard.keyboardDebug?.keyboardScrollToCount || 0,
                keyboardLogicalScrollCount: keyboard.keyboardDebug?.logicalKeyboardScrollCount || 0,
                keyboardEnsureCellVisibleMs: keyboard.keyboardDebug?.ensureCellVisibleMs || 0,
                keyboardEnsureCellVisibleCount: keyboard.keyboardDebug?.ensureCellVisibleCount || 0,
                wheelScrollFps: wheel.fps,
                wheelScrollMs: wheel.durationMs,
                wheelNativeScrollEvents: wheel.debug?.nativeScrollEvents || 0,
                wheelEventCount: wheel.debug?.wheelEventCount || 0,
                wheelPreventedCount: wheel.debug?.wheelPreventedCount || 0,
                wheelLogicalScrollCount: wheel.debug?.logicalWheelScrollCount || 0,
                wheelViewportCallbackCount: wheel.debug?.viewportCallbackCount || 0,
                wheelScrollToCount: wheel.debug?.scrollToCount || 0,
                wheelPaintFrameCount: wheel.debug?.paintFrameCount || 0,
                wheelContentPaintFrameCount: wheel.debug?.contentPaintFrameCount || 0,
                dragAutoscrollMs: drag.durationMs,
                dragAutoscrollFrames: drag.dragAutoscrollFrames || 0,
                dragLogicalScrollCount: drag.debug?.logicalPointerScrollCount || 0,
                dragViewportCallbackCount: drag.debug?.viewportCallbackCount || 0,
                dragSelectionCallbackCount: drag.debug?.selectionCallbackCount || 0,
                dragScrollToCount: drag.debug?.scrollToCount || 0,
                debugNativeScrollEvents: finalDebug.nativeScrollEventCount || 0,
                debugScrollToCount: finalDebug.scrollToCount || 0,
                debugKeyboardScrollToCount: finalDebug.keyboardScrollToCount || 0,
                debugEnsureCellVisibleMs: finalDebug.ensureCellVisibleTotalMs || 0,
                debugDrawCellsMs: finalDebug.drawCellsTotalMs || 0,
                debugDrawCellContentMs: finalDebug.drawCellContentTotalMs || 0,
                debugContextSaveClipRestoreCount: finalDebug.contextSaveClipRestoreTotalCount || 0,
                debugContextSaveMs: finalDebug.contextSaveTotalMs || 0,
                debugContextClipMs: finalDebug.contextClipTotalMs || 0,
                debugContextRestoreMs: finalDebug.contextRestoreTotalMs || 0,
                debugFastCellPathCount: finalDebug.fastCellPathCount || 0,
                debugSlowCellPathCount: finalDebug.slowCellPathCount || 0,
                debugUnclippedTextCount: finalDebug.unclippedTextCount || 0,
                debugClippedTextCount: finalDebug.clippedTextCount || 0,
                debugContextStateSetCount: finalDebug.contextStateSetCount || 0,
                debugContextStateSkipCount: finalDebug.contextStateSkipCount || 0,
                usedJsHeapSize: getMemory()
            };
        }
    };
})();
