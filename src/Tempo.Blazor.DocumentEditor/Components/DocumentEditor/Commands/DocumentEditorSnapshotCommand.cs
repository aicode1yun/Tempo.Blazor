using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Components.DocumentEditor.Commands;

/// <summary>Restores whole-document snapshots for mutations that touch multiple document collections.</summary>
public sealed class DocumentEditorSnapshotCommand : IDocumentEditorCommand
{
    private readonly DocumentEditorDocument _target;
    private readonly DocumentEditorDocument _before;
    private readonly DocumentEditorDocument _after;

    /// <summary>
    /// Creates a snapshot command. By default (<paramref name="assumeOwnership"/> = false) the
    /// command defensively clones <paramref name="before"/> and <paramref name="after"/> — the
    /// historical 2.0.x contract, safe for external consumers passing live documents. Internal
    /// perf call-sites that hand over dedicated clones pass <paramref name="assumeOwnership"/> = true
    /// to skip the two O(document) copies (perf plan N3.1; same pattern as
    /// CreateProviderBoundarySnapshot).
    /// </summary>
    public DocumentEditorSnapshotCommand(
        DocumentEditorDocument target,
        DocumentEditorDocument before,
        DocumentEditorDocument after,
        string? description = null,
        bool assumeOwnership = false)
    {
        _target = target;
        _before = assumeOwnership ? before : DocumentEditorCommandCloner.Clone(before);
        _after = assumeOwnership ? after : DocumentEditorCommandCloner.Clone(after);
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
        _target.Styles = DocumentEditorCommandCloner.Clone(source.Styles);
        _target.Assets = DocumentEditorCommandCloner.Clone(source.Assets);
        _target.Anchors = DocumentEditorCommandCloner.Clone(source.Anchors);
    }
}
