import { createParagraphLayoutEngineFactory } from '../../document-editor/layout/paragraph-engine.mjs';
import { createFontMetricsService } from '../../document-editor/layout/font-metrics.mjs';
import { DEFAULT_PAGE_SETUP, normalizePageSettings } from './page-geometry.mjs';
import {
    createCanvasRunStyle,
    createCanvasRunDisplayText,
    createCanvasRunText,
    normalizeCanvasAlignment,
    orderedCanvasBlocks,
    paragraphIndent,
    pointsToCssPixels,
} from './canvas-text-style.mjs';
import { layoutCanvasTable } from '../tables/table-layout.mjs';
import {
    footprintHeight,
    layoutCanvasImageObject,
    normalizeCanvasImageObject,
    objectExclusionIntervals,
} from '../objects/image-render.mjs';
import { balanceParagraphColumns, createColumnBreakLayout, createColumnFlow, columnFrame, nextColumnFrame } from './column-flow.mjs';
import { createLineNumberingState, lineNumbersForFragment } from './line-numbering.mjs';
import { buildSectionFlows, nextSectionIdForBreak, normalizeBreakType, pageSettingsEqual } from './sections.mjs';
import { resolveNumberingState } from '../lists/numbering-engine.mjs';
import { normalizeHyphenationOptions } from './hyphenation.mjs';
import { applyTabStopsToParagraphLayout } from './tab-stops.mjs';
import { layoutMathRun } from '../math/math-layout.mjs';

const DEFAULT_BLOCK_GAP = 8;
const DEFAULT_LIST_LABEL_GAP = 12;
const DEFAULT_LIST_INDENT_STEP = 24;

