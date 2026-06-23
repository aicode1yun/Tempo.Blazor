namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Represents one grouped section in the document editor toolbar overflow menu.</summary>
public sealed record DocumentToolbarOverflowMenuGroup(
    string GroupId,
    string Label,
    IReadOnlyList<DocumentToolbarOverflowMenuItem> Items);

