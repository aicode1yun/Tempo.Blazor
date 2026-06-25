import { layoutCanvasDocument } from '../../layout/pagination.mjs';

export function createSelectionTestLayout(text = defaultText()) {
    const model = {
        documentId: 'phase-7-selection-test',
        pageSettings: { width: 420, height: 720, marginTop: 56, marginRight: 56, marginBottom: 56, marginLeft: 56 },
        theme: { bodyFontFamily: 'Aptos, Arial, sans-serif', bodyFontSize: 11.25, paragraphSpacingAfter: 8 },
        body: {
            blocks: [
                {
                    id: 'paragraph-1',
                    type: 'paragraph',
                    order: 1,
                    paragraphProperties: { alignment: 'left', lineSpacing: 1.12 },
                    content: { type: 'paragraph', runs: [{ id: 'paragraph-1-run', type: 'text', text, marks: [] }] },
                },
                {
                    id: 'paragraph-2',
                    type: 'paragraph',
                    order: 2,
                    paragraphProperties: { alignment: 'left', lineSpacing: 1.12 },
                    content: { type: 'paragraph', runs: [{ id: 'paragraph-2-run', type: 'text', text: 'Second block for cross-block selection geometry.', marks: [] }] },
                },
            ],
        },
    };

    return {
        model,
        layout: layoutCanvasDocument(model, { fontMetrics: createDeterministicMetrics() }),
    };
}

function defaultText() {
    return 'Selection maps pointer coordinates to exact caret stops, paints native-feeling highlights, and keeps keyboard movement stable across wrapped lines.';
}

function createDeterministicMetrics() {
    return {
        measureRun(request) {
            const fontSize = Number(request.fontSize) || 16;
            const text = String(request.text || '');
            return {
                width: Math.max(1, Array.from(text).reduce((sum, ch) => sum + (/\s/.test(ch) ? fontSize * 0.32 : fontSize * 0.52), 0)),
                ascent: fontSize * 0.8,
                descent: fontSize * 0.2,
                lineHeight: Math.ceil(fontSize * 1.25),
            };
        },
    };
}
