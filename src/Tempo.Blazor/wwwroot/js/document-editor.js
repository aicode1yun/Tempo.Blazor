(function () {
    const handlers = new WeakMap();

    function openOfflineDb() {
        return new Promise((resolve, reject) => {
            if (!window.indexedDB) {
                reject(new Error("IndexedDB is not available."));
                return;
            }

            const request = window.indexedDB.open("tempo-blazor-document-editor", 1);
            request.onupgradeneeded = event => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains("drafts")) {
                    const store = db.createObjectStore("drafts", { keyPath: "id" });
                    store.createIndex("documentId", "documentId", { unique: false });
                    store.createIndex("state", "state", { unique: false });
                    store.createIndex("updatedAt", "updatedAt", { unique: false });
                }
            };
            request.onsuccess = event => resolve(event.target.result);
            request.onerror = event => reject(event.target.error || new Error("IndexedDB request failed."));
        });
    }

    function withDraftStore(mode, callback) {
        return openOfflineDb().then(db => new Promise((resolve, reject) => {
            const transaction = db.transaction("drafts", mode);
            const store = transaction.objectStore("drafts");
            let value;
            transaction.oncomplete = () => {
                db.close();
                resolve(value);
            };
            transaction.onerror = event => {
                db.close();
                reject(event.target.error || new Error("IndexedDB transaction failed."));
            };
            value = callback(store);
        }));
    }

    function requestToPromise(request) {
        return new Promise((resolve, reject) => {
            request.onsuccess = event => resolve(event.target.result);
            request.onerror = event => reject(event.target.error || new Error("IndexedDB request failed."));
        });
    }

    window.tmDocumentEditor = {
        downloadFile(fileName, contentType, base64Data) {
            const binary = atob(base64Data || "");
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }

            const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement("a");
            anchor.href = url;
            anchor.download = fileName || "document";
            document.body.appendChild(anchor);
            anchor.click();
            anchor.remove();
            setTimeout(() => URL.revokeObjectURL(url), 0);
        },

        offlineStore: {
            saveDraft(draft) {
                return withDraftStore("readwrite", store => store.put(draft));
            },

            loadDraft(draftId) {
                return withDraftStore("readonly", store => requestToPromise(store.get(draftId)));
            },

            deleteDraft(draftId) {
                return withDraftStore("readwrite", store => store.delete(draftId));
            },

            listPendingDrafts(documentId) {
                return withDraftStore("readonly", store => requestToPromise(store.getAll()).then(items => {
                    return (items || [])
                        .filter(item => item && item.state !== 2)
                        .filter(item => !documentId || item.documentId === documentId)
                        .sort((left, right) => String(right.updatedAt || "").localeCompare(String(left.updatedAt || "")));
                }));
            }
        },

        attachPaste(element, dotNet, maxFileSize, allowedContentTypes) {
            if (!element || !dotNet) {
                return;
            }

            this.detachPaste(element);

            const allowed = Array.isArray(allowedContentTypes)
                ? allowedContentTypes.map(value => String(value).toLowerCase())
                : [];

            const handler = async event => {
                const items = event.clipboardData && event.clipboardData.items
                    ? Array.from(event.clipboardData.items)
                    : [];
                const imageItem = items.find(item => item.kind === "file" && String(item.type || "").startsWith("image/"));
                if (!imageItem) {
                    return;
                }

                const file = imageItem.getAsFile();
                if (!file) {
                    return;
                }

                const type = String(file.type || "image/png").toLowerCase();
                if ((allowed.length > 0 && !allowed.includes(type)) || file.size > maxFileSize) {
                    event.preventDefault();
                    await dotNet.invokeMethodAsync("OnClipboardImagePasted", type, file.name || "clipboard-image", file.size, "");
                    return;
                }

                event.preventDefault();
                const buffer = await file.arrayBuffer();
                const bytes = new Uint8Array(buffer);
                let binary = "";
                for (let i = 0; i < bytes.byteLength; i++) {
                    binary += String.fromCharCode(bytes[i]);
                }

                await dotNet.invokeMethodAsync("OnClipboardImagePasted", type, file.name || "clipboard-image", file.size, btoa(binary));
            };

            element.addEventListener("paste", handler);
            handlers.set(element, handler);
        },

        enableBeforeUnloadGuard() {
            if (this._beforeUnloadHandler) return;
            this._beforeUnloadHandler = function (e) {
                e.preventDefault();
                e.returnValue = '';
            };
            window.addEventListener('beforeunload', this._beforeUnloadHandler);
        },

        disableBeforeUnloadGuard() {
            if (!this._beforeUnloadHandler) return;
            window.removeEventListener('beforeunload', this._beforeUnloadHandler);
            this._beforeUnloadHandler = null;
        },

        getBeforeUnloadGuardState() {
            return {
                active: !!this._beforeUnloadHandler
            };
        },

        setFullscreen(fullscreen) {
            if (fullscreen) {
                document.body.classList.add('tm-document-editor--fullscreen');
                document.body.style.overflow = 'hidden';
            } else {
                document.body.classList.remove('tm-document-editor--fullscreen');
                document.body.style.overflow = '';
            }
        },

        detachPaste(element) {
            const handler = handlers.get(element);
            if (!element || !handler) {
                return;
            }

            element.removeEventListener("paste", handler);
            handlers.delete(element);
        },

        getTextSelectionAnchor(element) {
            if (!element) {
                return null;
            }

            const active = document.activeElement;
            if (!active || !element.contains(active)) {
                return null;
            }

            const tag = String(active.tagName || "").toLowerCase();
            if (tag !== "textarea" && tag !== "input") {
                return null;
            }

            const start = Number(active.selectionStart);
            const end = Number(active.selectionEnd);
            if (!Number.isFinite(start) || !Number.isFinite(end) || start === end) {
                return null;
            }

            const block = active.closest("[data-block-id]");
            const blockId = block ? block.getAttribute("data-block-id") : null;
            if (!blockId) {
                return null;
            }

            return {
                type: 1,
                blockId,
                startInlineIndex: 0,
                startOffset: Math.min(start, end),
                endInlineIndex: 0,
                endOffset: Math.max(start, end)
            };
        }
    };
}());

