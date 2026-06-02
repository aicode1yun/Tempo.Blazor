// Phase D — objects/insert-image-payload.mjs
// Pure helpers for the InsertImage command pipeline.
//
// `createFirstDrawingRunFromSourceBlock({read, asArray, isDrawingRunSource})` →
//   `firstDrawingRunFromSourceBlock(sourceBlock)` — scans the inlines/runs of a
//   block-shaped payload and returns the first drawing-source run, or null.
// `createImagePayloadFromInsertImageCommand({compactCommandName, read,
//   firstDrawingRunFromSourceBlock, sortObject})` →
//   `imagePayloadFromInsertImageCommand(commandName, body)` — extracts
//   `{sourceBlock, image}` from the command body. When called with the
//   `insertimageurl` command and no explicit image, builds one from the body's
//   `Source/Url/AssetId/AltText/Caption/Size/NaturalSize/Layout/Metadata` fields.

export function createFirstDrawingRunFromSourceBlock(options) {
    const opts = options || {};
    for (const key of ['read', 'asArray', 'isDrawingRunSource']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createFirstDrawingRunFromSourceBlock requires options.${key} (function)`);
        }
    }
    const { read, asArray, isDrawingRunSource } = opts;
    return function firstDrawingRunFromSourceBlock(sourceBlock) {
        const content = read(sourceBlock || {}, 'Content', 'content', null);
        const inlines = asArray(read(content || {}, 'Inlines', 'inlines',
            read(content || {}, 'Runs', 'runs', [])));
        for (let i = 0; i < inlines.length; i++) {
            if (isDrawingRunSource(inlines[i])) return inlines[i];
        }
        return null;
    };
}

export function createImagePayloadFromInsertImageCommand(options) {
    const opts = options || {};
    for (const key of ['compactCommandName', 'read', 'firstDrawingRunFromSourceBlock', 'sortObject']) {
        if (typeof opts[key] !== 'function') {
            throw new TypeError(
                `createImagePayloadFromInsertImageCommand requires options.${key} (function)`);
        }
    }
    const {
        compactCommandName, read, firstDrawingRunFromSourceBlock, sortObject,
    } = opts;
    return function imagePayloadFromInsertImageCommand(commandName, body) {
        const compact = compactCommandName(commandName);
        let sourceBlock = read(body || {}, 'Block', 'block', null);
        if (sourceBlock && (sourceBlock.Block || sourceBlock.block)) {
            sourceBlock = sourceBlock.Block || sourceBlock.block;
        }
        let image = read(body || {}, 'Image', 'image', null);
        if (!image && sourceBlock) {
            image = firstDrawingRunFromSourceBlock(sourceBlock)
                || read(sourceBlock, 'Content', 'content', sourceBlock);
        }
        if (!image && compact === 'insertimageurl') {
            image = {
                Source: read(body || {}, 'Source', 'source', 0),
                Url: read(body || {}, 'Url', 'url', null),
                AssetId: read(body || {}, 'AssetId', 'assetId', null),
                AltText: read(body || {}, 'AltText', 'altText', ''),
                Caption: read(body || {}, 'Caption', 'caption', ''),
                Size: read(body || {}, 'Size', 'size', {}),
                NaturalSize: read(body || {}, 'NaturalSize', 'naturalSize', {}),
                Layout: read(body || {}, 'Layout', 'layout', {}),
                Metadata: read(body || {}, 'Metadata', 'metadata', {}),
            };
        }
        return sortObject({
            sourceBlock: sourceBlock || null,
            image: image || null,
        });
    };
}
