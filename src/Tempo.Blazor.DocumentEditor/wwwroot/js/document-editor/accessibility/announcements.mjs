// Phase D — accessibility/announcements.mjs
// Screen-reader announcement helper. Factory pattern: the caller provides the engine
// instance handle (with `.root` DOM element, `.lastAccessibilityAnnouncement` record,
// `.accessibilityAnnouncementTimer` slot) and the boundary callback for the Blazor
// side. The factory returns a `schedule(message, politeness)` function bound to that
// instance.
//
// Extracted from the legacy IIFE so the announcement contract is documented in one
// place. The legacy `scheduleAccessibilityAnnouncement` does the same work inline.

const ANNOUNCE_DEBOUNCE_MS = 160;

// `options.invokeBoundary` — `(method, ...args) => void` that bridges to Blazor's
//                            `HandleAccessibilityAnnouncement` method. The factory
//                            calls it after a short debounce so consecutive
//                            announcements from a single command coalesce.
// Returns: `{ schedule(message, politeness?), cancel() }`.
export function createAccessibilityAnnouncer(inst, options) {
    if (!inst) throw new TypeError('createAccessibilityAnnouncer requires an instance handle');
    const invokeBoundary = (options && options.invokeBoundary) || null;
    const setTimer = (options && options.setTimeout) || setTimeout;
    const clearTimer = (options && options.clearTimeout) || clearTimeout;
    const now = (options && options.now) || (() => Date.now());

    function schedule(message, politeness) {
        if (!message) return;
        const text = String(message);

        inst.lastAccessibilityAnnouncement = {
            message: text,
            politeness: politeness || 'polite',
            at: now(),
        };

        if (inst.root) {
            if (typeof inst.root.setAttribute === 'function') {
                inst.root.setAttribute('data-accessibility-announcement', text);
            }
            if (typeof inst.root.querySelector === 'function') {
                const live = inst.root.querySelector('[data-testid="document-wysiwyg-selection-live"]');
                if (live) live.textContent = text;
            }
        }

        if (inst.accessibilityAnnouncementTimer) {
            clearTimer(inst.accessibilityAnnouncementTimer);
        }
        inst.accessibilityAnnouncementTimer = setTimer(() => {
            inst.accessibilityAnnouncementTimer = null;
            if (typeof invokeBoundary === 'function') {
                invokeBoundary('HandleAccessibilityAnnouncement', text);
            }
        }, ANNOUNCE_DEBOUNCE_MS);
    }

    function cancel() {
        if (inst.accessibilityAnnouncementTimer) {
            clearTimer(inst.accessibilityAnnouncementTimer);
            inst.accessibilityAnnouncementTimer = null;
        }
    }

    return Object.freeze({ schedule, cancel });
}

export const announcementDebounceMs = ANNOUNCE_DEBOUNCE_MS;