(function () {
    'use strict';

    const overflowControllers = new WeakMap();

    function getOverflowedCommands(groupsEl) {
        if (!groupsEl || groupsEl.scrollWidth <= groupsEl.clientWidth + 2) {
            return [];
        }

        const containerRight = groupsEl.getBoundingClientRect().right;
        const commands = [];
        groupsEl.querySelectorAll('[data-command]').forEach(function (btn) {
            if (btn.getBoundingClientRect().right > containerRight + 4) {
                commands.push(btn.getAttribute('data-command'));
            }
        });

        return commands;
    }

    window.tmDocumentEditorToolbar = {
        createOverflowController(groupsEl, dotNetRef) {
            if (!groupsEl || !dotNetRef) return;

            const existing = overflowControllers.get(groupsEl);
            if (existing) existing.disconnect();

            function measure() {
                const isOverflowing = groupsEl.scrollWidth > groupsEl.clientWidth + 2;
                const commands = isOverflowing ? getOverflowedCommands(groupsEl) : [];
                dotNetRef.invokeMethodAsync('SetOverflowingAsync', isOverflowing, commands);
            }

            const observer = new ResizeObserver(measure);
            observer.observe(groupsEl);
            overflowControllers.set(groupsEl, observer);
            measure();
        },

        disposeOverflowController(groupsEl) {
            if (!groupsEl) return;
            const observer = overflowControllers.get(groupsEl);
            if (observer) {
                observer.disconnect();
                overflowControllers.delete(groupsEl);
            }
        }
    };
}());

(function () {
    'use strict';

    const MARGIN = 8;

    function computePosition(anchorRect, elementWidth, elementHeight, placement) {
        const vw = window.innerWidth || document.documentElement.clientWidth || 1024;
        const vh = window.innerHeight || document.documentElement.clientHeight || 768;

        let left, top;

        if (placement === 'above') {
            left = anchorRect.left;
            top = anchorRect.top - elementHeight - MARGIN;
        } else if (placement === 'right') {
            left = anchorRect.right + MARGIN;
            top = anchorRect.top;
        } else if (placement === 'left') {
            left = anchorRect.left - elementWidth - MARGIN;
            top = anchorRect.top;
        } else {
            // default: 'below'
            left = anchorRect.left;
            top = anchorRect.bottom + MARGIN;
        }

        // Clamp to viewport with margin
        left = Math.max(MARGIN, Math.min(left, vw - elementWidth - MARGIN));
        top = Math.max(MARGIN, Math.min(top, vh - elementHeight - MARGIN));

        return { left, top };
    }

    function applyPosition(element, anchorRect, placement) {
        const w = element.offsetWidth || element.getBoundingClientRect().width || 0;
        const h = element.offsetHeight || element.getBoundingClientRect().height || 0;
        if (!w && !h) return; // element not yet measured — skip

        const { left, top } = computePosition(anchorRect, w, h, placement);
        element.style.position = 'fixed';
        element.style.left = left + 'px';
        element.style.top = top + 'px';
    }

    window.tmDocumentEditorFloating = {
        createPositioner(element, anchorRect, options) {
            if (!element || !anchorRect) return { dispose() {} };

            const placement = (options && options.placement) || 'below';
            const rect = {
                left: Number(anchorRect.left) || 0,
                top: Number(anchorRect.top) || 0,
                right: Number(anchorRect.right) || 0,
                bottom: Number(anchorRect.bottom) || 0
            };

            function reposition() {
                applyPosition(element, rect, placement);
            }

            reposition();

            window.addEventListener('scroll', reposition, { capture: true, passive: true });
            window.addEventListener('resize', reposition, { passive: true });

            return {
                dispose() {
                    window.removeEventListener('scroll', reposition, { capture: true });
                    window.removeEventListener('resize', reposition);
                }
            };
        },

        placeAt(element, anchorRect, options) {
            if (!element || !anchorRect) return;
            const placement = (options && options.placement) || 'below';
            applyPosition(element, {
                left: Number(anchorRect.left) || 0,
                top: Number(anchorRect.top) || 0,
                right: Number(anchorRect.right) || 0,
                bottom: Number(anchorRect.bottom) || 0
            }, placement);
        },

        __testHooks: {
            computePosition,
            applyPosition
        }
    };
}());
