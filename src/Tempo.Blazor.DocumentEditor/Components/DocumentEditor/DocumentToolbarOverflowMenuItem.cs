using Tempo.Blazor.Components.DocumentEditor.Registry;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Represents one command rendered in the document editor toolbar overflow menu.</summary>
public sealed record DocumentToolbarOverflowMenuItem(
    string CommandName,
    DocumentToolbarItem Metadata,
    string Label,
    bool IsEnabled);