export function layoutCanvasDocument(model, options = {}) {
    const sourceModel = model || {};
    const pageSettings = normalizePageSettings(sourceModel.pageSettings || options.pageSettings || DEFAULT_PAGE_SETUP);
    const metrics = ensureMeasurementService(options.fontMetrics || createFontMetricsService(options.fontMetricsOptions));
    const layoutEngine = createCanvasLayoutEngine(sourceModel, metrics, options);
    const flows = buildSectionFlows(sourceModel, pageSettings);
    const orderedBlocks = flows.orderedBlocks();
    const numberingState = resolveNumberingState(sourceModel, orderedBlocks.map(entry => entry.block));
    let currentSection = flows.first;
    const pages = [createCanvasPageLayout(0, currentSection?.pageSettings || pageSettings, currentSection)];
    const blockLayouts = [];
    const objectLayouts = [];
    const textRects = [];
    const listLabels = [];
    const lineNumbers = [];
    const lineNumberingState = createLineNumberingState();
    let currentPageIndex = 0;
    let currentColumnIndex = 0;
    let cursorY = columnFrame(pages[0], currentColumnIndex).y;
    let sequence = 0;

    // Incremental layout cache (Phase 3): memoizes the expensive per-paragraph layout keyed by
    // block id. A reuse is valid only when both the block content AND its incoming flow state
    // (cursorY, page/column, section, frame geometry, active floats, sequence) are unchanged — so an
    // edit recomputes from the first changed block onward (its end cursorY shifts, invalidating the
    // next block's state signature), exactly like OnlyOffice's StartIndex recalc.
    const layoutCache = options.layoutCache instanceof Map ? options.layoutCache : null;
    const cacheStats = { hits: 0, misses: 0, reusedBlockIds: [] };
    const seenBlockIds = new Set();

    const ensurePage = (index, section = currentSection) => {
        while (pages.length <= index) {
            pages.push(createCanvasPageLayout(pages.length, section?.pageSettings || pageSettings, section));
        }

        return pages[index];
    };

    const moveToNextPage = (section = currentSection) => {
        currentPageIndex += 1;
        currentSection = section || currentSection;
        currentColumnIndex = 0;
        const expectedSectionId = currentSection?.id || '';
        if (pages[currentPageIndex] && pages[currentPageIndex].sectionId !== expectedSectionId) {
            pages[currentPageIndex] = createCanvasPageLayout(currentPageIndex, currentSection?.pageSettings || pageSettings, currentSection);
        }

        cursorY = columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).y;
    };

    const moveToNextColumnOrPage = (section = currentSection) => {
        const page = ensurePage(currentPageIndex, section);
        const nextColumn = nextColumnFrame(page, currentColumnIndex);
        currentSection = section || currentSection;
        if (nextColumn) {
            currentColumnIndex = nextColumn.columnIndex;
            cursorY = nextColumn.frame.y;
            return;
        }

        moveToNextPage(section);
    };

    for (const entry of orderedBlocks) {
        const block = entry.block;
        const entrySection = entry.section || flows.sectionForBlock(block, currentSection);
        if (entrySection && currentSection && entrySection.id !== currentSection.id) {
            moveAfterSectionBreak('nextPage', entrySection);
        } else {
            currentSection = entrySection || currentSection;
        }

        const type = canvasBlockType(block);
        if (type === 'pagebreak') {
            const breakType = normalizeBreakType(block);
            const nextSection = flows.nextSection(currentSection, nextSectionIdForBreak(block));
            if (breakType === 'column') {
                const page = ensurePage(currentPageIndex, currentSection);
                blockLayouts.push(createColumnBreakLayout(block, page, currentColumnIndex, sequence++));
                moveToNextColumnOrPage(currentSection);
                continue;
            }

            blockLayouts.push({
                id: `${block.id || `block-${sequence}`}-page-break`,
                blockId: block.id || '',
                type: 'pageBreak',
                pageIndex: currentPageIndex,
                columnIndex: currentColumnIndex,
                sectionId: currentSection?.id || '',
                rect: {
                    x: columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).x,
                    y: cursorY,
                    width: columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).width,
                    height: 0,
                },
                lines: [],
                segments: [],
                sequence: sequence++,
            });
            if (breakType === 'continuous' && pageSettingsEqual(currentSection?.pageSettings, nextSection?.pageSettings)) {
                currentSection = nextSection;
                continue;
            }

            moveAfterSectionBreak(breakType, nextSection);
            continue;
        }

        const spacingBefore = paragraphSpacing(block, 'spacingBefore');
        const spacingAfter = paragraphSpacing(block, 'spacingAfter', sourceModel?.theme?.paragraphSpacingAfter ?? sourceModel?.theme?.ParagraphSpacingAfter ?? DEFAULT_BLOCK_GAP);
        cursorY += spacingBefore;

        if (cursorY > columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).bottom) {
            moveToNextColumnOrPage(currentSection);
        }

        if (type === 'table') {
            const tableSequence = sequence++;
            const tableLayout = layoutCanvasTable({
                model: sourceModel,
                block,
                page: ensurePage(currentPageIndex, currentSection),
                y: cursorY,
                metrics,
                layoutEngine,
                normalizeTextBlock,
                ensurePage: index => ensurePage(index, currentSection),
                sequence: tableSequence,
            });
            if (cursorY > columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).y && tableLayout.rect.y + tableLayout.rect.height > columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).bottom) {
                moveToNextColumnOrPage(currentSection);
                Object.assign(tableLayout, layoutCanvasTable({
                    model: sourceModel,
                    block,
                    page: ensurePage(currentPageIndex, currentSection),
                    y: cursorY,
                    metrics,
                    layoutEngine,
                    normalizeTextBlock,
                    ensurePage: index => ensurePage(index, currentSection),
                    sequence: tableSequence,
                }));
            }

            blockLayouts.push(tableLayout);
            blockLayouts.push(...(tableLayout.nestedBlocks || []));
            textRects.push(...tableTextRects(tableLayout.nestedBlocks || []));
            currentPageIndex = Number(tableLayout.lastPageIndex ?? tableLayout.pageIndex ?? currentPageIndex) || currentPageIndex;
            cursorY = (Number(tableLayout.endY ?? (tableLayout.rect.y + tableLayout.rect.height)) || (tableLayout.rect.y + tableLayout.rect.height)) + spacingAfter;
            continue;
        }

        if (type === 'image') {
            const imageObject = normalizeCanvasImageObject({
                model: sourceModel,
                block,
                blockIndex: sequence,
                body: columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex),
                objectRole: 'imageBlock',
            });
            const imageLayout = layoutCanvasImageObject(imageObject, {
                fontMetrics: metrics,
                page: ensurePage(currentPageIndex, currentSection),
                // Always pass the current flow position; layoutCanvasImageObject applies the
                // object's vertical offset relative to its reference frame (paragraph/page/margin).
                // Passing `explicitY ?? cursorY` collapsed to 0 whenever the offset was 0
                // (`0 ?? cursorY === 0`), pinning every paragraph-anchored float to the page top.
                y: cursorY,
                sequence: sequence++,
            });
            if (cursorY > columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).y && imageLayout.rect.y + imageLayout.rect.height > columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex).bottom) {
                moveToNextColumnOrPage(currentSection);
                Object.assign(imageLayout, layoutCanvasImageObject(imageObject, {
                fontMetrics: metrics,
                    page: ensurePage(currentPageIndex, currentSection),
                    y: cursorY,
                    sequence: imageLayout.sequence,
                }));
            }

            blockLayouts.push(imageLayout);
            objectLayouts.push(imageLayout);
            currentPageIndex = imageLayout.pageIndex;
            if (!imageObject.isFloating || imageObject.wrapMode === 'TopBottom') {
                cursorY = imageLayout.rect.y + footprintHeight(imageLayout) + spacingAfter;
            }
            continue;
        }

        const drawingRun = standaloneDrawingRun(block);
        if (drawingRun) {
            const imageObject = normalizeCanvasImageObject({
                model: sourceModel,
                block,
                blockIndex: sequence,
                run: drawingRun.run,
                runIndex: drawingRun.runIndex,
                body: columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex),
                objectRole: 'drawingRun',
            });
            const imageLayout = layoutCanvasImageObject(imageObject, {
                fontMetrics: metrics,
                page: ensurePage(currentPageIndex, currentSection),
                // Always pass the current flow position; layoutCanvasImageObject applies the
                // object's vertical offset relative to its reference frame (paragraph/page/margin).
                // Passing `explicitY ?? cursorY` collapsed to 0 whenever the offset was 0
                // (`0 ?? cursorY === 0`), pinning every paragraph-anchored float to the page top.
                y: cursorY,
                sequence: sequence++,
            });

            blockLayouts.push(imageLayout);
            objectLayouts.push(imageLayout);
            currentPageIndex = imageLayout.pageIndex;
            if (!imageObject.isFloating || imageObject.wrapMode === 'TopBottom') {
                cursorY = imageLayout.rect.y + footprintHeight(imageLayout) + spacingAfter;
            }
            continue;
        }

        const paragraphSequence = sequence++;
        const blockKey = String(block?.id || '');
        const cacheable = layoutCache !== null && blockKey !== '' && isCacheableTextBlock(block);
        let paragraphLayout = null;
        let contentSig = 0;
        let stateSig = 0;
        if (cacheable) {
            seenBlockIds.add(blockKey);
            const frame = columnFrame(ensurePage(currentPageIndex, currentSection), currentColumnIndex);
            contentSig = textBlockContentSignature(block);
            stateSig = textBlockStateSignature({
                cursorY: Math.round(cursorY * 100),
                currentPageIndex,
                currentColumnIndex,
                sectionId: currentSection?.id || '',
                frame,
                sequence: paragraphSequence,
                spacingAfter,
                objects: objectLayoutsSignature(objectLayouts),
            });
            const cached = layoutCache.get(blockKey);
            if (cached && cached.contentSig === contentSig && cached.stateSig === stateSig) {
                paragraphLayout = cached.result;
                cacheStats.hits += 1;
                cacheStats.reusedBlockIds.push(blockKey);
                for (const fragment of paragraphLayout.fragments) {
                    ensurePage(fragment.pageIndex, currentSection);
                }
            }
        }

        if (paragraphLayout === null) {
            const normalizedBlock = normalizeTextBlock(sourceModel, block, metrics);
            paragraphLayout = layoutTextBlockAcrossPages({
                sourceModel,
                sourceBlock: block,
                normalizedBlock,
                layoutEngine,
                metrics,
                pages,
                ensurePage,
                currentPageIndex,
                currentColumnIndex,
                cursorY,
                sequence: paragraphSequence,
                objectLayouts,
                section: currentSection,
                numberingState,
            });
            if (cacheable) {
                cacheStats.misses += 1;
                layoutCache.set(blockKey, { contentSig, stateSig, result: paragraphLayout });
            }
        }

        blockLayouts.push(...paragraphLayout.fragments);
        textRects.push(...paragraphLayout.textRects);
        listLabels.push(...paragraphLayout.listLabels);
        for (const fragment of paragraphLayout.fragments) {
            lineNumbers.push(...lineNumbersForFragment(fragment, currentSection, ensurePage(fragment.pageIndex, currentSection), lineNumberingState, sourceModel, metrics));
        }
        currentPageIndex = paragraphLayout.currentPageIndex;
        currentColumnIndex = paragraphLayout.currentColumnIndex;
        cursorY = paragraphLayout.cursorY + spacingAfter;
    }

    function moveAfterSectionBreak(breakType, section) {
        currentSection = section || currentSection;
        if (breakType === 'evenPage' || breakType === 'oddPage') {
            moveToNextPage(currentSection);
            const shouldBeEven = breakType === 'evenPage';
            while (((currentPageIndex + 1) % 2 === 0) !== shouldBeEven) {
                moveToNextPage(currentSection);
            }
            return;
        }

        moveToNextPage(currentSection);
    }

    // Drop cache entries for blocks that no longer exist so the cache stays bounded by the document.
    if (layoutCache !== null) {
        for (const key of layoutCache.keys()) {
            if (!seenBlockIds.has(key)) {
                layoutCache.delete(key);
            }
        }
    }

    return {
        ok: true,
        schemaVersion: 1,
        pageSettings,
        pages,
        blocks: blockLayouts,
        objectLayouts,
        textRects,
        listLabels,
        lineNumbers,
        measurementStats: typeof metrics.getStats === 'function' ? metrics.getStats() : null,
        cacheStats,
    };
}

