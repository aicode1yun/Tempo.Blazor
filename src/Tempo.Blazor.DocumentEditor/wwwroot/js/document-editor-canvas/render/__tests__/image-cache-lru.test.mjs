import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveCachedImage, IMAGE_CACHE_LIMIT } from '../canvas-renderer.mjs';

// Phase N3.5 (canvas perf 2026-07-10): the module-level image cache used to grow without bound —
// every distinct image URL ever painted stayed alive for the page lifetime. It is now an LRU with
// a fixed cap: a hit refreshes recency, an insert over the cap evicts the least recently used URL.

class FakeImage {
    constructor() {
        this.src = '';
        this.decoding = '';
        this.onload = null;
    }
}

function fakeContext() {
    return {
        canvas: {
            ownerDocument: { defaultView: { Image: FakeImage } },
        },
    };
}

test('cache is capped: inserting past the limit evicts the least recently used entry', () => {
    const context = fakeContext();
    const first = resolveCachedImage(context, 'url-lru-0');
    for (let index = 1; index <= IMAGE_CACHE_LIMIT; index += 1) {
        resolveCachedImage(context, `url-lru-${index}`);
    }

    // url-lru-0 was the oldest entry when url-lru-<limit> pushed the size past the cap.
    const reloaded = resolveCachedImage(context, 'url-lru-0');
    assert.notStrictEqual(reloaded, first, 'evicted URL must be re-created on next request');
});

test('a cache hit refreshes recency so hot entries survive eviction pressure', () => {
    const context = fakeContext();
    const hot = resolveCachedImage(context, 'url-hot');
    // Fill up to the cap (url-hot + limit-1 fillers), then touch url-hot so it becomes MRU.
    for (let index = 0; index < IMAGE_CACHE_LIMIT - 1; index += 1) {
        resolveCachedImage(context, `url-fill-${index}`);
    }
    assert.strictEqual(resolveCachedImage(context, 'url-hot'), hot, 'hit must return the cached instance');

    // One more insert evicts the LRU — which is now url-fill-0, NOT the freshly touched url-hot.
    resolveCachedImage(context, 'url-overflow');
    assert.strictEqual(resolveCachedImage(context, 'url-hot'), hot, 'recently used entry must survive');
});

test('repeated requests for the same URL return the same instance (no churn)', () => {
    const context = fakeContext();
    const image = resolveCachedImage(context, 'url-stable');
    assert.strictEqual(resolveCachedImage(context, 'url-stable'), image);
    assert.strictEqual(resolveCachedImage(context, 'url-stable'), image);
});

test('environment without an Image constructor yields null and caches nothing', () => {
    const context = { canvas: { ownerDocument: { defaultView: {} } } };
    assert.equal(resolveCachedImage(context, 'url-no-image'), null);
});
