// Phase D — core/block-export.mjs
// Block-level export pipeline (internal model → C#-JSON). Pure functions extracted
// from the legacy IIFE. Mirrors `importBlock` shape-for-shape so a round-trip through
// the legacy IIFE produces the same wire format.

import { asArray, asText, clone, sortObject } from './helpers.mjs';
import {
    exportBlockType,
    exportTextAlignment,
} from './export-types.mjs';
import { exportInlineRun } from './inline-runs.mjs';

// Recursively export a block to its C#-JSON shape.
export function exportBlock(block) {
    if (block.type === 'image') {
        return sortObject({
            Id: block.id,
            Type: exportBlockType(block),
            Content: {
                $type: 'image',
                Id: block.content.objectId,
                Source: block.content.source ?? 0,
                Url: block.content.url,
                AssetId: block.content.assetId,
                AltText: block.content.altText,
                IsDecorative: block.content.isDecorative === true,
                Caption: block.content.caption,
                Size: clone(block.content.size || {}),
                NaturalSize: clone(block.content.naturalSize || {}),
                Alignment: block.content.alignment ?? 1,
                Layout: clone(block.content.layout || {}),
                Style: clone(block.content.style || {}),
                LinkUrl: block.content.linkUrl ?? null,
            },
            Style: clone(block.style || {}),
        });
    }

    if (block.type === 'table') {
        return sortObject({
            Id: block.id,
            Type: exportBlockType(block),
            Content: {
                $type: 'table',
                Rows: asArray(block.content.rows).map(row => ({
                    Id: row.id,
                    Cells: asArray(row.cells).map(cell => ({
                        Id: cell.id,
                        RowSpan: cell.rowSpan || 1,
                        ColSpan: cell.colSpan || 1,
                        Width: cell.width || null,
                        Height: cell.height || null,
                        Style: clone(cell.style || {}),
                        Blocks: asArray(cell.blocks).map(exportBlock),
                    })),
                })),
                Style: clone(block.content.style || {}),
            },
            Style: clone(block.style || {}),
        });
    }

    // Paragraph / heading / list / quote — the C# wire format uses the same shape with
    // a `$type` discriminator inside `Content`.
    const textContent = block.content || {};
    return sortObject({
        Id: block.id,
        Type: exportBlockType(block),
        ParagraphProperties: {
            Alignment: exportTextAlignment(textContent.alignment ?? textContent.Alignment),
            LineSpacing: Number(textContent.lineSpacing ?? textContent.LineSpacing ?? 1) || 1,
            SpacingBefore: Number(textContent.spacingBefore ?? textContent.SpacingBefore ?? 0) || 0,
            SpacingAfter: Number(textContent.spacingAfter ?? textContent.SpacingAfter ?? 0) || 0,
            LeftIndent: Number(textContent.leftIndent ?? textContent.LeftIndent ?? 0) || 0,
            RightIndent: Number(textContent.rightIndent ?? textContent.RightIndent ?? 0) || 0,
        },
        Content: {
            $type: block.type === 'heading' ? 'heading'
                : block.type === 'list' ? 'list'
                    : block.type === 'quote' ? 'quote'
                        : 'paragraph',
            Alignment: block.content.alignment,
            LineSpacing: block.content.lineSpacing,
            Inlines: asArray(block.content.runs).map(exportInlineRun),
            Style: clone(block.content.style || {}),
        },
        Style: clone(block.style || {}),
    });
}

// Lightweight comment-id reader — used by the comment exporter to pull `id` out of a
// camel-or-Pascal-shaped comment record.
export function readCommentId(comment) {
    return asText((comment && (comment.id || comment.Id)) || '');
}
