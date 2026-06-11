import { normalizeWrapModeName } from '../../document-editor/objects/wrap-modes.mjs';

const COMMAND_ALIASES = new Map([
    ['insertimage', 'insertImage'],
    ['insertimageurl', 'insertImage'],
    ['insertdrawing', 'insertDrawing'],
    ['insertshape', 'insertDrawing'],
    ['insertautoshape', 'insertDrawing'],
    ['inserttextbox', 'insertDrawing'],
    ['insertline', 'insertDrawing'],
    ['insertconnector', 'insertDrawing'],
    ['insertchart', 'insertDrawing'],
    ['updateimagelayout', 'updateImageLayout'],
    ['moveimage', 'updateImageLayout'],
    ['resizeimage', 'updateImageLayout'],
    ['setimagewrapmode', 'updateImageLayout'],
    ['setimagesize', 'updateImageLayout'],
    ['setimageposition', 'updateImageLayout'],
    ['setimageobjectposition', 'updateImageLayout'],
    ['setimageanchormode', 'updateImageLayout'],
    ['setimagemetadata', 'setImageMetadata'],
    ['setimagealttext', 'setImageMetadata'],
    ['setimagecaption', 'setImageMetadata'],
    ['setimagedecorative', 'setImageMetadata'],
    ['setimageurl', 'setImageMetadata'],
    ['toggleimagecaption', 'toggleImageCaption'],
    ['setimagezorder', 'setImageZOrder'],
    ['bringimageforward', 'bringImageForward'],
    ['sendimagebackward', 'sendImageBackward'],
    ['updatechartdata', 'updateChartData'],
    ['setchartdata', 'updateChartData'],
    ['editchartdata', 'updateChartData'],
    ['activatetextboxedit', 'activateTextBoxEdit'],
    ['entertextboxedit', 'activateTextBoxEdit'],
    ['focustextbox', 'activateTextBoxEdit'],
    ['exittextboxedit', 'exitTextBoxEdit'],
    ['inserttextboxtext', 'insertTextBoxText'],
    ['typetextboxtext', 'insertTextBoxText'],
    ['inserttextboxparagraph', 'insertTextBoxParagraph'],
    ['inserttextboxlinebreak', 'insertTextBoxParagraph'],
    ['deletetextboxtextbackward', 'deleteTextBoxTextBackward'],
    ['backspacetextboxtext', 'deleteTextBoxTextBackward'],
    ['deletetextboxtextforward', 'deleteTextBoxTextForward'],
    ['settextboxtext', 'setTextBoxText'],
    ['replacetextboxtext', 'setTextBoxText'],
    ['settextboxtextalignment', 'setTextBoxTextAlignment'],
    ['setdrawingtextalignment', 'setTextBoxTextAlignment'],
    ['settextboxtextstyle', 'setTextBoxTextStyle'],
    ['setdrawingtextstyle', 'setTextBoxTextStyle'],
    ['setdrawingtextformat', 'setTextBoxTextStyle'],
    ['updateconnectorendpoint', 'updateConnectorEndpoint'],
    ['setconnectorendpoint', 'updateConnectorEndpoint'],
    ['moveconnectorendpoint', 'updateConnectorEndpoint'],
    ['bringtofront', 'bringImageForward'],
    ['sendtoback', 'sendImageBackward'],
    ['groupobjects', 'groupObjects'],
    ['groupdrawings', 'groupObjects'],
    ['ungroupobjects', 'ungroupObjects'],
    ['ungroupdrawings', 'ungroupObjects'],
    ['alignobjects', 'alignObjects'],
    ['aligndrawingobjects', 'alignObjects'],
    ['distributeobjects', 'distributeObjects'],
    ['distributedrawingobjects', 'distributeObjects'],
    ['deleteimage', 'deleteImage'],
    ['deletedrawing', 'deleteImage'],
    ['deleteobject', 'deleteImage'],
    ['removeobject', 'deleteImage'],
]);

export function isImageCommand(commandId) {
    return COMMAND_ALIASES.has(compact(commandId));
}

export function canonicalImageCommandId(commandId) {
    return COMMAND_ALIASES.get(compact(commandId)) || '';
}

export function applyImageCommand(model, selection, commandId, payload = null) {
    const command = canonicalImageCommandId(commandId);
    const working = clone(model || {});
    ensureBody(working);

    if (command === 'insertImage') {
        return finalizeImageResult(insertImage(working, selection, payload));
    }

    if (command === 'insertDrawing') {
        return finalizeImageResult(insertDrawing(working, selection, commandId, payload));
    }

    if (command === 'groupObjects') {
        return finalizeImageResult(groupObjects(working, selection, payload));
    }

    if (command === 'ungroupObjects') {
        return finalizeImageResult(ungroupObjects(working, selection, payload));
    }

    if (command === 'alignObjects') {
        return finalizeImageResult(alignObjects(working, selection, payload));
    }

    if (command === 'distributeObjects') {
        return finalizeImageResult(distributeObjects(working, selection, payload));
    }

    const target = findImageTarget(working, payload, selection);
    if (!target) {
        return unchanged(working, selection, command);
    }

    let result;
    if (command === 'activateTextBoxEdit') {
        result = activateTextBoxEdit(target, payload, selection);
    } else if (command === 'exitTextBoxEdit') {
        result = exitTextBoxEdit(target, selection);
    } else if (command === 'insertTextBoxText') {
        result = insertTextBoxText(target, payload, selection);
    } else if (command === 'insertTextBoxParagraph') {
        result = insertTextBoxText(target, { ...payload, text: '\n', Text: undefined }, selection);
    } else if (command === 'deleteTextBoxTextBackward') {
        result = deleteTextBoxText(target, payload, selection, 'backward');
    } else if (command === 'deleteTextBoxTextForward') {
        result = deleteTextBoxText(target, payload, selection, 'forward');
    } else if (command === 'setTextBoxText') {
        result = setTextBoxText(target, payload, selection);
    } else if (command === 'setTextBoxTextAlignment') {
        result = setTextBoxTextAlignment(target, payload, selection);
    } else if (command === 'setTextBoxTextStyle') {
        result = setTextBoxTextStyle(target, payload, selection);
    } else if (command === 'updateImageLayout') {
        result = updateImageLayout(target, payload);
    } else if (command === 'setImageMetadata') {
        result = setImageMetadata(target, payload);
    } else if (command === 'toggleImageCaption') {
        result = toggleImageCaption(target, payload);
    } else if (command === 'setImageZOrder') {
        result = setImageZOrder(target, payload);
    } else if (command === 'bringImageForward' || command === 'sendImageBackward') {
        result = updateImageZIndex(target, command === 'bringImageForward' ? 1 : -1);
    } else if (command === 'updateChartData') {
        result = updateChartData(target, payload);
    } else if (command === 'updateConnectorEndpoint') {
        result = updateConnectorEndpoint(target, payload);
    } else if (command === 'deleteImage') {
        result = deleteImageTarget(working, target);
    } else {
        result = { changed: false };
    }

    if (!result.changed) {
        return {
            ...unchanged(working, result.selection || selection, command),
            object: result.object || objectSummary(target),
        };
    }

    working.version = Number(working.version || 0) + 1;
    synchronizeSectionsWithBody(working);
    return {
        changed: true,
        model: working,
        selection: result.selection || objectSelection(target),
        operation: command,
        dirtyBlockIds: result.dirtyBlockIds || [target.block.id],
        removedBlockIds: result.removedBlockIds || [],
        object: result.object || objectSummary(target),
    };
}

// Rebuilds each section's block list from the body so the section flows the layout reads stay in sync with an
// image/drawing mutation. Without this an object transform (move/resize/rotate) lands in body.blocks but the
// layout — which iterates sections[].blocks — keeps painting the pre-edit geometry (the section blocks are a
// separate clone after the model copy). Mirrors the same helper used by the text/table/clipboard commands.
function synchronizeSectionsWithBody(model) {
    if (!model || !Array.isArray(model.sections)) {
        return;
    }

    const blocks = Array.isArray(model.body?.blocks) ? model.body.blocks : [];
    for (const section of model.sections) {
        const sectionId = String(section?.id || '');
        section.blocks = blocks.filter(block => String(block.sectionId || '') === sectionId);
    }
}

// Syncs section flows for the early-return image commands (insert/group/align/...) that produce their own
// result object, so their body mutations are reflected in the layout's section blocks too.
function finalizeImageResult(result) {
    if (result && result.changed === true && result.model) {
        synchronizeSectionsWithBody(result.model);
    }

    return result;
}

export function queryImageCommandState(model, selection) {
    const target = findImageTarget(model, null, selection);
    const enabled = !!target;
    return {
        image: enabled ? objectSummary(target) : null,
        commands: {
            insertimage: commandState(true),
            insertdrawing: commandState(true),
            insertshape: commandState(true),
            inserttextbox: commandState(true),
            insertline: commandState(true),
            insertconnector: commandState(true),
            insertchart: commandState(true),
            updateimagelayout: commandState(enabled),
            setimagemetadata: commandState(enabled),
            setimagewrapmode: commandState(enabled),
            setimagesize: commandState(enabled),
            setimageposition: commandState(enabled),
            setimageobjectposition: commandState(enabled),
            setimageanchormode: commandState(enabled),
            setimagealttext: commandState(enabled),
            setimagecaption: commandState(enabled),
            setimagedecorative: commandState(enabled),
            setimageurl: commandState(enabled),
            toggleimagecaption: commandState(enabled),
            setimagezorder: commandState(enabled),
            bringimageforward: commandState(enabled),
            sendimagebackward: commandState(enabled),
            updatechartdata: commandState(enabled && drawingKindName(target?.source?.kind ?? target?.source?.Kind) === 'chart'),
            setchartdata: commandState(enabled && drawingKindName(target?.source?.kind ?? target?.source?.Kind) === 'chart'),
            activatetextboxedit: commandState(enabled && isTextEditableDrawingTarget(target)),
            exittextboxedit: commandState(enabled && isTextEditableDrawingTarget(target)),
            inserttextboxtext: commandState(enabled && isTextEditableDrawingTarget(target)),
            inserttextboxparagraph: commandState(enabled && isTextEditableDrawingTarget(target)),
            deletetextboxtextbackward: commandState(enabled && isTextEditableDrawingTarget(target)),
            deletetextboxtextforward: commandState(enabled && isTextEditableDrawingTarget(target)),
            settextboxtext: commandState(enabled && isTextEditableDrawingTarget(target)),
            settextboxtextalignment: commandState(enabled && isTextEditableDrawingTarget(target)),
            settextboxtextstyle: commandState(enabled && isTextEditableDrawingTarget(target)),
            updateconnectorendpoint: commandState(enabled && isConnectorTarget(target)),
            setconnectorendpoint: commandState(enabled && isConnectorTarget(target)),
            moveconnectorendpoint: commandState(enabled && isConnectorTarget(target)),
            groupobjects: commandState(true),
            ungroupobjects: commandState(enabled && drawingKindName(target?.source?.kind ?? target?.source?.Kind) === 'group'),
            alignobjects: commandState(true),
            distributeobjects: commandState(true),
            deleteimage: commandState(enabled),
            deletedrawing: commandState(enabled),
            deleteobject: commandState(enabled),
            removeobject: commandState(enabled),
        },
    };
}

