// Phase D — objects/image-insert-payload.mjs
// `createNormalizeImageInsertPayload({read, firstDrawingRunFromSourceBlock,
//   asText, stableId, clone, sortObject})` →
//   `normalizeImageInsertPayload(op)` — canonical `{sourceBlock, image, objectId,
//   metadata}` for the InsertImage operation handler. Resolves the source block
//   (unwrapping nested `block.Block`), picks an image (`firstDrawingRun…` from a
//   source block when `op.image` is absent, else falls back to the block's
//   `Content`), invents an `objectId` (`image-object-<ts>`) when none of `ObjectId`,
//   `BlockId`, `image.ObjectId`, `image.Id` or `sourceBlock.Id` are set. Metadata
//   gets stamped with String'd `FileName`/`ContentType`/`SizeBytes`/`Provider`/
//   `ProviderId`/`UploadId` from image or op when present.

const METADATA_KEYS = [
    'FileName', 'ContentType', 'SizeBytes', 'Provider', 'ProviderId', 'UploadId',
];

export function createNormalizeImageInsertPayload(options) {
    const opts = options || {};
    for (const key of ['read', 'firstDrawingRunFromSourceBlock', 'asText', 'stableId',
        'clone', 'sortObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createNormalizeImageInsertPayload requires options.${key} (function)`);
        }
    }
    const {
        read, firstDrawingRunFromSourceBlock, asText, stableId, clone, sortObject,
    } = opts;

    return function normalizeImageInsertPayload(op) {
        let sourceBlock = read(op || {}, 'Block', 'block', null);
        if (sourceBlock && (sourceBlock.Block || sourceBlock.block)) {
            sourceBlock = sourceBlock.Block || sourceBlock.block;
        }
        let image = read(op || {}, 'Image', 'image', null);
        if (!image && sourceBlock) {
            image = firstDrawingRunFromSourceBlock(sourceBlock)
                || read(sourceBlock, 'Content', 'content', sourceBlock);
        }
        image = image || {};
        let objectId = asText(
            read(op || {}, 'ObjectId', 'objectId',
                read(op || {}, 'BlockId', 'blockId',
                    read(image, 'ObjectId', 'objectId',
                        read(image, 'Id', 'id', read(sourceBlock || {}, 'Id', 'id', ''))))));
        if (!objectId) objectId = stableId('image-object', Date.now());
        const metadata = clone(
            read(image, 'Metadata', 'metadata',
                read(op || {}, 'Metadata', 'metadata', {})) || {});
        METADATA_KEYS.forEach(function (key) {
            const lower = key.charAt(0).toLowerCase() + key.slice(1);
            const opValue = op ? (op[key] ?? op[lower]) : undefined;
            const value = image[key] ?? image[lower] ?? opValue;
            if (value !== undefined && value !== null && value !== '') {
                metadata[key] = String(value);
            }
        });
        return sortObject({
            sourceBlock: sourceBlock || null,
            image,
            objectId,
            metadata,
        });
    };
}
