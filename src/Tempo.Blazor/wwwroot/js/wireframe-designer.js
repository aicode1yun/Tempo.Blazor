// Tempo Wireframe Designer – SVG canvas engine
// Pan, zoom, drag, resize, multi-select, toolbox drop, keyboard shortcuts, context menu
window.tmWireframeDesigner = {

    instances: new Map(),

    // ── Init / Destroy ────────────────────────────────────────────────────────

    init: function (svgElement, dotNetRef, options) {
        const id = svgElement.id || 'wfd-' + Math.random().toString(36).substr(2, 9);
        svgElement.id = id;

        const opts = options || {};
        const inst = {
            svg: svgElement,
            dotNetRef: dotNetRef,
            readOnly: !!opts.readOnly,
            gridSize: opts.gridSize || 8,
            showGrid: opts.showGrid !== false,
            canvasW: opts.canvasWidth || 1200,
            canvasH: opts.canvasHeight || 800,

            // drag-element state
            isDragging: false,
            dragElIds: [],          // ids being dragged (may be multi)
            dragStartSvg: null,     // SVG point at mousedown
            dragStartPositions: {}, // id → {x,y} at drag start

            // resize state
            isResizing: false,
            resizeElId: null,
            resizeHandle: null,     // 'nw','n','ne','e','se','s','sw','w'
            resizeStartSvg: null,
            resizeStartRect: null,  // {x,y,w,h} at resize start

            // rubber-band selection
            isRubberBand: false,
            rubberStart: null,      // SVG point
            rubberRect: null,       // SVG <rect> element

            // pan state
            isPanning: false,
            panStart: null,
            viewBoxStart: null,

            // tool mode: 'select' | 'pan'
            toolMode: 'select',
            spaceHeld: false,

            // current selection
            selectedIds: new Set(),

            // scale tracker
            scale: 1.0,
        };

        this.instances.set(id, inst);
        this._attachEvents(inst);
        return id;
    },

    destroy: function (svgElement) {
        const id = svgElement.id;
        const inst = this.instances.get(id);
        if (!inst) return;

        // Detach all DOM event listeners first
        this._detachEvents(inst);

        // Remove JS-injected overlay elements (selection handles, rubber-band rect, context menu)
        inst.svg.querySelectorAll('.tm-wd-selection, .tm-wd-rubber, .tm-wd-ctx-menu').forEach(el => el.remove());

        // Cancel any pending drag / resize animation frames
        if (inst._rafId) cancelAnimationFrame(inst._rafId);

        // Release the .NET object reference so the GC can collect the Blazor component
        if (inst.dotNetRef && typeof inst.dotNetRef.dispose === 'function') {
            inst.dotNetRef.dispose();
        }

        // Null-out references to help GC
        inst.svg = null;
        inst.dotNetRef = null;
        inst.rubberRect = null;

        this.instances.delete(id);
    },

    setReadOnly: function (svgElement, readOnly) {
        const inst = this.instances.get(svgElement.id);
        if (inst) inst.readOnly = !!readOnly;
    },

    // ── Tool mode (select / pan) ──────────────────────────────────────────────

    setToolMode: function (svgElement, mode) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return;
        inst.toolMode = mode;
        inst.svg.style.cursor = mode === 'pan' ? 'grab' : '';
    },

    // ── Canvas size update (called after ResizeCanvasCommand) ─────────────────

    updateCanvasSize: function (svgElement, w, h) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return;
        inst.canvasW = w;
        inst.canvasH = h;
        // Blazor re-renders the background rect — just update stored size for fitToView
    },

    setGridSize: function (svgElement, size) {
        const inst = this.instances.get(svgElement.id);
        if (inst) inst.gridSize = size;
    },

    // ── Event attachment ──────────────────────────────────────────────────────

    _attachEvents: function (inst) {
        inst._onMouseDown = (e) => this._onMouseDown(e, inst);
        inst._onMouseMove = (e) => this._onMouseMove(e, inst);
        inst._onMouseUp   = (e) => this._onMouseUp(e, inst);
        inst._onWheel     = (e) => this._onWheel(e, inst);
        inst._onKeyDown   = (e) => this._onKeyDown(e, inst);
        inst._onKeyUp     = (e) => this._onKeyUp(e, inst);
        inst._onContextMenu = (e) => this._onContextMenu(e, inst);
        inst._onDragOver  = (e) => this._onDragOver(e, inst);
        inst._onDrop      = (e) => this._onDrop(e, inst);

        inst.svg.addEventListener('mousedown',   inst._onMouseDown);
        document.addEventListener('mousemove',   inst._onMouseMove);
        document.addEventListener('mouseup',     inst._onMouseUp);
        inst.svg.addEventListener('wheel',       inst._onWheel, { passive: false });
        inst.svg.addEventListener('contextmenu', inst._onContextMenu);
        inst.svg.addEventListener('dragover',    inst._onDragOver);
        inst.svg.addEventListener('drop',        inst._onDrop);
        document.addEventListener('keydown',     inst._onKeyDown);
        document.addEventListener('keyup',       inst._onKeyUp);
    },

    _detachEvents: function (inst) {
        inst.svg.removeEventListener('mousedown',   inst._onMouseDown);
        document.removeEventListener('mousemove',   inst._onMouseMove);
        document.removeEventListener('mouseup',     inst._onMouseUp);
        inst.svg.removeEventListener('wheel',       inst._onWheel);
        inst.svg.removeEventListener('contextmenu', inst._onContextMenu);
        inst.svg.removeEventListener('dragover',    inst._onDragOver);
        inst.svg.removeEventListener('drop',        inst._onDrop);
        document.removeEventListener('keydown',     inst._onKeyDown);
        document.removeEventListener('keyup',       inst._onKeyUp);
    },

    // ── Coordinate helpers ────────────────────────────────────────────────────

    _svgPoint: function (inst, clientX, clientY) {
        const pt  = inst.svg.createSVGPoint();
        pt.x = clientX; pt.y = clientY;
        const ctm = inst.svg.getScreenCTM();
        if (!ctm) return { x: 0, y: 0 };
        const r = pt.matrixTransform(ctm.inverse());
        return { x: r.x, y: r.y };
    },

    _getViewBox: function (inst) {
        const vb = inst.svg.viewBox.baseVal;
        return { x: vb.x, y: vb.y, w: vb.width, h: vb.height };
    },

    _setViewBox: function (inst, x, y, w, h) {
        inst.svg.setAttribute('viewBox', x + ' ' + y + ' ' + w + ' ' + h);
    },

    _snap: function (inst, v) {
        const g = inst.gridSize;
        return g > 0 ? Math.round(v / g) * g : v;
    },

    // ── Element lookup helpers ────────────────────────────────────────────────

    _elGroup: function (inst, id) {
        return inst.svg.querySelector('[data-el-id="' + id + '"]');
    },

    _elRect: function (inst, id) {
        const g = this._elGroup(inst, id);
        if (!g) return null;
        const t = g.getAttribute('transform') || '';
        const m = t.match(/translate\(\s*([-\d.e+]+)\s*,\s*([-\d.e+]+)\s*\)/);
        const x = m ? parseFloat(m[1]) : 0;
        const y = m ? parseFloat(m[2]) : 0;
        const dw = parseFloat(g.getAttribute('data-w') || '0');
        const dh = parseFloat(g.getAttribute('data-h') || '0');
        return { x, y, w: dw, h: dh };
    },

    _setElTransform: function (inst, id, x, y) {
        const g = this._elGroup(inst, id);
        if (g) g.setAttribute('transform', 'translate(' + x + ', ' + y + ')');
    },

    // ── Mouse down ───────────────────────────────────────────────────────────

    _onMouseDown: function (e, inst) {
        if (inst.readOnly && e.button !== 1) return;

        // Middle mouse button OR Space held OR pan mode → pan canvas
        const wantPan = e.button === 1 || inst.spaceHeld || inst.toolMode === 'pan';
        if (wantPan) {
            e.preventDefault();
            inst.isPanning   = true;
            inst.panStart    = { x: e.clientX, y: e.clientY };
            inst.viewBoxStart = this._getViewBox(inst);
            inst.svg.style.cursor = 'grabbing';
            return;
        }

        if (e.button !== 0) return;

        // Resize handle?
        const handle = e.target.closest('[data-handle]');
        if (handle) {
            e.preventDefault();
            e.stopPropagation();
            // Handles live inside .tm-wd-selection which carries data-sel-for (not data-el-id)
            const selG = handle.closest('.tm-wd-selection');
            if (!selG) return;
            inst.isResizing   = true;
            inst.resizeElId   = selG.getAttribute('data-sel-for');
            inst.resizeHandle = handle.getAttribute('data-handle');
            inst.resizeStartSvg  = this._svgPoint(inst, e.clientX, e.clientY);
            inst.resizeStartRect = this._elRect(inst, inst.resizeElId);
            return;
        }

        // Element?
        const elG = e.target.closest('[data-el-id]');
        if (elG) {
            e.preventDefault();
            const id = elG.getAttribute('data-el-id');

            // Shift+click: toggle selection
            if (e.shiftKey) {
                if (inst.selectedIds.has(id)) {
                    inst.selectedIds.delete(id);
                } else {
                    inst.selectedIds.add(id);
                }
                this._updateHandles(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [...inst.selectedIds]);
                return;
            }

            // If clicked element not already selected, replace selection
            if (!inst.selectedIds.has(id)) {
                inst.selectedIds = new Set([id]);
                this._updateHandles(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [id]);
            }

            // Begin drag
            inst.isDragging = true;
            inst.dragElIds  = [...inst.selectedIds];
            inst.dragStartSvg = this._svgPoint(inst, e.clientX, e.clientY);
            inst.dragStartPositions = {};
            inst.dragElIds.forEach(eid => {
                const r = this._elRect(inst, eid);
                if (r) inst.dragStartPositions[eid] = { x: r.x, y: r.y };
            });
            // Notify Blazor of drag start so it can snapshot positions for MoveElementsCommand
            inst.dotNetRef.invokeMethodAsync('OnDragStarted', inst.dragElIds);
            inst.svg.style.cursor = 'grabbing';
            return;
        }

        // Empty canvas: rubber-band OR pan (space / middle button handled elsewhere)
        e.preventDefault();
        const svgPt = this._svgPoint(inst, e.clientX, e.clientY);
        inst.isRubberBand = true;
        inst.rubberStart  = svgPt;
        inst.rubberRect   = this._createRubberRect(inst, svgPt.x, svgPt.y);

        // Clear selection (no shift)
        if (!e.shiftKey && inst.selectedIds.size > 0) {
            inst.selectedIds.clear();
            this._updateHandles(inst);
            inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', []);
        }
    },

    // ── Mouse move ───────────────────────────────────────────────────────────

    _onMouseMove: function (e, inst) {
        if (inst.isResizing) {
            const cur  = this._svgPoint(inst, e.clientX, e.clientY);
            const dx   = cur.x - inst.resizeStartSvg.x;
            const dy   = cur.y - inst.resizeStartSvg.y;
            const orig = inst.resizeStartRect;
            const h    = inst.resizeHandle;
            const minW = 20, minH = 16;

            let { x, y, w, ww: _ww, h: _h } = { x: orig.x, y: orig.y, w: orig.w, h: orig.h };
            let nw = orig.w, nh = orig.h, nx = orig.x, ny = orig.y;

            if (h.includes('e'))  nw = Math.max(minW, orig.w + dx);
            if (h.includes('s'))  nh = Math.max(minH, orig.h + dy);
            if (h.includes('w')) { nw = Math.max(minW, orig.w - dx); nx = orig.x + orig.w - nw; }
            if (h.includes('n')) { nh = Math.max(minH, orig.h - dy); ny = orig.y + orig.h - nh; }

            const snappedX = this._snap(inst, nx);
            const snappedY = this._snap(inst, ny);
            const snappedW = this._snap(inst, nw);
            const snappedH = this._snap(inst, nh);

            const g = this._elGroup(inst, inst.resizeElId);
            if (g) {
                g.setAttribute('transform', 'translate(' + snappedX + ', ' + snappedY + ')');
                g.setAttribute('data-w', snappedW);
                g.setAttribute('data-h', snappedH);
                // Resize the inner SVG element
                const inner = g.querySelector('svg');
                if (inner) {
                    inner.setAttribute('width', snappedW);
                    inner.setAttribute('height', snappedH);
                    inner.setAttribute('viewBox', '0 0 ' + snappedW + ' ' + snappedH);
                }
                this._updateHandles(inst);
            }
            return;
        }

        if (inst.isDragging && inst.dragElIds.length > 0) {
            const cur = this._svgPoint(inst, e.clientX, e.clientY);
            const dx = cur.x - inst.dragStartSvg.x;
            const dy = cur.y - inst.dragStartSvg.y;

            inst.dragElIds.forEach(id => {
                const start = inst.dragStartPositions[id];
                if (!start) return;
                const nx = this._snap(inst, start.x + dx);
                const ny = this._snap(inst, start.y + dy);
                this._setElTransform(inst, id, nx, ny);
            });
            this._updateHandles(inst);
            return;
        }

        if (inst.isRubberBand && inst.rubberRect) {
            const cur = this._svgPoint(inst, e.clientX, e.clientY);
            const rx = Math.min(cur.x, inst.rubberStart.x);
            const ry = Math.min(cur.y, inst.rubberStart.y);
            const rw = Math.abs(cur.x - inst.rubberStart.x);
            const rh = Math.abs(cur.y - inst.rubberStart.y);
            inst.rubberRect.setAttribute('x', rx);
            inst.rubberRect.setAttribute('y', ry);
            inst.rubberRect.setAttribute('width', rw);
            inst.rubberRect.setAttribute('height', rh);
            return;
        }

        if (inst.isPanning && inst.panStart) {
            const vb = inst.viewBoxStart;
            const svgRect = inst.svg.getBoundingClientRect();
            const scaleX = vb.w / svgRect.width;
            const scaleY = vb.h / svgRect.height;
            const dx = (e.clientX - inst.panStart.x) * scaleX;
            const dy = (e.clientY - inst.panStart.y) * scaleY;
            this._setViewBox(inst, vb.x - dx, vb.y - dy, vb.w, vb.h);
        }
    },

    // ── Mouse up ─────────────────────────────────────────────────────────────

    _onMouseUp: function (e, inst) {
        if (inst.isResizing) {
            const id   = inst.resizeElId;
            const rect = this._elRect(inst, id);
            inst.isResizing   = false;
            inst.resizeElId   = null;
            inst.resizeHandle = null;
            inst.resizeStartSvg  = null;
            inst.resizeStartRect = null;
            if (rect) {
                inst.dotNetRef.invokeMethodAsync('OnElementResized', id, rect.x, rect.y, rect.w, rect.h);
            }
            return;
        }

        if (inst.isDragging) {
            const moves = inst.dragElIds.map(id => {
                const r = this._elRect(inst, id);
                return r ? { id, x: r.x, y: r.y } : null;
            }).filter(Boolean);

            inst.isDragging = false;
            inst.dragElIds  = [];
            inst.dragStartSvg = null;
            inst.dragStartPositions = {};
            inst.svg.style.cursor = '';

            if (moves.length === 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementMoved', moves[0].id, moves[0].x, moves[0].y);
            } else if (moves.length > 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementsMoved', moves);
            }
            return;
        }

        if (inst.isRubberBand) {
            const rubber = inst.rubberRect;
            inst.isRubberBand = false;
            inst.rubberStart  = null;
            inst.rubberRect   = null;

            if (rubber) {
                const rx = parseFloat(rubber.getAttribute('x') || '0');
                const ry = parseFloat(rubber.getAttribute('y') || '0');
                const rw = parseFloat(rubber.getAttribute('width') || '0');
                const rh = parseFloat(rubber.getAttribute('height') || '0');
                rubber.remove();

                if (rw > 4 && rh > 4) {
                    // Collect all elements inside the rubber band
                    const hits = [];
                    inst.svg.querySelectorAll('[data-el-id]').forEach(g => {
                        const r = this._elRect(inst, g.getAttribute('data-el-id'));
                        if (!r) return;
                        if (r.x >= rx && r.y >= ry && r.x + r.w <= rx + rw && r.y + r.h <= ry + rh) {
                            hits.push(g.getAttribute('data-el-id'));
                        }
                    });
                    if (hits.length > 0) {
                        inst.selectedIds = new Set(hits);
                        this._updateHandles(inst);
                        inst.dotNetRef.invokeMethodAsync('OnMultiSelect', hits);
                    }
                }
            }
            return;
        }

        if (inst.isPanning) {
            inst.isPanning    = false;
            inst.panStart     = null;
            inst.viewBoxStart = null;
            // Restore cursor: grab if space/pan-mode still active, otherwise default
            inst.svg.style.cursor = (inst.spaceHeld || inst.toolMode === 'pan') ? 'grab' : '';
            const vb = this._getViewBox(inst);
            inst.dotNetRef.invokeMethodAsync('OnViewBoxChanged', vb.x, vb.y, vb.w, vb.h);
        }
    },

    // ── Wheel zoom ───────────────────────────────────────────────────────────

    _onWheel: function (e, inst) {
        e.preventDefault();
        const factor = e.deltaY > 0 ? 1.1 : 0.9;
        const vb = this._getViewBox(inst);
        const pt = this._svgPoint(inst, e.clientX, e.clientY);

        const newW = vb.w * factor;
        const newH = vb.h * factor;

        // Clamp 0.15x – 5x
        const svgRect = inst.svg.getBoundingClientRect();
        const newScale = svgRect.width / newW;
        if (newScale < 0.15 || newScale > 5.0) return;

        const newX = pt.x - (pt.x - vb.x) * factor;
        const newY = pt.y - (pt.y - vb.y) * factor;

        this._setViewBox(inst, newX, newY, newW, newH);
        inst.scale = newScale;
        inst.dotNetRef.invokeMethodAsync('OnZoomChanged', newScale);
    },

    // ── Context menu ─────────────────────────────────────────────────────────

    _onContextMenu: function (e, inst) {
        e.preventDefault();
        const svgPt = this._svgPoint(inst, e.clientX, e.clientY);
        const elG   = e.target.closest('[data-el-id]');
        const id    = elG ? elG.getAttribute('data-el-id') : null;
        const container = inst.svg.parentElement || inst.svg;
        const rect  = container.getBoundingClientRect();
        const offX  = e.clientX - rect.left;
        const offY  = e.clientY - rect.top;

        if (id) {
            inst.dotNetRef.invokeMethodAsync('OnElementContextMenu', id, offX, offY);
        } else {
            inst.dotNetRef.invokeMethodAsync('OnCanvasContextMenu', svgPt.x, svgPt.y, offX, offY);
        }
    },

    // ── Toolbox drop ─────────────────────────────────────────────────────────

    _onDragOver: function (e, inst) {
        if (inst.readOnly) return;
        if (e.dataTransfer && e.dataTransfer.types.includes('text/plain')) {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        }
    },

    _onDrop: function (e, inst) {
        if (inst.readOnly) return;
        e.preventDefault();
        const componentType = e.dataTransfer && e.dataTransfer.getData('text/plain');
        if (!componentType) return;
        const svgPt = this._svgPoint(inst, e.clientX, e.clientY);
        const x = this._snap(inst, svgPt.x);
        const y = this._snap(inst, svgPt.y);
        inst.dotNetRef.invokeMethodAsync('OnElementDropped', componentType, x, y);
    },

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    _onKeyDown: function (e, inst) {
        // Only handle if SVG or a child has focus (or SVG is inside an active editor)
        if (!inst.svg.closest(':focus-within') && document.activeElement !== document.body) return;

        const ids = [...inst.selectedIds];

        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (ids.length === 0) return;
            // Don't intercept if user is typing in an input
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            e.preventDefault();
            inst.selectedIds.clear();
            this._updateHandles(inst);
            inst.dotNetRef.invokeMethodAsync('OnDeleteSelected', ids);
            return;
        }

        if (e.key === 'Escape') {
            e.preventDefault();
            inst.selectedIds.clear();
            this._updateHandles(inst);
            inst.dotNetRef.invokeMethodAsync('OnClearSelection');
            return;
        }

        if (e.ctrlKey || e.metaKey) {
            switch (e.key.toLowerCase()) {
                case 'z':
                    e.preventDefault();
                    if (e.shiftKey) {
                        inst.dotNetRef.invokeMethodAsync('OnRedo');
                    } else {
                        inst.dotNetRef.invokeMethodAsync('OnUndo');
                    }
                    return;
                case 'y':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnRedo');
                    return;
                case 'a':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnSelectAll');
                    return;
                case 'd':
                    e.preventDefault();
                    if (ids.length > 0) inst.dotNetRef.invokeMethodAsync('OnDuplicate', ids);
                    return;
            }
        }

        // Space → temporary pan mode
        if (e.code === 'Space' && !e.target.matches('input,textarea,select')) {
            e.preventDefault();
            if (!inst.spaceHeld) {
                inst.spaceHeld = true;
                if (!inst.isPanning) inst.svg.style.cursor = 'grab';
            }
            return;
        }

        // H → switch to pan tool, V → switch to select tool
        if (!e.ctrlKey && !e.metaKey && !e.target.matches('input,textarea,select')) {
            if (e.code === 'KeyH') {
                e.preventDefault();
                inst.toolMode = 'pan';
                inst.svg.style.cursor = 'grab';
                inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'pan');
                return;
            }
            if (e.code === 'KeyV') {
                e.preventDefault();
                inst.toolMode = 'select';
                inst.svg.style.cursor = '';
                inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'select');
                return;
            }
        }

        // Arrow key nudge
        if (['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key)) {
            if (ids.length === 0) return;
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            e.preventDefault();
            const step = e.shiftKey ? 10 : 1;
            const dx = e.key === 'ArrowLeft' ? -step : e.key === 'ArrowRight' ? step : 0;
            const dy = e.key === 'ArrowUp'   ? -step : e.key === 'ArrowDown'  ? step : 0;
            ids.forEach(id => {
                const r = this._elRect(inst, id);
                if (!r) return;
                this._setElTransform(inst, id, r.x + dx, r.y + dy);
            });
            this._updateHandles(inst);
            // Notify Blazor of final positions
            const moves = ids.map(id => {
                const r = this._elRect(inst, id);
                return r ? { id, x: r.x, y: r.y } : null;
            }).filter(Boolean);
            if (moves.length === 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementMoved', moves[0].id, moves[0].x, moves[0].y);
            } else if (moves.length > 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementsMoved', moves);
            }
        }
    },

    // ── Key up ───────────────────────────────────────────────────────────────

    _onKeyUp: function (e, inst) {
        if (e.code === 'Space') {
            inst.spaceHeld = false;
            if (!inst.isPanning) {
                // Restore cursor based on current tool mode
                inst.svg.style.cursor = inst.toolMode === 'pan' ? 'grab' : '';
            }
        }
    },

    // ── Selection handles ─────────────────────────────────────────────────────

    _updateHandles: function (inst) {
        // Remove all existing handle groups
        inst.svg.querySelectorAll('.tm-wd-selection').forEach(el => el.remove());
        if (inst.readOnly) return;

        const ns = 'http://www.w3.org/2000/svg';

        inst.selectedIds.forEach(id => {
            const r = this._elRect(inst, id);
            if (!r) return;

            const selG = document.createElementNS(ns, 'g');
            selG.classList.add('tm-wd-selection');
            selG.setAttribute('data-sel-for', id);
            selG.setAttribute('pointer-events', 'none');

            // Selection border
            const border = document.createElementNS(ns, 'rect');
            border.setAttribute('x',      r.x - 1);
            border.setAttribute('y',      r.y - 1);
            border.setAttribute('width',  r.w + 2);
            border.setAttribute('height', r.h + 2);
            border.setAttribute('fill',   'none');
            border.setAttribute('stroke', '#3b82f6');
            border.setAttribute('stroke-width', '1.5');
            border.setAttribute('stroke-dasharray', '4 2');
            border.setAttribute('rx', '2');
            selG.appendChild(border);

            // 8 resize handles – only if single selection
            if (inst.selectedIds.size === 1) {
                const handles = [
                    { key: 'nw', cx: r.x,           cy: r.y,           cursor: 'nw-resize' },
                    { key: 'n',  cx: r.x + r.w / 2, cy: r.y,           cursor: 'n-resize'  },
                    { key: 'ne', cx: r.x + r.w,     cy: r.y,           cursor: 'ne-resize' },
                    { key: 'e',  cx: r.x + r.w,     cy: r.y + r.h / 2, cursor: 'e-resize'  },
                    { key: 'se', cx: r.x + r.w,     cy: r.y + r.h,     cursor: 'se-resize' },
                    { key: 's',  cx: r.x + r.w / 2, cy: r.y + r.h,     cursor: 's-resize'  },
                    { key: 'sw', cx: r.x,           cy: r.y + r.h,     cursor: 'sw-resize' },
                    { key: 'w',  cx: r.x,           cy: r.y + r.h / 2, cursor: 'w-resize'  },
                ];

                handles.forEach(h => {
                    const dot = document.createElementNS(ns, 'rect');
                    dot.setAttribute('x',      h.cx - 4);
                    dot.setAttribute('y',      h.cy - 4);
                    dot.setAttribute('width',  8);
                    dot.setAttribute('height', 8);
                    dot.setAttribute('rx',     2);
                    dot.setAttribute('fill',   'white');
                    dot.setAttribute('stroke', '#3b82f6');
                    dot.setAttribute('stroke-width', '1.5');
                    dot.setAttribute('data-handle', h.key);
                    dot.setAttribute('pointer-events', 'all');
                    dot.style.cursor = h.cursor;
                    selG.appendChild(dot);
                });
            }

            inst.svg.appendChild(selG);
        });
    },

    // ── Rubber band helper ────────────────────────────────────────────────────

    _createRubberRect: function (inst, x, y) {
        const ns   = 'http://www.w3.org/2000/svg';
        const rect = document.createElementNS(ns, 'rect');
        rect.setAttribute('x',      x);
        rect.setAttribute('y',      y);
        rect.setAttribute('width',  0);
        rect.setAttribute('height', 0);
        rect.setAttribute('fill',   'rgba(59,130,246,0.08)');
        rect.setAttribute('stroke', '#3b82f6');
        rect.setAttribute('stroke-width', '1');
        rect.setAttribute('stroke-dasharray', '4 2');
        rect.setAttribute('pointer-events', 'none');
        rect.classList.add('tm-wd-rubber');
        inst.svg.appendChild(rect);
        return rect;
    },

    // ── Programmatic selection update (called from Blazor) ────────────────────

    setSelection: function (svgElement, ids) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return;
        inst.selectedIds = new Set(ids || []);
        this._updateHandles(inst);
    },

    // ── Programmatic zoom ─────────────────────────────────────────────────────

    zoomTo: function (svgElement, scale) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return;
        const vb  = this._getViewBox(inst);
        const cx  = vb.x + vb.w / 2;
        const cy  = vb.y + vb.h / 2;
        const svgRect = inst.svg.getBoundingClientRect();
        const newW = svgRect.width  / scale;
        const newH = svgRect.height / scale;
        this._setViewBox(inst, cx - newW / 2, cy - newH / 2, newW, newH);
        inst.scale = scale;
    },

    fitToView: function (svgElement, padding) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return 1.0;
        padding = (padding != null) ? padding : 40;

        const groups = inst.svg.querySelectorAll('[data-el-id]');
        if (groups.length === 0) {
            // Fit to canvas
            const svgRect = inst.svg.getBoundingClientRect();
            this._setViewBox(inst, 0, 0, inst.canvasW, inst.canvasH);
            inst.scale = Math.min(svgRect.width / inst.canvasW, svgRect.height / inst.canvasH);
            return inst.scale;
        }

        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        groups.forEach(g => {
            const id = g.getAttribute('data-el-id');
            const r  = this._elRect(inst, id);
            if (!r) return;
            minX = Math.min(minX, r.x);
            minY = Math.min(minY, r.y);
            maxX = Math.max(maxX, r.x + r.w);
            maxY = Math.max(maxY, r.y + r.h);
        });

        if (!isFinite(minX)) return 1.0;

        const contentW = maxX - minX + padding * 2;
        const contentH = maxY - minY + padding * 2;
        const svgRect  = inst.svg.getBoundingClientRect();
        const fitScale = Math.min(svgRect.width / contentW, svgRect.height / contentH, 2.0);
        const newW = svgRect.width  / fitScale;
        const newH = svgRect.height / fitScale;
        this._setViewBox(inst, minX - padding, minY - padding, newW, newH);
        inst.scale = fitScale;
        return fitScale;
    },

    // ── Pan to canvas origin ──────────────────────────────────────────────────

    resetView: function (svgElement) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return;
        const svgRect = inst.svg.getBoundingClientRect();
        this._setViewBox(inst, 0, 0, svgRect.width, svgRect.height);
        inst.scale = 1.0;
    },

    // ── Toolbox drag init ─────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a single delegated dragstart listener to a toolbox container.
    /// Items inside the container must carry data-component-type="…".
    /// </summary>
    initToolbox: function (toolboxElement) {
        if (!toolboxElement || toolboxElement._wdToolboxInit) return;
        toolboxElement._wdToolboxInit = true;
        toolboxElement.addEventListener('dragstart', function (e) {
            const item = e.target.closest('[data-component-type]');
            if (!item) return;
            const type = item.getAttribute('data-component-type');
            e.dataTransfer.setData('text/plain', type);
            e.dataTransfer.effectAllowed = 'copy';
        });
    },

    // ── Export SVG (strip UI overlays) ────────────────────────────────────────

    exportSvg: function (svgElement) {
        const inst = this.instances.get(svgElement.id);
        if (!inst) return '';
        const clone = inst.svg.cloneNode(true);
        clone.querySelectorAll('.tm-wd-selection, .tm-wd-rubber, .tm-wd-grid-overlay').forEach(el => el.remove());
        return clone.outerHTML;
    },

    // ── Scroll / navigate to a document-space centre point ───────────────────

    scrollTo: function (svgElement, centreX, centreY) {
        const inst = this.instances.get(svgElement ? svgElement.id : null);
        if (!inst) return;
        const vb   = this._getViewBox(inst);
        const newX = centreX - vb.w / 2;
        const newY = centreY - vb.h / 2;
        this._setViewBox(inst, newX, newY, vb.w, vb.h);
    },

    // ── File download helper (used by C# export) ──────────────────────────────

    downloadFile: function (anchorEl, fileName, mimeType, base64Data) {
        const dataUrl = 'data:' + mimeType + ';base64,' + base64Data;
        anchorEl.href     = dataUrl;
        anchorEl.download = fileName;
        anchorEl.click();
        // Clean up to avoid stale blob URLs
        setTimeout(() => { anchorEl.href = ''; }, 1000);
    },
};