function insertDrawing(model, selection, commandId, payload = null) {
    const blocks = model.body.blocks;
    const target = targetTextBlock(model, selection, payload);
    if (!target.block) {
        return unchanged(model, selection, 'insertDrawing');
    }

    const kind = drawingKindFromCommand(commandId, payload);
    const objectId = uniqueObjectId(model, payload?.objectId ?? payload?.ObjectId ?? payload?.id ?? payload?.Id ?? `canvas-${kind}`);
    const width = Math.max(24, Number(payload?.width ?? payload?.Width ?? defaultDrawingSize(kind).width) || defaultDrawingSize(kind).width);
    const height = Math.max(24, Number(payload?.height ?? payload?.Height ?? defaultDrawingSize(kind).height) || defaultDrawingSize(kind).height);
    const run = {
        id: String(payload?.runId ?? payload?.RunId ?? `${objectId}-run`),
        type: 'drawing',
        marks: [],
        drawing: {
            objectId,
            kind: drawingKindValue(kind),
            source: 0,
            url: '',
            assetId: '',
            altText: String(payload?.altText ?? payload?.AltText ?? ''),
            isDecorative: payload?.isDecorative === true || payload?.IsDecorative === true,
            caption: String(payload?.caption ?? payload?.Caption ?? ''),
            size: { width, height, lockAspectRatio: payload?.lockAspectRatio === true || payload?.LockAspectRatio === true },
            naturalSize: { width, height, lockAspectRatio: payload?.lockAspectRatio === true || payload?.LockAspectRatio === true },
            layout: createLayoutPayload({
                anchorBlockId: target.block.id || '',
                anchorOffset: target.offset,
                width,
                height,
                wrapMode: payload?.wrapMode ?? payload?.WrapMode ?? 'Inline',
                x: payload?.x ?? payload?.X ?? 0,
                y: payload?.y ?? payload?.Y ?? 0,
                zIndex: payload?.zIndex ?? payload?.ZIndex ?? 0,
                rotation: payload?.rotation ?? payload?.Rotation ?? payload?.shape?.rotation ?? payload?.Shape?.Rotation ?? 0,
                flipHorizontal: payload?.flipHorizontal ?? payload?.FlipHorizontal ?? payload?.flipH ?? payload?.FlipH ?? payload?.flip?.horizontal ?? payload?.Flip?.Horizontal ?? false,
                flipVertical: payload?.flipVertical ?? payload?.FlipVertical ?? payload?.flipV ?? payload?.FlipV ?? payload?.flip?.vertical ?? payload?.Flip?.Vertical ?? false,
                lockAspectRatio: payload?.lockAspectRatio === true || payload?.LockAspectRatio === true,
            }),
            shape: normalizeDrawingPayloadShape(kind, payload?.shape ?? payload?.Shape),
            textBody: normalizeDrawingPayloadTextBody(kind, payload?.textBody ?? payload?.TextBody, payload),
            chart: normalizeDrawingPayloadChart(kind, payload?.chart ?? payload?.Chart),
            group: payload?.group ?? payload?.Group ?? null,
            metadata: normalizeMetadata(payload?.metadata ?? payload?.Metadata),
        },
    };
    const anchorIndex = blocks.findIndex(block => block === target.block);
    const drawingBlock = {
        id: uniqueId(model, `${objectId}-paragraph`),
        sectionId: target.block.sectionId || target.block.SectionId || '',
        type: 'paragraph',
        order: nextOrder(blocks, anchorIndex),
        paragraphProperties: {
            ...(target.block.paragraphProperties || {}),
            spacingAfter: target.block.paragraphProperties?.spacingAfter ?? 8,
        },
        content: {
            type: 'paragraph',
            runs: [run],
        },
        preserve: {},
    };
    blocks.splice(anchorIndex >= 0 ? anchorIndex + 1 : blocks.length, 0, drawingBlock);
    normalizeOrders(blocks);
    model.version = Number(model.version || 0) + 1;
    const commandTarget = { block: drawingBlock, run, source: run.drawing, role: 'drawingRun' };
    return {
        changed: true,
        model,
        selection: objectSelection(commandTarget),
        operation: 'insertDrawing',
        dirtyBlockIds: [drawingBlock.id],
        insertedBlockIds: [drawingBlock.id],
        object: objectSummary(commandTarget),
    };
}

function insertImage(model, selection, payload) {
    const url = String(payload?.url ?? payload?.Url ?? '').trim();
    const assetId = String(payload?.assetId ?? payload?.AssetId ?? '').trim();
    if (!url && !assetId) {
        return unchanged(model, selection, 'insertImage');
    }

    const blocks = model.body.blocks;
    const anchorBlockId = String(payload?.anchorBlockId ?? payload?.AnchorBlockId ?? selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    const anchorIndex = blocks.findIndex(block => String(block?.id || '') === anchorBlockId);
    const imageId = uniqueId(model, payload?.id ?? payload?.Id ?? 'canvas-image');
    const width = Math.max(24, Number(payload?.width ?? payload?.Width ?? 220) || 220);
    const height = Math.max(24, Number(payload?.height ?? payload?.Height ?? 124) || 124);
    const block = {
        id: imageId,
        type: 'image',
        order: nextOrder(blocks, anchorIndex),
        paragraphProperties: {},
        content: {
            type: 'image',
            image: {
                source: assetId ? 1 : 0,
                url,
                assetId,
                altText: String(payload?.altText ?? payload?.AltText ?? ''),
                isDecorative: payload?.isDecorative === true || payload?.IsDecorative === true,
                caption: String(payload?.caption ?? payload?.Caption ?? ''),
                size: { width, height, lockAspectRatio: payload?.lockAspectRatio !== false && payload?.LockAspectRatio !== false },
                naturalSize: { width, height, lockAspectRatio: true },
                alignment: alignmentValue(payload?.alignment ?? payload?.Alignment ?? 'Center'),
                layout: createLayoutPayload({
                    anchorBlockId,
                    width,
                    height,
                    wrapMode: payload?.wrapMode ?? payload?.WrapMode ?? 'Inline',
                    x: payload?.x ?? payload?.X ?? 0,
                    y: payload?.y ?? payload?.Y ?? 0,
                    zIndex: payload?.zIndex ?? payload?.ZIndex ?? 0,
                    rotation: payload?.rotation ?? payload?.Rotation ?? 0,
                    flipHorizontal: payload?.flipHorizontal ?? payload?.FlipHorizontal ?? payload?.flipH ?? payload?.FlipH ?? payload?.flip?.horizontal ?? payload?.Flip?.Horizontal ?? false,
                    flipVertical: payload?.flipVertical ?? payload?.FlipVertical ?? payload?.flipV ?? payload?.FlipV ?? payload?.flip?.vertical ?? payload?.Flip?.Vertical ?? false,
                    lockAspectRatio: payload?.lockAspectRatio !== false && payload?.LockAspectRatio !== false,
                }),
                linkUrl: String(payload?.linkUrl ?? payload?.LinkUrl ?? ''),
            },
        },
        preserve: {},
    };
    blocks.splice(anchorIndex >= 0 ? anchorIndex + 1 : blocks.length, 0, block);
    normalizeOrders(blocks);
    const target = { block, source: block.content.image, role: 'imageBlock' };
    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: objectSelection(target),
        operation: 'insertImage',
        dirtyBlockIds: [block.id],
        insertedBlockIds: [block.id],
        object: objectSummary(target),
    };
}

function updateImageLayout(target, payload) {
    const source = target.source;
    const current = normalizeLayout(source.layout || source.Layout || {});
    const beforeGroupRect = drawingKindName(source.kind ?? source.Kind) === 'group'
        ? targetRect(target)
        : null;
    const nextWidth = Number(payload?.width ?? payload?.Width);
    const nextHeight = Number(payload?.height ?? payload?.Height);
    const dx = Number(payload?.dx ?? payload?.Dx ?? 0) || 0;
    const dy = Number(payload?.dy ?? payload?.Dy ?? 0) || 0;
    const hasDx = payload?.dx != null || payload?.Dx != null;
    const hasDy = payload?.dy != null || payload?.Dy != null;
    const currentWidth = Number(current.transform.width || source.size?.width || 220) || 220;
    const currentHeight = Number(current.transform.height || source.size?.height || 124) || 124;
    const width = Math.max(24, Number.isFinite(nextWidth) ? nextWidth : currentWidth);
    const height = Math.max(24, Number.isFinite(nextHeight) ? nextHeight : currentHeight);
    const x = Number(payload?.x ?? payload?.X);
    const y = Number(payload?.y ?? payload?.Y);
    // x/y resolution preserving absence: an explicit move wins; a keyboard delta nudges from the current
    // offset; otherwise the current value (possibly absent) is kept so a rotation/resize does not pin an
    // alignment-positioned object to 0,0.
    const nextX = Number.isFinite(x) ? x : (hasDx ? (current.position.x ?? 0) + dx : current.position.x);
    const nextY = Number.isFinite(y) ? y : (hasDy ? (current.position.y ?? 0) + dy : current.position.y);
    const wrapMode = payload?.wrapMode ?? payload?.WrapMode ?? current.wrap.mode;
    const zIndex = payload?.zIndex ?? payload?.ZIndex ?? current.stacking.zIndex;
    const requestedRotation = Number(payload?.rotation ?? payload?.Rotation);
    const requestedRotationDelta = Number(payload?.deltaRotation ?? payload?.DeltaRotation ?? payload?.rotationDelta ?? payload?.RotationDelta ?? 0);
    const rotation = normalizeRotation(Number.isFinite(requestedRotation)
        ? requestedRotation
        : current.transform.rotation + (Number.isFinite(requestedRotationDelta) ? requestedRotationDelta : 0));
    const flipHorizontal = boolValue(
        payload?.flipHorizontal ?? payload?.FlipHorizontal ?? payload?.flipH ?? payload?.FlipH ?? payload?.flip?.horizontal ?? payload?.Flip?.Horizontal,
        current.transform.flipHorizontal);
    const flipVertical = boolValue(
        payload?.flipVertical ?? payload?.FlipVertical ?? payload?.flipV ?? payload?.FlipV ?? payload?.flip?.vertical ?? payload?.Flip?.Vertical,
        current.transform.flipVertical);
    const horizontalPosition = payload?.horizontalPosition ?? payload?.HorizontalPosition;
    const horizontalAlignment = payload?.horizontalAlignment ?? payload?.HorizontalAlignment ?? horizontalPosition;
    if (horizontalAlignment != null) {
        source.alignment = alignmentValue(horizontalAlignment);
    }

    source.layout = createLayoutPayload({
        anchorBlockId: current.anchor.blockId,
        anchorOffset: current.anchor.offset,
        moveWithText: payload?.moveWithText ?? payload?.MoveWithText ?? current.anchor.moveWithText,
        fixedOnPage: payload?.fixedOnPage ?? payload?.FixedOnPage ?? current.anchor.fixedOnPage,
        lockAnchor: payload?.lockAnchor ?? payload?.LockAnchor ?? current.anchor.lockAnchor,
        width,
        height,
        wrapMode,
        x: nextX,
        y: nextY,
        zIndex,
        rotation,
        flipHorizontal,
        flipVertical,
        horizontalRelativeTo: current.position.horizontalRelativeTo,
        verticalRelativeTo: current.position.verticalRelativeTo,
        horizontalAlignment: horizontalAlignment ?? current.position.horizontalAlignment,
        verticalAlignment: current.position.verticalAlignment,
        lockAspectRatio: payload?.lockAspectRatio ?? payload?.LockAspectRatio ?? current.transform.lockAspectRatio,
        distanceLeft: payload?.distanceLeft ?? payload?.DistanceLeft ?? current.wrap.distanceLeft,
        distanceRight: payload?.distanceRight ?? payload?.DistanceRight ?? current.wrap.distanceRight,
        distanceTop: payload?.distanceTop ?? payload?.DistanceTop ?? current.wrap.distanceTop,
        distanceBottom: payload?.distanceBottom ?? payload?.DistanceBottom ?? current.wrap.distanceBottom,
    });
    source.size = { ...(source.size || {}), width, height, lockAspectRatio: source.layout.transform.lockAspectRatio };
    if (source.shape && typeof source.shape === 'object'
        && (payload?.rotation != null || payload?.Rotation != null || payload?.deltaRotation != null || payload?.DeltaRotation != null || payload?.rotationDelta != null || payload?.RotationDelta != null)) {
        source.shape.rotation = rotation;
    }

    const dirtyBlockIds = [String(target?.block?.id || '')].filter(Boolean);
    if (beforeGroupRect) {
        dirtyBlockIds.push(...updateGroupChildTransforms(target, beforeGroupRect, targetRect(target)));
    }

    return { changed: true, dirtyBlockIds: Array.from(new Set(dirtyBlockIds)) };
}

