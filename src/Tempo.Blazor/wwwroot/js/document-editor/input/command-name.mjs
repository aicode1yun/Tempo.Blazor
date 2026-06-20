// Phase D — input/command-name.mjs
// `compactCommandName` — turn any command id ('InsertImage', 'insert_image', 'insert
// image', 'INSERT-IMAGE') into a stable lowercase compacted form ('insertimage') for
// dispatcher routing.

export function compactCommandName(value) {
    return String(value || '').replace(/[\s_-]+/g, '').toLowerCase();
}