// A text block is safe to memoize when its layout depends only on its own content and incoming flow
// state. Lists are excluded because their labels depend on the document-wide numbering state, and
// non-text blocks (tables/images/page breaks) are cheap or have side effects on the object flow.
function isCacheableTextBlock(block) {
    const type = canvasBlockType(block);
    return type === 'paragraph' || type === 'heading' || type === 'quote';
}

function hashLayoutString(input) {
    const str = String(input);
    let hash = 5381;
    for (let i = 0; i < str.length; i += 1) {
        hash = (((hash << 5) + hash) + str.charCodeAt(i)) | 0;
    }

    return hash;
}

function textBlockContentSignature(block) {
    return hashLayoutString(JSON.stringify({
        t: canvasBlockType(block),
        s: block?.styleId ?? block?.StyleId ?? null,
        p: block?.paragraphProperties ?? block?.ParagraphProperties ?? null,
        c: block?.content ?? block?.Content ?? null,
    }));
}

function textBlockStateSignature(parts) {
    const frame = parts.frame || {};
    return hashLayoutString(JSON.stringify({
        y: parts.cursorY,
        p: parts.currentPageIndex,
        c: parts.currentColumnIndex,
        s: parts.sectionId,
        fx: Math.round(Number(frame.x || 0)),
        fw: Math.round(Number(frame.width || 0)),
        fy: Math.round(Number(frame.y || 0)),
        fb: Math.round(Number(frame.bottom || 0)),
        seq: parts.sequence,
        sa: parts.spacingAfter,
        o: parts.objects,
    }));
}