function setImageMetadata(target, payload) {
    const source = target.source;
    let changed = false;
    if (payload?.altText != null || payload?.AltText != null) {
        source.altText = String(payload.altText ?? payload.AltText ?? '');
        changed = true;
    }

    if (payload?.caption != null || payload?.Caption != null) {
        source.caption = String(payload.caption ?? payload.Caption ?? '');
        changed = true;
    }

    if (payload?.url != null || payload?.Url != null) {
        source.url = String(payload.url ?? payload.Url ?? '').trim();
        changed = true;
    }

    if (payload?.assetId != null || payload?.AssetId != null) {
        source.assetId = String(payload.assetId ?? payload.AssetId ?? '').trim();
        source.source = source.assetId ? 1 : source.source;
        changed = true;
    }

    if (payload?.isDecorative != null || payload?.IsDecorative != null) {
        source.isDecorative = payload?.isDecorative === true || payload?.IsDecorative === true;
        changed = true;
    }

    return { changed };
}

function toggleImageCaption(target, payload) {
    const source = target.source;
    const current = String(source.caption ?? source.Caption ?? '');
    const nextCaption = current.trim().length > 0
        ? ''
        : String(payload?.caption ?? payload?.Caption ?? '').trim();
    if (current === nextCaption) {
        return { changed: false };
    }

    source.caption = nextCaption;
    return { changed: true };
}

function setImageZOrder(target, payload) {
    const direction = String(payload?.direction ?? payload?.Direction ?? '').toLowerCase();
    if (direction === 'backward' || direction === 'sendbackward' || direction === 'back') {
        return updateImageZIndex(target, -1);
    }

    if (direction === 'front' || direction === 'bringtofront') {
        return setImageZIndex(target, 999);
    }

    if (direction === 'backmost' || direction === 'sendtoback') {
        return setImageZIndex(target, -999);
    }

    return updateImageZIndex(target, 1);
}

function updateImageZIndex(target, delta) {
    const source = target.source;
    const layout = normalizeLayout(source.layout || source.Layout || {});
    return setImageZIndex(target, Number(layout.stacking.zIndex || 0) + delta);
}

function setImageZIndex(target, zIndex) {
    const source = target.source;
    const layout = normalizeLayout(source.layout || source.Layout || {});
    const previousZIndex = layout.stacking.zIndex;
    const nextZIndex = Number(zIndex || 0) || 0;
    source.layout = createLayoutPayload({
        anchorBlockId: layout.anchor.blockId,
        anchorOffset: layout.anchor.offset,
        moveWithText: layout.anchor.moveWithText,
        fixedOnPage: layout.anchor.fixedOnPage,
        lockAnchor: layout.anchor.lockAnchor,
        width: layout.transform.width,
        height: layout.transform.height,
        wrapMode: layout.wrap.mode,
        x: layout.position.x,
        y: layout.position.y,
        zIndex: nextZIndex,
        rotation: layout.transform.rotation,
        flipHorizontal: layout.transform.flipHorizontal,
        flipVertical: layout.transform.flipVertical,
        lockAspectRatio: layout.transform.lockAspectRatio,
        distanceLeft: layout.wrap.distanceLeft,
        distanceRight: layout.wrap.distanceRight,
        distanceTop: layout.wrap.distanceTop,
        distanceBottom: layout.wrap.distanceBottom,
    });

    const dirtyBlockIds = [String(target?.block?.id || '')].filter(Boolean);
    if (drawingKindName(source.kind ?? source.Kind) === 'group') {
        dirtyBlockIds.push(...updateGroupChildZIndexes(target, previousZIndex, nextZIndex));
    }

    return { changed: true, dirtyBlockIds: Array.from(new Set(dirtyBlockIds)) };
}

function updateChartData(target, payload) {
    const source = target?.source || {};
    if (drawingKindName(source.kind ?? source.Kind) !== 'chart') {
        return { changed: false };
    }

    const chartPayload = payload?.chart ?? payload?.Chart ?? payload;
    const nextChart = normalizeDrawingPayloadChart('chart', chartPayload);
    const current = JSON.stringify(source.chart || source.Chart || null);
    const next = JSON.stringify(nextChart);
    if (current === next) {
        return { changed: false };
    }

    source.chart = nextChart;
    return { changed: true };
}

function updateConnectorEndpoint(target, payload) {
    if (!isConnectorTarget(target)) {
        return { changed: false };
    }

    const source = target.source;
    const kind = drawingKindName(source.kind ?? source.Kind);
    const currentLayout = normalizeLayout(source.layout || source.Layout || {});
    const shape = normalizeDrawingPayloadShape(kind, source.shape || source.Shape || {});
    const endpointName = String(payload?.endpoint ?? payload?.Endpoint ?? 'end')
        .replace(/[\s_-]/g, '')
        .toLowerCase();
    const dragStart = endpointName === 'start';
    const pageX = finiteNumber(payload?.pageX ?? payload?.PageX ?? payload?.x ?? payload?.X);
    const pageY = finiteNumber(payload?.pageY ?? payload?.PageY ?? payload?.y ?? payload?.Y);
    if (!Number.isFinite(pageX) || !Number.isFinite(pageY)) {
        return { changed: false };
    }

    const start = dragStart
        ? { x: pageX, y: pageY }
        : pointFromPayload(payload, 'currentStart')
            || normalizedConnectorPoint(shape.points?.[0], currentLayout, payload)
            || { x: Number(payload?.bodyX ?? payload?.BodyX ?? 0) + currentLayout.position.x, y: Number(payload?.bodyY ?? payload?.BodyY ?? 0) + currentLayout.position.y + currentLayout.transform.height / 2 };
    const end = dragStart
        ? pointFromPayload(payload, 'currentEnd')
            || normalizedConnectorPoint(shape.points?.[1], currentLayout, payload)
            || { x: Number(payload?.bodyX ?? payload?.BodyX ?? 0) + currentLayout.position.x + currentLayout.transform.width, y: Number(payload?.bodyY ?? payload?.BodyY ?? 0) + currentLayout.position.y + currentLayout.transform.height / 2 }
        : { x: pageX, y: pageY };
    const bodyX = finiteNumber(payload?.bodyX ?? payload?.BodyX) ?? 0;
    const bodyY = finiteNumber(payload?.bodyY ?? payload?.BodyY) ?? 0;
    const box = connectorBox(start, end);
    const nextShape = {
        ...shape,
        points: [
            normalizePointInBox(start, box),
            normalizePointInBox(end, box),
        ],
    };
    if (dragStart) {
        nextShape.startConnection = null;
    } else {
        nextShape.endConnection = null;
    }

    source.shape = nextShape;
    source.layout = createLayoutPayload({
        anchorBlockId: currentLayout.anchor.blockId,
        anchorOffset: currentLayout.anchor.offset,
        moveWithText: currentLayout.anchor.moveWithText,
        fixedOnPage: currentLayout.anchor.fixedOnPage,
        lockAnchor: currentLayout.anchor.lockAnchor,
        width: box.width,
        height: box.height,
        wrapMode: currentLayout.wrap.mode,
        x: box.x - bodyX,
        y: box.y - bodyY,
        zIndex: currentLayout.stacking.zIndex,
        rotation: currentLayout.transform.rotation,
        flipHorizontal: currentLayout.transform.flipHorizontal,
        flipVertical: currentLayout.transform.flipVertical,
        lockAspectRatio: false,
        distanceLeft: currentLayout.wrap.distanceLeft,
        distanceRight: currentLayout.wrap.distanceRight,
        distanceTop: currentLayout.wrap.distanceTop,
        distanceBottom: currentLayout.wrap.distanceBottom,
    });
    source.size = { ...(source.size || {}), width: box.width, height: box.height, lockAspectRatio: false };
    return {
        changed: true,
        dirtyBlockIds: [String(target?.block?.id || '')].filter(Boolean),
        object: objectSummary(target),
    };
}

function deleteImageTarget(model, target) {
    const blocks = model?.body?.blocks || [];
    const blockIndex = blocks.findIndex(block => block === target.block || String(block?.id || '') === String(target.block?.id || ''));
    if (blockIndex < 0) {
        return { changed: false };
    }

    const dirtyBlockIds = [String(target.block?.id || '')].filter(Boolean);
    const removedBlockIds = [];
    if (target.role === 'drawingRun' && target.run) {
        const runs = Array.isArray(target.block?.content?.runs) ? target.block.content.runs : [];
        const runIndex = runs.findIndex(run => run === target.run || String(run?.id || '') === String(target.run?.id || ''));
        if (runIndex < 0) {
            return { changed: false };
        }

        runs.splice(runIndex, 1);
        if (runs.length === 0) {
            removedBlockIds.push(String(target.block.id || ''));
            blocks.splice(blockIndex, 1);
            normalizeOrders(blocks);
        }
    } else {
        removedBlockIds.push(String(target.block.id || ''));
        blocks.splice(blockIndex, 1);
        normalizeOrders(blocks);
    }

    return {
        changed: true,
        dirtyBlockIds,
        removedBlockIds: removedBlockIds.filter(Boolean),
        selection: nearestTextSelection(model, blockIndex),
        object: objectSummary(target),
    };
}

