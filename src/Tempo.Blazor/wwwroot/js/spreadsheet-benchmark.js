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

    const measureKeyNavigation = async (grid, count) => {
        if (!grid) {
            return { durationMs: 0, totalDurationMs: 0, count: 0 };
        }

        grid.focus();
        const start = performance.now();
        for (let i = 0; i < count; i++) {
            grid.dispatchEvent(new KeyboardEvent("keydown", {
                key: "ArrowRight",
                code: "ArrowRight",
                bubbles: true,
                cancelable: true
            }));

            if ((i + 1) % 5 === 0) {
                await frame();
            }
        }

        await frame();
        await frame();
        const totalDurationMs = performance.now() - start;
        return { durationMs: count > 0 ? totalDurationMs / count : 0, totalDurationMs, count };
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
            const keyNavigation = await measureKeyNavigation(grid, settings.keyNavigationCount || 20);

            return {
                stableFrameMs,
                scrollFps: scroll.fps,
                scrollFrames: scroll.frames,
                scrollDurationMs: scroll.durationMs,
                maxFrameMs: scroll.maxFrameMs,
                scrollTop: scroll.scrollTop,
                scrollLeft: scroll.scrollLeft,
                keyNavigationMs: keyNavigation.durationMs,
                keyNavigationTotalMs: keyNavigation.totalDurationMs,
                keyNavigationCount: keyNavigation.count,
                usedJsHeapSize: getMemory()
            };
        }
    };
})();