function objectLayoutsSignature(objectLayouts) {
    if (!Array.isArray(objectLayouts) || objectLayouts.length === 0) {
        return '';
    }

    // Only floating, text-affecting objects change paragraph layout; include their geometry so a
    // moved/resized float invalidates the paragraphs that wrap around it.
    const parts = [];
    for (const layout of objectLayouts) {
        const object = layout?.object || layout || {};
        const isFloating = object.isFloating ?? layout?.isFloating;
        if (!isFloating) {
            continue;
        }

        const rect = layout?.rect || {};
        parts.push([
            String(layout?.objectId || layout?.blockId || ''),
            String(object.wrapMode || ''),
            Math.round(Number(rect.x || 0)),
            Math.round(Number(rect.y || 0)),
            Math.round(Number(rect.width || 0)),
            Math.round(Number(rect.height || 0)),
        ].join(':'));
    }

    return parts.join('|');
}

export function createCanvasLayoutEngine(model, metrics, options = {}) {
    const measurementService = ensureMeasurementService(metrics || createFontMetricsService(options.fontMetricsOptions));
    const blocksById = new Map(orderedCanvasBlocks(model).map(block => [String(block.id || ''), normalizeTextBlock(model, block, measurementService)]));
    const factory = createParagraphLayoutEngineFactory({
        findBlock(_, blockId) {
            return blocksById.get(String(blockId || '')) || null;
        },
    });
    const layoutOptions = {
        ...(options.layoutOptions || {}),
        hyphenation: normalizeHyphenationOptions(options.layoutOptions?.hyphenation || model?.hyphenation || model?.Hyphenation, orderedBlocksText(model)),
    };
    return factory(measurementService, layoutOptions);
}

function orderedBlocksText(model) {
    return orderedCanvasBlocks(model)
        .flatMap(block => Array.isArray(block?.content?.runs) ? block.content.runs : [])
        .map(run => String(run?.text ?? run?.Text ?? ''))
        .join('\n');
}

export function normalizeTextBlock(model, block, metrics = null) {
    const type = canvasBlockType(block);
    const content = block?.content || {};
    const paragraphProperties = block?.paragraphProperties || {};
    const runs = Array.isArray(content.runs) ? content.runs : [];
    return {
        id: block?.id || '',
        type: type === 'pagebreak' ? 'pageBreak' : 'paragraph',
        sourceType: type,
        style: {},
        paragraphProperties,
        content: {
            alignment: normalizeCanvasAlignment(paragraphProperties.alignment ?? paragraphProperties.Alignment),
            runs: runs.map((run, index) => {
                const kind = run?.type === 'drawing' ? 'drawing' : (run?.type || 'text');
                const style = createCanvasRunStyle(model, block, run);
                const mathLayout = String(kind || '').replace(/[\s_-]/g, '').toLowerCase() === 'math'
                    ? measureCanvasMathRun(run, style, metrics)
                    : null;
                return {
                    id: run?.id || `${block?.id || 'block'}-run-${index}`,
                    kind,
                    type: run?.type || 'text',
                    text: createCanvasRunDisplayText(run),
                    marks: Array.isArray(run?.marks) ? run.marks : [],
                    style,
                    fieldType: run?.field?.fieldType || run?.field?.FieldType || null,
                    object: run?.drawing || null,
                    math: run?.math || null,
                    mathLayoutWidth: mathLayout?.width ?? null,
                    mathLayoutHeight: mathLayout?.height ?? null,
                    mathLayoutAscent: mathLayout?.ascent ?? null,
                    mathLayoutDescent: mathLayout?.descent ?? null,
                    contentControl: run?.contentControl || null,
                    objectId: run?.drawing?.id || run?.id || null,
                };
            }),
        },
    };
}

function measureCanvasMathRun(run, style, metrics) {
    const math = run?.math || run?.Math || null;
    if (!math) {
        return null;
    }

    const layout = layoutMathRun(math, {
        style,
        metrics,
    });
    const fontSize = Math.max(10, Number(style?.fontSize || 16) || 16);
    const inlinePadding = Math.max(6, fontSize * 0.35);
    const verticalPadding = Math.max(2, fontSize * 0.12);
    return {
        width: Math.max(1, (Number(layout.width || 0) || 1) + inlinePadding),
        height: Math.max(1, (Number(layout.height || 0) || 1) + verticalPadding),
        ascent: Math.max(1, Number(layout.ascent || 0) || 1),
        descent: Math.max(0, Number(layout.descent || 0) || 0),
    };
}

export function createCanvasPageLayout(index, pageSettings, section = null) {
    const page = {
        index,
        sectionId: section?.id || '',
        width: pageSettings.width,
        height: pageSettings.height,
        body: {
            x: pageSettings.marginLeft,
            y: pageSettings.marginTop,
            width: Math.max(1, pageSettings.width - pageSettings.marginLeft - pageSettings.marginRight),
            height: Math.max(1, pageSettings.height - pageSettings.marginTop - pageSettings.marginBottom),
        },
    };
    const flow = createColumnFlow(page, section);
    page.columns = flow.columns;
    page.columnSeparatorLine = flow.separatorLine;
    page.columnBalanced = flow.balanced;
    return page;
}

