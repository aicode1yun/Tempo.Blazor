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
