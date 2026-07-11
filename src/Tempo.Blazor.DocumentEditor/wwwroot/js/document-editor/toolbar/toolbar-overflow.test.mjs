import assert from 'node:assert/strict';
import test from 'node:test';
import {
    computeOverflowState,
    createOverflowController,
    disposeOverflowController,
} from './toolbar-overflow.mjs';

// Overflow measurement controller: JS strana kontraktu SetOverflowingAsync (TmDocumentEditorToolbar
// jej volá přes createOverflowController/disposeOverflowController, ale žádný JS controller dosud
// neexistoval → More/overflow menu se v aplikaci nikdy neukázalo). Kontrakt:
// - položka je „overflowed", když je viditelná MÉNĚ než polovinou své šířky (částečně oříznuté,
//   ale většinově viditelné položky jsou stále klikatelné → nehlásí se),
// - položky uvnitř plovoucího UI (role=dialog/menu/listbox) se neměří,
// - .NET se volá jen při skutečné změně stavu (signature dedup, žádný chatty interop),
// - scroll/resize/mutation přeměřuje přes requestAnimationFrame coalescing.

test('fully visible commands do not report overflow', () => {
    const container = buildContainer({ left: 0, right: 600 }, [
        commandElement('bold', { left: 0, right: 100 }),
        commandElement('italic', { left: 100, right: 200 }),
    ]);

    const state = computeOverflowState(container);
    assert.equal(state.isOverflowing, false);
    assert.deepEqual(state.overflowedCommandNames, []);
});

test('commands past the visible right edge are overflowed in DOM order and deduplicated', () => {
    const container = buildContainer({ left: 0, right: 300 }, [
        commandElement('bold', { left: 0, right: 100 }),
        commandElement('align', { left: 320, right: 420 }),
        commandElement('align', { left: 430, right: 530 }),
        commandElement('table', { left: 540, right: 640 }),
    ]);

    const state = computeOverflowState(container);
    assert.equal(state.isOverflowing, true);
    assert.deepEqual(state.overflowedCommandNames, ['align', 'table']);
});

test('a command visible by at least half of its width is not overflowed', () => {
    const container = buildContainer({ left: 0, right: 300 }, [
        commandElement('mostlyVisible', { left: 240, right: 340 }),
        commandElement('mostlyHidden', { left: 260, right: 360 }),
    ]);

    const state = computeOverflowState(container);
    assert.deepEqual(state.overflowedCommandNames, ['mostlyHidden']);
});

test('commands scrolled past the LEFT edge are overflowed too', () => {
    const container = buildContainer({ left: 100, right: 400 }, [
        commandElement('scrolledOut', { left: 0, right: 90 }),
        commandElement('visible', { left: 110, right: 210 }),
    ]);

    const state = computeOverflowState(container);
    assert.deepEqual(state.overflowedCommandNames, ['scrolledOut']);
});

test('commands inside floating UI (role=dialog/menu/listbox) are never measured', () => {
    const container = buildContainer({ left: 0, right: 300 }, []);
    const dialog = new FakeElement('SECTION');
    dialog.attributes.set('role', 'dialog');
    dialog.parentElement = container;
    const insideDialog = commandElement('styleGallery', { left: 900, right: 1000 });
    insideDialog.parentElement = dialog;
    container.commandElements = [insideDialog];

    const state = computeOverflowState(container);
    assert.equal(state.isOverflowing, false);
});

test('zero-width (hidden) commands are ignored', () => {
    const container = buildContainer({ left: 0, right: 300 }, [
        commandElement('hidden', { left: 500, right: 500 }),
    ]);

    assert.equal(computeOverflowState(container).isOverflowing, false);
});

test('controller reports the initial state and only re-reports on a real change', async () => {
    const harness = mountController([
        commandElement('bold', { left: 0, right: 100 }),
        commandElement('table', { left: 400, right: 500 }),
    ]);

    await flushAsync();
    assert.equal(harness.calls.length, 1, 'initial measure reports once');
    assert.deepEqual(harness.calls[0], [true, ['table']]);

    harness.container.dispatchScroll();
    await flushAsync();
    assert.equal(harness.calls.length, 1, 'unchanged state must not re-invoke .NET');

    // Scroll doprava: table se stane viditelnou, bold zmizí vlevo.
    harness.container.commandElements[0].rect = { left: -400, right: -300 };
    harness.container.commandElements[1].rect = { left: 0, right: 100 };
    harness.container.dispatchScroll();
    await flushAsync();
    assert.equal(harness.calls.length, 2);
    assert.deepEqual(harness.calls[1], [true, ['bold']]);

    disposeOverflowController(harness.container);
});