function layoutTextBlockAcrossPages(context) {
    const {
        sourceModel,
        sourceBlock,
        normalizedBlock,
        layoutEngine,
        metrics,
        pages,
        ensurePage,
        sequence,
    } = context;
    let currentPageIndex = context.currentPageIndex;
    let currentColumnIndex = Number(context.currentColumnIndex || 0) || 0;
    const section = context.section || null;
    const startFrame = columnFrame(ensurePage(currentPageIndex, section), currentColumnIndex);
    const startY = Math.max(startFrame.y, Number(context.cursorY) || startFrame.y);
    const properties = sourceBlock?.paragraphProperties || {};
    const blockType = canvasBlockType(sourceBlock);
    const list = blockType === 'list' ? createListMetrics(sourceModel, sourceBlock, metrics, context.numberingState) : null;
    const objectLayouts = Array.isArray(context.objectLayouts) ? context.objectLayouts : [];
    let intervalRequestIndex = 0;

    function resolveAvailableIntervals(atY, lineHeight) {
        let page = ensurePage(currentPageIndex);
        let frame = columnFrame(page, currentColumnIndex);
        let y = Math.max(frame.y, Number(atY) || frame.y);
        const height = Math.max(1, Number(lineHeight) || 18);
        let moved = false;

        if (y + height > frame.bottom && y > frame.y) {
            const nextColumn = nextColumnFrame(page, currentColumnIndex);
            if (nextColumn) {
                currentColumnIndex = nextColumn.columnIndex;
                frame = nextColumn.frame;
            } else {
                currentPageIndex += 1;
                page = ensurePage(currentPageIndex, section);
                currentColumnIndex = 0;
                frame = columnFrame(page, currentColumnIndex);
            }

            y = frame.y;
            moved = true;
        }

        const isFirstLine = intervalRequestIndex === 0;
        intervalRequestIndex += 1;
        const baseInterval = createTextInterval(page, sourceBlock, properties, isFirstLine, list, frame);
        const intervals = objectExclusionIntervals(objectLayouts, page, y, height)
            .map(interval => constrainTextInterval(baseInterval, interval))
            .filter(interval => interval.width >= 24);
        const availableIntervals = intervals.length > 0 ? intervals : [baseInterval];
        for (const interval of availableIntervals) {
            interval.y = y;
            interval.height = height;
            interval.pageIndex = page.index;
            interval.columnIndex = currentColumnIndex;
        }

        return {
            moved,
            movedToY: y,
            intervals: availableIntervals,
            availableIntervals,
            pageIndex: page.index,
        };
    }

    const firstPage = ensurePage(currentPageIndex, section);
    const firstBaseInterval = createTextInterval(firstPage, sourceBlock, properties, true, list, startFrame);
    const firstInterval = objectExclusionIntervals(objectLayouts, firstPage, startY, 18)
        .map(interval => constrainTextInterval(firstBaseInterval, interval))
        .find(interval => interval.width >= 24) || firstBaseInterval;
    firstInterval.columnIndex = currentColumnIndex;
    let rawLayout = layoutEngine.layoutParagraph(normalizedBlock, {
        x: firstInterval.x,
        y: startY,
        width: firstInterval.width,
        minReadableWidth: 32,
        lineGap: lineGapForBlock(sourceBlock),
        resolveAvailableIntervals,
    });
    if ((!Array.isArray(rawLayout.lines) || rawLayout.lines.length === 0)
        || (isEmptyTextBlock(normalizedBlock) && (!Array.isArray(rawLayout.caretStops) || rawLayout.caretStops.length === 0))) {
        rawLayout = createEmptyParagraphLayout(normalizedBlock, sourceBlock, firstInterval, startY, currentPageIndex);
    }
    annotateLineColumnIndexes(rawLayout, firstInterval, ensurePage);
    rawLayout = ensureTerminalCaretStop(rawLayout, normalizedBlock, sourceBlock, firstInterval, ensurePage);
    rawLayout = applyTabStopsToParagraphLayout(rawLayout, sourceBlock, { metrics });
    rawLayout = balanceParagraphColumns(rawLayout, pageIndex => ensurePage(pageIndex, section), section);
    const fragments = fragmentParagraphLayout(rawLayout, sourceBlock, sequence, section);
    const textRects = rawLayout.segments
        .filter(segment => segment.type !== 'space' && segment.type !== 'tab')
        .map(segment => ({
            id: segment.id,
            blockId: segment.blockId,
            runId: segment.runId,
            pageIndex: Number(segment.pageIndex ?? lineForSegment(rawLayout, segment)?.pageIndex ?? 0) || 0,
            rect: segment.rect,
        }));
    const listLabels = createListLabels(sourceBlock, rawLayout, list);
    const flowEnd = paragraphFlowEnd(rawLayout, currentPageIndex, currentColumnIndex, startY);

    for (const fragment of fragments) {
        ensurePage(fragment.pageIndex);
    }

    return {
        fragments,
        textRects,
        listLabels,
        currentPageIndex: flowEnd.pageIndex,
        currentColumnIndex: flowEnd.columnIndex,
        cursorY: flowEnd.bottom,
    };
}

