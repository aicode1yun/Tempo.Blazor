// Phase D — objects/anchor-region.mjs
// Floating-object anchor region helpers: Body=0, Header=1, Footer=2, TableCell=6.
// Plus the `readObjectLayoutInCell` precedence resolver which walks the layout/anchor/
// docx/anchorXml/metadata hierarchy looking for an explicit `layoutInCell` boolean.

import { asText } from '../core/helpers.mjs';
import { readOptionalBoolean } from '../core/value-readers.mjs';

// Normalize numeric ordinal or string variants to one of {Body, Header, Footer, TableCell}.
export function normalizeAnchorRegionName(value) {
    if (value === 1) return 'Header';
    if (value === 2) return 'Footer';
    if (value === 6) return 'TableCell';
    const raw = asText(value || '').trim().toLowerCase();
    if (raw === '1' || raw === 'header' || raw === 'headers') return 'Header';
    if (raw === '2' || raw === 'footer' || raw === 'footers') return 'Footer';
    if (raw === '6' || raw === 'tablecell' || raw === 'table-cell' || raw === 'cell') return 'TableCell';
    return 'Body';
}

// Inverse — string name → numeric ordinal.
export function anchorRegionToValue(value) {
    const normalized = normalizeAnchorRegionName(value);
    if (normalized === 'Header') return 1;
    if (normalized === 'Footer') return 2;
    if (normalized === 'TableCell') return 6;
    return 0;
}

// Resolve the `layoutInCell` flag for a floating image/drawing. Word/docx embeds this
// flag at multiple levels; this helper walks them in precedence order: direct → anchor
// → layout → docx → anchorXml → metadata. Falls back to `true` if no level sets it
// (matches Word's default of "layout inside cell").
export function readObjectLayoutInCell(object) {
    const source = object || {};
    const layout = source.layout || source.Layout || {};
    const anchor = source.anchor || source.Anchor || layout.anchor || layout.Anchor || {};
    const docx = source.docx || source.Docx || {};
    const anchorXml = source.anchorXml || source.AnchorXml
        || docx.anchorXml || docx.AnchorXml || {};
    const metadata = source.metadata || source.Metadata || {};

    const direct = readOptionalBoolean(source, ['layoutInCell', 'LayoutInCell']);
    if (direct !== null) return direct;
    const anchorValue = readOptionalBoolean(anchor, ['layoutInCell', 'LayoutInCell']);
    if (anchorValue !== null) return anchorValue;
    const layoutValue = readOptionalBoolean(layout, ['layoutInCell', 'LayoutInCell']);
    if (layoutValue !== null) return layoutValue;
    const docxValue = readOptionalBoolean(docx, ['layoutInCell', 'LayoutInCell']);
    if (docxValue !== null) return docxValue;
    const anchorXmlValue = readOptionalBoolean(anchorXml, ['layoutInCell', 'LayoutInCell']);
    if (anchorXmlValue !== null) return anchorXmlValue;
    const metadataValue = readOptionalBoolean(metadata, ['layoutInCell', 'LayoutInCell']);
    return metadataValue !== null ? metadataValue : true;
}