test('rapid scroll events coalesce into one measure per animation frame', async () => {
    const harness = mountController([
        commandElement('bold', { left: 400, right: 500 }),
    ]);
    await flushAsync();
    const measuresAfterInit = harness.window.frameCallbacks.scheduled;

    harness.container.dispatchScroll();
    harness.container.dispatchScroll();
    harness.container.dispatchScroll();
    assert.equal(harness.window.frameCallbacks.scheduled, measuresAfterInit + 1,
        'three synchronous scrolls must schedule a single animation frame');

    disposeOverflowController(harness.container);
});

test('dispose disconnects observers, removes the scroll listener and stops reporting', async () => {
    const harness = mountController([
        commandElement('bold', { left: 400, right: 500 }),
    ]);
    await flushAsync();

    disposeOverflowController(harness.container);
    assert.ok(harness.window.resizeObservers.every(observer => observer.disconnected));
    assert.ok(harness.window.mutationObservers.every(observer => observer.disconnected));
    assert.equal(harness.container.scrollListeners.length, 0);

    const callsAfterDispose = harness.calls.length;
    harness.container.dispatchScroll();
    await flushAsync();
    assert.equal(harness.calls.length, callsAfterDispose, 'disposed controller must stay silent');
});

test('mutation (ribbon tab switch swaps panel content) triggers a re-measure', async () => {
    const harness = mountController([
        commandElement('bold', { left: 0, right: 100 }),
    ]);
    await flushAsync();
    assert.deepEqual(harness.calls.at(-1), [false, []]);

    harness.container.commandElements = [commandElement('pageMargins', { left: 400, right: 500 })];
    harness.window.mutationObservers[0].trigger();
    await flushAsync();
    assert.deepEqual(harness.calls.at(-1), [true, ['pageMargins']]);

    disposeOverflowController(harness.container);
});

// ── fake DOM harness ─────────────────────────────────────────────────────────────────────────────

function mountController(commandElements) {
    const fakeWindow = createFakeWindow();
    const container = buildContainer({ left: 0, right: 300 }, commandElements, fakeWindow);
    const calls = [];
    const dotNetRef = {
        invokeMethodAsync(methodName, isOverflowing, names) {
            assert.equal(methodName, 'SetOverflowingAsync');
            calls.push([isOverflowing, names]);
            return Promise.resolve();
        },
    };
    createOverflowController(container, dotNetRef);
    return { container, calls, window: fakeWindow };
}

function buildContainer(rect, commandElements, fakeWindow = createFakeWindow()) {
    const container = new FakeElement('DIV');
    container.rect = { ...rect };
    container.commandElements = commandElements;
    container.ownerDocument = { defaultView: fakeWindow };
    for (const element of commandElements) {
        element.parentElement = container;
    }
    return container;
}

function commandElement(command, rect) {
    const element = new FakeElement('BUTTON');
    element.attributes.set('data-command', command);
    element.rect = { ...rect };
    return element;
}

class FakeElement {
    constructor(tagName) {
        this.tagName = tagName;
        this.attributes = new Map();
        this.parentElement = null;
        this.rect = { left: 0, right: 0 };
        this.scrollListeners = [];
        this.commandElements = [];
    }

    getAttribute(name) { return this.attributes.has(name) ? this.attributes.get(name) : null; }
    getBoundingClientRect() {
        const { left, right } = this.rect;
        return { left, right, top: 0, bottom: 32, width: right - left, height: 32 };
    }
    querySelectorAll(selector) {
        assert.equal(selector, '[data-command]');
        return this.commandElements;
    }
    addEventListener(type, listener) { if (type === 'scroll') { this.scrollListeners.push(listener); } }
    removeEventListener(type, listener) {
        if (type === 'scroll') { this.scrollListeners = this.scrollListeners.filter(item => item !== listener); }
    }
    dispatchScroll() { for (const listener of [...this.scrollListeners]) { listener(); } }
}

function createFakeWindow() {
    const frameCallbacks = { scheduled: 0, queue: [] };
    const fakeWindow = {
        frameCallbacks,
        resizeObservers: [],
        mutationObservers: [],
        requestAnimationFrame(callback) {
            frameCallbacks.scheduled += 1;
            frameCallbacks.queue.push(callback);
            queueMicrotask(() => {
                const pending = frameCallbacks.queue.splice(0);
                for (const item of pending) { item(); }
            });
            return frameCallbacks.scheduled;
        },
        ResizeObserver: class {
            constructor(callback) { this.callback = callback; this.disconnected = false; fakeWindow.resizeObservers.push(this); }
            observe() {}
            disconnect() { this.disconnected = true; }
            trigger() { this.callback([]); }
        },
        MutationObserver: class {
            constructor(callback) { this.callback = callback; this.disconnected = false; fakeWindow.mutationObservers.push(this); }
            observe() {}
            disconnect() { this.disconnected = true; }
            trigger() { this.callback([]); }
        },
    };
    return fakeWindow;
}

async function flushAsync() {
    await new Promise(resolve => setTimeout(resolve, 0));
    await new Promise(resolve => setTimeout(resolve, 0));
}