function groupObjects(model, selection, payload = null) {
    const objectIds = parseObjectIds(payload, selection);
    const targets = objectTargetsByIds(model, objectIds);
    if (targets.length < 2) {
        return unchanged(model, selection, 'groupObjects');
    }

    const bounds = boundsForTargets(targets);
    const groupId = uniqueObjectId(model, payload?.objectId ?? payload?.ObjectId ?? payload?.id ?? payload?.Id ?? 'canvas-group');
    const anchorTarget = targets[0];
    const blocks = model.body.blocks;
    const insertAfter = Math.max(...targets.map(target => blocks.findIndex(block => block === target.block)));
    const zIndex = Math.max(...targets.map(target => targetRect(target).zIndex)) + 1;
    const groupRun = {
        id: String(payload?.runId ?? payload?.RunId ?? `${groupId}-run`),
        type: 'drawing',
        marks: [],
        drawing: {
            objectId: groupId,
            kind: drawingKindValue('group'),
            source: 0,
            url: '',
            assetId: '',
            altText: String(payload?.altText ?? payload?.AltText ?? 'Grouped drawing objects'),
            isDecorative: payload?.isDecorative === true || payload?.IsDecorative === true,
            caption: String(payload?.caption ?? payload?.Caption ?? ''),
            size: { width: bounds.width, height: bounds.height, lockAspectRatio: false },
            naturalSize: { width: bounds.width, height: bounds.height, lockAspectRatio: false },
            layout: createLayoutPayload({
                anchorBlockId: String(payload?.anchorBlockId ?? payload?.AnchorBlockId ?? anchorTarget.block?.id ?? ''),
                anchorOffset: 0,
                width: bounds.width,
                height: bounds.height,
                wrapMode: payload?.wrapMode ?? payload?.WrapMode ?? 'InFrontOfText',
                x: bounds.x,
                y: bounds.y,
                zIndex,
                lockAspectRatio: false,
            }),
            shape: normalizeDrawingPayloadShape('group', payload?.shape ?? payload?.Shape ?? {
                preset: 'rectangle',
                fill: { type: 'none', color: '#ffffff', opacity: 0 },
                stroke: { color: '#475569', width: 1.5, dash: 'dash' },
            }),
            textBody: null,
            chart: null,
            group: {
                childObjectIds: targets.map(target => objectIdForTarget(target)),
                origin: { x: bounds.x, y: bounds.y },
                size: { x: bounds.width, y: bounds.height },
            },
            metadata: normalizeMetadata(payload?.metadata ?? payload?.Metadata),
        },
    };
    const groupBlock = {
        id: uniqueId(model, `${groupId}-paragraph`),
        sectionId: anchorTarget.block?.sectionId || anchorTarget.block?.SectionId || '',
        type: 'paragraph',
        order: nextOrder(blocks, insertAfter),
        paragraphProperties: {
            spacingAfter: 8,
        },
        content: {
            type: 'paragraph',
            runs: [groupRun],
        },
        preserve: {},
    };

    for (const target of targets) {
        const metadata = normalizeMetadata(target.source.metadata ?? target.source.Metadata);
        metadata.groupId = groupId;
        target.source.metadata = metadata;
    }

    blocks.splice(insertAfter >= 0 ? insertAfter + 1 : blocks.length, 0, groupBlock);
    normalizeOrders(blocks);
    model.version = Number(model.version || 0) + 1;
    const groupTarget = { model, block: groupBlock, run: groupRun, source: groupRun.drawing, role: 'drawingRun' };
    return {
        changed: true,
        model,
        selection: objectSelection(groupTarget),
        operation: 'groupObjects',
        dirtyBlockIds: Array.from(new Set([groupBlock.id, ...targets.map(target => String(target.block?.id || '')).filter(Boolean)])),
        insertedBlockIds: [groupBlock.id],
        object: objectSummary(groupTarget),
    };
}

function ungroupObjects(model, selection, payload = null) {
    const target = findImageTarget(model, payload, selection);
    if (!target || drawingKindName(target.source?.kind ?? target.source?.Kind) !== 'group') {
        return unchanged(model, selection, 'ungroupObjects');
    }

    const childIds = groupChildIds(target.source);
    for (const child of objectTargetsByIds(model, childIds)) {
        const metadata = normalizeMetadata(child.source.metadata ?? child.source.Metadata);
        if (metadata.groupId === objectIdForTarget(target)) {
            delete metadata.groupId;
        }

        child.source.metadata = metadata;
    }

    const removed = deleteImageTarget(model, target);
    if (!removed.changed) {
        return unchanged(model, selection, 'ungroupObjects');
    }

    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection: removed.selection || nearestTextSelection(model, 0),
        operation: 'ungroupObjects',
        dirtyBlockIds: Array.from(new Set([...(removed.dirtyBlockIds || []), ...childIds.map(id => objectTargetById(model, id)?.block?.id).filter(Boolean)])),
        removedBlockIds: removed.removedBlockIds || [],
        object: objectSummary(target),
    };
}

function alignObjects(model, selection, payload = null) {
    const objectIds = parseObjectIds(payload, selection);
    const targets = objectTargetsByIds(model, objectIds);
    if (targets.length < 2) {
        return unchanged(model, selection, 'alignObjects');
    }

    const bounds = boundsForTargets(targets);
    const mode = String(payload?.alignment ?? payload?.Alignment ?? payload?.mode ?? payload?.Mode ?? 'left')
        .replace(/[\s_-]/g, '')
        .toLowerCase();
    for (const target of targets) {
        const rect = targetRect(target);
        const next = { ...rect };
        if (mode === 'left') next.x = bounds.x;
        else if (mode === 'center' || mode === 'horizontalcenter') next.x = bounds.x + (bounds.width - rect.width) / 2;
        else if (mode === 'right') next.x = bounds.x + bounds.width - rect.width;
        else if (mode === 'top') next.y = bounds.y;
        else if (mode === 'middle' || mode === 'verticalcenter') next.y = bounds.y + (bounds.height - rect.height) / 2;
        else if (mode === 'bottom') next.y = bounds.y + bounds.height - rect.height;
        setTargetRect(target, next);
    }

    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection,
        operation: 'alignObjects',
        dirtyBlockIds: dirtyBlockIdsForTargets(targets),
    };
}

function distributeObjects(model, selection, payload = null) {
    const objectIds = parseObjectIds(payload, selection);
    const targets = objectTargetsByIds(model, objectIds);
    if (targets.length < 3) {
        return unchanged(model, selection, 'distributeObjects');
    }

    const axis = String(payload?.axis ?? payload?.Axis ?? payload?.direction ?? payload?.Direction ?? 'horizontal')
        .replace(/[\s_-]/g, '')
        .toLowerCase();
    const horizontal = axis !== 'vertical';
    const sorted = targets.slice().sort((left, right) => {
        const a = targetRect(left);
        const b = targetRect(right);
        return horizontal
            ? ((a.x + a.width / 2) - (b.x + b.width / 2))
            : ((a.y + a.height / 2) - (b.y + b.height / 2));
    });
    const first = targetRect(sorted[0]);
    const last = targetRect(sorted.at(-1));
    const startCenter = horizontal ? first.x + first.width / 2 : first.y + first.height / 2;
    const endCenter = horizontal ? last.x + last.width / 2 : last.y + last.height / 2;
    const step = (endCenter - startCenter) / (sorted.length - 1);
    for (let index = 1; index < sorted.length - 1; index += 1) {
        const rect = targetRect(sorted[index]);
        const center = startCenter + step * index;
        const next = { ...rect };
        if (horizontal) {
            next.x = center - rect.width / 2;
        } else {
            next.y = center - rect.height / 2;
        }

        setTargetRect(sorted[index], next);
    }

    model.version = Number(model.version || 0) + 1;
    return {
        changed: true,
        model,
        selection,
        operation: 'distributeObjects',
        dirtyBlockIds: dirtyBlockIdsForTargets(targets),
    };
}

function activateTextBoxEdit(target, payload = null, selection = null) {
    if (!isTextEditableDrawingTarget(target)) {
        return { changed: false };
    }

    const textBody = ensureTextBoxBody(target.source);
    const text = textBodyPlainText(textBody);
    const offset = clampTextOffset(payload?.offset ?? payload?.Offset ?? selection?.object?.textBox?.offset ?? text.length, text);
    const object = objectSummary(target);
    object.textBox = {
        active: true,
        offset,
        selectionAnchorOffset: offset,
        selectionFocusOffset: offset,
        textLength: text.length,
        text,
        alignment: paragraphAlignmentAtOffset(textBody, offset),
    };
    return {
        changed: false,
        selection: {
            anchor: { blockId: target.block?.id || '', offset: 0 },
            focus: { blockId: target.block?.id || '', offset: 0 },
            object,
        },
        object,
    };
}

function exitTextBoxEdit(target, selection = null) {
    const object = objectSummary(target);
    object.textBox = {
        active: false,
        offset: clampTextOffset(selection?.object?.textBox?.offset ?? 0, textBodyPlainText(ensureTextBoxBody(target.source))),
        selectionAnchorOffset: 0,
        selectionFocusOffset: 0,
        textLength: textBodyPlainText(ensureTextBoxBody(target.source)).length,
    };
    return {
        changed: false,
        selection: {
            anchor: { blockId: target.block?.id || '', offset: 0 },
            focus: { blockId: target.block?.id || '', offset: 0 },
            object,
        },
        object,
    };
}

function insertTextBoxText(target, payload = null, selection = null) {
    if (!isTextEditableDrawingTarget(target)) {
        return { changed: false };
    }

    const text = String(payload?.text ?? payload?.Text ?? payload?.data ?? payload?.Data ?? '');
    if (!text) {
        return { changed: false };
    }

    const textBody = ensureTextBoxBody(target.source);
    const before = textBodyPlainText(textBody);
    const range = textBoxEditRange(payload, selection, before);
    const nextText = `${before.slice(0, range.start)}${text}${before.slice(range.end)}`;
    if (nextText === before) {
        return { changed: false };
    }

    target.source.textBody = textBodyFromPlainText(nextText, textBody);
    const offset = range.start + text.length;
    return textBoxEditResult(target, offset, 'insertTextBoxText');
}

function deleteTextBoxText(target, payload = null, selection = null, direction = 'backward') {
    if (!isTextEditableDrawingTarget(target)) {
        return { changed: false };
    }

    const textBody = ensureTextBoxBody(target.source);
    const before = textBodyPlainText(textBody);
    const range = textBoxEditRange(payload, selection, before);
    let start = range.start;
    let end = range.end;
    if (start === end) {
        if (direction === 'forward') {
            end = Math.min(before.length, start + 1);
        } else {
            start = Math.max(0, start - 1);
        }
    }

    if (start === end) {
        return { changed: false };
    }

    const nextText = `${before.slice(0, start)}${before.slice(end)}`;
    target.source.textBody = textBodyFromPlainText(nextText, textBody);
    return textBoxEditResult(target, start, direction === 'forward' ? 'deleteTextBoxTextForward' : 'deleteTextBoxTextBackward');
}

