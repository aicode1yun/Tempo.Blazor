// Minimal, self-contained interop for the Tempo Report Server web host.
// Clipboard access is the only capability that cannot be expressed in pure Blazor.
window.tempoReportServer = {
    copyToClipboard: function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text);
        }

        return Promise.resolve();
    }
};
