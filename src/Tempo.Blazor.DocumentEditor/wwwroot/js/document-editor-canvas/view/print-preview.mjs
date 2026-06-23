export function createPrintPreviewSnapshot(model = {}, layout = {}, render = {}, viewState = {}) {
    const pages = Array.isArray(render?.displayList?.pages) && render.displayList.pages.length > 0
        ? render.displayList.pages
        : (Array.isArray(layout?.pages) ? layout.pages : []);
    const commands = Array.isArray(render?.displayList?.commands) ? render.displayList.commands : [];
    const textRunCount = commands.filter(command => command?.type === 'textRun' || command?.type === 'field' || command?.type === 'listLabel').length;
    const printableCommandCount = commands.filter(command => command?.layer !== 'selection-caret' && command?.layer !== 'diagnostics').length;

    return {
        active: viewState?.printPreview?.active === true,
        documentId: String(model?.documentId || ''),
        pageCount: pages.length,
        commandCount: commands.length,
        printableCommandCount,
        textRunCount,
        zoomPercent: Number(viewState?.zoom?.percent || 100) || 100,
        viewMode: String(viewState?.viewMode || 'print'),
        pages: pages.map(page => ({
            index: Number(page?.index || 0) || 0,
            width: Math.round(Number(page?.width || 0) || 0),
            height: Math.round(Number(page?.height || 0) || 0),
        })),
        isBlank: printableCommandCount === 0 || textRunCount === 0,
        generatedAt: new Date().toISOString(),
    };
}

export function createPrintDialogResult(view, previewSnapshot = {}) {
    const target = view || globalThis;
    const canPrint = typeof target?.print === 'function';
    if (canPrint) {
        target.print();
    }

    return {
        requested: true,
        invoked: canPrint,
        pageCount: Number(previewSnapshot?.pageCount || 0) || 0,
        printableCommandCount: Number(previewSnapshot?.printableCommandCount || 0) || 0,
    };
}