function setTextBoxText(target, payload = null, selection = null) {
    if (!isTextEditableDrawingTarget(target)) {
        return { changed: false };
    }

    const textBody = ensureTextBoxBody(target.source);
    const before = textBodyPlainText(textBody);
    const nextText = String(payload?.text ?? payload?.Text ?? before);
    if (nextText === before) {
        return { changed: false };
    }

    target.source.textBody = textBodyFromPlainText(nextText, textBody);
    return textBoxEditResult(target, clampTextOffset(payload?.offset ?? payload?.Offset ?? nextText.length, nextText), 'setTextBoxText');
}

function setTextBoxTextAlignment(target, payload = null, selection = null) {
    if (!isTextEditableDrawingTarget(target)) {
        return { changed: false };
    }

    const alignment = normalizeTextAlignment(payload?.alignment ?? payload?.Alignment ?? payload?.value ?? payload?.Value);
    const textBody = ensureTextBoxBody(target.source);
    let changed = false;
    const selectedIndex = paragraphIndexAtOffset(textBody, selection?.object?.textBox?.offset ?? payload?.offset ?? payload?.Offset ?? 0);
    const applyAll = payload?.all === true || payload?.All === true || payload?.paragraphIndex == null && payload?.ParagraphIndex == null;
    textBody.paragraphs = textBody.paragraphs.map((paragraph, index) => {
        if (!applyAll && index !== selectedIndex) {
            return paragraph;
        }

        if (paragraph.alignment === alignment) {
            return paragraph;
        }

        changed = true;
        return { ...paragraph, alignment };
    });

    if (!changed) {
        return { changed: false };
    }

    target.source.textBody = textBody;
    return textBoxEditResult(target, clampTextOffset(selection?.object?.textBox?.offset ?? payload?.offset ?? 0, textBodyPlainText(textBody)), 'setTextBoxTextAlignment');
}

function setTextBoxTextStyle(target, payload = null, selection = null) {
    if (!isTextEditableDrawingTarget(target)) {
        return { changed: false };
    }

    const textBody = ensureTextBoxBody(target.source);
    const patch = textStylePatch(payload);
    if (Object.keys(patch).length === 0) {
        return { changed: false };
    }

    let changed = false;
    const selectedIndex = paragraphIndexAtOffset(textBody, selection?.object?.textBox?.offset ?? payload?.offset ?? payload?.Offset ?? 0);
    const applyAll = payload?.all === true || payload?.All === true || payload?.paragraphIndex == null && payload?.ParagraphIndex == null;
    textBody.paragraphs = textBody.paragraphs.map((paragraph, index) => {
        if (!applyAll && index !== selectedIndex) {
            return paragraph;
        }

        const style = { ...(paragraph.style || {}) };
        let paragraphChanged = false;
        for (const [key, value] of Object.entries(patch)) {
            if (style[key] !== value) {
                style[key] = value;
                paragraphChanged = true;
                changed = true;
            }
        }

        return paragraphChanged ? { ...paragraph, style } : paragraph;
    });

    if (!changed) {
        return { changed: false };
    }

    target.source.textBody = textBody;
    return textBoxEditResult(target, clampTextOffset(selection?.object?.textBox?.offset ?? payload?.offset ?? 0, textBodyPlainText(textBody)), 'setTextBoxTextStyle');
}

function textBoxEditResult(target, offset, operation) {
    const text = textBodyPlainText(ensureTextBoxBody(target.source));
    const safeOffset = clampTextOffset(offset, text);
    const object = objectSummary(target);
    object.textBox = {
        active: true,
        offset: safeOffset,
        selectionAnchorOffset: safeOffset,
        selectionFocusOffset: safeOffset,
        textLength: text.length,
        text,
        alignment: paragraphAlignmentAtOffset(ensureTextBoxBody(target.source), safeOffset),
    };
    return {
        changed: true,
        selection: {
            anchor: { blockId: target.block?.id || '', offset: 0 },
            focus: { blockId: target.block?.id || '', offset: 0 },
            object,
        },
        operation,
        dirtyBlockIds: [String(target?.block?.id || '')].filter(Boolean),
        object,
    };
}

function textBoxEditRange(payload = null, selection = null, text = '') {
    const textLength = String(text || '').length;
    const textBox = selection?.object?.textBox || selection?.object?.TextBox || {};
    const explicitStart = payload?.start ?? payload?.Start;
    const explicitEnd = payload?.end ?? payload?.End;
    if (explicitStart != null || explicitEnd != null) {
        const start = clampTextOffset(explicitStart ?? explicitEnd ?? 0, text);
        const end = clampTextOffset(explicitEnd ?? explicitStart ?? start, text);
        return { start: Math.min(start, end), end: Math.max(start, end) };
    }

    const anchor = Number.isFinite(Number(textBox.selectionAnchorOffset))
        ? clampTextOffset(textBox.selectionAnchorOffset, text)
        : clampTextOffset(textBox.offset ?? textLength, text);
    const focus = Number.isFinite(Number(textBox.selectionFocusOffset))
        ? clampTextOffset(textBox.selectionFocusOffset, text)
        : clampTextOffset(textBox.offset ?? anchor, text);
    return { start: Math.min(anchor, focus), end: Math.max(anchor, focus) };
}

function ensureTextBoxBody(source) {
    const current = normalizeDrawingPayloadTextBody('textBox', source?.textBody ?? source?.TextBody ?? {}, {});
    if (current.paragraphs.length === 0) {
        current.paragraphs.push(defaultTextBoxParagraph(''));
    }

    source.textBody = current;
    return current;
}

function textBodyPlainText(textBody) {
    return normalizeTextBodyParagraphs(textBody)
        .map(paragraph => String(paragraph.text ?? paragraph.Text ?? ''))
        .join('\n');
}

function textBodyFromPlainText(text, previousTextBody) {
    const previous = normalizeTextBodyParagraphs(previousTextBody);
    const fallback = previous[0] || defaultTextBoxParagraph('');
    const lines = String(text ?? '').split('\n');
    return {
        ...previousTextBody,
        paragraphs: lines.map((line, index) => {
            const source = previous[Math.min(index, previous.length - 1)] || fallback;
            return {
                text: line,
                alignment: normalizeTextAlignment(source.alignment ?? source.Alignment ?? fallback.alignment),
                style: normalizeTextStyle(source.style ?? source.Style ?? fallback.style),
            };
        }),
        insetLeft: Number(previousTextBody?.insetLeft ?? previousTextBody?.InsetLeft ?? 8) || 0,
        insetTop: Number(previousTextBody?.insetTop ?? previousTextBody?.InsetTop ?? 6) || 0,
        insetRight: Number(previousTextBody?.insetRight ?? previousTextBody?.InsetRight ?? 8) || 0,
        insetBottom: Number(previousTextBody?.insetBottom ?? previousTextBody?.InsetBottom ?? 6) || 0,
        verticalAlignment: String(previousTextBody?.verticalAlignment ?? previousTextBody?.VerticalAlignment ?? 'top'),
        wrapText: (previousTextBody?.wrapText ?? previousTextBody?.WrapText ?? true) !== false,
        autoFit: String(previousTextBody?.autoFit ?? previousTextBody?.AutoFit ?? 'none'),
    };
}

function normalizeTextBodyParagraphs(textBody) {
    const paragraphs = Array.isArray(textBody?.paragraphs ?? textBody?.Paragraphs)
        ? (textBody.paragraphs ?? textBody.Paragraphs)
        : [];
    return paragraphs.length > 0
        ? paragraphs.map(paragraph => ({
            text: String(paragraph?.text ?? paragraph?.Text ?? ''),
            alignment: normalizeTextAlignment(paragraph?.alignment ?? paragraph?.Alignment ?? 'left'),
            style: normalizeTextStyle(paragraph?.style ?? paragraph?.Style ?? {}),
        }))
        : [defaultTextBoxParagraph('')];
}

function defaultTextBoxParagraph(text) {
    return {
        text: String(text ?? ''),
        alignment: 'left',
        style: normalizeTextStyle({}),
    };
}

function normalizeTextStyle(style) {
    const source = style && typeof style === 'object' ? style : {};
    return {
        fontFamily: String(source.fontFamily ?? source.FontFamily ?? 'Aptos, Arial, sans-serif'),
        fontSize: Number(source.fontSize ?? source.FontSize ?? 14) || 14,
        color: String(source.color ?? source.Color ?? '#0f172a'),
        bold: source.bold === true || source.Bold === true,
        italic: source.italic === true || source.Italic === true,
    };
}

function textStylePatch(payload = null) {
    const patch = {};
    const source = payload?.style ?? payload?.Style ?? payload ?? {};
    if (source.fontFamily != null || source.FontFamily != null) {
        patch.fontFamily = String(source.fontFamily ?? source.FontFamily);
    }

    if (source.fontSize != null || source.FontSize != null) {
        patch.fontSize = Math.max(6, Math.min(96, Number(source.fontSize ?? source.FontSize) || 14));
    }

    if (source.color != null || source.Color != null) {
        patch.color = String(source.color ?? source.Color);
    }

    if (source.bold != null || source.Bold != null) {
        patch.bold = source.bold === true || source.Bold === true;
    }

    if (source.italic != null || source.Italic != null) {
        patch.italic = source.italic === true || source.Italic === true;
    }

    return patch;
}

function paragraphIndexAtOffset(textBody, offsetValue) {
    const paragraphs = normalizeTextBodyParagraphs(textBody);
    const offset = Math.max(0, Number(offsetValue || 0) || 0);
    let cursor = 0;
    for (let index = 0; index < paragraphs.length; index += 1) {
        const length = String(paragraphs[index].text || '').length;
        if (offset <= cursor + length || index === paragraphs.length - 1) {
            return index;
        }

        cursor += length + 1;
    }

    return 0;
}

function paragraphAlignmentAtOffset(textBody, offset) {
    const paragraphs = normalizeTextBodyParagraphs(textBody);
    return paragraphs[paragraphIndexAtOffset(textBody, offset)]?.alignment || 'left';
}

function normalizeTextAlignment(value) {
    const normalized = String(value || 'left').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'center' || normalized === 'middle') return 'center';
    if (normalized === 'right' || normalized === 'end') return 'right';
    if (normalized === 'justify' || normalized === 'justified') return 'justify';
    return 'left';
}

function clampTextOffset(value, text) {
    const length = String(text || '').length;
    return Math.max(0, Math.min(length, Number(value ?? length) || 0));
}

function isTextEditableDrawingTarget(target) {
    const kind = drawingKindName(target?.source?.kind ?? target?.source?.Kind);
    return kind === 'textBox' || kind === 'shape';
}

