// Phase D — objects/hit-priority.mjs
// `objectHitPriority(item)` — z-index used by hit-test ranking. If the item carries
// an explicit LayerPriority field, use it; otherwise derive from layer name + wrap
// mode via hitTestLayerPriority.

import { finiteNumber } from '../layout/caret-math.mjs';
import { hitTestLayerPriority } from './layer-priority.mjs';

export function objectHitPriority(item) {
    if (item && (item.LayerPriority !== undefined || item.layerPriority !== undefined)) {
        return finiteNumber(item.LayerPriority ?? item.layerPriority, 0);
    }
    return hitTestLayerPriority(
        item && (item.Layer || item.layer),
        item && (item.WrapMode || item.wrapMode));
}
