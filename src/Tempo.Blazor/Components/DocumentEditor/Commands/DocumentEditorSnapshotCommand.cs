using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Restores whole-document snapshots for mutations that touch multiple document collections.</summary>
public sealed class DocumentEditorSnapshotCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _target;
    private readonly DocumentEditorDocument _before;
    private readonly DocumentEditorDocument _after;

    /// <summary>Creates a snapshot command.</summary>
    public DocumentEditorSnapshotCommand(
        DocumentEditorDocument target,
        DocumentEditorDocument before,
        DocumentEditorDocument after,
        string? description = null)
    {
        _target = target;
        _before = DocumentEditorCommandCloner.Clone(before);
        _after = DocumentEditorCommandCloner.Clone(after);
        Description = string.IsNullOrWhiteSpace(description) ? "Update document" : description;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task ExecuteAsync()
    {
        CopyFrom(_after);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UndoAsync()
    {
        CopyFrom(_before);
        return Task.CompletedTask;
    }

    private void CopyFrom(DocumentEditorDocument source)
    {
        _target.SchemaVersion = source.SchemaVersion;
        _target.DocumentId = source.DocumentId;
        _target.Metadata = DocumentEditorCommandCloner.Clone(source.Metadata);
        _target.PageSettings = DocumentEditorCommandCloner.Clone(source.PageSettings);
        _target.Theme = DocumentEditorCommandCloner.Clone(source.Theme);
        _target.Sections = DocumentEditorCommandCloner.Clone(source.Sections);
        _target.Blocks = DocumentEditorCommandCloner.Clone(source.Blocks);
        _target.Comments = DocumentEditorCommandCloner.Clone(source.Comments);
        _target.Notes = DocumentEditorCommandCloner.Clone(source.Notes);
        _target.HeadersFooters = DocumentEditorCommandCloner.Clone(source.HeadersFooters);
        _target.Revisions = DocumentEditorCommandCloner.Clone(source.Revisions);
        _target.Assets = DocumentEditorCommandCloner.Clone(source.Assets);
        _target.Anchors = DocumentEditorCommandCloner.Clone(source.Anchors);
    }
}