function findImageTarget(model, payload, selection) {
    const explicitObjectId = payload?.objectId ?? payload?.ObjectId;
    const explicitBlockId = payload?.blockId ?? payload?.BlockId;
    const explicitRunId = payload?.runId ?? payload?.RunId;
    const hasExplicitTarget = explicitObjectId != null || explicitBlockId != null || explicitRunId != null;
    const objectId = String(explicitObjectId ?? (hasExplicitTarget ? '' : selection?.object?.objectId ?? selection?.objectId) ?? '');
    const blockId = String(explicitBlockId ?? (hasExplicitTarget ? '' : selection?.object?.blockId ?? selection?.focus?.blockId) ?? '');
    const runId = String(explicitRunId ?? (hasExplicitTarget ? '' : selection?.object?.runId) ?? '');
    for (const block of model?.body?.blocks || []) {
        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'image') {
            const source = block.content?.image;
            const candidateObjectId = String(source?.objectId ?? source?.ObjectId ?? block.id ?? '');
            if ((!objectId && !blockId) || objectId === candidateObjectId || blockId === String(block.id || '')) {
                return { model, block, source, role: 'imageBlock' };
            }
        }

        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        for (const run of runs) {
            if (String(run?.type || '').toLowerCase() !== 'drawing' || !run.drawing) {
                continue;
            }

            const candidateObjectId = String(run.drawing.objectId ?? run.drawing.ObjectId ?? run.id ?? '');
            if ((!objectId && !runId && !blockId)
                || objectId === candidateObjectId
                || runId === String(run.id || '')
                || blockId === String(block.id || '')) {
                return { model, block, run, source: run.drawing, role: 'drawingRun' };
            }
        }
    }

    return null;
}

function createLayoutPayload(options) {
    const mode = normalizeWrapModeName(options.wrapMode);
    // Reference frame + alignment default to column/paragraph/Left/centre ONLY when the caller has nothing to
    // preserve (a fresh insert). updateImageLayout passes the object's current values so a rebuild keeps them.
    const position = {
        horizontalRelativeTo: options.horizontalRelativeTo ?? 2,
        verticalRelativeTo: options.verticalRelativeTo ?? 3,
        horizontalAlignment: alignmentValue(options.horizontalAlignment ?? 'Left'),
        verticalAlignment: options.verticalAlignment ?? 1,
    };
    // Only materialise an explicit x/y when one is actually provided — an alignment-positioned object must keep
    // NO x/y so the layout resolves it from its alignment, not from a spurious 0 offset.
    const px = numberOrNull(options.x);
    const py = numberOrNull(options.y);
    if (px != null) {
        position.x = px;
    }

    if (py != null) {
        position.y = py;
    }

    return {
        kind: mode === 'Inline' ? 0 : 1,
        anchor: {
            blockId: String(options.anchorBlockId ?? ''),
            offset: Math.max(0, Number(options.anchorOffset || 0) || 0),
            region: 0,
            moveWithText: options.moveWithText ?? mode !== 'Inline',
            fixedOnPage: options.fixedOnPage === true,
            lockAnchor: options.lockAnchor === true,
        },
        position,
        wrap: {
            mode: wrapModeValue(mode),
            side: 0,
            distanceLeft: Math.max(0, Number(options.distanceLeft ?? 12) || 0),
            distanceRight: Math.max(0, Number(options.distanceRight ?? 12) || 0),
            distanceTop: Math.max(0, Number(options.distanceTop ?? 8) || 0),
            distanceBottom: Math.max(0, Number(options.distanceBottom ?? 8) || 0),
            wrapContourPoints: [],
        },
        transform: {
            width: Math.max(24, Number(options.width || 220) || 220),
            height: Math.max(24, Number(options.height || 124) || 124),
            rotation: normalizeRotation(options.rotation ?? 0),
            lockAspectRatio: options.lockAspectRatio !== false,
            flip: {
                horizontal: boolValue(options.flipHorizontal, false),
                vertical: boolValue(options.flipVertical, false),
            },
        },
        stacking: {
            allowOverlap: false,
            zIndex: Number(options.zIndex || 0) || 0,
        },
    };
}

function normalizeLayout(layout) {
    const transform = layout.transform || layout.Transform || {};
    const position = layout.position || layout.Position || {};
    const wrap = layout.wrap || layout.Wrap || {};
    const stacking = layout.stacking || layout.Stacking || {};
    const anchor = layout.anchor || layout.Anchor || {};
    return {
        transform: {
            width: Math.max(24, Number(transform.width ?? transform.Width ?? 220) || 220),
            height: Math.max(24, Number(transform.height ?? transform.Height ?? 124) || 124),
            rotation: normalizeRotation(transform.rotation ?? transform.Rotation ?? 0),
            lockAspectRatio: (transform.lockAspectRatio ?? transform.LockAspectRatio ?? true) !== false,
            flipHorizontal: boolValue(transform.flip?.horizontal ?? transform.Flip?.Horizontal ?? transform.flipH ?? transform.FlipH, false),
            flipVertical: boolValue(transform.flip?.vertical ?? transform.Flip?.Vertical ?? transform.flipV ?? transform.FlipV, false),
        },
        position: {
            // x/y preserve ABSENCE (null) so an alignment-positioned object is not silently pinned to 0,0 by a
            // rotation/resize that does not move it. The reference frame + alignment are read so they survive a
            // layout rebuild instead of being reset to the column/paragraph/Left defaults.
            x: numberOrNull(position.x ?? position.X),
            y: numberOrNull(position.y ?? position.Y),
            horizontalRelativeTo: position.horizontalRelativeTo ?? position.HorizontalRelativeTo ?? null,
            verticalRelativeTo: position.verticalRelativeTo ?? position.VerticalRelativeTo ?? null,
            horizontalAlignment: position.horizontalAlignment ?? position.HorizontalAlignment ?? null,
            verticalAlignment: position.verticalAlignment ?? position.VerticalAlignment ?? null,
        },
        wrap: {
            mode: wrap.mode ?? wrap.Mode ?? 'Inline',
            distanceLeft: Math.max(0, Number(wrap.distanceLeft ?? wrap.DistanceLeft ?? 12) || 0),
            distanceRight: Math.max(0, Number(wrap.distanceRight ?? wrap.DistanceRight ?? 12) || 0),
            distanceTop: Math.max(0, Number(wrap.distanceTop ?? wrap.DistanceTop ?? 8) || 0),
            distanceBottom: Math.max(0, Number(wrap.distanceBottom ?? wrap.DistanceBottom ?? 8) || 0),
        },
        stacking: {
            zIndex: Number(stacking.zIndex ?? stacking.ZIndex ?? 0) || 0,
        },
        anchor: {
            blockId: String(anchor.blockId ?? anchor.BlockId ?? ''),
            offset: Math.max(0, Number(anchor.offset ?? anchor.Offset ?? 0) || 0),
            moveWithText: (anchor.moveWithText ?? anchor.MoveWithText ?? true) !== false,
            fixedOnPage: anchor.fixedOnPage === true || anchor.FixedOnPage === true,
            lockAnchor: anchor.lockAnchor === true || anchor.LockAnchor === true,
        },
    };
}

function objectSelection(target) {
    return {
        anchor: { blockId: target.block.id || '', offset: 0 },
        focus: { blockId: target.block.id || '', offset: 0 },
        object: objectSummary(target),
    };
}

function parseObjectIds(payload = null, selection = null) {
    const source = payload && typeof payload === 'object' ? payload : {};
    const raw = source.objectIds ?? source.ObjectIds ?? source.objects ?? source.Objects ?? source.ids ?? source.Ids;
    const values = Array.isArray(raw)
        ? raw
        : raw != null
            ? String(raw).split(',')
            : [source.objectId ?? source.ObjectId ?? selection?.object?.objectId ?? selection?.objectId].filter(Boolean);
    return Array.from(new Set(values
        .map(item => typeof item === 'string' ? item : (item?.objectId ?? item?.ObjectId ?? item?.id ?? item?.Id ?? ''))
        .map(item => String(item).trim())
        .filter(Boolean)));
}

function objectTargetsByIds(model, objectIds) {
    const requested = new Set((objectIds || []).map(item => String(item || '')));
    if (requested.size === 0) {
        return [];
    }

    return allObjectTargets(model)
        .filter(target => requested.has(objectIdForTarget(target)))
        .filter((target, index, targets) => targets.findIndex(candidate => objectIdForTarget(candidate) === objectIdForTarget(target)) === index);
}

function objectTargetById(model, objectId) {
    return allObjectTargets(model).find(target => objectIdForTarget(target) === String(objectId || '')) || null;
}

function allObjectTargets(model) {
    const targets = [];
    for (const block of model?.body?.blocks || []) {
        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'image' && block.content?.image) {
            targets.push({ model, block, source: block.content.image, role: 'imageBlock' });
        }

        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        for (const run of runs) {
            if (String(run?.type || '').toLowerCase() === 'drawing' && run.drawing) {
                targets.push({ model, block, run, source: run.drawing, role: 'drawingRun' });
            }
        }
    }

    return targets;
}

function objectIdForTarget(target) {
    const source = target?.source || {};
    return String(source.objectId ?? source.ObjectId ?? target?.run?.id ?? target?.block?.id ?? '');
}

function targetRect(target) {
    const source = target?.source || {};
    const layout = normalizeLayout(source.layout || source.Layout || {});
    const size = source.size || source.Size || {};
    return {
        x: layout.position.x,
        y: layout.position.y,
        width: Math.max(24, Number(layout.transform.width ?? size.width ?? size.Width ?? 220) || 220),
        height: Math.max(24, Number(layout.transform.height ?? size.height ?? size.Height ?? 124) || 124),
        zIndex: layout.stacking.zIndex,
    };
}

function isConnectorTarget(target) {
    const kind = drawingKindName(target?.source?.kind ?? target?.source?.Kind);
    return kind === 'line' || kind === 'connector';
}

function pointFromPayload(payload, prefix) {
    const x = finiteNumber(payload?.[`${prefix}X`] ?? payload?.[`${capitalize(prefix)}X`]);
    const y = finiteNumber(payload?.[`${prefix}Y`] ?? payload?.[`${capitalize(prefix)}Y`]);
    return Number.isFinite(x) && Number.isFinite(y) ? { x, y } : null;
}

function normalizedConnectorPoint(point, layout, payload) {
    if (!point || typeof point !== 'object') {
        return null;
    }

    const px = finiteNumber(point.x ?? point.X);
    const py = finiteNumber(point.y ?? point.Y);
    if (!Number.isFinite(px) || !Number.isFinite(py)) {
        return null;
    }

    const bodyX = finiteNumber(payload?.bodyX ?? payload?.BodyX) ?? 0;
    const bodyY = finiteNumber(payload?.bodyY ?? payload?.BodyY) ?? 0;
    const rect = {
        x: bodyX + layout.position.x,
        y: bodyY + layout.position.y,
        width: layout.transform.width,
        height: layout.transform.height,
    };
    return {
        x: px >= 0 && px <= 1 ? rect.x + px * Math.max(1, rect.width) : px,
        y: py >= 0 && py <= 1 ? rect.y + py * Math.max(1, rect.height) : py,
    };
}

function connectorBox(start, end) {
    const startX = finiteNumber(start?.x) ?? 0;
    const startY = finiteNumber(start?.y) ?? 0;
    const endX = finiteNumber(end?.x) ?? startX + 24;
    const endY = finiteNumber(end?.y) ?? startY;
    let x = Math.min(startX, endX);
    let y = Math.min(startY, endY);
    let width = Math.abs(endX - startX);
    let height = Math.abs(endY - startY);
    if (width < 24) {
        const delta = (24 - width) / 2;
        x -= delta;
        width = 24;
    }

    if (height < 24) {
        const delta = (24 - height) / 2;
        y -= delta;
        height = 24;
    }

    return {
        x: round(x),
        y: round(y),
        width: round(width),
        height: round(height),
    };
}

