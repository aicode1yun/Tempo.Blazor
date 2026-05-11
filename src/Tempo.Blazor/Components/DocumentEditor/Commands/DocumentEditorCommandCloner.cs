using System.Text.Json;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

internal static class DocumentEditorCommandCloner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Document editor command snapshot could not be cloned.");
    }

    public static DocumentBlock CloneBlock(DocumentBlock block) => Clone(block);

    public static DocumentBlockContent CloneContent(DocumentBlockContent content) => Clone(content);
}