function paragraphFlowEnd(layout, fallbackPageIndex, fallbackColumnIndex, fallbackY) {
    const lines = Array.isArray(layout?.lines) ? layout.lines : [];
    let end = {
        pageIndex: Number(fallbackPageIndex || 0) || 0,
        columnIndex: Number(fallbackColumnIndex || 0) || 0,
        bottom: Number(fallbackY || 0) || 0,
    };

    for (const line of lines) {
        const rect = line?.rect || {};
        const pageIndex = Number(line?.pageIndex ?? fallbackPageIndex) || 0;
        const columnIndex = Number(line?.columnIndex ?? fallbackColumnIndex) || 0;
        const bottom = (Number(rect.y || 0) || 0) + Math.max(1, Number(rect.height || 0) || 1);
        if (pageIndex > end.pageIndex || (pageIndex === end.pageIndex && bottom > end.bottom)) {
            end = { pageIndex, columnIndex, bottom };
        }
    }

    return end;
}

function annotateLineColumnIndexes(layout, fallbackInterval, ensurePage) {
    for (const line of layout?.lines || []) {
        const interval = (Array.isArray(line.availableIntervals) ? line.availableIntervals : [])
            .find(item => Number.isFinite(Number(item?.columnIndex)))
            || fallbackInterval
            || {};
        const page = ensurePage(Number(line.pageIndex || 0) || 0);
        line.columnIndex = columnIndexForLine(page, line, interval);
        for (const segment of line.segments || []) {
            segment.columnIndex = line.columnIndex;
        }
    }
}

function columnIndexForLine(page, line, interval = null) {
    const columns = Array.isArray(page?.columns) ? page.columns : [];
    if (columns.length <= 1) {
        return 0;
    }

    const x = Number(line?.rect?.x || 0) || 0;
    let bestIndex = 0;
    let bestDistance = Number.POSITIVE_INFINITY;
    for (const column of columns) {
        const distance = Math.abs(x - (Number(column.x) || 0));
        if (distance < bestDistance) {
            bestDistance = distance;
            bestIndex = Number(column.index || 0) || 0;
        }
    }

    if (bestDistance <= 1 || !Number.isFinite(Number(interval?.columnIndex))) {
        return bestIndex;
    }

    return Number(interval.columnIndex || 0) || 0;
}

function constrainTextInterval(base, available) {
    const left = Math.max(Number(base.x || 0) || 0, Number(available.x || 0) || 0);
    const right = Math.min(
        (Number(base.x || 0) || 0) + Math.max(1, Number(base.width || 1) || 1),
        (Number(available.x || 0) || 0) + Math.max(1, Number(available.width || 1) || 1));
    return {
        ...base,
        x: left,
        width: Math.max(1, right - left),
        columnIndex: base.columnIndex,
    };
}

function isEmptyTextBlock(normalizedBlock) {
    return textForNormalizedBlock(normalizedBlock).length === 0;
}

function textForNormalizedBlock(normalizedBlock) {
    return (normalizedBlock?.content?.runs || [])
        .map(run => String(run?.text || ''))
        .join('');
}

function createEmptyParagraphLayout(normalizedBlock, sourceBlock, interval, y, pageIndex) {
    const blockId = normalizedBlock?.id || sourceBlock?.id || '';
    const firstRun = normalizedBlock?.content?.runs?.[0] || {};
    const fontSize = Math.max(1, Number(firstRun?.style?.fontSize || 16) || 16);
    const lineHeight = Math.max(16, Math.ceil(fontSize * Number(sourceBlock?.paragraphProperties?.lineSpacing || sourceBlock?.paragraphProperties?.LineSpacing || 1.2)));
    const lineId = `${blockId || 'block'}-empty-line-${pageIndex}`;
    const rect = {
        x: Number(interval.x || 0) || 0,
        y: Number(y || 0) || 0,
        width: Math.max(1, Number(interval.width || 1) || 1),
        height: lineHeight,
    };

    return {
        blockId,
        lines: [{
            id: lineId,
            blockId,
            pageIndex,
            rect,
            segments: [],
        }],
        segments: [],
        caretStops: [{
            blockId,
            offset: 0,
            lineId,
            pageIndex,
            affinity: 'after',
            rect: {
                x: rect.x,
                y: rect.y,
                width: 1,
                height: rect.height,
            },
        }],
    };
}

