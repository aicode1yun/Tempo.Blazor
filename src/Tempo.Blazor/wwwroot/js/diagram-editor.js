// Tempo Diagram Editor – Hybrid SVG + HTML canvas engine
// Pan, zoom, drag, touch, multi-select, keyboard shortcuts
window.tmDiagramEditor = {

    instances: new Map(),

    // ── MathJax support ──────────────────────────────────────────────────────

    mathJaxReady: false,

    loadMathJax: function () {
        if (this.mathJaxReady) return Promise.resolve();
        if (typeof window.MathJax === 'undefined') return Promise.resolve();
        const self = this;
        return new Promise(function (resolve) {
            const check = function () {
                if (window.MathJax && window.MathJax.typesetPromise) {
                    self.mathJaxReady = true;
                    resolve();
                } else {
                    setTimeout(check, 100);
                }
            };
            check();
        });
    },

    typesetMath: function (container, nodeId) {
        const self = this;
        return this.loadMathJax().then(function () {
            const inst = self.instances.get(container.id);
            if (!inst || !inst.dotNetRef) return;

            const selector = nodeId
                ? '[data-node-id="' + nodeId + '"] .tm-diagram-math'
                : '.tm-diagram-math';
            const elements = container.querySelectorAll(selector);
            if (elements.length === 0) return;

            return window.MathJax.typesetPromise(Array.from(elements)).then(function () {
                elements.forEach(function (el) {
                    const svg = el.querySelector('svg');
                    if (svg) {
                        const nid = el.closest('[data-node-id]')?.getAttribute('data-node-id');
                        if (nid) {
                            const serializer = new XMLSerializer();
                            const svgString = serializer.serializeToString(svg);
                            inst.dotNetRef.invokeMethodAsync('OnMathSvgCached', nid, svgString);
                        }
                    }
                });
            });
        });
    },

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
            activeGroupId: opts.activeGroupId || null,

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

            // jetty drag
            isDraggingJetty: false,
            dragJettyEdgeId: null,
            dragJettyType: null,
            dragJettySide: null,
            dragJettyNodeId: null,
            dragJettyStartDoc: null,

            // pinch zoom
            pinchStartDist: 0,
            pinchStartScale: 1,
            pinchMidDoc: null,
            pinchViewBoxStart: null,

            // long press
            longPressTimer: null,
            longPressStart: null,
            longPressNodeId: null,

            // magnetic guidelines
            guideLines: [],

            // group bounds
            groupBoundsEls: [],
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
        inst.container.style.cursor = mode === 'pan' ? 'grab' : (mode === 'edge' ? 'crosshair' : '');
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
        inst._onContextMenu = (e) => this._onContextMenu(e, inst);

        inst.container.addEventListener('mousedown', inst._onMouseDown);
        inst.container.addEventListener('wheel', inst._onWheel, { passive: false });
        inst.container.addEventListener('touchstart', inst._onTouchStart, { passive: false });
        inst.container.addEventListener('touchmove', inst._onTouchMove, { passive: false });
        inst.container.addEventListener('touchend', inst._onTouchEnd);
        inst.container.addEventListener('touchcancel', inst._onTouchEnd);
        inst.container.addEventListener('drop', inst._onDrop);
        inst.container.addEventListener('dragover', inst._onDragOver);
        inst.container.addEventListener('contextmenu', inst._onContextMenu);
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
        inst.container.removeEventListener('contextmenu', inst._onContextMenu);
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

    _onContextMenu: function (e, inst) {
        if (inst.readOnly || !inst.dotNetRef) return;

        // If clicking on the custom context menu itself, close it and prevent native menu
        if (e.target.closest('.tm-diagram-editor__context-menu')) {
            e.preventDefault();
            inst.dotNetRef.invokeMethodAsync('CloseContextMenu');
            return;
        }

        e.preventDefault();

        // No need to call CloseContextMenu here — OnNodeContextMenu/OnEdgeContextMenu/OnCanvasContextMenu
        // will set _contextMenuOpen = true and openContextMenu will replace the old menu and listeners.

        const labelGroupEl = e.target.closest('.tm-diagram-edge-label-group');
        if (labelGroupEl) {
            const labelHandle = labelGroupEl.querySelector('[data-label-handle]');
            const edgeId = labelHandle ? labelHandle.getAttribute('data-edge-id') : null;
            if (edgeId) {
                inst.dotNetRef.invokeMethodAsync('OnEdgeContextMenu', edgeId, e.clientX, e.clientY);
                return;
            }
        }

        const tableCellEl = e.target.closest('.tm-diagram-node__table-cell');
        const nodeEl = e.target.closest('.tm-diagram-node');
        const edgeEl = e.target.closest('.tm-diagram-edge-hit-path');

        if (tableCellEl && nodeEl) {
            const nodeId = nodeEl.getAttribute('data-node-id');
            const row = parseInt(tableCellEl.getAttribute('data-row'), 10);
            const col = parseInt(tableCellEl.getAttribute('data-col'), 10);
            if (nodeId && !isNaN(row) && !isNaN(col)) {
                inst.dotNetRef.invokeMethodAsync('OnTableCellContextMenu', nodeId, row, col, e.clientX, e.clientY);
            }
        } else if (nodeEl) {
            const nodeId = nodeEl.getAttribute('data-node-id');
            if (nodeId) {
                inst.dotNetRef.invokeMethodAsync('OnNodeContextMenu', nodeId, e.clientX, e.clientY);
            }
        } else if (edgeEl) {
            const edgeId = edgeEl.getAttribute('data-edge-id');
            if (edgeId) {
                inst.dotNetRef.invokeMethodAsync('OnEdgeContextMenu', edgeId, e.clientX, e.clientY);
            }
        } else {
            const docPt = this._screenToDoc(inst, e.clientX, e.clientY);
            inst.dotNetRef.invokeMethodAsync('OnCanvasContextMenu', docPt.x, docPt.y, e.clientX, e.clientY);
        }
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

    _isNodeInActiveGroup: function (inst, nodeEl) {
        if (!nodeEl) return false;
        const parentGroupId = nodeEl.getAttribute('data-parent-group-id') || null;
        if (!inst.activeGroupId) return !parentGroupId;
        return parentGroupId === inst.activeGroupId;
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
        let cx = Math.max(minX, Math.min(x, maxX));
        let cy = Math.max(minY, Math.min(y, maxY));

        // Also clamp to parent group bounds
        const groupId = childEl ? childEl.getAttribute('data-parent-group-id') : null;
        if (groupId) {
            const groupRect = this._nodeRect(inst, groupId);
            if (groupRect && childRect && !inst.dragNodeIds.includes(groupId)) {
                const gMinX = groupRect.x + padding;
                const gMinY = groupRect.y + padding;
                const gMaxX = groupRect.x + groupRect.w - childRect.w - padding;
                const gMaxY = groupRect.y + groupRect.h - childRect.h - padding;
                cx = Math.max(gMinX, Math.min(cx, gMaxX));
                cy = Math.max(gMinY, Math.min(cy, gMaxY));
            }
        }
        return { x: cx, y: cy };
    },

    _includeGroupNodes: function (inst, ids) {
        const allIds = new Set(ids);
        const groupIds = new Set();
        ids.forEach(nid => {
            const el = this._nodeEl(inst, nid);
            const gid = el ? el.getAttribute('data-group-id') : null;
            if (gid) groupIds.add(gid);
        });
        if (groupIds.size > 0 && inst.htmlLayer) {
            inst.htmlLayer.querySelectorAll('[data-group-id]').forEach(el => {
                if (!this._isNodeInActiveGroup(inst, el)) return;
                const gid = el.getAttribute('data-group-id');
                const nid = el.getAttribute('data-node-id');
                if (gid && groupIds.has(gid) && nid) allIds.add(nid);
            });
        }
        return [...allIds];
    },

    // ── Group bounds ─────────────────────────────────────────────────────────

    _renderGroupBounds: function (inst) {
        this._clearGroupBounds(inst);
        if (!inst.htmlLayer) return;
        const groupMap = {};
        inst.selectedIds.forEach(id => {
            const el = this._nodeEl(inst, id);
            const gid = el ? el.getAttribute('data-group-id') : null;
            if (!gid) return;
            if (!groupMap[gid]) groupMap[gid] = [];
            const r = this._nodeRect(inst, id);
            if (r) groupMap[gid].push(r);
        });

        Object.keys(groupMap).forEach(gid => {
            const rects = groupMap[gid];
            if (rects.length === 0) return;
            const minX = Math.min(...rects.map(r => r.x)) - 6;
            const minY = Math.min(...rects.map(r => r.y)) - 6;
            const maxX = Math.max(...rects.map(r => r.x + r.w)) + 6;
            const maxY = Math.max(...rects.map(r => r.y + r.h)) + 6;
            const el = document.createElement('div');
            el.className = 'tm-diagram-group-bounds';
            el.style.position = 'absolute';
            el.style.left = minX + 'px';
            el.style.top = minY + 'px';
            el.style.width = (maxX - minX) + 'px';
            el.style.height = (maxY - minY) + 'px';
            el.style.border = '1px dashed var(--tm-color-primary, #3b82f6)';
            el.style.borderRadius = '4px';
            el.style.pointerEvents = 'none';
            inst.htmlLayer.appendChild(el);
            inst.groupBoundsEls.push(el);
        });
    },

    _clearGroupBounds: function (inst) {
        if (!inst.htmlLayer) return;
        inst.htmlLayer.querySelectorAll('.tm-diagram-group-bounds').forEach(el => el.remove());
        inst.groupBoundsEls = [];
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

        const isRightClick = e.button === 2;
        if (e.button !== 0 && !isRightClick) return;

        // Right-click on edge or label -> select edge
        if (isRightClick) {
            const edgeEl = e.target.closest('.tm-diagram-edge-hit-path');
            if (edgeEl) {
                const edgeId = edgeEl.getAttribute('data-edge-id');
                if (edgeId) {
                    if (e.ctrlKey || e.metaKey) {
                        if (inst.selectedIds.has(edgeId)) inst.selectedIds.delete(edgeId);
                        else inst.selectedIds.add(edgeId);
                    } else {
                        inst.selectedIds = new Set([edgeId]);
                        this._updateSelection(inst);
                    }
                    inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [...inst.selectedIds]);
                    return;
                }
            }
            const labelGroupEl = e.target.closest('.tm-diagram-edge-label-group');
            if (labelGroupEl) {
                const labelHandle = labelGroupEl.querySelector('[data-label-handle]');
                const edgeId = labelHandle ? labelHandle.getAttribute('data-edge-id') : null;
                if (edgeId) {
                    inst.selectedIds = new Set([edgeId]);
                    this._updateSelection(inst);
                    inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [edgeId]);
                    return;
                }
            }
        }

        // Edge label handle clicked?
        const labelHitEl = e.target.closest('[data-label-handle="true"]');
        if (labelHitEl && !inst.readOnly) {
            e.preventDefault();
            e.stopPropagation();
            inst.isDraggingEdgeLabel = true;
            inst.dragEdgeLabelId = labelHitEl.getAttribute('data-edge-id');
            inst.dragEdgeLabelStart = { x: e.clientX, y: e.clientY };
            return;
        }

        // Edge waypoint / jetty handle clicked?
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
            const jettyType = handleEl.getAttribute('data-jetty');
            if (jettyType) {
                e.preventDefault();
                e.stopPropagation();
                inst.isDraggingJetty = true;
                inst.dragJettyEdgeId = handleEl.getAttribute('data-edge-id');
                inst.dragJettyType = jettyType;
                inst.dragJettySide = handleEl.getAttribute('data-jetty-side');
                inst.dragJettyNodeId = handleEl.getAttribute('data-node-id');
                const pt = this._screenToDoc(inst, e.clientX, e.clientY);
                inst.dragJettyStartDoc = pt;
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

        // Link clicked? (on a node with data-link)
        const linkEl = e.target.closest('[data-link]');
        if (linkEl && !e.ctrlKey && !e.metaKey && !e.shiftKey) {
            const link = linkEl.getAttribute('data-link');
            const nodeId = linkEl.getAttribute('data-node-id');
            if (link && nodeId && inst.dotNetRef) {
                e.preventDefault();
                e.stopPropagation();
                inst.dotNetRef.invokeMethodAsync('OnNodeLinkClicked', nodeId, link);
                return;
            }
        }

        // Node clicked?
        const nodeEl = e.target.closest('[data-node-id]');
        if (nodeEl && this._isNodeInActiveGroup(inst, nodeEl)) {
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

            if (inst.toolMode === 'edge' && !inst.readOnly) {
                const rect = this._nodeRect(inst, id);
                if (rect) {
                    const docPt = this._screenToDoc(inst, e.clientX, e.clientY);
                    const dx = docPt.x - (rect.x + rect.w / 2);
                    const dy = docPt.y - (rect.y + rect.h / 2);
                    const adx = Math.abs(dx);
                    const ady = Math.abs(dy);
                    let side = null;
                    let offset = 0.5;
                    if (rect.w > 0 && rect.h > 0 && (adx > 0 || ady > 0)) {
                        if (adx / rect.w > ady / rect.h) {
                            side = dx > 0 ? 'right' : 'left';
                            offset = Math.max(0, Math.min(1, (docPt.y - rect.y) / rect.h));
                        } else {
                            side = dy > 0 ? 'bottom' : 'top';
                            offset = Math.max(0, Math.min(1, (docPt.x - rect.x) / rect.w));
                        }
                    }
                    this._startEdgeDraw(inst, id, null, e.clientX, e.clientY, side, offset);
                }
                e.preventDefault();
                return;
            }

            if (!inst.selectedIds.has(id)) {
                inst.selectedIds = new Set([id]);
                this._updateSelection(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', [id]);
            }

            if (isLocked || inst.readOnly) return;
            if (isRightClick) return;

            inst.isDragging = true;
            inst.dragNodeIds = [...inst.selectedIds];

            // Include children of selected nodes
            const allIds = new Set(inst.dragNodeIds);
            inst.dragNodeIds.forEach(nid => {
                if (inst.htmlLayer) {
                    inst.htmlLayer.querySelectorAll('[data-parent-id="' + nid + '"]').forEach(childEl => {
                        const cid = childEl.getAttribute('data-node-id');
                        if (cid && this._isNodeInActiveGroup(inst, childEl)) allIds.add(cid);
                    });
                }
            });
            inst.dragNodeIds = [...allIds];

            // Include group nodes
            inst.dragNodeIds = this._includeGroupNodes(inst, inst.dragNodeIds);

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

        // Edge tool: cancel on empty canvas click
        if (inst.toolMode === 'edge') {
            inst.toolMode = 'select';
            inst.container.style.cursor = '';
            if (inst.dotNetRef) inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'select');
            return;
        }

        // Empty canvas: rubber-band selection (or clear selection on right-click)
        if (isRightClick) {
            if (inst.selectedIds.size > 0) {
                inst.selectedIds.clear();
                this._updateSelection(inst);
                inst.dotNetRef.invokeMethodAsync('OnSelectionChanged', []);
            }
            return;
        }
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

        if (inst.isDraggingJetty && inst.dragJettyEdgeId) {
            const pt = this._screenToDoc(inst, e.clientX, e.clientY);
            const nodeRect = this._nodeRect(inst, inst.dragJettyNodeId);
            if (nodeRect) {
                const side = inst.dragJettySide;
                let newSpacing = 0;
                if (side === 'left') newSpacing = nodeRect.x - pt.x;
                else if (side === 'right') newSpacing = pt.x - (nodeRect.x + nodeRect.w);
                else if (side === 'top') newSpacing = nodeRect.y - pt.y;
                else if (side === 'bottom') newSpacing = pt.y - (nodeRect.y + nodeRect.h);
                newSpacing = Math.max(0, Math.round(newSpacing));
                const sourceSpacing = inst.dragJettyType === 'source' ? newSpacing : null;
                const targetSpacing = inst.dragJettyType === 'target' ? newSpacing : null;
                inst.dotNetRef.invokeMethodAsync('OnEdgeSpacingChanged', inst.dragJettyEdgeId, sourceSpacing, targetSpacing);
            }
            return;
        }

        if (inst.isDraggingEdgeLabel && inst.dragEdgeLabelId) {
            const edgeId = inst.dragEdgeLabelId;
            const pathEl = inst.svg.querySelector('.tm-diagram-edge-path[data-edge-id="' + edgeId + '"]') || inst.svg.querySelector('path[data-edge-id="' + edgeId + '"]');
            if (pathEl) {
                const len = pathEl.getTotalLength();
                const rect = inst.container.getBoundingClientRect();
                const screenPt = { x: e.clientX - rect.left, y: e.clientY - rect.top };
                // Find closest point on path
                let bestT = 0;
                let bestDist = Infinity;
                const samples = 50;
                for (let i = 0; i <= samples; i++) {
                    const t = i / samples;
                    const p = pathEl.getPointAtLength(t * len);
                    const dx = p.x - screenPt.x;
                    const dy = p.y - screenPt.y;
                    const d = dx * dx + dy * dy;
                    if (d < bestDist) {
                        bestDist = d;
                        bestT = t;
                    }
                }
                inst.dotNetRef.invokeMethodAsync('OnEdgeLabelMoved', edgeId, bestT);
            }
            return;
        }

        if (inst.isDragging && inst.dragNodeIds.length > 0) {
            const dxScreen = e.clientX - inst.dragStartScreen.x;
            const dyScreen = e.clientY - inst.dragStartScreen.y;
            let dxDoc = dxScreen / inst.scale;
            let dyDoc = dyScreen / inst.scale;

            const guides = this._computeSnapGuides(inst, inst.dragNodeIds, inst.dragStartPositions, dxDoc, dyDoc);
            if (guides.x !== null) dxDoc += guides.x.delta;
            if (guides.y !== null) dyDoc += guides.y.delta;
            this._drawGuideLines(inst, guides);

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

        // Ruler cursor
        if (inst.dotNetRef) {
            const docPt = this._screenToDoc(inst, e.clientX, e.clientY);
            inst.dotNetRef.invokeMethodAsync('OnRulerCursorMoved', docPt.x, docPt.y);
        }

        // Connection hover icons
        if (!inst.readOnly && !inst.isDrawingEdge && !inst.isDragging && !inst.isDraggingWaypoint && !inst.isDraggingJetty && !inst.isDraggingEdgeLabel && !inst.isPanning && !inst.isRubberBand) {
            this._updateConnectHoverIcons(inst, e.clientX, e.clientY);
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
                    // Allow self-connections (loop edges)
                    inst.dotNetRef.invokeMethodAsync('JsOnEdgeCreated',
                        inst.drawSource.nodeId, inst.drawSource.portId,
                        targetNodeId, targetPortId,
                        inst.drawSource.side, inst.drawSource.offset, null, 0.5);
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

        if (inst.isDraggingJetty) {
            inst.isDraggingJetty = false;
            inst.dragJettyEdgeId = null;
            inst.dragJettyType = null;
            inst.dragJettySide = null;
            inst.dragJettyNodeId = null;
            inst.dragJettyStartDoc = null;
            return;
        }

        if (inst.isDraggingEdgeLabel) {
            inst.isDraggingEdgeLabel = false;
            inst.dragEdgeLabelId = null;
            inst.dragEdgeLabelStart = null;
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
            this._clearGuideLines(inst);

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
                            if (!this._isNodeInActiveGroup(inst, el)) return;
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

            // Include group nodes
            inst.dragNodeIds = this._includeGroupNodes(inst, inst.dragNodeIds);

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
                    inst.dotNetRef.invokeMethodAsync('OnNodeContextMenu', inst.longPressNodeId, inst.longPressStart.x, inst.longPressStart.y);
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
            let dxDoc = dxScreen / inst.scale;
            let dyDoc = dyScreen / inst.scale;

            const guides = this._computeSnapGuides(inst, inst.dragNodeIds, inst.dragStartPositions, dxDoc, dyDoc);
            if (guides.x !== null) dxDoc += guides.x.delta;
            if (guides.y !== null) dyDoc += guides.y.delta;
            this._drawGuideLines(inst, guides);

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
            this._clearGuideLines(inst);

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

    _startEdgeDraw: function (inst, nodeId, portId, clientX, clientY, side, offset) {
        inst.isDrawingEdge = true;
        const docPt = this._screenToDoc(inst, clientX, clientY);
        inst.drawSource = { nodeId: nodeId, portId: portId, side: side || null, offset: offset || 0.5, x: docPt.x, y: docPt.y };

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
        let endX = docPt.x;
        let endY = docPt.y;

        // Magnet snapping: find nearest node perimeter point
        const el = document.elementFromPoint(clientX, clientY);
        const nodeEl = el ? el.closest('[data-node-id]') : null;
        if (nodeEl) {
            const nid = nodeEl.getAttribute('data-node-id');
            const rect = this._nodeRect(inst, nid);
            if (rect) {
                const snap = this._snapToNodePerimeter(rect, docPt.x, docPt.y);
                endX = snap.x;
                endY = snap.y;
            }
        }

        const d = 'M ' + inst.drawSource.x + ' ' + inst.drawSource.y + ' L ' + endX + ' ' + endY;
        inst.drawTempPath.setAttribute('d', d);

        // Port snapping highlight
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

    _snapToNodePerimeter: function (rect, x, y) {
        // Find closest point on rectangle perimeter
        let cx = Math.max(rect.x, Math.min(x, rect.x + rect.w));
        let cy = Math.max(rect.y, Math.min(y, rect.y + rect.h));
        const dx = cx - x;
        const dy = cy - y;
        // If inside, project to nearest edge
        if (dx === 0 && dy === 0) {
            const dl = x - rect.x;
            const dr = rect.x + rect.w - x;
            const dt = y - rect.y;
            const db = rect.y + rect.h - y;
            const min = Math.min(dl, dr, dt, db);
            if (min === dl) return { x: rect.x, y: y };
            if (min === dr) return { x: rect.x + rect.w, y: y };
            if (min === dt) return { x: x, y: rect.y };
            return { x: x, y: rect.y + rect.h };
        }
        return { x: cx, y: cy };
    },

    _updateConnectHoverIcons: function (inst, clientX, clientY) {
        if (inst.isDrawingEdge || inst.isDragging || inst.isDraggingWaypoint || inst.isDraggingJetty || inst.isDraggingEdgeLabel || inst.isPanning || inst.isRubberBand) {
            this._hideConnectHoverIcons(inst);
            return;
        }
        const el = document.elementFromPoint(clientX, clientY);
        const nodeEl = el ? el.closest('[data-node-id]') : null;
        if (!nodeEl || !this._isNodeInActiveGroup(inst, nodeEl)) {
            this._hideConnectHoverIcons(inst);
            return;
        }
        const nodeId = nodeEl.getAttribute('data-node-id');
        const rect = this._nodeRect(inst, nodeId);
        if (!rect) {
            this._hideConnectHoverIcons(inst);
            return;
        }
        const docPt = this._screenToDoc(inst, clientX, clientY);
        const dx = Math.max(rect.x - docPt.x, 0, docPt.x - (rect.x + rect.w));
        const dy = Math.max(rect.y - docPt.y, 0, docPt.y - (rect.y + rect.h));
        const distPx = Math.max(dx, dy) * inst.scale;
        if (distPx > 15) {
            this._hideConnectHoverIcons(inst);
            return;
        }
        this._showConnectHoverIcons(inst, nodeId, rect, nodeEl);
    },

    _showConnectHoverIcons: function (inst, nodeId, rect, nodeEl) {
        if (inst.hoverConnectNodeId === nodeId) return;
        this._hideConnectHoverIcons(inst);
        inst.hoverConnectNodeId = nodeId;
        const dirs = [
            { css: 'n', side: 'top' },
            { css: 'e', side: 'right' },
            { css: 's', side: 'bottom' },
            { css: 'w', side: 'left' },
        ];
        dirs.forEach(d => {
            const btn = document.createElement('div');
            btn.className = 'tm-diagram-hover-connect tm-diagram-hover-connect--' + d.css;
            btn.setAttribute('data-hover-side', d.side);
            btn.style.cssText = 'position:absolute;width:22px;height:22px;border-radius:50%;background:#fff;border:1px solid var(--tm-color-primary,#3b82f6);display:flex;align-items:center;justify-content:center;cursor:crosshair;z-index:50;box-shadow:0 1px 3px rgba(0,0,0,0.12);pointer-events:all;';
            if (d.css === 'n') { btn.style.top = '-24px'; btn.style.left = 'calc(50% - 11px)'; }
            else if (d.css === 'e') { btn.style.top = 'calc(50% - 11px)'; btn.style.right = '-24px'; }
            else if (d.css === 's') { btn.style.bottom = '-24px'; btn.style.left = 'calc(50% - 11px)'; }
            else if (d.css === 'w') { btn.style.top = 'calc(50% - 11px)'; btn.style.left = '-24px'; }
            btn.innerHTML = '<span style="font-size:12px;color:var(--tm-color-primary,#3b82f6);transform:rotate(' + (d.css === 'n' ? '-90deg' : d.css === 's' ? '90deg' : d.css === 'w' ? '180deg' : '0deg') + ')">→</span>';
            btn.onmousedown = (e) => {
                e.preventDefault();
                e.stopPropagation();
                const docPt = this._screenToDoc(inst, e.clientX, e.clientY);
                let offset = 0.5;
                if (d.side === 'top' || d.side === 'bottom') {
                    offset = Math.max(0, Math.min(1, (docPt.x - rect.x) / rect.w));
                } else {
                    offset = Math.max(0, Math.min(1, (docPt.y - rect.y) / rect.h));
                }
                this._startEdgeDraw(inst, nodeId, null, e.clientX, e.clientY, d.side, offset);
            };
            nodeEl.appendChild(btn);
            if (!inst.hoverConnectEls) inst.hoverConnectEls = [];
            inst.hoverConnectEls.push(btn);
        });
    },

    _hideConnectHoverIcons: function (inst) {
        if (inst.hoverConnectEls) {
            inst.hoverConnectEls.forEach(el => { if (el.parentNode) el.parentNode.removeChild(el); });
            inst.hoverConnectEls = null;
        }
        inst.hoverConnectNodeId = null;
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

        // Return to select mode after using edge tool
        if (inst.toolMode === 'edge') {
            inst.toolMode = 'select';
            inst.container.style.cursor = '';
            if (inst.dotNetRef) inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'select');
        }
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
                case 'c':
                    e.preventDefault();
                    if (e.shiftKey) {
                        if (ids.length > 0) inst.dotNetRef.invokeMethodAsync('OnCopyStyle');
                    } else {
                        if (ids.length > 0) inst.dotNetRef.invokeMethodAsync('OnCopy', ids);
                    }
                    return;
                case 'v':
                    e.preventDefault();
                    if (e.shiftKey) {
                        inst.dotNetRef.invokeMethodAsync('OnPasteStyle');
                    } else {
                        inst.dotNetRef.invokeMethodAsync('OnPaste');
                    }
                    return;
                case 'f':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnShowSearch');
                    return;
                case 'h':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnShowReplace');
                    return;
                case 'b':
                    e.preventDefault();
                    if (ids.length > 0) inst.dotNetRef.invokeMethodAsync('OnToggleBold');
                    return;
                case 'i':
                    e.preventDefault();
                    if (ids.length > 0) inst.dotNetRef.invokeMethodAsync('OnToggleItalic');
                    return;
                case 'u':
                    e.preventDefault();
                    if (ids.length > 0) inst.dotNetRef.invokeMethodAsync('OnToggleUnderline');
                    return;
                case 'home':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnNavigateToCorner', 'top-left');
                    return;
                case 'end':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnNavigateToCorner', 'bottom-right');
                    return;
                case 'pageup':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnSwitchPage', -1);
                    return;
                case 'pagedown':
                    e.preventDefault();
                    inst.dotNetRef.invokeMethodAsync('OnSwitchPage', 1);
                    return;
                case 'g':
                    if (e.shiftKey) {
                        e.preventDefault();
                        if (ids.length === 1) {
                            const selId = ids[0];
                            const el = this._nodeEl(inst, selId);
                            if (el && el.getAttribute('data-stencil-id') === 'general.group') {
                                inst.dotNetRef.invokeMethodAsync('OnEnterGroup', selId);
                                return;
                            }
                        }
                        if (ids.length > 1) inst.dotNetRef.invokeMethodAsync('OnGroupSelected');
                    }
                    return;
                case 'f':
                    if (e.shiftKey) {
                        e.preventDefault();
                        inst.dotNetRef.invokeMethodAsync('OnExitGroup');
                    }
                    return;
                case 'l':
                    if (e.shiftKey) {
                        e.preventDefault();
                        inst.dotNetRef.invokeMethodAsync('OnLockSelected');
                    }
                    return;
            }
        }

        // Quick-insert stencils (A,S,D,F,R)
        if (!e.ctrlKey && !e.metaKey && !e.target.matches('input,textarea,select')) {
            const stencilMap = {
                'KeyA': 'general.text',
                'KeyS': 'general.sticky-note',
                'KeyD': 'general.rectangle',
                'KeyF': 'general.ellipse',
                'KeyR': 'general.rhombus'
            };
            if (stencilMap[e.code]) {
                e.preventDefault();
                inst.dotNetRef.invokeMethodAsync('OnQuickInsert', stencilMap[e.code]);
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
            if (e.code === 'KeyL' || e.code === 'KeyC') {
                e.preventDefault();
                inst.toolMode = 'edge';
                inst.container.style.cursor = 'crosshair';
                inst.dotNetRef.invokeMethodAsync('OnToolModeChanged', 'edge');
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

            // Filter to active group and include group nodes
            let nudgeIds = ids.filter(id => {
                const el = this._nodeEl(inst, id);
                return this._isNodeInActiveGroup(inst, el);
            });
            nudgeIds = this._includeGroupNodes(inst, nudgeIds);

            nudgeIds.forEach(id => {
                const r = this._nodeRect(inst, id);
                if (!r) return;
                const clamped = this._clampChildPosition(inst, id, r.x + dx, r.y + dy);
                this._setNodeTranslate(inst, id, clamped.x, clamped.y);
            });
            this._updateSelection(inst);

            const moves = nudgeIds.map(id => {
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

    // ── Obstacle-avoiding orthogonal router ─────────────────────────────────

    _simplifyWaypoints: function (waypoints) {
        if (!waypoints || waypoints.length < 3) return waypoints;
        const result = [waypoints[0]];
        for (let i = 1; i < waypoints.length - 1; i++) {
            const prev = result[result.length - 1];
            const curr = waypoints[i];
            const next = waypoints[i + 1];
            if ((prev[0] === curr[0] && curr[0] === next[0]) || (prev[1] === curr[1] && curr[1] === next[1])) {
                continue;
            }
            result.push(curr);
        }
        result.push(waypoints[waypoints.length - 1]);
        return result;
    },

    _computeObstacleAvoidingWaypoints: function (sx1, sy1, sx2, sy2, obstacles) {
        if (!obstacles || obstacles.length === 0) return null;

        const margin = 12;
        const expanded = obstacles.map(function (o) {
            return {
                x: o.x - margin,
                y: o.y - margin,
                w: o.w + margin * 2,
                h: o.h + margin * 2
            };
        });

        const xSet = new Set();
        const ySet = new Set();
        xSet.add(sx1);
        xSet.add(sx2);
        ySet.add(sy1);
        ySet.add(sy2);

        for (let i = 0; i < expanded.length; i++) {
            const o = expanded[i];
            xSet.add(o.x);
            xSet.add(o.x + o.w);
            ySet.add(o.y);
            ySet.add(o.y + o.h);
        }

        let xs = Array.from(xSet).sort(function (a, b) { return a - b; });
        let ys = Array.from(ySet).sort(function (a, b) { return a - b; });

        // Add midpoints between adjacent coordinates to ensure narrow channels are represented
        const augmentedXs = [];
        for (let i = 0; i < xs.length; i++) {
            augmentedXs.push(xs[i]);
            if (i < xs.length - 1 && xs[i + 1] - xs[i] > 1) {
                augmentedXs.push((xs[i] + xs[i + 1]) / 2);
            }
        }
        const augmentedYs = [];
        for (let i = 0; i < ys.length; i++) {
            augmentedYs.push(ys[i]);
            if (i < ys.length - 1 && ys[i + 1] - ys[i] > 1) {
                augmentedYs.push((ys[i] + ys[i + 1]) / 2);
            }
        }

        xs = augmentedXs.sort(function (a, b) { return a - b; });
        ys = augmentedYs.sort(function (a, b) { return a - b; });

        const xToIdx = {};
        const yToIdx = {};
        for (let i = 0; i < xs.length; i++) xToIdx[xs[i]] = i;
        for (let i = 0; i < ys.length; i++) yToIdx[ys[i]] = i;

        const startX = xToIdx[sx1];
        const startY = yToIdx[sy1];
        const endX = xToIdx[sx2];
        const endY = yToIdx[sy2];

        function intersectsObstacle(x1, y1, x2, y2) {
            const minX = Math.min(x1, x2);
            const maxX = Math.max(x1, x2);
            const minY = Math.min(y1, y2);
            const maxY = Math.max(y1, y2);
            for (let i = 0; i < expanded.length; i++) {
                const o = expanded[i];
                const ox1 = o.x, oy1 = o.y, ox2 = o.x + o.w, oy2 = o.y + o.h;
                if (maxX <= ox1 || minX >= ox2 || maxY <= oy1 || minY >= oy2) continue;
                if (y1 === y2) {
                    if (y1 > oy1 && y1 < oy2 && maxX > ox1 && minX < ox2) return true;
                } else if (x1 === x2) {
                    if (x1 > ox1 && x1 < ox2 && maxY > oy1 && minY < oy2) return true;
                }
            }
            return false;
        }

        // A* with turn penalty
        const open = [];
        const closed = new Set();
        const gScore = {};
        const fScore = {};
        const cameFrom = {};

        function makeKey(x, y) { return x + ',' + y; }

        function pushOpen(k, f) {
            open.push({ k: k, f: f });
            open.sort(function (a, b) { return a.f - b.f; });
        }

        function popOpen() {
            return open.shift().k;
        }

        const sKey = makeKey(startX, startY);
        gScore[sKey] = 0;
        fScore[sKey] = Math.abs(sx2 - sx1) + Math.abs(sy2 - sy1);
        pushOpen(sKey, fScore[sKey]);

        while (open.length > 0) {
            const current = popOpen();
            if (current === makeKey(endX, endY)) {
                const path = [];
                let c = current;
                while (c) {
                    const parts = c.split(',');
                    const cx = parseInt(parts[0], 10);
                    const cy = parseInt(parts[1], 10);
                    path.push([xs[cx], ys[cy]]);
                    c = cameFrom[c];
                }
                path.reverse();
                return this._simplifyWaypoints(path);
            }

            closed.add(current);

            const parts = current.split(',');
            const cx = parseInt(parts[0], 10);
            const cy = parseInt(parts[1], 10);
            const cxVal = xs[cx];
            const cyVal = ys[cy];

            const neighbors = [];
            if (cx > 0) neighbors.push([cx - 1, cy, xs[cx - 1], cyVal]);
            if (cx < xs.length - 1) neighbors.push([cx + 1, cy, xs[cx + 1], cyVal]);
            if (cy > 0) neighbors.push([cx, cy - 1, cxVal, ys[cy - 1]]);
            if (cy < ys.length - 1) neighbors.push([cx, cy + 1, cxVal, ys[cy + 1]]);

            for (let i = 0; i < neighbors.length; i++) {
                const n = neighbors[i];
                const nx = n[0], ny = n[1], nxVal = n[2], nyVal = n[3];
                const nKey = makeKey(nx, ny);
                if (closed.has(nKey)) continue;
                if (intersectsObstacle(cxVal, cyVal, nxVal, nyVal)) continue;

                const moveDist = Math.abs(nxVal - cxVal) + Math.abs(nyVal - cyVal);
                let turnPenalty = 0;
                if (cameFrom[current]) {
                    const cparts = cameFrom[current].split(',');
                    const px = parseInt(cparts[0], 10);
                    const py = parseInt(cparts[1], 10);
                    const dx1 = cx - px;
                    const dy1 = cy - py;
                    const dx2 = nx - cx;
                    const dy2 = ny - cy;
                    if (dx1 !== dx2 || dy1 !== dy2) turnPenalty = 40;
                }

                const tentativeG = gScore[current] + moveDist + turnPenalty;
                if (!(nKey in gScore) || tentativeG < gScore[nKey]) {
                    cameFrom[nKey] = current;
                    gScore[nKey] = tentativeG;
                    fScore[nKey] = tentativeG + Math.abs(nxVal - sx2) + Math.abs(nyVal - sy2);
                    pushOpen(nKey, fScore[nKey]);
                }
            }
        }

        return null;
    },

    // ── Orthogonal router ────────────────────────────────────────────────────

    _computeOrthogonalWaypoints: function (x1, y1, side1, x2, y2, side2, routing, sourceSpacing, targetSpacing, srcBounds, tgtBounds, obstacles) {
        routing = (routing || 'orthogonal').toLowerCase();
        sourceSpacing = sourceSpacing || 0;
        targetSpacing = targetSpacing || 0;

        let s1 = (side1 || '').toLowerCase();
        let s2 = (side2 || '').toLowerCase();

        // Swimlane-aware routing: force entry direction based on swimlane orientation
        if (srcBounds && srcBounds.isSwimlane) {
            if (srcBounds.isHorizontal) {
                // Force left/right exit for horizontal swimlane
                if (s1 !== 'left' && s1 !== 'right') s1 = x1 < x2 ? 'right' : 'left';
            } else {
                // Force top/bottom exit for vertical swimlane
                if (s1 !== 'top' && s1 !== 'bottom') s1 = y1 < y2 ? 'bottom' : 'top';
            }
        }
        if (tgtBounds && tgtBounds.isSwimlane) {
            if (tgtBounds.isHorizontal) {
                // Force left/right entry for horizontal swimlane
                if (s2 !== 'left' && s2 !== 'right') s2 = x1 < x2 ? 'left' : 'right';
            } else {
                // Force top/bottom entry for vertical swimlane
                if (s2 !== 'top' && s2 !== 'bottom') s2 = y1 < y2 ? 'top' : 'bottom';
            }
        }

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
        if (routing === 'orthogonal' || routing === 'rounded') {
            const obstaclePath = this._computeObstacleAvoidingWaypoints(sx1, sy1, sx2, sy2, obstacles);
            if (obstaclePath && obstaclePath.length >= 2) {
                // obstaclePath includes start and end; we need intermediate waypoints only
                return obstaclePath.slice(1, obstaclePath.length - 1);
            }
        }

        // Simple fallback for orthogonal, rounded, and any other routing
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

    setActiveGroupId: function (container, activeGroupId) {
        const inst = this.instances.get(container.id);
        if (!inst) return;
        inst.activeGroupId = activeGroupId || null;
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

    openContextMenu: function (container, menuEl) {
        if (!container || !menuEl) return;
        const inst = this.instances.get(container.id);
        if (!inst) return;
        // Clean up any existing dismiss handlers first
        if (inst._menuDismissHandler) {
            document.removeEventListener('mousedown', inst._menuDismissHandler);
            document.removeEventListener('keydown', inst._menuKeyHandler);
            inst._menuDismissHandler = null;
            inst._menuKeyHandler = null;
        }
        setTimeout(function () {
            if (!menuEl) return;
            const rect = menuEl.getBoundingClientRect();
            const vw = window.innerWidth;
            const vh = window.innerHeight;
            const margin = 8;
            let left = parseFloat(menuEl.style.left) || 0;
            let top = parseFloat(menuEl.style.top) || 0;
            if (left + rect.width + margin > vw) {
                left = Math.max(margin, vw - rect.width - margin);
            }
            if (top + rect.height + margin > vh) {
                top = Math.max(margin, vh - rect.height - margin);
            }
            menuEl.style.left = left + 'px';
            menuEl.style.top = top + 'px';

            inst._menuDismissHandler = function (e) {
                if (e.button !== 0) return; // only left click outside
                if (e.target.closest('.tm-diagram-editor__context-menu')) return;
                if (inst.dotNetRef) inst.dotNetRef.invokeMethodAsync('CloseContextMenu');
                document.removeEventListener('mousedown', inst._menuDismissHandler);
                document.removeEventListener('keydown', inst._menuKeyHandler);
                inst._menuDismissHandler = null;
                inst._menuKeyHandler = null;
            };
            inst._menuKeyHandler = function (e) {
                if (e.key === 'Escape') {
                    if (inst.dotNetRef) inst.dotNetRef.invokeMethodAsync('CloseContextMenu');
                    document.removeEventListener('mousedown', inst._menuDismissHandler);
                    document.removeEventListener('keydown', inst._menuKeyHandler);
                    inst._menuDismissHandler = null;
                    inst._menuKeyHandler = null;
                }
            };
            document.addEventListener('mousedown', inst._menuDismissHandler);
            document.addEventListener('keydown', inst._menuKeyHandler);
        }, 0);
    },

    closeContextMenu: function (container) {
        if (!container) return;
        const inst = this.instances.get(container.id);
        if (!inst) return;
        if (inst._menuDismissHandler) {
            document.removeEventListener('mousedown', inst._menuDismissHandler);
            document.removeEventListener('keydown', inst._menuKeyHandler);
            inst._menuDismissHandler = null;
            inst._menuKeyHandler = null;
        }
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

    zoomToRect: function (container, x, y, w, h, padding) {
        const inst = this.instances.get(container ? container.id : null);
        if (!inst || !inst.svg) return;
        padding = (padding != null) ? padding : 40;
        const svgRect = inst.svg.getBoundingClientRect();
        const contentW = w + padding * 2;
        const contentH = h + padding * 2;
        const scale = Math.min(svgRect.width / contentW, svgRect.height / contentH, 2.0);
        const clampedScale = Math.max(0.25, scale);
        const newW = svgRect.width / clampedScale;
        const newH = svgRect.height / clampedScale;
        const nx = x + w / 2 - newW / 2;
        const ny = y + h / 2 - newH / 2;
        this._setViewBox(inst, nx, ny, newW, newH);
        if (inst.dotNetRef)
            inst.dotNetRef.invokeMethodAsync('OnZoomChanged', clampedScale);
        if (inst.dotNetRef)
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', nx, ny, newW, newH);
    },

    addPageViewMargin: function (container, margin) {
        const inst = this.instances.get(container ? container.id : null);
        if (!inst || !inst.svg) return;
        const vb = this._getViewBox(inst);
        this._setViewBox(inst, vb.x - margin, vb.y - margin, vb.w + margin * 2, vb.h + margin * 2);
        if (inst.dotNetRef)
            inst.dotNetRef.invokeMethodAsync('OnViewportChanged', vb.x - margin, vb.y - margin, vb.w + margin * 2, vb.h + margin * 2);
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

    computeOrthogonalWaypoints: function (x1, y1, side1, x2, y2, side2, routing, sourceSpacing, targetSpacing, srcBounds, tgtBounds, obstacles) {
        return this._computeOrthogonalWaypoints(x1, y1, side1, x2, y2, side2, routing, sourceSpacing, targetSpacing, srcBounds, tgtBounds, obstacles);
    },

    screenToDoc: function (container, clientX, clientY) {
        const inst = this.instances.get(container.id);
        if (!inst) return { x: clientX, y: clientY };
        return this._screenToDoc(inst, clientX, clientY);
    },

    // ── Magnetic guidelines ────────────────────────────────────────────────

    _includeGroupNodes: function (inst, nodeIds) {
        const all = new Set(nodeIds);
        const addChildren = (parentId) => {
            if (!inst.htmlLayer) return;
            inst.htmlLayer.querySelectorAll('[data-parent-id="' + parentId + '"]').forEach(childEl => {
                const cid = childEl.getAttribute('data-node-id');
                if (cid && !all.has(cid)) {
                    all.add(cid);
                    addChildren(cid);
                }
            });
        };
        nodeIds.forEach(id => addChildren(id));
        return [...all];
    },

    _computeSnapGuides: function (inst, dragNodeIds, dragStartPositions, dxDoc, dyDoc) {
        const threshold = 8 / inst.scale; // 8 screen px
        const guides = { x: null, y: null, distances: { x: null, y: null } };
        if (!inst.htmlLayer) return guides;

        const draggedRects = dragNodeIds.map(id => {
            const start = dragStartPositions[id];
            if (!start) return null;
            const w = start.w ?? 0;
            const h = start.h ?? 0;
            return {
                id,
                left: start.x + dxDoc,
                right: start.x + dxDoc + w,
                top: start.y + dyDoc,
                bottom: start.y + dyDoc + h,
                centerX: start.x + dxDoc + w / 2,
                centerY: start.y + dyDoc + h / 2,
            };
        }).filter(Boolean);

        const allRects = [];
        inst.htmlLayer.querySelectorAll('.tm-diagram-node').forEach(el => {
            const id = el.getAttribute('data-node-id');
            if (!id || dragNodeIds.includes(id)) return;
            const r = this._nodeRect(inst, id);
            if (!r) return;
            allRects.push({
                id,
                left: r.x,
                right: r.x + r.w,
                top: r.y,
                bottom: r.y + r.h,
                centerX: r.x + r.w / 2,
                centerY: r.y + r.h / 2,
            });
        });

        function findBest(dragged, targets, propNames) {
            let best = null;
            for (const d of dragged) {
                for (const t of targets) {
                    for (const dp of propNames) {
                        for (const tp of propNames) {
                            const delta = t[tp] - d[dp];
                            if (Math.abs(delta) <= threshold) {
                                if (!best || Math.abs(delta) < Math.abs(best.delta)) {
                                    best = { delta, dProp: dp, tProp: tp, dVal: d[dp], tVal: t[tp] };
                                }
                            }
                        }
                    }
                }
            }
            return best;
        }

        const xBest = findBest(draggedRects, allRects, ['left', 'right', 'centerX']);
        const yBest = findBest(draggedRects, allRects, ['top', 'bottom', 'centerY']);

        if (xBest) guides.x = xBest;
        if (yBest) guides.y = yBest;

        // Distance guides
        if (draggedRects.length > 0) {
            const dragBounds = {
                left: Math.min(...draggedRects.map(r => r.left)),
                right: Math.max(...draggedRects.map(r => r.right)),
                top: Math.min(...draggedRects.map(r => r.top)),
                bottom: Math.max(...draggedRects.map(r => r.bottom)),
            };
            const distThreshold = 500; // document px max distance to show label

            // X axis distances
            let leftNeighbor = null;
            let rightNeighbor = null;
            let minLeftGap = Infinity;
            let minRightGap = Infinity;
            for (const t of allRects) {
                const gapLeft = dragBounds.left - t.right;
                const gapRight = t.left - dragBounds.right;
                if (gapLeft >= 0 && gapLeft < minLeftGap) { minLeftGap = gapLeft; leftNeighbor = t; }
                if (gapRight >= 0 && gapRight < minRightGap) { minRightGap = gapRight; rightNeighbor = t; }
            }
            if (leftNeighbor && minLeftGap <= distThreshold) {
                const consistent = allRects.some(t => t !== leftNeighbor && Math.abs((dragBounds.left - t.right) - minLeftGap) < 1);
                guides.distances.x = { value: minLeftGap, from: leftNeighbor.right, to: dragBounds.left, side: 'left', consistent };
            } else if (rightNeighbor && minRightGap <= distThreshold) {
                const consistent = allRects.some(t => t !== rightNeighbor && Math.abs((t.left - dragBounds.right) - minRightGap) < 1);
                guides.distances.x = { value: minRightGap, from: dragBounds.right, to: rightNeighbor.left, side: 'right', consistent };
            }

            // Y axis distances
            let topNeighbor = null;
            let bottomNeighbor = null;
            let minTopGap = Infinity;
            let minBottomGap = Infinity;
            for (const t of allRects) {
                const gapTop = dragBounds.top - t.bottom;
                const gapBottom = t.top - dragBounds.bottom;
                if (gapTop >= 0 && gapTop < minTopGap) { minTopGap = gapTop; topNeighbor = t; }
                if (gapBottom >= 0 && gapBottom < minBottomGap) { minBottomGap = gapBottom; bottomNeighbor = t; }
            }
            if (topNeighbor && minTopGap <= distThreshold) {
                const consistent = allRects.some(t => t !== topNeighbor && Math.abs((dragBounds.top - t.bottom) - minTopGap) < 1);
                guides.distances.y = { value: minTopGap, from: topNeighbor.bottom, to: dragBounds.top, side: 'top', consistent };
            } else if (bottomNeighbor && minBottomGap <= distThreshold) {
                const consistent = allRects.some(t => t !== bottomNeighbor && Math.abs((t.top - dragBounds.bottom) - minBottomGap) < 1);
                guides.distances.y = { value: minBottomGap, from: dragBounds.bottom, to: bottomNeighbor.top, side: 'bottom', consistent };
            }
        }

        return guides;
    },

    _formatDistance: function (inst, px) {
        const unit = (inst.rulerUnit || 'px').toLowerCase();
        const scale = inst.pageScale || 1.0;
        const dpi = 96.0;
        let factor = 1.0;
        switch (unit) {
            case 'pt': factor = 72.0 / dpi; break;
            case 'in': factor = 1.0 / dpi; break;
            case 'mm': factor = 25.4 / dpi; break;
            case 'm': factor = 0.0254 / dpi; break;
        }
        const val = px * factor / scale;
        let s = val.toFixed(2);
        if (s.endsWith('.00')) s = s.slice(0, -3);
        else if (s.endsWith('0')) s = s.slice(0, -1);
        if (s === '-0') s = '0';
        return s + ' ' + unit;
    },

    _drawGuideLines: function (inst, guides) {
        this._clearGuideLines(inst);
        if (!inst.svg) return;
        const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        g.id = 'tm-diagram-snap-guides';
        g.setAttribute('pointer-events', 'none');

        const vb = this._getViewBox(inst);
        const color = '#3b82f6'; // blue-500

        if (guides.x !== null) {
            const x = guides.x.tVal;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', x);
            line.setAttribute('y1', vb.y);
            line.setAttribute('x2', x);
            line.setAttribute('y2', vb.y + vb.h);
            line.setAttribute('stroke', color);
            line.setAttribute('stroke-width', 1 / inst.scale);
            line.setAttribute('stroke-dasharray', (4 / inst.scale) + ',' + (4 / inst.scale));
            g.appendChild(line);
        }
        if (guides.y !== null) {
            const y = guides.y.tVal;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', vb.x);
            line.setAttribute('y1', y);
            line.setAttribute('x2', vb.x + vb.w);
            line.setAttribute('y2', y);
            line.setAttribute('stroke', color);
            line.setAttribute('stroke-width', 1 / inst.scale);
            line.setAttribute('stroke-dasharray', (4 / inst.scale) + ',' + (4 / inst.scale));
            g.appendChild(line);
        }

        // Distance guide lines & labels
        if (guides.distances) {
            if (guides.distances.x) {
                const d = guides.distances.x;
                const midY = vb.y + vb.h / 2;
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', d.from);
                line.setAttribute('y1', midY);
                line.setAttribute('x2', d.to);
                line.setAttribute('y2', midY);
                line.setAttribute('stroke', '#10b981'); // emerald-500
                line.setAttribute('stroke-width', 1 / inst.scale);
                line.setAttribute('marker-start', 'url(#arrow-emerald)');
                line.setAttribute('marker-end', 'url(#arrow-emerald)');
                g.appendChild(line);
                const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text.setAttribute('x', (d.from + d.to) / 2);
                text.setAttribute('y', midY - (4 / inst.scale));
                text.setAttribute('text-anchor', 'middle');
                text.setAttribute('class', 'tm-diagram-distance-label');
                text.setAttribute('font-size', (11 / inst.scale));
                text.textContent = this._formatDistance(inst, d.value);
                g.appendChild(text);
            }
            if (guides.distances.y) {
                const d = guides.distances.y;
                const midX = vb.x + vb.w / 2;
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', midX);
                line.setAttribute('y1', d.from);
                line.setAttribute('x2', midX);
                line.setAttribute('y2', d.to);
                line.setAttribute('stroke', '#10b981');
                line.setAttribute('stroke-width', 1 / inst.scale);
                line.setAttribute('marker-start', 'url(#arrow-emerald)');
                line.setAttribute('marker-end', 'url(#arrow-emerald)');
                g.appendChild(line);
                const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text.setAttribute('x', midX + (4 / inst.scale));
                text.setAttribute('y', (d.from + d.to) / 2);
                text.setAttribute('text-anchor', 'start');
                text.setAttribute('dominant-baseline', 'middle');
                text.setAttribute('class', 'tm-diagram-distance-label');
                text.setAttribute('font-size', (11 / inst.scale));
                text.textContent = this._formatDistance(inst, d.value);
                g.appendChild(text);
            }
        }

        if (g.childNodes.length > 0) {
            inst.svg.appendChild(g);
        }
    },

    _clearGuideLines: function (inst) {
        if (!inst.svg) return;
        const g = inst.svg.getElementById('tm-diagram-snap-guides');
        if (g) g.remove();
    },

    // ── Layout (dagre) ─────────────────────────────────────────────────────

    runDagreLayout: function (container, nodes, edges, direction) {
        if (typeof dagre === 'undefined') {
            console.warn('dagre not loaded');
            return null;
        }
        const inst = this.instances.get(container.id);
        if (!inst) return null;

        const g = new dagre.graphlib.Graph().setGraph({
            rankdir: direction || 'TB',
            ranksep: 80,
            nodesep: 40,
            marginx: 20,
            marginy: 20,
        }).setDefaultEdgeLabel(function() { return {}; });

        nodes.forEach(function(n) {
            g.setNode(n.id, { width: n.width, height: n.height });
        });

        edges.forEach(function(e) {
            g.setEdge(e.source, e.target);
        });

        dagre.layout(g);

        const result = [];
        g.nodes().forEach(function(v) {
            const node = g.node(v);
            result.push({ id: v, x: node.x - node.width / 2, y: node.y - node.height / 2 });
        });

        return result;
    },

    // ── Tree Layout (d3-hierarchy) ───────────────────────────────────────────

    runTreeLayout: function (container, nodes, edges, direction) {
        if (typeof d3 === 'undefined' || !d3.tree) {
            console.warn('d3-hierarchy not loaded');
            return null;
        }
        const nodeMap = new Map(nodes.map(n => [n.id, n]));
        const inDegree = new Map();
        nodes.forEach(n => inDegree.set(n.id, 0));
        edges.forEach(e => {
            if (inDegree.has(e.target)) inDegree.set(e.target, inDegree.get(e.target) + 1);
        });

        const roots = nodes.filter(n => inDegree.get(n.id) === 0).map(n => n.id);

        let hierarchyData;
        if (roots.length === 0) {
            // Cycle: pick arbitrary root
            hierarchyData = { name: nodes[0].id, children: this._buildTreeChildren(nodes[0].id, nodeMap, edges, new Set()) };
        } else if (roots.length === 1) {
            hierarchyData = { name: roots[0], children: this._buildTreeChildren(roots[0], nodeMap, edges, new Set()) };
        } else {
            const children = roots.map(r => ({ name: r, children: this._buildTreeChildren(r, nodeMap, edges, new Set()) }));
            hierarchyData = { name: '__virtual_root__', children: children };
        }

        const root = d3.hierarchy(hierarchyData);
        const nodeCount = nodes.length;
        const avgSize = Math.max(60, nodes.reduce((s, n) => s + n.width + n.height, 0) / (nodeCount * 2));
        const sizeW = nodeCount * avgSize * 1.5;
        const sizeH = nodeCount * avgSize * 1.2;

        const isHorizontal = direction === 'LR' || direction === 'RL';
        const treeLayout = d3.tree().size(isHorizontal ? [sizeH, sizeW] : [sizeW, sizeH]);
        treeLayout(root);

        const result = [];
        root.descendants().forEach(d => {
            if (d.data.name === '__virtual_root__') return;
            const n = nodeMap.get(d.data.name);
            if (!n) return;
            let x = d.x;
            let y = d.y;
            if (direction === 'BT') {
                y = sizeH - y;
            } else if (direction === 'RL') {
                x = sizeW - x;
            }
            result.push({ id: d.data.name, x: x - n.width / 2, y: y - n.height / 2 });
        });

        return result;
    },

    _buildTreeChildren: function (parentId, nodeMap, edges, visited) {
        visited.add(parentId);
        const children = [];
        edges.forEach(e => {
            if (e.source === parentId && !visited.has(e.target)) {
                children.push({
                    name: e.target,
                    children: this._buildTreeChildren(e.target, nodeMap, edges, visited)
                });
            }
        });
        return children;
    },

    // ── Force-directed Layout (d3-force) ─────────────────────────────────────

    runForceLayout: function (container, nodes, edges, options) {
        if (typeof d3 === 'undefined' || !d3.forceSimulation) {
            console.warn('d3-force not loaded');
            return null;
        }
        const opts = options || {};
        const width = opts.width || 800;
        const height = opts.height || 600;
        const linkDistance = opts.linkDistance || 100;
        const chargeStrength = opts.chargeStrength || -300;
        const collideRadius = opts.collideRadius || 40;
        const ticks = opts.ticks || 300;

        const nodeMap = new Map();
        const simNodes = nodes.map(n => {
            const sn = { id: n.id, x: width / 2 + (Math.random() - 0.5) * 50, y: height / 2 + (Math.random() - 0.5) * 50, width: n.width, height: n.height };
            nodeMap.set(n.id, sn);
            return sn;
        });

        const simLinks = edges.map(e => ({
            source: nodeMap.get(e.source),
            target: nodeMap.get(e.target)
        })).filter(l => l.source && l.target);

        const simulation = d3.forceSimulation(simNodes)
            .force('link', d3.forceLink(simLinks).id(d => d.id).distance(linkDistance))
            .force('charge', d3.forceManyBody().strength(chargeStrength))
            .force('center', d3.forceCenter(width / 2, height / 2))
            .force('collide', d3.forceCollide().radius(d => collideRadius))
            .stop();

        for (let i = 0; i < ticks; i++) {
            simulation.tick();
        }

        const result = [];
        simNodes.forEach(n => {
            const original = nodes.find(x => x.id === n.id);
            const w = original ? original.width : 0;
            const h = original ? original.height : 0;
            const x = Number.isFinite(n.x) ? n.x : width / 2;
            const y = Number.isFinite(n.y) ? n.y : height / 2;
            result.push({ id: n.id, x: x - w / 2, y: y - h / 2 });
        });

        return result;
    },

    // ── Circle Layout ────────────────────────────────────────────────────────

    runCircleLayout: function (container, nodes, options) {
        const opts = options || {};
        const maxSize = Math.max(60, ...nodes.map(n => Math.max(n.width, n.height)));
        const radius = opts.radius || (maxSize * nodes.length / (2 * Math.PI) + maxSize);
        const centerX = opts.centerX || radius + maxSize;
        const centerY = opts.centerY || radius + maxSize;
        const startAngle = opts.startAngle || 0;

        const result = [];
        const count = nodes.length;
        nodes.forEach((n, i) => {
            const angle = startAngle + (2 * Math.PI * i / count);
            const x = centerX + radius * Math.cos(angle);
            const y = centerY + radius * Math.sin(angle);
            result.push({ id: n.id, x: x - n.width / 2, y: y - n.height / 2 });
        });

        return result;
    },

    // ── Grid / Matrix Layout ─────────────────────────────────────────────────

    runGridLayout: function (container, nodes, options) {
        const opts = options || {};
        const cellWidth = opts.cellWidth || 180;
        const cellHeight = opts.cellHeight || 120;
        const padding = opts.padding || 20;
        const columns = opts.columns || Math.ceil(Math.sqrt(nodes.length));

        const result = [];
        nodes.forEach((n, i) => {
            const col = n.gridColumn !== undefined ? n.gridColumn : (i % columns);
            const row = n.gridRow !== undefined ? n.gridRow : Math.floor(i / columns);
            const x = padding + col * (cellWidth + padding);
            const y = padding + row * (cellHeight + padding);
            result.push({ id: n.id, x: x, y: y });
        });

        return result;
    },
};
