window.tmGantt = {
    getScrollLeft: function (el) { return el ? el.scrollLeft : 0; },
    setScrollLeft: function (el, v) { if (el) el.scrollTo({ left: v }); },
    getScrollTop: function (el) { return el ? el.scrollTop : 0; },
    setScrollTop: function (el, v) { if (el) el.scrollTop = v; }
};
