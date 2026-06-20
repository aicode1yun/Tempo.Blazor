using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Mcp.DocumentEditor;

internal static class DocumentEditorMcpPostFixer
{
    public static IReadOnlyList<DocumentPostFixerWarning> Fix(DocumentEditorDocument document)
        => new DocumentEditorPostFixer().Fix(document).Warnings;

    public static object[] ToToolWarnings(IReadOnlyList<DocumentPostFixerWarning> warnings)
        => warnings.Select(w => (object)new
        {
            code = w.Code,
            message = w.Message
        }).ToArray();
}
