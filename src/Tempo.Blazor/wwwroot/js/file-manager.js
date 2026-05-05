// TempoFileManager — keyboard navigation helpers
window.TempoFileManager = window.TempoFileManager || {};

window.TempoFileManager.getGridColumnCount = function (element) {
    if (!element) return 1;
    var style = window.getComputedStyle(element);
    var template = style.gridTemplateColumns;
    if (!template) return 1;
    // gridTemplateColumns returns something like "120px 120px 120px" or "1fr 1fr"
    return template.trim().split(/\s+/).length;
};

window.TempoFileManager.downloadFileFromStream = async function (fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
};