function ensureTerminalCaretStop(layout, normalizedBlock, sourceBlock, interval, ensurePage) {
    const text = textForNormalizedBlock(normalizedBlock);
    const blockId = normalizedBlock?.id || sourceBlock?.id || layout?.blockId || '';
    const terminalOffset = text.length;
    const caretStops = Array.isArray(layout?.caretStops) ? layout.caretStops : [];
    if (caretStops.some(stop => String(stop.blockId || '') === String(blockId) && Number(stop.offset || 0) === terminalOffset)) {
        return layout;
    }

    const lines = Array.isArray(layout?.lines) ? layout.lines : [];
    const firstRun = normalizedBlock?.content?.runs?.[0] || {};
    const fontSize = Math.max(1, Number(firstRun?.style?.fontSize || 16) || 16);
    const lineHeight = Math.max(16, Math.ceil(fontSize * Number(sourceBlock?.paragraphProperties?.lineSpacing || sourceBlock?.paragraphProperties?.LineSpacing || 1.2)));
    let targetLine = lines[lines.length - 1] || null;

    if (!targetLine || text.endsWith('\n') || text.endsWith('\r')) {
        const previousLine = targetLine;
        let pageIndex = Number(previousLine?.pageIndex ?? 0) || 0;
        let page = ensurePage(pageIndex);
        let y = previousLine?.rect
            ? Number(previousLine.rect.y || 0) + Math.max(1, Number(previousLine.rect.height || lineHeight) || lineHeight)
            : page.body.y;

        if (y + lineHeight > page.body.y + page.body.height && y > page.body.y) {
            pageIndex += 1;
            page = ensurePage(pageIndex);
            y = page.body.y;
        }

        targetLine = {
            id: `${blockId || 'block'}-terminal-line-${pageIndex}-${terminalOffset}`,
            blockId,
            pageIndex,
            rect: {
                x: Number(interval.x || page.body.x || 0) || 0,
                y,
                width: Math.max(1, Number(interval.width || page.body.width || 1) || 1),
                height: lineHeight,
            },
            segments: [],
        };
        lines.push(targetLine);
        layout.lines = lines;
    }

    const lineSegments = Array.isArray(targetLine.segments) ? targetLine.segments : [];
    const lastSegment = lineSegments[lineSegments.length - 1] || null;
    const x = lastSegment?.rect
        ? Number(lastSegment.rect.x || 0) + Math.max(0, Number(lastSegment.rect.width || 0) || 0)
        : Number(targetLine.rect?.x || interval.x || 0) || 0;
    caretStops.push({
        blockId,
        offset: terminalOffset,
        lineId: targetLine.id,
        pageIndex: Number(targetLine.pageIndex || 0) || 0,
        affinity: 'after',
        rect: {
            x,
            y: Number(targetLine.rect?.y || 0) || 0,
            width: 1,
            height: Math.max(1, Number(targetLine.rect?.height || lineHeight) || lineHeight),
        },
    });
    layout.caretStops = caretStops;
    return layout;
}

function fragmentParagraphLayout(layout, sourceBlock, sequence, section = null) {
    const fragmentsByPage = new Map();
    for (const line of layout.lines || []) {
        const pageIndex = Number(line.pageIndex ?? 0) || 0;
        if (!fragmentsByPage.has(pageIndex)) {
            fragmentsByPage.set(pageIndex, {
                id: `${layout.blockId || sourceBlock?.id || 'block'}-fragment-${pageIndex}`,
                blockId: layout.blockId || sourceBlock?.id || '',
                type: 'paragraph',
                sourceType: canvasBlockType(sourceBlock),
                pageIndex,
                columnIndex: Number(line.columnIndex ?? 0) || 0,
                sectionId: section?.id || sourceBlock?.sectionId || '',
                sequence,
                rect: { x: line.rect.x, y: line.rect.y, width: line.rect.width, height: line.rect.height },
                lines: [],
                segments: [],
                caretStops: [],
                list: sourceBlock?.content?.list || null,
            });
        }

        const fragment = fragmentsByPage.get(pageIndex);
        fragment.lines.push(line);
        for (const segment of line.segments || []) {
            fragment.segments.push(segment);
        }

        for (const stop of (layout.caretStops || []).filter(item => item.lineId === line.id)) {
            fragment.caretStops.push({
                ...stop,
                pageIndex,
            });
        }

        const lineRight = line.rect.x + line.rect.width;
        const fragmentRight = fragment.rect.x + fragment.rect.width;
        fragment.rect.x = Math.min(fragment.rect.x, line.rect.x);
        fragment.rect.y = Math.min(fragment.rect.y, line.rect.y);
        fragment.rect.width = Math.max(fragmentRight, lineRight) - fragment.rect.x;
        fragment.rect.height = Math.max(fragment.rect.height, line.rect.y + line.rect.height - fragment.rect.y);
    }

    return Array.from(fragmentsByPage.values());
}

function createTextInterval(page, block, properties, isFirstLine, list, frame = null) {
    const body = frame || page.body;
    const rightIndent = Math.max(0, paragraphIndent(block, 'rightIndent'));
    if (list) {
        const textX = body.x + list.textIndent;
        return {
            x: Math.min(body.x + body.width - 1, textX),
            width: Math.max(1, body.x + body.width - rightIndent - textX),
        };
    }

    const leftIndent = Math.max(0, paragraphIndent(block, 'leftIndent'));
    const firstLineIndent = isFirstLine ? paragraphIndent(block, 'firstLineIndent') : 0;
    const x = Math.max(body.x, body.x + leftIndent + firstLineIndent);
    return {
        x,
        width: Math.max(1, body.x + body.width - rightIndent - x),
    };
}

function createListMetrics(model, block, metrics, numberingState = null) {
    const content = block?.content || {};
    const list = content.list || {};
    const ordered = list.ordered === true || list.Ordered === true;
    const indentLevel = Math.max(0, Number(list.indentLevel ?? list.IndentLevel ?? 0) || 0);
    const resolved = numberingState?.items?.get(String(block?.id || '')) || null;
    const label = resolved?.label || (ordered ? `${Math.max(1, Number(list.startNumber ?? list.StartNumber ?? 1) || 1)}.` : '\u2022');
    const style = createCanvasRunStyle(model, block, { marks: [] });
    const measured = metrics.measureText
        ? metrics.measureText(label, style)
        : metrics.measureRun({ text: label, ...style });
    const width = Math.max(12, Number(measured.width) || 12);
    const gap = Math.max(4, Number(resolved?.gap ?? DEFAULT_LIST_LABEL_GAP) || DEFAULT_LIST_LABEL_GAP);
    const labelIndent = Math.max(0, Number(list.labelIndent ?? list.LabelIndent ?? resolved?.labelIndent ?? (indentLevel * DEFAULT_LIST_INDENT_STEP)) || 0);
    const hangingIndent = Math.max(width + gap, Number(list.hangingIndent ?? list.HangingIndent ?? resolved?.hangingIndent ?? (width + gap)) || (width + gap));
    const textIndent = Math.max(labelIndent + width + gap, labelIndent + hangingIndent);
    return {
        text: label,
        style,
        width,
        gap,
        labelIndent,
        textIndent,
        indent: labelIndent,
    };
}

