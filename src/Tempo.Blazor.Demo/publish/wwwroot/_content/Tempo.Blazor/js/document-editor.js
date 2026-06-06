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
