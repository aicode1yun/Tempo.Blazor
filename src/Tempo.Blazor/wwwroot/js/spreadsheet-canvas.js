window.tmSpreadsheetCanvas = window.tmSpreadsheetCanvas || {};

(function () {
    const stateKey = "__tmSpreadsheetCanvas";
    const imageCache = new Map();

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

    function syncModelViewport(root, model) {
        write(model, "ScrollLeft", root.scrollLeft || 0);
        write(model, "ScrollTop", root.scrollTop || 0);
        write(model, "ViewportWidth", root.clientWidth || read(model, "ViewportWidth", 1));
        write(model, "ViewportHeight", root.clientHeight || read(model, "ViewportHeight", 1));
    }

    function scheduleLocalRender(root) {
        const s = getState(root);
        if (!s || !s.model || s.localFrame) return;
        s.localFrame = requestAnimationFrame(() => {
            s.localFrame = 0;
            syncModelViewport(root, s.model);
            renderModel(root, s.canvas, s.model);
        });
    }

    function sendViewport(root, force) {
        const s = getState(root);
        if (!s) return;
        if (s.viewportTimer) {
            clearTimeout(s.viewportTimer);
            s.viewportTimer = 0;
        }

        if (s.viewportInFlight && !force) {
            s.viewportPending = true;
            return;
        }

        s.viewportInFlight = true;
        s.viewportPending = false;
        s.syncedScrollLeft = root.scrollLeft || 0;
        s.syncedScrollTop = root.scrollTop || 0;
        s.lastViewportSync = performance.now();

        s.dotNet.invokeMethodAsync(
            "OnCanvasViewportChanged",
            root.scrollLeft || 0,
            root.scrollTop || 0,
            root.clientWidth || 0,
            root.clientHeight || 0
        ).catch(() => {}).finally(() => {
            s.viewportInFlight = false;
            if (s.viewportPending) {
                sendViewport(root, true);
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
            primarySubtle: css(root, "--tm-color-primary-subtle", "rgba(37, 99, 235, 0.12)")
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

    function findCell(model, row, col) {
        for (const cell of read(model, "Cells", [])) {
            if (read(cell, "Row", -1) === row && read(cell, "Col", -1) === col) {
                return cell;
            }
        }

        return null;
    }

    function updateLocalActiveCell(root, row, col, extendSelection) {
        const s = getState(root);
        const model = s?.model;
        if (!model) return;

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

        renderModel(root, s.canvas, model);
    }

    function closeLocalEditor(root, commit) {
        const s = getState(root);
        const editor = s?.editor;
        if (!s || !editor) return;

        s.editor = null;
        if (commit && s.model) {
            const value = editor.input.value;
            const cell = findCell(s.model, editor.row, editor.col);
            if (cell) write(cell, "Value", value);
            s.dotNet.invokeMethodAsync("OnCanvasCellEditCommitted", editor.row, editor.col, value).catch(() => {});
            renderModel(root, s.canvas, s.model);
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

    window.tmSpreadsheetCanvas.register = function (root, canvas, dotNet) {
        if (!root || !canvas || !dotNet) return;
        window.tmSpreadsheetCanvas.dispose(root);

        const s = {
            canvas,
            dotNet,
            model: null,
            localFrame: 0,
            viewportFrame: 0,
            viewportTimer: 0,
            viewportInFlight: false,
            viewportPending: false,
            lastViewportSync: 0,
            syncedScrollLeft: root.scrollLeft || 0,
            syncedScrollTop: root.scrollTop || 0,
            editor: null,
            palette: buildPalette(root),
            resize: null,
            listeners: []
        };

        const onScroll = () => {
            closeLocalEditor(root, true);
            scheduleLocalRender(root);
            notifyViewport(root, false);
        };
        const onPointerMove = ev => {
            if (s.resize) {
                root.style.cursor = s.resize.kind === "column" ? "col-resize" : "row-resize";
                return;
            }
            const hit = hitResize(root, ev);
            root.style.cursor = hit ? (hit.kind === "column" ? "col-resize" : "row-resize") : "";
        };
        const onPointerDown = ev => {
            const hit = hitResize(root, ev);
            if (!hit) return;
            s.resize = hit;
            root.setPointerCapture?.(ev.pointerId);
            ev.preventDefault();
        };
        const onPointerUp = ev => {
            if (!s.resize) return;
            const resize = s.resize;
            s.resize = null;
            root.releasePointerCapture?.(ev.pointerId);
            const delta = resize.kind === "column" ? ev.clientX - resize.start : ev.clientY - resize.start;
            const next = Math.max(resize.kind === "column" ? 16 : 8, resize.size + delta);
            const method = resize.kind === "column" ? "OnCanvasColumnResize" : "OnCanvasRowResize";
            dotNet.invokeMethodAsync(method, resize.index, next).catch(() => {});
            ev.preventDefault();
        };
        const onClick = ev => {
            if (s.resize) return;
            const p = toContentPoint(root, ev);
            const hit = hitCell(root, p);
            if (hit) {
                closeLocalEditor(root, true);
                if (!read(s.model, "IsFormulaPointMode", false)) {
                    updateLocalActiveCell(root, hit.row, hit.col, !!ev.shiftKey);
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
                updateLocalActiveCell(root, hit.row, hit.col, false);
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
        root.addEventListener("pointerdown", onPointerDown);
        root.addEventListener("pointerup", onPointerUp);
        root.addEventListener("click", onClick);
        root.addEventListener("dblclick", onDblClick);
        root.addEventListener("contextmenu", onContextMenu);
        s.listeners.push(
            ["scroll", onScroll],
            ["pointermove", onPointerMove],
            ["pointerdown", onPointerDown],
            ["pointerup", onPointerUp],
            ["click", onClick],
            ["dblclick", onDblClick],
            ["contextmenu", onContextMenu]
        );

        if (typeof ResizeObserver !== "undefined") {
            s.resizeObserver = new ResizeObserver(() => {
                s.palette = buildPalette(root);
                notifyViewport(root, true);
                if (s.model) renderModel(root, canvas, s.model);
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
        if (s.viewportTimer) clearTimeout(s.viewportTimer);
        closeLocalEditor(root, false);
        root.style.cursor = "";
        delete root[stateKey];
    };

    window.tmSpreadsheetCanvas.render = function (root, canvas, model) {
        if (!root || !canvas || !model) return;
        const s = getState(root);
        syncModelViewport(root, model);
        if (s) {
            s.model = model;
            s.palette = buildPalette(root);
            s.syncedScrollLeft = root.scrollLeft || 0;
            s.syncedScrollTop = root.scrollTop || 0;
        }
        renderModel(root, canvas, model);
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

    function renderModel(root, canvas, model) {
        const dpr = window.devicePixelRatio || 1;
        const width = Math.max(1, root.clientWidth || read(model, "ViewportWidth", 1));
        const height = Math.max(1, root.clientHeight || read(model, "ViewportHeight", 1));
        canvas.style.width = width + "px";
        canvas.style.height = height + "px";
        if (canvas.width !== Math.round(width * dpr) || canvas.height !== Math.round(height * dpr)) {
            canvas.width = Math.round(width * dpr);
            canvas.height = Math.round(height * dpr);
        }

        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);

        const palette = getState(root)?.palette || buildPalette(root);

        ctx.fillStyle = palette.surface;
        ctx.fillRect(0, 0, width, height);

        drawCells(ctx, root, model, palette);
        drawHeaders(ctx, model, palette, width, height);
    }

    function drawHeaders(ctx, model, palette, width, height) {
        const rowHeaderWidth = read(model, "RowHeaderWidth", 40);
        const columnHeaderHeight = read(model, "ColumnHeaderHeight", 20);

        ctx.fillStyle = palette.elevated;
        ctx.fillRect(0, 0, width, columnHeaderHeight);
        ctx.fillRect(0, 0, rowHeaderWidth, height);
        ctx.strokeStyle = palette.border;
        ctx.strokeRect(0.5, 0.5, rowHeaderWidth - 0.5, columnHeaderHeight - 0.5);

        ctx.font = "500 11px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillStyle = palette.muted;

        for (const col of read(model, "Columns", [])) {
            const x = screenX(model, col);
            const w = read(col, "Width", 0);
            if (x + w < rowHeaderWidth || x > width || w <= 0) continue;
            ctx.fillStyle = palette.elevated;
            ctx.fillRect(x, 0, w, columnHeaderHeight);
            ctx.strokeStyle = palette.border;
            ctx.strokeRect(Math.floor(x) + 0.5, 0.5, w, columnHeaderHeight - 0.5);
            ctx.fillStyle = palette.muted;
            ctx.fillText(read(col, "Label", ""), x + w / 2, columnHeaderHeight / 2);
        }

        for (const row of read(model, "Rows", [])) {
            const y = screenY(model, row);
            const h = read(row, "Height", 0);
            if (y + h < columnHeaderHeight || y > height || h <= 0) continue;
            ctx.fillStyle = palette.elevated;
            ctx.fillRect(0, y, rowHeaderWidth, h);
            ctx.strokeStyle = palette.border;
            ctx.strokeRect(0.5, Math.floor(y) + 0.5, rowHeaderWidth - 0.5, h);
            ctx.fillStyle = palette.muted;
            ctx.fillText(String(read(row, "Index", 0) + 1), rowHeaderWidth / 2, y + h / 2);
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

            const style = read(cell, "Style", {});
            const bg = read(style, "BackgroundColor", null);
            ctx.fillStyle = read(cell, "Selected", false) ? palette.primarySubtle : (bg || palette.surface);
            ctx.fillRect(x, y, w, h);

            drawCellContent(ctx, root, cell, style, palette, x, y, w, h);

            if (showGridLines) {
                ctx.strokeStyle = palette.subtle;
                ctx.lineWidth = 1;
                ctx.strokeRect(Math.floor(x) + 0.5, Math.floor(y) + 0.5, w, h);
            }

            drawBorders(ctx, style, x, y, w, h, palette);

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

    function drawCellContent(ctx, root, cell, style, palette, x, y, w, h) {
        const imageUrl = read(cell, "ImageUrl", null);
        if (imageUrl) {
            drawImage(ctx, root, imageUrl, x, y, w, h);
            return;
        }

        let value = read(cell, "Value", "");
        if (value == null || value === "") return;
        value = String(value);

        const fontSize = Number(read(style, "FontSize", 10)) || 10;
        const fontFamily = read(style, "FontFamily", null) || "ui-monospace, SFMono-Regular, Menlo, Consolas, monospace";
        const weight = read(style, "Bold", false) ? "700" : "400";
        const italic = read(style, "Italic", false) ? "italic " : "";
        ctx.font = `${italic}${weight} ${fontSize}pt ${fontFamily}`;
        ctx.fillStyle = read(style, "ForeColor", null) || palette.text;
        ctx.textBaseline = verticalBaseline(read(style, "VerticalAlign", "bottom"));
        ctx.textAlign = horizontalAlign(read(style, "HorizontalAlign", "left"));

        const padding = 4;
        const textX = textAnchorX(read(style, "HorizontalAlign", "left"), x, w, padding);
        const textY = textAnchorY(read(style, "VerticalAlign", "bottom"), y, h, padding);
        ctx.save();
        ctx.beginPath();
        ctx.rect(x + 1, y + 1, Math.max(0, w - 2), Math.max(0, h - 2));
        ctx.clip();
        ctx.fillText(value, textX, textY);

        const metrics = ctx.measureText(value);
        if (read(style, "Underline", false) || read(style, "StrikeThrough", false) || read(cell, "Hyperlink", null)) {
            const lineY = read(style, "StrikeThrough", false) ? textY - fontSize * 0.25 : textY + 2;
            const startX = textXForDecoration(ctx.textAlign, textX, metrics.width);
            ctx.strokeStyle = ctx.fillStyle;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(startX, lineY);
            ctx.lineTo(startX + metrics.width, lineY);
            ctx.stroke();
        }
        ctx.restore();
    }

    function drawImage(ctx, root, imageUrl, x, y, w, h) {
        let image = imageCache.get(imageUrl);
        if (!image) {
            image = new Image();
            image.onload = () => {
                const s = getState(root);
                if (s?.model) renderModel(root, s.canvas, s.model);
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

    function drawBorders(ctx, style, x, y, w, h, palette) {
        drawBorder(ctx, read(style, "BorderTop", null), x, y, x + w, y, palette);
        drawBorder(ctx, read(style, "BorderRight", null), x + w, y, x + w, y + h, palette);
        drawBorder(ctx, read(style, "BorderBottom", null), x, y + h, x + w, y + h, palette);
        drawBorder(ctx, read(style, "BorderLeft", null), x, y, x, y + h, palette);
    }

    function drawBorder(ctx, border, x1, y1, x2, y2, palette) {
        const style = read(border, "Style", "none");
        if (!border || style === "none") return;
        ctx.strokeStyle = read(border, "Color", null) || palette.border;
        ctx.lineWidth = style === "medium" ? 2 : style === "thick" ? 3 : 1;
        ctx.setLineDash(style === "dashed" ? [4, 3] : style === "dotted" ? [1, 2] : []);
        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.stroke();
        ctx.setLineDash([]);
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