function createListLabels(block, layout, list) {
    if (!list || !layout.lines?.length) {
        return [];
    }

    const firstLine = layout.lines[0];
    const pageIndex = Number(firstLine.pageIndex ?? 0) || 0;
    const availableIntervals = Array.isArray(firstLine.availableIntervals) && firstLine.availableIntervals.length > 0
        ? firstLine.availableIntervals
        : [{ x: firstLine.rect?.x ?? layout.rect?.x ?? layout.x ?? 0 }];
    const pageBodyX = Math.min(...availableIntervals.map(interval => Number(interval.x) || 0));
    const labelX = Math.max(0, pageBodyX - (list.textIndent - list.labelIndent));
    return [{
        id: `${block.id || layout.blockId}-list-label`,
        blockId: block.id || layout.blockId || '',
        pageIndex,
        text: list.text,
        style: list.style,
        x: labelX,
        y: firstLine.rect.y,
        width: list.width,
        height: firstLine.rect.height,
        baseline: firstLine.baseline,
    }];
}

function tableTextRects(blocks) {
    return blocks.flatMap(block => (block.segments || [])
        .filter(segment => segment.type !== 'space' && segment.type !== 'tab')
        .map(segment => ({
            id: segment.id,
            blockId: segment.blockId,
            runId: segment.runId,
            pageIndex: Number(segment.pageIndex ?? block.pageIndex ?? 0) || 0,
            rect: segment.rect,
            tableId: block.cell?.tableId || '',
            cellId: block.cell?.cellId || '',
        })));
}

function standaloneDrawingRun(block) {
    const type = canvasBlockType(block);
    if (!isTextBlockType(type)) {
        return null;
    }

    const runs = Array.isArray(block?.content?.runs) ? block.content.runs : [];
    if (runs.length !== 1 || String(runs[0]?.type || '').toLowerCase() !== 'drawing' || !runs[0]?.drawing) {
        return null;
    }

    return { run: runs[0], runIndex: 0 };
}

function isTextBlockType(type) {
    return type === 'paragraph' || type === 'heading' || type === 'list' || type === 'quote';
}

function lineForSegment(layout, segment) {
    return (layout.lines || []).find(line => (line.segments || []).some(candidate => candidate.id === segment.id)) || null;
}

function paragraphSpacing(block, key, fallback = 0) {
    const properties = block?.paragraphProperties || {};
    return pointsToCssPixels(properties[key] ?? properties[pascalCase(key)] ?? fallback);
}

function lineGapForBlock(block) {
    const properties = block?.paragraphProperties || {};
    const spacing = Number(properties.lineSpacing ?? properties.LineSpacing ?? 1) || 1;
    const baseFont = 16;
    return Math.max(0, baseFont * Math.max(0, spacing - 1));
}

function canvasBlockType(block) {
    const raw = block?.type ?? block?.Type ?? block?.content?.type ?? block?.content?.Type ?? 'paragraph';
    const numeric = Number(raw);
    if (Number.isInteger(numeric)) {
        if (numeric === 4) {
            return 'table';
        }

        if (numeric === 5) {
            return 'image';
        }

        if (numeric === 6) {
            return 'pagebreak';
        }

        if (numeric === 7) {
            return 'contentcontrol';
        }

        return numeric === 2 ? 'list' : 'paragraph';
    }

    return String(raw || 'paragraph').replace(/[\s_-]/g, '').toLowerCase();
}

function pageBodyBottom(page) {
    return page.body.y + page.body.height;
}

function pascalCase(value) {
    return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}

function ensureMeasurementService(service) {
    if (!service || typeof service.measureRun !== 'function') {
        return createFontMetricsService();
    }

    if (typeof service.measureText === 'function' && typeof service.measureTextRun === 'function') {
        return service;
    }

    return Object.assign(Object.create(service), {
        measureText(text, style) {
            const metrics = service.measureRun({ ...(style || {}), text });
            return {
                width: Math.max(1, Number(metrics.width) || 1),
                height: Math.max(1, Number(metrics.lineHeight ?? metrics.height) || Number(style?.fontSize) * 1.25 || 16),
            };
        },
        measureTextRun(request) {
            const metrics = service.measureRun(request || {});
            return {
                Text: String(request?.text ?? request?.Text ?? ''),
                Width: Math.max(1, Number(metrics.width) || 1),
                Height: Math.max(1, Number(metrics.lineHeight ?? metrics.height) || Number(request?.fontSize ?? request?.FontSize) * 1.25 || 16),
            };
        },
        getStats() {
            return typeof service.getStats === 'function' ? service.getStats() : null;
        },
    });
}