function normalizePointInBox(point, box) {
    return {
        x: round(((finiteNumber(point?.x) ?? box.x) - box.x) / Math.max(1, box.width)),
        y: round(((finiteNumber(point?.y) ?? box.y) - box.y) / Math.max(1, box.height)),
    };
}

function boundsForTargets(targets) {
    const rects = targets.map(targetRect);
    const left = Math.min(...rects.map(rect => rect.x));
    const top = Math.min(...rects.map(rect => rect.y));
    const right = Math.max(...rects.map(rect => rect.x + rect.width));
    const bottom = Math.max(...rects.map(rect => rect.y + rect.height));
    return {
        x: round(left),
        y: round(top),
        width: Math.max(24, round(right - left)),
        height: Math.max(24, round(bottom - top)),
    };
}

function setTargetRect(target, rect) {
    const source = target?.source || {};
    const layout = normalizeLayout(source.layout || source.Layout || {});
    const width = Math.max(24, Number(rect?.width || layout.transform.width || 220) || 220);
    const height = Math.max(24, Number(rect?.height || layout.transform.height || 124) || 124);
    source.layout = createLayoutPayload({
        anchorBlockId: layout.anchor.blockId,
        anchorOffset: layout.anchor.offset,
        moveWithText: layout.anchor.moveWithText,
        fixedOnPage: layout.anchor.fixedOnPage,
        lockAnchor: layout.anchor.lockAnchor,
        width,
        height,
        wrapMode: layout.wrap.mode,
        x: round(rect?.x ?? layout.position.x),
        y: round(rect?.y ?? layout.position.y),
        zIndex: rect?.zIndex ?? layout.stacking.zIndex,
        rotation: layout.transform.rotation,
        flipHorizontal: layout.transform.flipHorizontal,
        flipVertical: layout.transform.flipVertical,
        lockAspectRatio: layout.transform.lockAspectRatio,
        distanceLeft: layout.wrap.distanceLeft,
        distanceRight: layout.wrap.distanceRight,
        distanceTop: layout.wrap.distanceTop,
        distanceBottom: layout.wrap.distanceBottom,
    });
    source.size = { ...(source.size || {}), width, height, lockAspectRatio: source.layout.transform.lockAspectRatio };
}

function groupChildIds(source) {
    const group = source?.group || source?.Group || {};
    const ids = group.childObjectIds ?? group.ChildObjectIds ?? [];
    return Array.isArray(ids) ? ids.map(item => String(item || '')).filter(Boolean) : [];
}

function updateGroupChildTransforms(groupTarget, beforeRect, afterRect) {
    const model = groupTarget?.model;
    const childIds = groupChildIds(groupTarget?.source);
    if (!model || childIds.length === 0) {
        return [];
    }

    const scaleX = Math.max(0.01, Number(afterRect.width || 1) / Math.max(1, Number(beforeRect.width || 1) || 1));
    const scaleY = Math.max(0.01, Number(afterRect.height || 1) / Math.max(1, Number(beforeRect.height || 1) || 1));
    const dirtyBlockIds = [];
    for (const child of objectTargetsByIds(model, childIds)) {
        if (objectIdForTarget(child) === objectIdForTarget(groupTarget)) {
            continue;
        }

        const rect = targetRect(child);
        const nextRect = {
            ...rect,
            x: afterRect.x + (rect.x - beforeRect.x) * scaleX,
            y: afterRect.y + (rect.y - beforeRect.y) * scaleY,
            width: rect.width * scaleX,
            height: rect.height * scaleY,
        };
        setTargetRect(child, nextRect);
        if (drawingKindName(child.source?.kind ?? child.source?.Kind) === 'group') {
            dirtyBlockIds.push(...updateGroupChildTransforms(child, rect, nextRect));
        }

        if (child.block?.id) {
            dirtyBlockIds.push(String(child.block.id));
        }
    }

    const group = groupTarget.source.group || groupTarget.source.Group || {};
    group.origin = { x: afterRect.x, y: afterRect.y };
    group.size = { x: afterRect.width, y: afterRect.height };
    groupTarget.source.group = group;
    return dirtyBlockIds;
}

function updateGroupChildZIndexes(groupTarget, beforeZIndex, afterZIndex) {
    const model = groupTarget?.model;
    const childIds = groupChildIds(groupTarget?.source);
    const delta = (Number(afterZIndex || 0) || 0) - (Number(beforeZIndex || 0) || 0);
    if (!model || childIds.length === 0 || delta === 0) {
        return [];
    }

    const dirtyBlockIds = [];
    for (const child of objectTargetsByIds(model, childIds)) {
        if (objectIdForTarget(child) === objectIdForTarget(groupTarget)) {
            continue;
        }

        const rect = targetRect(child);
        const nextRect = {
            ...rect,
            zIndex: rect.zIndex + delta,
        };
        setTargetRect(child, nextRect);
        if (drawingKindName(child.source?.kind ?? child.source?.Kind) === 'group') {
            dirtyBlockIds.push(...updateGroupChildZIndexes(child, rect.zIndex, nextRect.zIndex));
        }

        if (child.block?.id) {
            dirtyBlockIds.push(String(child.block.id));
        }
    }

    return dirtyBlockIds;
}

function dirtyBlockIdsForTargets(targets) {
    return Array.from(new Set((targets || []).map(target => String(target?.block?.id || '')).filter(Boolean)));
}

function objectSummary(target) {
    const source = target?.source || {};
    const layout = normalizeLayout(source.layout || {});
    const textBody = isTextEditableDrawingTarget(target)
        ? normalizeDrawingPayloadTextBody('textBox', source.textBody ?? source.TextBody ?? {}, {})
        : null;
    const text = textBody ? textBodyPlainText(textBody) : '';
    const summary = {
        objectId: String(source.objectId ?? source.ObjectId ?? target?.run?.id ?? target?.block?.id ?? ''),
        blockId: String(target?.block?.id || ''),
        runId: String(target?.run?.id || ''),
        role: target?.role || 'imageBlock',
        width: layout.transform.width,
        height: layout.transform.height,
        rotation: layout.transform.rotation,
        flipHorizontal: layout.transform.flipHorizontal,
        flipVertical: layout.transform.flipVertical,
        x: layout.position.x,
        y: layout.position.y,
        wrapMode: normalizeWrapModeName(layout.wrap.mode),
        zIndex: layout.stacking.zIndex,
        url: String(source.url ?? source.Url ?? ''),
        assetId: String(source.assetId ?? source.AssetId ?? ''),
        altText: String(source.altText ?? source.AltText ?? ''),
        caption: String(source.caption ?? source.Caption ?? ''),
        isDecorative: source.isDecorative === true || source.IsDecorative === true,
        kind: drawingKindName(source.kind ?? source.Kind),
    };
    if (textBody) {
        summary.textBox = {
            active: false,
            offset: text.length,
            selectionAnchorOffset: text.length,
            selectionFocusOffset: text.length,
            textLength: text.length,
            text,
            alignment: paragraphAlignmentAtOffset(textBody, text.length),
        };
    }

    return summary;
}

function round(value) {
    const number = Number(value);
    return Number.isFinite(number) ? Math.round(number * 1000) / 1000 : 0;
}

function finiteNumber(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
}

function capitalize(value) {
    const text = String(value || '');
    return text ? `${text[0].toUpperCase()}${text.slice(1)}` : '';
}

function normalizeRotation(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) {
        return 0;
    }

    let normalized = number % 360;
    if (normalized > 180) {
        normalized -= 360;
    } else if (normalized <= -180) {
        normalized += 360;
    }

    return Math.round(normalized * 1000) / 1000;
}

// Coerces a value to a finite number, or null when it is absent/non-numeric — used to preserve the ABSENCE of
// an explicit x/y (an alignment-positioned object) through a layout rebuild rather than coercing it to 0.
function numberOrNull(value) {
    if (value == null) {
        return null;
    }

    const number = Number(value);
    return Number.isFinite(number) ? number : null;
}

function boolValue(value, fallback = false) {
    if (value == null) {
        return fallback === true;
    }

    if (typeof value === 'string') {
        return value.toLowerCase() === 'true';
    }

    return value === true;
}

function targetTextBlock(model, selection, payload) {
    const blocks = model?.body?.blocks || [];
    const blockId = String(payload?.anchorBlockId ?? payload?.AnchorBlockId ?? selection?.focus?.blockId ?? selection?.anchor?.blockId ?? '');
    let block = blocks.find(item => String(item?.id || '') === blockId && isTextLikeBlock(item));
    if (!block) {
        block = blocks.find(item => isTextLikeBlock(item)) || null;
    }

    if (!block) {
        block = {
            id: uniqueId(model, payload?.anchorBlockId ?? payload?.AnchorBlockId ?? 'canvas-drawing-anchor'),
            type: 'paragraph',
            order: blocks.length + 1,
            paragraphProperties: {},
            content: { type: 'paragraph', runs: [] },
            preserve: {},
        };
        blocks.push(block);
    }

    block.content = block.content && typeof block.content === 'object' ? block.content : { type: 'paragraph', runs: [] };
    block.content.type = block.content.type || block.type || 'paragraph';
    block.content.runs = Array.isArray(block.content.runs) ? block.content.runs : [];
    return {
        block,
        offset: Math.max(0, Number(payload?.offset ?? payload?.Offset ?? selection?.focus?.offset ?? selection?.anchor?.offset ?? textLength(block.content.runs)) || 0),
    };
}

function nearestTextSelection(model, preferredIndex) {
    const blocks = model?.body?.blocks || [];
    if (blocks.length === 0) {
        return {
            anchor: { blockId: '', offset: 0 },
            focus: { blockId: '', offset: 0 },
        };
    }

    const indexes = [];
    const start = Math.max(0, Math.min(blocks.length - 1, Number(preferredIndex || 0) || 0));
    for (let index = start; index < blocks.length; index += 1) {
        indexes.push(index);
    }

    for (let index = start - 1; index >= 0; index -= 1) {
        indexes.push(index);
    }

    for (const index of indexes) {
        const block = blocks[index];
        if (!isTextLikeBlock(block)) {
            continue;
        }

        const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
        const offset = Math.min(textLength(runs), Math.max(0, textLength(runs)));
        const position = { blockId: String(block.id || ''), offset };
        return { anchor: position, focus: position };
    }

    const block = blocks[Math.max(0, Math.min(blocks.length - 1, start))];
    const position = { blockId: String(block?.id || ''), offset: 0 };
    return { anchor: position, focus: position };
}

