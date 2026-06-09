// Extracts the comment + revision lists from the engine model for the C# comment rail / revision panel (B3).
// The engine model is the live source of truth; reading it directly lets those panels stop depending on the
// debounced C# document mirror. Comments and revisions already round-trip as the canonical C#
// DocumentComment / DocumentRevision shapes (CanvasDocumentModel.Comments/Revisions), so the returned objects
// deserialize straight into those types. Pure + dependency-free for unit testing.
export function extractAnnotations(model) {
    const m = model && typeof model === 'object' ? model : {};
    return {
        comments: pickList(m.comments, m.Comments),
        revisions: pickList(m.revisions, m.Revisions),
    };
}

// Counts words in the document body for the status bar (B6: read live from the engine instead of the C#
// document mirror, which is no longer kept current per edit). Walks model.body collecting every run `text`
// and splits on whitespace. Pure + dependency-free for unit testing.
export function countModelWords(model) {
    const body = model && typeof model === 'object' ? (model.body ?? model.Body) : null;
    if (!body || typeof body !== 'object') {
        return 0;
    }

    const parts = [];
    const stack = [body];
    const seen = new Set();
    while (stack.length > 0) {
        const node = stack.pop();
        if (Array.isArray(node)) {
            for (const child of node) {
                stack.push(child);
            }
            continue;
        }
        if (!node || typeof node !== 'object' || seen.has(node)) {
            continue;
        }
        seen.add(node);
        const text = node.text ?? node.Text;
        if (typeof text === 'string' && text.length > 0) {
            parts.push(text);
        }
        for (const key in node) {
            const value = node[key];
            if (value && typeof value === 'object') {
                stack.push(value);
            }
        }
    }

    const joined = parts.join(' ').replace(/\s+/g, ' ').trim();
    return joined.length === 0 ? 0 : joined.split(' ').length;
}

function pickList(lower, upper) {
    if (Array.isArray(lower)) {
        return lower;
    }
    if (Array.isArray(upper)) {
        return upper;
    }
    return [];
}
