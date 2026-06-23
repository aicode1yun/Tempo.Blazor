import { normalizePageSettings } from './page-geometry.mjs';
import { orderedCanvasBlocks } from './canvas-text-style.mjs';

export function buildSectionFlows(model, defaultPageSettings) {
    const sourceSections = Array.isArray(model?.sections || model?.Sections) ? (model.sections || model.Sections) : [];
    const bodyBlocks = orderedCanvasBlocks(model);
    const sections = sourceSections.length > 0
        ? sourceSections.map((section, index) => normalizeSection(section, index, defaultPageSettings, bodyBlocks))
        : [normalizeSection({
            id: 'section-1',
            order: 0,
            pageSettings: defaultPageSettings,
            blocks: bodyBlocks,
        }, 0, defaultPageSettings, bodyBlocks)];

    const hasSectionBlocks = sections.some(section => section.blocks.length > 0);
    if (!hasSectionBlocks && bodyBlocks.length > 0) {
        distributeBodyBlocks(sections, bodyBlocks);
    }

    const sorted = sections
        .slice()
        .sort((left, right) => left.order - right.order || left.id.localeCompare(right.id));
    const byId = new Map(sorted.map(section => [section.id, section]));

    return {
        sections: sorted,
        byId,
        first: sorted[0],
        sectionForBlock(block, fallback = sorted[0]) {
            const sectionId = String(block?.sectionId || '');
            return (sectionId && byId.get(sectionId)) || fallback || sorted[0];
        },
        nextSection(current, nextSectionId = null) {
            if (nextSectionId && byId.has(String(nextSectionId))) {
                return byId.get(String(nextSectionId));
            }

            const index = sorted.findIndex(section => section.id === current?.id);
            return sorted[Math.min(sorted.length - 1, Math.max(0, index + 1))] || current || sorted[0];
        },
        orderedBlocks() {
            return sorted.flatMap(section => section.blocks.map(block => ({ block, section })));
        },
    };
}

export function normalizeBreakType(block) {
    const content = block?.content || {};
    const pageBreak = content.pageBreak || content.PageBreak || {};
    const raw = pageBreak.breakType ?? pageBreak.BreakType ?? content.breakType ?? content.BreakType ?? 'page';
    const numeric = Number(raw);
    if (Number.isInteger(numeric)) {
        if (numeric === 1) {
            return 'nextPage';
        }

        if (numeric === 2) {
            return 'continuous';
        }

        if (numeric === 3) {
            return 'evenPage';
        }

        if (numeric === 4) {
            return 'oddPage';
        }

        if (numeric === 5) {
            return 'column';
        }
    }

    const normalized = String(raw || 'page').replace(/[\s_-]/g, '').toLowerCase();
    if (normalized === 'nextpage' || normalized === 'sectionnextpage') {
        return 'nextPage';
    }

    if (normalized === 'continuous' || normalized === 'sectioncontinuous') {
        return 'continuous';
    }

    if (normalized === 'evenpage' || normalized === 'sectionevenpage') {
        return 'evenPage';
    }

    if (normalized === 'oddpage' || normalized === 'sectionoddpage') {
        return 'oddPage';
    }

    if (normalized === 'column' || normalized === 'columnbreak') {
        return 'column';
    }

    return 'page';
}

export function nextSectionIdForBreak(block) {
    const content = block?.content || {};
    const pageBreak = content.pageBreak || content.PageBreak || {};
    const value = pageBreak.nextSectionId ?? pageBreak.NextSectionId ?? content.nextSectionId ?? content.NextSectionId ?? null;
    return value == null ? null : String(value);
}

export function pageSettingsEqual(left, right) {
    const a = normalizePageSettings(left);
    const b = normalizePageSettings(right);
    return ['width', 'height', 'marginTop', 'marginRight', 'marginBottom', 'marginLeft']
        .every(key => Math.abs((Number(a[key]) || 0) - (Number(b[key]) || 0)) < 0.001);
}

function normalizeSection(section, index, defaultPageSettings, bodyBlocks) {
    const id = String(section?.id ?? section?.Id ?? `section-${index + 1}`);
    const properties = section?.properties || section?.Properties || {};
    const rawBlocks = Array.isArray(section?.blocks || section?.Blocks) ? (section.blocks || section.Blocks) : [];
    const matchingBodyBlocks = bodyBlocks.filter(block => String(block?.sectionId ?? block?.SectionId ?? '') === id);
    return {
        ...section,
        id,
        order: Number.isFinite(Number(section?.order ?? section?.Order)) ? Number(section.order ?? section.Order) : index,
        title: section?.title ?? section?.Title ?? null,
        properties,
        pageSettings: normalizePageSettings(section?.pageSettings || section?.PageSettings || properties.pageSettings || properties.PageSettings || defaultPageSettings),
        blocks: (rawBlocks.length > 0 ? rawBlocks : matchingBodyBlocks)
            .slice()
            .sort((left, right) => (Number(left?.order ?? left?.Order) || 0) - (Number(right?.order ?? right?.Order) || 0) || String(left?.id ?? left?.Id ?? '').localeCompare(String(right?.id ?? right?.Id ?? ''))),
    };
}

function distributeBodyBlocks(sections, bodyBlocks) {
    const first = sections[0];
    for (const section of sections) {
        section.blocks = bodyBlocks.filter(block => String(block?.sectionId ?? block?.SectionId ?? '') === section.id);
    }

    const unassigned = bodyBlocks.filter(block => !String(block?.sectionId ?? block?.SectionId ?? ''));
    first.blocks = [...first.blocks, ...unassigned]
        .sort((left, right) => (Number(left?.order ?? left?.Order) || 0) - (Number(right?.order ?? right?.Order) || 0) || String(left?.id ?? left?.Id ?? '').localeCompare(String(right?.id ?? right?.Id ?? '')));
}
