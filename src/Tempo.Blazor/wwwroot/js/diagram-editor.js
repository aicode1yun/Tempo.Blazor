// Tempo Diagram Editor – Hybrid SVG + HTML canvas engine
// Pan, zoom, drag, touch, multi-select, keyboard shortcuts
window.tmDiagramEditor = {

    instances: new Map(),

    // ── Init / Destroy ────────────────────────────────────────────────────────

    init: function (container, dotNetRef, options) {
        const id = container.id || 'tmd-' + Math.random().toString(36).substr(2, 9);
        container.id = id;

        const svg = container.querySelector('.tm-diagram-canvas__svg');
        const htmlLayer = container.querySelector('.tm-diagram-canvas__overlay .tm-diagram-transform-layer');
        const interactionLayer = container.querySelector('.tm-diagram-canvas__interaction');

        if (!svg) throw new Error('TmDiagramCanvas requires an SVG element with class tm-diagram-canvas__svg');

        const opts = options || {};
        const inst = {
            container: container,
            svg: svg,
            htmlLayer: htmlLayer,
            interactionLayer: interactionLayer,
            dotNetRef: dotNetRef,

            readOnly: !!opts.readOnly,
            gridSize: opts.gridSize || 8,
            showGrid: opts.showGrid !== false,
            canvasW: opts.canvasWidth || 3000,
            canvasH: opts.canvasHeight || 2000,

            // drag node state
            isDragging: false,
            dragNodeIds: [],
            dragStartScreen: null,
            dragStartPositions: {},

            // pan state
            isPanning: false,
            panStartScreen: null,
            viewBoxStart: null,

            // rubber-band selection
            isRubberBand: false,
            rubberStartDoc: null,
            rubberEl: null,

            // tool mode
            toolMode: 'select',
            spaceHeld: false,

            // selection
            selectedIds: new Set(),

            // scale tracker
            scale: 1.0,

            // edge drawing
            isDrawingEdge: false,
            drawSource: null,
            drawTempPath: null,

            // pinch zoom
            pinchStartDist: 0,
            pinchStartScale: 1,
            pinchMidDoc: null,
            pinchViewBoxStart: null,

            // long press
            longPressTimer: null,
            longPressStart: null,
            longPressNodeId: null,
        };

        this.instances.set(id, inst);
        this._attachEvents(inst);
        this._syncHtmlTransform(inst);
        const vb = this._getViewBox(inst);
        if (dotNetRef)
            dotNetRef.invokeMethodAsync('OnViewportChanged', vb.x, vb.y, vb.w, vb.h);
        return id;
    },

    destroy: function (container) {
        const id = container.id;
        const inst = this.instances.get(id);
        if (!inst) return;

        this._detachEvents(inst);
        if (inst.rubberEl) inst.rubberEl.remove();
        this._clearSelectionOutlines(inst);

        if (inst.dotNetRef && typeof inst.dotNetRef.dispose === 'function') {
            inst.dotNetRef.dispose();
        }

        inst.svg = null;
        inst.htmlLayer = null;
        inst.interactionLayer = null;
        inst.dotNetRef = null;
        this.instances.delete(id);
    },

    // ── Tool mode ─────────────────────────────────────────────────────────────

    setToolMode: function (container, mode) {
        const inst = this.instances.get(container.id);
        if (!inst) return;
        inst.toolMode = mode;
        inst.container.style.cursor = mode === 'pan' ? 'grab' : '';
    },

    // ── Canvas size update ────────────────────────────────────────────────────

    updateCanvasSize: function (container, w, h) {
        const inst = this.instances.get(container.id);
        if (!inst) return;
        inst.canvasW = w;
        inst.canvasH = h;
    },

    // ── Event attachment ──────────────────────────────────────────────────────

    _attachEvents: function (inst) {
        inst._onMouseDown = (e) => this._onMouseDown(e, inst);
        inst._onMouseMove = (e) => this._onMouseMove(e, inst);
        inst._onMouseUp = (e) => this._onMouseUp(e, inst);
        inst._onWheel = (e) => this._onWheel(e, inst);
        inst._onKeyDown = (e) => this._onKeyDown(e, inst);
        inst._onKeyUp = (e) => this._onKeyUp(e, inst);
        inst._onTouchStart = (e) => this._onTouchStart(e, inst);
        inst._onTouchMove = (e) => this._onTouchMove(e, inst);
        inst._onTouchEnd = (e) => this._onTouchEnd(e, inst);
        inst._onDrop = (e) => this._onDrop(e, inst);
        inst._onDragOver = (e) => { e.preventDefault(); };

        inst.container.addEventListener('mousedown', inst._onMouseDown);
        inst.container.addEventListener('wheel', inst._onWheel, { passive: false });
        inst.container.addEventListener('touchstart', inst._onTouchStart, { passive: false });
        inst.container.addEventListener('touchmove', inst._onTouchMove, { passive: false });
        inst.container.addEventListener('touchend', inst._onTouchEnd);
        inst.container.addEventListener('touchcancel', inst._onTouchEnd);
        inst.container.addEventListener('drop', inst._onDrop);
        inst.container.addEventListener('dragover', inst._onDragOver);
        document.addEventListener('mousemove', inst._onMouseMove);
        document.addEventListener('mouseup', inst._onMouseUp);
        document.addEventListener('keydown', inst._onKeyDown);
        document.addEventListener('keyup', inst._onKeyUp);
    },

    _detachEvents: function (inst) {
        inst.container.removeEventListener('mousedown', inst._onMouseDown);
        inst.container.removeEventListener('wheel', inst._onWheel);
        inst.container.removeEventListener('touchstart', inst._onTouchStart);
        inst.container.removeEventListener('touchmove', inst._onTouchMove);
        inst.container.removeEventListener('touchend', inst._onTouchEnd);
        inst.container.removeEventListener('touchcancel', inst._onTouchEnd);
        inst.container.removeEventListener('drop', inst._onDrop);
        inst.container.removeEventListener('dragover', inst._onDragOver);
        document.removeEventListener('mousemove', inst._onMouseMove);
        document.removeEventListener('mouseup', inst._onMouseUp);
        document.removeEventListener('keydown', inst._onKeyDown);
        document.removeEventListener('keyup', inst._onKeyUp);
    },

    _onDrop: function (e, inst) {
        e.preventDefault();
        if (inst.readOnly) return;
        const stencilId = window.__tmDiagramDragStencil;
        if (!stencilId) return;

        const docPt = this._screenToDoc(inst, e.clientX, e.clientY);

        inst.dotNetRef.invokeMethodAsync('OnDropFromToolbox', stencilId, docPt.x, docPt.y);
        window.__tmDiagramDragStencil = null;
    },

    // ── Coordinate helpers ────────────────────────────────────────────────────

    _getViewBox: function (inst) {
        const vb = inst.svg.viewBox.baseVal;
        return { x: vb.x, y: vb.y, w: vb.width, h: vb.height };
    },

    _setViewBox: function (inst, x, y, w, h) {
        inst.svg.setAttribute('viewBox', x + ' ' + y + ' ' + w + ' ' + h);
        inst.scale = Math.max(inst.svg.getBoundingClientRect().width, 1) / w;
        this._syncHtmlTransform(inst);
    },

    _syncHtmlTransform: function (inst) {
        if (!inst.htmlLayer) return;
        const vb = this._getViewBox(inst);
        const rect = inst.svg.getBoundingClientRect();
        const scale = Math.max(rect.width, 1) / vb.w;
        inst.scale = scale;
        const tx = -vb.x * scale;
        const ty = -vb.y * scale;
        inst.htmlLayer.style.transform = 'translate(' + tx + 'px, ' + ty + 'px) scale(' + scale + ')';
    },

    _snap: function (inst, v) {
        const g = inst.gridSize;
        return g > 0 ? Math.round(v / g) * g : v;
    },

    _screenToDoc: function (inst, clientX, clientY) {
        const pt = inst.svg.createSVGPoint();
        pt.x = clientX;
        pt.y = clientY;
        const ctm = inst.svg.getScreenCTM();
        if (!ctm) return { x: 0, y: 0 };
        const r = pt.matrixTransform(ctm.inverse());
        return { x: r.x, y: r.y };
    },

    // ── Node lookup helpers ───────────────────────────────────────────────────

    _nodeEl: function (inst, id) {
        return inst.htmlLayer ? inst.htmlLayer.querySelector('[data-node-id="' + id + '"]') : null;
    },

    _nodeRect: function (inst, id) {
        const el = this._nodeEl(inst, id);
        if (!el) return null;
        const style = el.style.transform || '';
        const m = style.match(/translate\(\s*([-\d.e+]+)px\s*,\s*([-\d.e+]+)px\s*\)/);
        const x = m ? parseFloat(m[1]) : 0;
        const y = m ? parseFloat(m[2]) : 0;
        const dw = parseFloat(el.getAttribute('data-w') || el.style.width || '0');
        const dh = parseFloat(el.getAttribute('data-h') || el.style.height || '0');
        return { x, y, w: dw, h: dh };
    },

    _getNodeRotation: function (el) {
        const style = el ? el.style.transform : '';
        const m = style.match(/rotate\(([-\d.e+]+)deg\)/);
        return m ? parseFloat(m[1]) : 0;
    },

    _setNodeTranslate: function (inst, id, x, y) {
        const el = this._nodeEl(inst, id);
        if (!el) return;
        const rot = this._getNodeRotation(el);
        el.style.transform = 'translate(' + x + 'px, ' + y + 'px)' + (rot ? ' rotate(' + rot + 'deg)' : '');
    },

    _clampChildPosition: function (inst, childId, x, y) {
        const childEl = this._nodeEl(inst, childId);
        const parentId = childEl ? childEl.getAttribute('data-parent-id') : null;
        if (!parentId || inst.dragNodeIds.includes(parentId)) return { x, y };
        const parentRect = this._nodeRect(inst, parentId);
        const childRect = this._nodeRect(inst, childId);
        if (!parentRect || !childRect) return { x, y };
        const padding = 4;
        const minX = parentRect.x + padding;
        const minY = parentRect.y + padding;
        const maxX = parentRect.x + parentRect.w - childRect.w - padding;
        const maxY = parentRect.y + parentRect.h - childRect.h - padding;
        return {
            x: Math.max(minX, Math.min(x, maxX)),
            y: Math.max(minY, Math.min(y, maxY))
        };
    },

    // ── Mouse down ───────────────────────────────────────────────────────────

    _onMouseDown: function (e, inst) {
        if (inst.readOnly && e.button !== 1) return;

        // Ignore when interacting with inline edit inputs so text selection and cursor work
        if (e.target.closest('.tm-diagram-node__inline-input, .tm-diagram-node__inline-textarea, .tm-diagram-edge-label-input')) {
            return;
        }

        inst.container.focus({ preventScroll: true });

        // Middle mouse OR Space held OR pan mode => pan
        const wantPan = e.button === 1 || inst.spaceHeld || inst.toolMode === 'pan';
        if (wantPan) {
            e.preventDefault();
            inst.isPanning = true;
            inst.panStartScreen = { x: e.clientX, y: e.clientY };
            inst.viewBoxStart = this._getViewBox(inst);
            inst.container.style.cursor = 'grabbing';
            return;
        }

        if (e.button !== 0) return;

        // Edge waypoint handle clicked?
        const handleEl = e.target.closest('.tm-diagram-edge-handle');
        if (handleEl && !inst.readOnly) {
            const isWaypoint = handleEl.getAttribute('data-waypoint') === 'true';
            if (isWaypoint) {
                e.preventDefault();
                e.stopPropagation();
                inst.isDraggingWaypoint = true;
                inst.dragWaypointEdgeId = handleEl.getAttribute('data-edge-id');
                inst.dragWaypointIndex = parseInt(handleEl.getAttribute('data-handle-index'), 10) - 1;
                inst.dragWaypointStartScreen = { x: e.clientX, y: e.clientY };
                const pt = this._screenToDoc(inst, e.clientX, e.clientY);
                inst.dragWaypointStartDoc = pt;
                return;
            }
        }

        // Port clicked? -> start edge drawing
        const portEl = e.target.closest('.tm-diagram-port');
        if (portEl && !inst.readOnly) {
            e.preventDefault();
            e.stopPropagation();
            const nodeEl = portEl.closest('[data-node-id]');
            if (nodeEl) {
                this._startEdgeDraw(inst, nodeEl.getAttribute('data-node-id'), portEl.getAttribute('data-port-id'), e.clientX, e.clientY);
            }
            return;
        }

        // Resize / rotate / connect clicked? -> let Blazor handle it (do NOT stopPropagation,
        // otherwise Blazor's document-level event delegation never receives the event)
        if (e.target.closest('.tm-diagram-resize-handle, .tm-diagram-rotate-handle, .tm-diagram-connect-arrow')) {
            return;
        }

        // Node clicked?
        const nodeEl = e.target.closest('[data-node-id]');
        if (nodeEl) {
            e.preventDefault();
            const id = nodeEl.getAttribute('data-node-id');
            const isLocked = nodeEl.getAttribute('data-locked') === 'true';

            if (e.ctrlKey || e.metaKey) {
                if (inst.selectedIds.has(id)) inst.selectedIds.delete(id);
                else inst.selectedIds.add(id);
                this._updateSelection(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [...inst.selectedIds]);
                return;
            }

            if (!inst.selectedIds.has(id)) {
                inst.selectedIds = new Set([id]);
                this._updateSelection(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [id]);
            }

            if (isLocked || inst.readOnly) return;

            inst.isDragging = true;
            inst.dragNodeIds = [...inst.selectedIds];

            // Include children of selected nodes
            const allIds = new Set(inst.dragNodeIds);
            inst.dragNodeIds.forEach(nid => {
                if (inst.htmlLayer) {
                    inst.htmlLayer.querySelectorAll('[data-parent-id="' + nid + '"]').forEach(childEl => {
                        const cid = childEl.getAttribute('data-node-id');
                        if (cid) allIds.add(cid);
                    });
                }
            });
            inst.dragNodeIds = [...allIds];

            inst.dragStartScreen = { x: e.clientX, y: e.clientY };
            inst.dragStartPositions = {};
            inst.dragNodeIds.forEach(nid => {
                const r = this._nodeRect(inst, nid);
                if (r) inst.dragStartPositions[nid] = { x: r.x, y: r.y };
            });
            inst.dotNetRef.invokeMethodAsync('OnDragStarted', inst.dragNodeIds);
            inst.container.style.cursor = 'grabbing';
            return;
        }

        // Empty canvas: rubber-band selection
        e.preventDefault();
        const rect = inst.container.getBoundingClientRect();
        const screenPt = { x: e.clientX - rect.left, y: e.clientY - rect.top };
        inst.isRubberBand = true;
        inst.rubberStartScreen = screenPt;
        inst.rubberEl = this._createRubberEl(inst, screenPt.x, screenPt.y);

        if (!e.shiftKey && inst.selectedIds.size > 0) {
            inst.selectedIds.clear();
            this._updateSelection(inst);
            inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', []);
        }
    },

    // ── Mouse move ───────────────────────────────────────────────────────────

    _onMouseMove: function (e, inst) {
        if (inst.isDrawingEdge) {
            this._updateEdgeDraw(inst, e.clientX, e.clientY);
            return;
        }

        if (inst.isDraggingWaypoint) {
            const pt = this._screenToDoc(inst, e.clientX, e.clientY);
            this._updateWaypointVisuals(inst, inst.dragWaypointEdgeId, inst.dragWaypointIndex, pt.x, pt.y);
            return;
        }

        if (inst.isDragging && inst.dragNodeIds.length > 0) {
            const dxScreen = e.clientX - inst.dragStartScreen.x;
            const dyScreen = e.clientY - inst.dragStartScreen.y;
            const dxDoc = dxScreen / inst.scale;
            const dyDoc = dyScreen / inst.scale;

            inst.dragNodeIds.forEach(id => {
                const start = inst.dragStartPositions[id];
                if (!start) return;
                let nx = this._snap(inst, start.x + dxDoc);
                let ny = this._snap(inst, start.y + dyDoc);
                const clamped = this._clampChildPosition(inst, id, nx, ny);
                this._setNodeTranslate(inst, id, clamped.x, clamped.y);
            });
            this._updateSelection(inst);
            return;
        }

        if (inst.isRubberBand && inst.rubberEl) {
            const rect = inst.container.getBoundingClientRect();
            const cur = { x: e.clientX - rect.left, y: e.clientY - rect.top };
            const rx = Math.min(cur.x, inst.rubberStartScreen.x);
            const ry = Math.min(cur.y, inst.rubberStartScreen.y);
            const rw = Math.abs(cur.x - inst.rubberStartScreen.x);
            const rh = Math.abs(cur.y - inst.rubberStartScreen.y);
            inst.rubberEl.style.left = rx + 'px';
            inst.rubberEl.style.top = ry + 'px';
            inst.rubberEl.style.width = rw + 'px';
            inst.rubberEl.style.height = rh + 'px';
            return;
        }

        if (inst.isPanning && inst.panStartScreen) {
            const vb = inst.viewBoxStart;
            const dxScreen = e.clientX - inst.panStartScreen.x;
            const dyScreen = e.clientY - inst.panStartScreen.y;
            const dxDoc = dxScreen / inst.scale;
            const dyDoc = dyScreen / inst.scale;
            this._setViewBox(inst, vb.x - dxDoc, vb.y - dyDoc, vb.w, vb.h);
        }
    },

    // ── Mouse up ─────────────────────────────────────────────────────────────

    _onMouseUp: function (e, inst) {
        if (inst.isDrawingEdge) {
            const portEl = e.target.closest('.tm-diagram-port');
            if (portEl) {
                const nodeEl = portEl.closest('[data-node-id]');
                if (nodeEl) {
                    const targetNodeId = nodeEl.getAttribute('data-node-id');
                    const targetPortId = portEl.getAttribute('data-port-id');
                    if (targetNodeId !== inst.drawSource.nodeId || targetPortId !== inst.drawSource.portId) {
                        inst.dotNetRef.invokeMethodAsync('JsOnEdgeCreated',
                            inst.drawSource.nodeId, inst.drawSource.portId,
                            targetNodeId, targetPortId);
                    }
                }
            }
            this._cancelEdgeDraw(inst);
            return;
        }

        if (inst.isDraggingWaypoint) {
            const pt = this._screenToDoc(inst, e.clientX, e.clientY);
            inst.dotNetRef.invokeMethodAsync('OnEdgeWaypointMoved',
                inst.dragWaypointEdgeId, inst.dragWaypointIndex, pt.x, pt.y);
            inst.isDraggingWaypoint = false;
            inst.dragWaypointEdgeId = null;
            inst.dragWaypointIndex = null;
            inst.dragWaypointStartScreen = null;
            inst.dragWaypointStartDoc = null;
            return;
        }

        if (inst.isDragging) {
            const moves = inst.dragNodeIds.map(id => {
                const r = this._nodeRect(inst, id);
                return r ? { id, x: r.x, y: r.y } : null;
            }).filter(Boolean);

            inst.isDragging = false;
            inst.dragNodeIds = [];
            inst.dragStartScreen = null;
            inst.dragStartPositions = {};
            inst.container.style.cursor = inst.toolMode === 'pan' ? 'grab' : '';

            if (moves.length === 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementMoved', moves[0].id, moves[0].x, moves[0].y);
            } else if (moves.length > 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementsMoved', moves);
            }
            return;
        }

        if (inst.isRubberBand) {
            const rubber = inst.rubberEl;
            inst.isRubberBand = false;
            inst.rubberStartScreen = null;
            inst.rubberEl = null;

            if (rubber) {
                const rect = inst.container.getBoundingClientRect();
                const leftScreen = parseFloat(rubber.style.left);
                const topScreen = parseFloat(rubber.style.top);
                const widthScreen = parseFloat(rubber.style.width);
                const heightScreen = parseFloat(rubber.style.height);
                rubber.remove();

                if (widthScreen > 4 && heightScreen > 4) {
                    const docTopLeft = this._screenToDoc(inst, rect.left + leftScreen, rect.top + topScreen);
                    const docBottomRight = this._screenToDoc(inst, rect.left + leftScreen + widthScreen, rect.top + topScreen + heightScreen);
                    const left = Math.min(docTopLeft.x, docBottomRight.x);
                    const top = Math.min(docTopLeft.y, docBottomRight.y);
                    const width = Math.abs(docBottomRight.x - docTopLeft.x);
                    const height = Math.abs(docBottomRight.y - docTopLeft.y);

                    const hits = [];
                    if (inst.htmlLayer) {
                        inst.htmlLayer.querySelectorAll('[data-node-id]').forEach(el => {
                            const id = el.getAttribute('data-node-id');
                            const r = this._nodeRect(inst, id);
                            if (!r) return;
                            if (r.x >= left && r.y >= top && r.x + r.w <= left + width && r.y + r.h <= top + height) {
                                hits.push(id);
                            }
                        });
                    }
                    if (hits.length > 0) {
                        inst.selectedIds = new Set(hits);
                        this._updateSelection(inst);
                        inst.dotNetRef.invokeMethodAsync('OnMultiSelect', hits);
                    }
                }
            }
            return;
        }

        if (inst.isPanning) {
            inst.isPanning = false;
            inst.panStartScreen = null;
            inst.viewBoxStart = null;
            inst.container.style.cursor = (inst.spaceHeld || inst.toolMode === 'pan') ? 'grab' : '';
            const vb = this._getViewBox(inst);
            inst.dotNetRef.invokeMethodAsync('OnViewBoxChanged', vb.x, vb.y, vb.w, vb.h);
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', vb.x, vb.y, vb.w, vb.h);
        }
    },

    // ── Wheel zoom ───────────────────────────────────────────────────────────

    _onWheel: function (e, inst) {
        if (e.target.closest('.tm-diagram-node__inline-input, .tm-diagram-node__inline-textarea, .tm-diagram-edge-label-input')) {
            return;
        }
        e.preventDefault();
        const factor = e.deltaY > 0 ? 1.1 : 0.9;
        const vb = this._getViewBox(inst);
        const pt = this._screenToDoc(inst, e.clientX, e.clientY);

        const newW = vb.w * factor;
        const newH = vb.h * factor;

        const svgRect = inst.svg.getBoundingClientRect();
        const newScale = svgRect.width / newW;
        if (newScale < 0.15 || newScale > 5.0) return;

        const newX = pt.x - (pt.x - vb.x) * factor;
        const newY = pt.y - (pt.y - vb.y) * factor;

        this._setViewBox(inst, newX, newY, newW, newH);
        inst.dotNetRef.invokeMethodAsync('OnZoomChanged', newScale);
        inst.dotNetRef.invokeMethodAsync('OnViewportChanged', newX, newY, newW, newH);
    },

    // ── Touch events ─────────────────────────────────────────────────────────

    _onTouchStart: function (e, inst) {
        if (inst.readOnly) return;

        // Pinch zoom start
        if (e.touches.length === 2) {
            e.preventDefault();
            this._cancelLongPress(inst);
            const t1 = e.touches[0];
            const t2 = e.touches[1];
            const dx = t2.clientX - t1.clientX;
            const dy = t2.clientY - t1.clientY;
            inst.pinchStartDist = Math.sqrt(dx * dx + dy * dy);
            inst.pinchStartScale = inst.scale;
            inst.pinchMidDoc = this._screenToDoc(inst, (t1.clientX + t2.clientX) / 2, (t1.clientY + t2.clientY) / 2);
            inst.pinchViewBoxStart = this._getViewBox(inst);
            return;
        }

        if (e.touches.length !== 1) return;

        const t = e.touches[0];
        const nodeEl = document.elementFromPoint(t.clientX, t.clientY)?.closest('[data-node-id]');

        if (nodeEl) {
            e.preventDefault();
            const id = nodeEl.getAttribute('data-node-id');
            const isLocked = nodeEl.getAttribute('data-locked') === 'true';
            if (!inst.selectedIds.has(id)) {
                inst.selectedIds = new Set([id]);
                this._updateSelection(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [id]);
            }

            if (isLocked || inst.readOnly) {
                // Cancel long press for locked nodes
                this._cancelLongPress(inst);
                return;
            }

            inst.isDragging = true;
            inst.dragNodeIds = [...inst.selectedIds];

            // Include children of selected nodes
            const allIds = new Set(inst.dragNodeIds);
            inst.dragNodeIds.forEach(nid => {
                if (inst.htmlLayer) {
                    inst.htmlLayer.querySelectorAll('[data-parent-id="' + nid + '"]').forEach(childEl => {
                        const cid = childEl.getAttribute('data-node-id');
                        if (cid) allIds.add(cid);
                    });
                }
            });
            inst.dragNodeIds = [...allIds];

            inst.dragStartScreen = { x: t.clientX, y: t.clientY };
            inst.dragStartPositions = {};
            inst.dragNodeIds.forEach(nid => {
                const r = this._nodeRect(inst, nid);
                if (r) inst.dragStartPositions[nid] = { x: r.x, y: r.y };
            });
            inst.dotNetRef.invokeMethodAsync('OnDragStarted', inst.dragNodeIds);

            // Long press for context menu
            inst.longPressNodeId = id;
            inst.longPressStart = { x: t.clientX, y: t.clientY };
            inst.longPressTimer = setTimeout(() => {
                if (inst.longPressNodeId && inst.dotNetRef) {
                    inst.dotNetRef.invokeMethodAsync('OnContextMenu', inst.longPressNodeId, inst.longPressStart.x, inst.longPressStart.y);
                }
                inst.longPressTimer = null;
                inst.longPressNodeId = null;
            }, 600);
            return;
        }

        // Pan on empty canvas
        e.preventDefault();
        inst.isPanning = true;
        inst.panStartScreen = { x: t.clientX, y: t.clientY };
        inst.viewBoxStart = this._getViewBox(inst);
    },

    _onTouchMove: function (e, inst) {
        // Pinch zoom
        if (e.touches.length === 2 && inst.pinchStartDist > 0) {
            e.preventDefault();
            const t1 = e.touches[0];
            const t2 = e.touches[1];
            const dx = t2.clientX - t1.clientX;
            const dy = t2.clientY - t1.clientY;
            const dist = Math.sqrt(dx * dx + dy * dy);
            const factor = inst.pinchStartDist / dist;

            const vb = inst.pinchViewBoxStart;
            const mid = inst.pinchMidDoc;
            if (!vb || !mid) return;

            const newW = vb.w * factor;
            const newH = vb.h * factor;
            const svgRect = inst.svg.getBoundingClientRect();
            const newScale = svgRect.width / newW;
            if (newScale < 0.15 || newScale > 5.0) return;

            const newX = mid.x - (mid.x - vb.x) * factor;
            const newY = mid.y - (mid.y - vb.y) * factor;

            this._setViewBox(inst, newX, newY, newW, newH);
            if (inst.dotNetRef)
                inst.dotNetRef.invokeMethodAsync('OnViewportChanged', newX, newY, newW, newH);
            return;
        }

        if (e.touches.length !== 1) return;
        const t = e.touches[0];

        // Cancel long press if finger moved significantly
        if (inst.longPressTimer && inst.longPressStart) {
            const moveDist = Math.sqrt(Math.pow(t.clientX - inst.longPressStart.x, 2) + Math.pow(t.clientY - inst.longPressStart.y, 2));
            if (moveDist > 10) {
                this._cancelLongPress(inst);
            }
        }

        if (inst.isDragging && inst.dragNodeIds.length > 0) {
            e.preventDefault();
            const dxScreen = t.clientX - inst.dragStartScreen.x;
            const dyScreen = t.clientY - inst.dragStartScreen.y;
            const dxDoc = dxScreen / inst.scale;
            const dyDoc = dyScreen / inst.scale;

            inst.dragNodeIds.forEach(id => {
                const start = inst.dragStartPositions[id];
                if (!start) return;
                let nx = this._snap(inst, start.x + dxDoc);
                let ny = this._snap(inst, start.y + dyDoc);
                const clamped = this._clampChildPosition(inst, id, nx, ny);
                this._setNodeTranslate(inst, id, clamped.x, clamped.y);
            });
            this._updateSelection(inst);
            return;
        }

        if (inst.isPanning && inst.panStartScreen) {
            e.preventDefault();
            const vb = inst.viewBoxStart;
            const dxScreen = t.clientX - inst.panStartScreen.x;
            const dyScreen = t.clientY - inst.panStartScreen.y;
            const dxDoc = dxScreen / inst.scale;
            const dyDoc = dyScreen / inst.scale;
            this._setViewBox(inst, vb.x - dxDoc, vb.y - dyDoc, vb.w, vb.h);
        }
    },

    _onTouchEnd: function (e, inst) {
        this._cancelLongPress(inst);

        // End pinch zoom
        if (inst.pinchStartDist > 0 && e.touches.length < 2) {
            inst.pinchStartDist = 0;
            inst.pinchStartScale = 1;
            inst.pinchMidDoc = null;
            inst.pinchViewBoxStart = null;
            const vb = this._getViewBox(inst);
            if (inst.dotNetRef)
                inst.dotNetRef.invokeMethodAsync('OnViewportChanged', vb.x, vb.y, vb.w, vb.h);
        }

        if (inst.isDragging) {
            const moves = inst.dragNodeIds.map(id => {
                const r = this._nodeRect(inst, id);
                return r ? { id, x: r.x, y: r.y } : null;
            }).filter(Boolean);

            inst.isDragging = false;
            inst.dragNodeIds = [];
            inst.dragStartScreen = null;
            inst.dragStartPositions = {};

            if (moves.length === 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementMoved', moves[0].id, moves[0].x, moves[0].y);
            } else if (moves.length > 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementsMoved', moves);
            }
            return;
        }

        if (inst.isPanning) {
            inst.isPanning = false;
            inst.panStartScreen = null;
            inst.viewBoxStart = null;
            const vb = this._getViewBox(inst);
            inst.dotNetRef.invokeMethodAsync('OnViewBoxChanged', vb.x, vb.y, vb.w, vb.h);
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', vb.x, vb.y, vb.w, vb.h);
        }
    },

    _cancelLongPress: function (inst) {
        if (inst.longPressTimer) {
            clearTimeout(inst.longPressTimer);
            inst.longPressTimer = null;
        }
        inst.longPressStart = null;
        inst.longPressNodeId = null;
    },

    // ── Edge drawing helpers ─────────────────────────────────────────────────

    _startEdgeDraw: function (inst, nodeId, portId, clientX, clientY) {
        inst.isDrawingEdge = true;
        const docPt = this._screenToDoc(inst, clientX, clientY);
        inst.drawSource = { nodeId: nodeId, portId: portId, x: docPt.x, y: docPt.y };

        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('class', 'tm-diagram-edge-draw-path');
        path.setAttribute('fill', 'none');
        path.setAttribute('stroke', '#3b82f6');
        path.setAttribute('stroke-width', '2');
        path.setAttribute('marker-end', 'url(#arrow-default)');
        path.setAttribute('pointer-events', 'none');
        inst.svg.appendChild(path);
        inst.drawTempPath = path;

        // Highlight source port
        const portEl = inst.htmlLayer?.querySelector('.tm-diagram-port[data-port-id="' + portId + '"]');
        if (portEl) portEl.classList.add('tm-diagram-port--active');
    },

    _updateEdgeDraw: function (inst, clientX, clientY) {
        if (!inst.drawTempPath || !inst.drawSource) return;
        const docPt = this._screenToDoc(inst, clientX, clientY);
        const d = 'M ' + inst.drawSource.x + ' ' + inst.drawSource.y + ' L ' + docPt.x + ' ' + docPt.y;
        inst.drawTempPath.setAttribute('d', d);

        // Port snapping highlight
        const el = document.elementFromPoint(clientX, clientY);
        const portEl = el ? el.closest('.tm-diagram-port') : null;
        inst.htmlLayer?.querySelectorAll('.tm-diagram-port.tm-diagram-port--target').forEach(p => p.classList.remove('tm-diagram-port--target'));
        if (portEl) {
            const nid = portEl.closest('[data-node-id]')?.getAttribute('data-node-id');
            const pid = portEl.getAttribute('data-port-id');
            if (nid !== inst.drawSource.nodeId || pid !== inst.drawSource.portId) {
                portEl.classList.add('tm-diagram-port--target');
            }
        }
    },

    _cancelEdgeDraw: function (inst) {
        inst.isDrawingEdge = false;
        if (inst.drawTempPath) {
            inst.drawTempPath.remove();
            inst.drawTempPath = null;
        }
        if (inst.drawSource) {
            const portEl = inst.htmlLayer?.querySelector('.tm-diagram-port[data-port-id="' + inst.drawSource.portId + '"]');
            if (portEl) portEl.classList.remove('tm-diagram-port--active');
            inst.drawSource = null;
        }
        inst.htmlLayer?.querySelectorAll('.tm-diagram-port.tm-diagram-port--target').forEach(p => p.classList.remove('tm-diagram-port--target'));
    },

    // ── Keyboard shortcuts ───────────────────────────────────────────────────

    _onKeyDown: function (e, inst) {
        if (!inst.container.contains(document.activeElement) && document.activeElement !== document.body) return;

        const ids = [...inst.selectedIds];

        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (ids.length === 0) return;
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            e.preventDefault();
            inst.selectedIds.clear();
            this._updateSelection(inst);
            inst.dotNetRef.invokeMethodAsync('OnDeleteSelected', ids);
            return;
        }

        if (e.key === 'Escape') {
            e.preventDefault();
            if (inst.isDrawingEdge) {
                this._cancelEdgeDraw(inst);
                return;
            }
            inst.selectedIds.clear();
            this._updateSelection(inst);
            inst.dotNetRef.invokeMethodAsync('OnClearSelection');
            return;
        }

        if (e.ctrlKey || e.metaKey) {
            switch (e.key.toLowerCase()) {
                case 'z':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync(e.shiftKey ? 'OnRedo' : 'OnUndo');
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

        // Space -> temporary pan mode
        if (e.code === 'Space' && !e.target.matches('input,textarea,select')) {
            e.preventDefault();
            if (!inst.spaceHeld) {
                inst.spaceHeld = true;
                if (!inst.isPanning) inst.container.style.cursor = 'grab';
            }
            return;
        }

        // H -> pan tool, V -> select tool
        if (!e.ctrlKey && !e.metaKey && !e.target.matches('input,textarea,select')) {
            if (e.code === 'KeyH') {
                e.preventDefault();
                inst.toolMode = 'pan';
                inst.container.style.cursor = 'grab';
                inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'pan');
                return;
            }
            if (e.code === 'KeyV') {
                e.preventDefault();
                inst.toolMode = 'select';
                inst.container.style.cursor = '';
                inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'select');
                return;
            }
        }

        // Arrow nudge
        if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key)) {
            if (ids.length === 0) return;
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
            e.preventDefault();
            const step = e.shiftKey ? 10 : 1;
            const dx = e.key === 'ArrowLeft' ? -step : e.key === 'ArrowRight' ? step : 0;
            const dy = e.key === 'ArrowUp' ? -step : e.key === 'ArrowDown' ? step : 0;

            ids.forEach(id => {
                const r = this._nodeRect(inst, id);
                if (!r) return;
                this._setNodeTranslate(inst, id, r.x + dx, r.y + dy);
            });
            this._updateSelection(inst);

            const moves = ids.map(id => {
                const r = this._nodeRect(inst, id);
                return r ? { id, x: r.x, y: r.y } : null;
            }).filter(Boolean);

            if (moves.length === 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementMoved', moves[0].id, moves[0].x, moves[0].y);
            } else if (moves.length > 1) {
                inst.dotNetRef.invokeMethodAsync('OnElementsMoved', moves);
            }
        }
    },

    _onKeyUp: function (e, inst) {
        if (e.code === 'Space') {
            inst.spaceHeld = false;
            if (!inst.isPanning) {
                inst.container.style.cursor = inst.toolMode === 'pan' ? 'grab' : '';
            }
        }
    },

    // ── Selection outlines ───────────────────────────────────────────────────

    _updateSelection: function (inst) {
        this._clearSelectionOutlines(inst);
        if (!inst.htmlLayer) return;

        inst.selectedIds.forEach(id => {
            const r = this._nodeRect(inst, id);
            if (!r) return;
            const nodeEl = this._nodeEl(inst, id);
            const rot = this._getNodeRotation(nodeEl);
            const el = document.createElement('div');
            el.className = 'tm-diagram-selection-outline';
            el.style.position = 'absolute';
            el.style.left = '-4px';
            el.style.top = '-4px';
            el.style.width = (r.w + 8) + 'px';
            el.style.height = (r.h + 8) + 'px';
            el.style.transformOrigin = 'center center';
            el.style.transform = 'translate(' + r.x + 'px, ' + r.y + 'px)' + (rot ? ' rotate(' + rot + 'deg)' : '');
            el.style.pointerEvents = 'none';
            el.style.boxSizing = 'border-box';
            el.setAttribute('data-sel-for', id);
            inst.htmlLayer.appendChild(el);
        });
    },

    _clearSelectionOutlines: function (inst) {
        if (!inst.htmlLayer) return;
        inst.htmlLayer.querySelectorAll('.tm-diagram-selection-outline').forEach(el => el.remove());
    },

    _updateWaypointVisuals: function (inst, edgeId, waypointIndex, x, y) {
        const svg = inst.svg;
        if (!svg) return;
        const handleIndex = waypointIndex + 1;
        const handle = svg.querySelector('circle.tm-diagram-edge-handle[data-edge-id="' + edgeId + '"][data-handle-index="' + handleIndex + '"]');
        if (handle) {
            handle.setAttribute('cx', x);
            handle.setAttribute('cy', y);
        }
        const hitPath = svg.querySelector('path.tm-diagram-edge-hit-path[data-edge-id="' + edgeId + '"]');
        const visPath = svg.querySelector('path.tm-diagram-edge-path[data-edge-id="' + edgeId + '"]');
        const handles = Array.from(svg.querySelectorAll('circle.tm-diagram-edge-handle[data-edge-id="' + edgeId + '"]'))
            .sort(function (a, b) {
                return parseInt(a.getAttribute('data-handle-index'), 10) - parseInt(b.getAttribute('data-handle-index'), 10);
            });
        if (handles.length > 1) {
            let d = 'M ' + handles[0].getAttribute('cx') + ' ' + handles[0].getAttribute('cy');
            for (let i = 1; i < handles.length; i++) {
                d += ' L ' + handles[i].getAttribute('cx') + ' ' + handles[i].getAttribute('cy');
            }
            if (hitPath) hitPath.setAttribute('d', d);
            if (visPath) visPath.setAttribute('d', d);
        }
    },

    // ── Rubber band helper ───────────────────────────────────────────────────

    _createRubberEl: function (inst, x, y) {
        const el = document.createElement('div');
        el.className = 'tm-diagram-rubber';
        el.style.position = 'absolute';
        el.style.left = x + 'px';
        el.style.top = y + 'px';
        el.style.width = '0';
        el.style.height = '0';
        el.style.pointerEvents = 'none';
        el.style.zIndex = '10';
        inst.container.appendChild(el);
        return el;
    },

    // ── Orthogonal router ────────────────────────────────────────────────────

    _computeOrthogonalWaypoints: function (x1, y1, side1, x2, y2, side2, routing, sourceSpacing, targetSpacing) {
        routing = (routing || 'orthogonal').toLowerCase();
        sourceSpacing = sourceSpacing || 0;
        targetSpacing = targetSpacing || 0;

        const s1 = (side1 || '').toLowerCase();
        const s2 = (side2 || '').toLowerCase();

        const dx1 = s1 === 'left' ? -1 : s1 === 'right' ? 1 : 0;
        const dy1 = s1 === 'top' ? -1 : s1 === 'bottom' ? 1 : 0;
        const dx2 = s2 === 'left' ? -1 : s2 === 'right' ? 1 : 0;
        const dy2 = s2 === 'top' ? -1 : s2 === 'bottom' ? 1 : 0;

        // Apply spacing
        const sx1 = x1 + dx1 * sourceSpacing;
        const sy1 = y1 + dy1 * sourceSpacing;
        const sx2 = x2 + dx2 * targetSpacing;
        const sy2 = y2 + dy2 * targetSpacing;

        if (routing === 'elbow') {
            if (dx1 !== 0 && dx2 !== 0) {
                const midX = (sx1 + sx2) / 2;
                return [[midX, sy1], [midX, sy2]];
            }
            if (dy1 !== 0 && dy2 !== 0) {
                const midY = (sy1 + sy2) / 2;
                return [[sx1, midY], [sx2, midY]];
            }
            if (dx1 !== 0 && dy2 !== 0) {
                return [[sx2, sy1]];
            }
            if (dy1 !== 0 && dx2 !== 0) {
                return [[sx1, sy2]];
            }
            return [[sx1, sy2]];
        }

        if (routing === 'segment') {
            if (dx1 !== 0 && dx2 !== 0) {
                const midX = (sx1 + sx2) / 2;
                return [[midX, sy1], [midX, sy2]];
            }
            if (dy1 !== 0 && dy2 !== 0) {
                const midY = (sy1 + sy2) / 2;
                return [[sx1, midY], [sx2, midY]];
            }
            if (dx1 !== 0 && dy2 !== 0) {
                return [[sx2, sy1]];
            }
            if (dy1 !== 0 && dx2 !== 0) {
                return [[sx1, sy2]];
            }
            return [[sx1, sy2]];
        }

        // orthogonal (default) and rounded
        if (dx1 !== 0 && dx2 !== 0) {
            const midX = (sx1 + sx2) / 2;
            return [[midX, sy1], [midX, sy2]];
        }
        if (dy1 !== 0 && dy2 !== 0) {
            const midY = (sy1 + sy2) / 2;
            return [[sx1, midY], [sx2, midY]];
        }
        if (dx1 !== 0 && dy2 !== 0) {
            return [[sx2, sy1]];
        }
        if (dy1 !== 0 && dx2 !== 0) {
            return [[sx1, sy2]];
        }
        const midX = (sx1 + sx2) / 2;
        return [[midX, sy1], [midX, sy2]];
    },

    // ── Programmatic API ─────────────────────────────────────────────────────

    setSelection: function (container, ids) {
        const inst = this.instances.get(container.id);
        if (!inst) return;
        inst.selectedIds = new Set(ids || []);
        this._updateSelection(inst);
    },

    zoomTo: function (container, scale) {
        const inst = this.instances.get(container.id);
        if (!inst) return;
        const vb = this._getViewBox(inst);
        const cx = vb.x + vb.w / 2;
        const cy = vb.y + vb.h / 2;
        const svgRect = inst.svg.getBoundingClientRect();
        const newW = svgRect.width / scale;
        const newH = svgRect.height / scale;
        const nx = cx - newW / 2;
        const ny = cy - newH / 2;
        this._setViewBox(inst, nx, ny, newW, newH);
        if (inst.dotNetRef)
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', nx, ny, newW, newH);
    },

    fitToView: function (container, padding) {
        const inst = this.instances.get(container.id);
        if (!inst) return 1.0;
        padding = (padding != null) ? padding : 40;

        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        let hasNodes = false;

        if (inst.htmlLayer) {
            inst.htmlLayer.querySelectorAll('[data-node-id]').forEach(el => {
                const id = el.getAttribute('data-node-id');
                const r = this._nodeRect(inst, id);
                if (!r) return;
                hasNodes = true;
                minX = Math.min(minX, r.x);
                minY = Math.min(minY, r.y);
                maxX = Math.max(maxX, r.x + r.w);
                maxY = Math.max(maxY, r.y + r.h);
            });
        }

        if (!hasNodes) {
            const svgRect = inst.svg.getBoundingClientRect();
            this._setViewBox(inst, 0, 0, inst.canvasW, inst.canvasH);
            if (inst.dotNetRef)
                inst.dotNetRef.invokeMethodAsync('OnViewportChanged', 0, 0, inst.canvasW, inst.canvasH);
            return svgRect.width / inst.canvasW;
        }

        const contentW = maxX - minX + padding * 2;
        const contentH = maxY - minY + padding * 2;
        const svgRect = inst.svg.getBoundingClientRect();
        const fitScale = Math.min(svgRect.width / contentW, svgRect.height / contentH, 2.0);
        const newW = svgRect.width / fitScale;
        const newH = svgRect.height / fitScale;
        const nx = minX - padding;
        const ny = minY - padding;
        this._setViewBox(inst, nx, ny, newW, newH);
        if (inst.dotNetRef)
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', nx, ny, newW, newH);
        return fitScale;
    },

    scrollTo: function (container, centreX, centreY) {
        const inst = this.instances.get(container ? container.id : null);
        if (!inst) return;
        const vb = this._getViewBox(inst);
        const newX = centreX - vb.w / 2;
        const newY = centreY - vb.h / 2;
        this._setViewBox(inst, newX, newY, vb.w, vb.h);
        if (inst.dotNetRef)
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', newX, newY, vb.w, vb.h);
    },

    // ── Toolbox drag init ─────────────────────────────────────────────────────

    initToolbox: function (toolboxElement) {
        if (!toolboxElement || toolboxElement._tmdToolboxInit) return;
        toolboxElement._tmdToolboxInit = true;
        toolboxElement.addEventListener('dragstart', function (e) {
            const item = e.target.closest('[data-stencil-id]');
            if (!item) return;
            const type = item.getAttribute('data-stencil-id');
            window.__tmDiagramDragStencil = type;
            e.dataTransfer.setData('text/plain', type);
            e.dataTransfer.effectAllowed = 'copy';
        });
    },

    updateSelectionOutlines: function (container) {
        const inst = this.instances.get(container.id);
        if (inst) this._updateSelection(inst);
    },

    getDragStencilId: function () {
        return window.__tmDiagramDragStencil ?? null;
    },

    _applyNodeRotation: function (inst, nodeId, angle) {
        const el = this._nodeEl(inst, nodeId);
        if (!el) return;
        const style = el.style.transform || '';
        const m = style.match(/translate\(\s*([-\d.e+]+)px\s*,\s*([-\d.e+]+)px\s*\)/);
        const translate = m ? 'translate(' + m[1] + 'px, ' + m[2] + 'px)' : 'translate(0px, 0px)';
        el.style.transform = translate + ' rotate(' + angle + 'deg)';
        this._updateSelection(inst);
    },

    setNodeRotation: function (container, nodeId, angle) {
        const inst = this.instances.get(container ? container.id : null);
        if (!inst) return;
        this._applyNodeRotation(inst, nodeId, angle);
    },

    startRotate: function (container, nodeId, clientX, clientY, initialRotation, snap) {
        const inst = this.instances.get(container ? container.id : null);
        if (!inst) return;
        const r = this._nodeRect(inst, nodeId);
        if (!r) return;

        inst.isRotating = true;
        inst.rotateNodeId = nodeId;
        inst.rotateCenterDoc = { x: r.x + r.w / 2, y: r.y + r.h / 2 };
        inst.rotateStartNodeRotation = initialRotation;
        inst.rotateSnap = snap || 0;

        const startPt = this._screenToDoc(inst, clientX, clientY);
        inst.rotateStartAngle = Math.atan2(startPt.y - inst.rotateCenterDoc.y, startPt.x - inst.rotateCenterDoc.x) * 180 / Math.PI;

        const self = this;
        const move = function (e) {
            if (!inst.isRotating) return;
            e.preventDefault();
            const pt = self._screenToDoc(inst, e.clientX, e.clientY);
            const angle = Math.atan2(pt.y - inst.rotateCenterDoc.y, pt.x - inst.rotateCenterDoc.x) * 180 / Math.PI;
            let delta = angle - inst.rotateStartAngle;
            let rot = inst.rotateStartNodeRotation + delta;
            if (inst.rotateSnap > 0) {
                rot = Math.round(rot / inst.rotateSnap) * inst.rotateSnap;
            }
            self._applyNodeRotation(inst, inst.rotateNodeId, rot);
        };
        const up = function (e) {
            if (!inst.isRotating) return;
            inst.isRotating = false;
            document.removeEventListener('mousemove', move);
            document.removeEventListener('mouseup', up);
            const nodeEl = self._nodeEl(inst, inst.rotateNodeId);
            const rot = nodeEl ? self._getNodeRotation(nodeEl) : inst.rotateStartNodeRotation;
            if (inst.dotNetRef) {
                inst.dotNetRef.invokeMethodAsync('OnRotateEnded', inst.rotateNodeId, rot);
            }
            inst.rotateNodeId = null;
        };

        document.addEventListener('mousemove', move);
        document.addEventListener('mouseup', up);
    },

    computeOrthogonalWaypoints: function (x1, y1, side1, x2, y2, side2, routing, sourceSpacing, targetSpacing) {
        return this._computeOrthogonalWaypoints(x1, y1, side1, x2, y2, side2, routing, sourceSpacing, targetSpacing);
    },

    screenToDoc: function (container, clientX, clientY) {
        const inst = this.instances.get(container.id);
        if (!inst) return { x: clientX, y: clientY };
        return this._screenToDoc(inst, clientX, clientY);
    },
};
