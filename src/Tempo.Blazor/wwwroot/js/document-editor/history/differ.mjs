// Phase D — history/differ.mjs
// `createDiffer()` — accumulates per-operation change records (text insertions,
// deletions, attribute changes, object updates, marker changes) plus invalidated
// layout/overlay scopes. Used by `applyOperation` to collect diff info while
// individual handlers mutate the model.
//
// Pure factory — no closure over engine state.

import { asArray, sortObject, unique } from '../core/helpers.mjs';

export function createDiffer() {
    return {
        insertedRanges: [],
        removedRanges: [],
        attributeChanges: [],
        objectChanges: [],
        markerChanges: [],
        invalidatedLayoutScopes: [],
        invalidatedOverlayScopes: [],

        record(entry) {
            const item = entry || {};
            if (item.insertedRange) this.insertedRanges.push(item.insertedRange);
            if (item.removedRange) this.removedRanges.push(item.removedRange);
            if (item.attributeChange) this.attributeChanges.push(item.attributeChange);
            if (item.objectChange) this.objectChanges.push(item.objectChange);
            if (item.markerChange) this.markerChanges.push(item.markerChange);
            this.invalidatedLayoutScopes = unique(this.invalidatedLayoutScopes
                .concat(asArray(item.invalidatedLayoutScopes)));
            this.invalidatedOverlayScopes = unique(this.invalidatedOverlayScopes
                .concat(asArray(item.invalidatedOverlayScopes)));
        },

        getChangedRanges() {
            return this.insertedRanges.concat(this.removedRanges);
        },

        getInvalidatedLayoutScopes() {
            return this.invalidatedLayoutScopes.slice();
        },

        getInvalidatedOverlayScopes() {
            return this.invalidatedOverlayScopes.slice();
        },

        clear() {
            this.insertedRanges = [];
            this.removedRanges = [];
            this.attributeChanges = [];
            this.objectChanges = [];
            this.markerChanges = [];
            this.invalidatedLayoutScopes = [];
            this.invalidatedOverlayScopes = [];
        },

        snapshot() {
            return sortObject({
                insertedRanges: this.insertedRanges,
                removedRanges: this.removedRanges,
                attributeChanges: this.attributeChanges,
                objectChanges: this.objectChanges,
                markerChanges: this.markerChanges,
                invalidatedLayoutScopes: this.invalidatedLayoutScopes,
                invalidatedOverlayScopes: this.invalidatedOverlayScopes,
            });
        },
    };
}