function insertInlineRun(block, insertedRun, offset) {
    const runs = block.content.runs;
    let cursor = 0;
    for (let index = 0; index < runs.length; index += 1) {
        const run = runs[index];
        const length = runInlineLength(run);
        if (offset <= cursor + length) {
            if (String(run?.type || '').toLowerCase() === 'text') {
                const text = String(run.text ?? '');
                const localOffset = Math.max(0, Math.min(text.length, offset - cursor));
                if (localOffset > 0 && localOffset < text.length) {
                    const before = { ...run, text: text.slice(0, localOffset) };
                    const after = { ...run, id: run.id ? `${run.id}-after-${insertedRun.id}` : undefined, text: text.slice(localOffset) };
                    runs.splice(index, 1, before, insertedRun, after);
                    return;
                }
            }

            runs.splice(offset <= cursor ? index : index + 1, 0, insertedRun);
            return;
        }

        cursor += length;
    }

    runs.push(insertedRun);
}

function runInlineLength(run) {
    if (String(run?.type || '').toLowerCase() === 'text') {
        return String(run.text ?? '').length;
    }

    return 1;
}

function textLength(runs) {
    return (runs || []).reduce((sum, run) => sum + runInlineLength(run), 0);
}

function isTextLikeBlock(block) {
    const type = String(block?.type || block?.content?.type || '').replace(/[\s_-]/g, '').toLowerCase();
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
}

function drawingKindFromCommand(commandId, payload) {
    const explicit = payload?.kind ?? payload?.Kind;
    if (explicit != null) {
        return drawingKindName(explicit);
    }

    const compacted = compact(commandId);
    if (compacted.includes('textbox')) return 'textBox';
    if (compacted.includes('line')) return 'line';
    if (compacted.includes('connector')) return 'connector';
    if (compacted.includes('chart')) return 'chart';
    return 'shape';
}

function drawingKindName(value) {
    if (typeof value === 'number') {
        return ['image', 'shape', 'textBox', 'line', 'connector', 'chart', 'group'][Math.max(0, Math.min(6, Math.trunc(value)))] || 'image';
    }

    const normalized = String(value || '').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'textbox') return 'textBox';
    if (normalized === 'line') return 'line';
    if (normalized === 'connector') return 'connector';
    if (normalized === 'chart') return 'chart';
    if (normalized === 'group') return 'group';
    if (normalized === 'image') return 'image';
    return 'shape';
}

function drawingKindValue(kind) {
    if (kind === 'shape') return 1;
    if (kind === 'textBox') return 2;
    if (kind === 'line') return 3;
    if (kind === 'connector') return 4;
    if (kind === 'chart') return 5;
    if (kind === 'group') return 6;
    return 0;
}

function defaultDrawingSize(kind) {
    if (kind === 'line' || kind === 'connector') return { width: 180, height: 28 };
    if (kind === 'chart') return { width: 320, height: 220 };
    if (kind === 'textBox') return { width: 220, height: 92 };
    return { width: 160, height: 96 };
}

function normalizeDrawingPayloadShape(kind, shape) {
    const source = shape && typeof shape === 'object' ? shape : {};
    const fill = source.fill || source.Fill || {};
    const stroke = source.stroke || source.Stroke || {};
    return {
        preset: String(source.preset ?? source.Preset ?? (kind === 'line' || kind === 'connector' ? 'line' : 'rectangle')),
        fill: {
            type: String(fill.type ?? fill.Type ?? (kind === 'line' || kind === 'connector' ? 'none' : 'solid')),
            color: String(fill.color ?? fill.Color ?? '#ffffff'),
            secondaryColor: fill.secondaryColor ?? fill.SecondaryColor ?? null,
            opacity: Math.max(0, Math.min(1, Number(fill.opacity ?? fill.Opacity ?? 1) || 0)),
            angle: Number(fill.angle ?? fill.Angle ?? 0) || 0,
        },
        stroke: {
            color: String(stroke.color ?? stroke.Color ?? '#2563eb'),
            width: Math.max(0, Number(stroke.width ?? stroke.Width ?? 1.5) || 0),
            dash: String(stroke.dash ?? stroke.Dash ?? 'solid'),
            opacity: Math.max(0, Math.min(1, Number(stroke.opacity ?? stroke.Opacity ?? 1) || 0)),
            lineCap: String(stroke.lineCap ?? stroke.LineCap ?? 'round'),
            lineJoin: String(stroke.lineJoin ?? stroke.LineJoin ?? 'round'),
            startArrow: stroke.startArrow ?? stroke.StartArrow ?? null,
            endArrow: stroke.endArrow ?? stroke.EndArrow ?? (kind === 'line' || kind === 'connector' ? 'triangle' : null),
        },
        shadow: source.shadow ?? source.Shadow ?? null,
        rotation: Number(source.rotation ?? source.Rotation ?? 0) || 0,
        adjustments: source.adjustments ?? source.Adjustments ?? {},
        points: Array.isArray(source.points ?? source.Points) ? (source.points ?? source.Points) : [],
        startConnection: source.startConnection ?? source.StartConnection ?? null,
        endConnection: source.endConnection ?? source.EndConnection ?? null,
        routing: source.routing ?? source.Routing ?? null,
    };
}

function normalizeDrawingPayloadTextBody(kind, textBody, payload) {
    if (kind !== 'textBox' && kind !== 'shape') {
        return null;
    }

    const source = textBody && typeof textBody === 'object' ? textBody : {};
    const paragraphs = Array.isArray(source.paragraphs ?? source.Paragraphs)
        ? (source.paragraphs ?? source.Paragraphs)
        : (payload?.text || payload?.Text ? [{ text: payload.text ?? payload.Text }] : []);
    return {
        paragraphs: paragraphs.map(item => ({
            text: String(item?.text ?? item?.Text ?? ''),
            alignment: String(item?.alignment ?? item?.Alignment ?? 'left'),
            style: {
                fontFamily: String(item?.style?.fontFamily ?? item?.Style?.FontFamily ?? 'Aptos, Arial, sans-serif'),
                fontSize: Number(item?.style?.fontSize ?? item?.Style?.FontSize ?? 14) || 14,
                color: String(item?.style?.color ?? item?.Style?.Color ?? '#0f172a'),
                bold: item?.style?.bold === true || item?.Style?.Bold === true,
                italic: item?.style?.italic === true || item?.Style?.Italic === true,
            },
        })),
        insetLeft: Number(source.insetLeft ?? source.InsetLeft ?? 8) || 0,
        insetTop: Number(source.insetTop ?? source.InsetTop ?? 6) || 0,
        insetRight: Number(source.insetRight ?? source.InsetRight ?? 8) || 0,
        insetBottom: Number(source.insetBottom ?? source.InsetBottom ?? 6) || 0,
        verticalAlignment: String(source.verticalAlignment ?? source.VerticalAlignment ?? 'top'),
        wrapText: (source.wrapText ?? source.WrapText ?? true) !== false,
        autoFit: String(source.autoFit ?? source.AutoFit ?? 'none'),
    };
}

function normalizeDrawingPayloadChart(kind, chart) {
    if (kind !== 'chart') {
        return null;
    }

    const source = chart && typeof chart === 'object' ? chart : {};
    const series = Array.isArray(source.series ?? source.Series) ? (source.series ?? source.Series) : [];
    return {
        type: String(source.type ?? source.Type ?? 'bar'),
        title: source.title ?? source.Title ?? null,
        categories: Array.isArray(source.categories ?? source.Categories) ? (source.categories ?? source.Categories).map(item => String(item)) : [],
        series: series.map(item => ({
            name: String(item?.name ?? item?.Name ?? ''),
            values: Array.isArray(item?.values ?? item?.Values) ? (item.values ?? item.Values).map(value => Number(value) || 0) : [],
            color: item?.color ?? item?.Color ?? null,
        })),
        showLegend: (source.showLegend ?? source.ShowLegend ?? true) !== false,
        palette: Array.isArray(source.palette ?? source.Palette) ? (source.palette ?? source.Palette).map(item => String(item)) : [],
    };
}

function normalizeMetadata(metadata) {
    if (!metadata || typeof metadata !== 'object' || Array.isArray(metadata)) {
        return {};
    }

    return Object.fromEntries(Object.entries(metadata).map(([key, value]) => [key, value == null ? null : String(value)]));
}

function uniqueObjectId(model, base) {
    const existing = new Set();
    for (const block of model?.body?.blocks || []) {
        if (String(block?.type || block?.content?.type || '').toLowerCase() === 'image') {
            existing.add(String(block.content?.image?.objectId ?? block.content?.image?.ObjectId ?? block.id ?? ''));
        }

        for (const run of block?.content?.runs || []) {
            if (String(run?.type || '').toLowerCase() === 'drawing') {
                existing.add(String(run.drawing?.objectId ?? run.drawing?.ObjectId ?? run.id ?? ''));
            }
        }
    }

    const candidate = String(base || 'canvas-drawing').replace(/[^a-zA-Z0-9_-]+/g, '-');
    if (!existing.has(candidate)) {
        return candidate;
    }

    let index = 2;
    while (existing.has(`${candidate}-${index}`)) {
        index += 1;
    }

    return `${candidate}-${index}`;
}

function commandState(enabled) {
    return { disabled: !enabled, active: false, mixed: false, value: null, state: enabled ? 'inactive' : 'disabled' };
}

function wrapModeValue(mode) {
    const normalized = normalizeWrapModeName(mode);
    if (normalized === 'Square') return 1;
    if (normalized === 'Tight') return 2;
    if (normalized === 'Through') return 3;
    if (normalized === 'TopBottom') return 4;
    if (normalized === 'BehindText') return 5;
    if (normalized === 'InFrontOfText') return 6;
    return 0;
}

function alignmentValue(value) {
    if (typeof value === 'number') {
        return Math.max(0, Math.min(2, Math.trunc(value)));
    }

    const normalized = String(value || '').toLowerCase();
    if (normalized === 'start' || normalized === 'left') return 0;
    if (normalized === 'end' || normalized === 'right') return 2;
    return 1;
}

function nextOrder(blocks, anchorIndex) {
    if (anchorIndex >= 0 && anchorIndex < blocks.length) {
        return Number(blocks[anchorIndex]?.order || anchorIndex) + 0.5;
    }

    return blocks.reduce((max, block, index) => Math.max(max, Number(block?.order || index) || 0), 0) + 1;
}

function normalizeOrders(blocks) {
    blocks.forEach((block, index) => {
        block.order = index + 1;
    });
}

function uniqueId(model, base) {
    const existing = new Set((model?.body?.blocks || []).map(block => String(block?.id || '')));
    const candidate = String(base || 'canvas-image').replace(/[^a-zA-Z0-9_-]+/g, '-');
    if (!existing.has(candidate)) {
        return candidate;
    }

    let index = 2;
    while (existing.has(`${candidate}-${index}`)) {
        index += 1;
    }

    return `${candidate}-${index}`;
}

function ensureBody(model) {
    model.body = model.body && typeof model.body === 'object' ? model.body : { blocks: [] };
    model.body.blocks = Array.isArray(model.body.blocks) ? model.body.blocks : [];
}

function unchanged(model, selection, operation) {
    return { changed: false, model, selection, operation, dirtyBlockIds: [] };
}

function compact(commandId) {
    return String(commandId == null ? '' : commandId).replace(/[\s_-]/g, '').toLowerCase();
}

function clone(value) {
    if (typeof structuredClone === 'function') {
        return structuredClone(value);
    }

    return JSON.parse(JSON.stringify(value ?? null));
}
