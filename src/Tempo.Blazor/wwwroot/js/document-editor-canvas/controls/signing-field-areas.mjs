// Derives signing fields (with their areas) from a display list (plan S2.13/S2.14). Areas are NEVER
// stored — they are computed by grouping the display list's signing field commands by field uuid and
// normalizing each command's rect into the 0..1 page coordinate space. A body field has exactly one
// command (one area); a header/footer field has one command per page it renders on (N areas), with
// the same uuid — so it becomes one field stamped onto every page. Scope (first/even/odd) is honoured
// automatically because the layout only emits a command where the field actually renders.

export function extractSigningFields(displayList) {
    const commands = Array.isArray(displayList?.commands) ? displayList.commands : [];
    const pages = pageSizeIndex(displayList);
    const byUuid = new Map();

    for (const command of commands) {
        if (command?.type !== 'signingField') {
            continue;
        }

        const uuid = String(command.fieldUuid || '');
        if (!uuid) {
            continue;
        }

        if (!byUuid.has(uuid)) {
            byUuid.set(uuid, {
                uuid,
                fieldType: String(command.fieldType || 'text'),
                submitterUuid: String(command.submitterUuid || ''),
                required: command.required === true,
                label: String(command.label || ''),
                options: Array.isArray(command.options) ? command.options : [],
                areas: [],
            });
        }

        const field = byUuid.get(uuid);
        field.areas.push(normalizeArea(command, pages));
    }

    // Stable order: by first area page/position, then uuid — deterministic for tests and downstream.
    return Array.from(byUuid.values())
        .map(field => ({ ...field, areas: field.areas.sort((left, right) => left.page - right.page || left.y - right.y || left.x - right.x) }))
        .sort((left, right) => firstAreaSortKey(left) - firstAreaSortKey(right) || left.uuid.localeCompare(right.uuid));
}

function normalizeArea(command, pages) {
    const page = Number(command.pageIndex || 0) || 0;
    const size = pages.get(page) || pages.get(0) || { width: 794, height: 1123 };
    const width = Math.max(1, Number(size.width) || 1);
    const height = Math.max(1, Number(size.height) || 1);
    return {
        page,
        x: clamp01(Number(command.x || 0) / width),
        y: clamp01(Number(command.y || 0) / height),
        width: clamp01(Number(command.width || 0) / width),
        height: clamp01(Number(command.height || 0) / height),
    };
}

function pageSizeIndex(displayList) {
    const index = new Map();
    const pages = Array.isArray(displayList?.pages) ? displayList.pages : [];
    for (const page of pages) {
        index.set(Number(page?.index || 0) || 0, { width: Number(page?.width) || 794, height: Number(page?.height) || 1123 });
    }

    return index;
}

function firstAreaSortKey(field) {
    const first = field.areas[0];
    return first ? first.page * 1000 + first.y : 0;
}

function clamp01(value) {
    if (!Number.isFinite(value)) {
        return 0;
    }

    return Math.max(0, Math.min(1, value));
}
