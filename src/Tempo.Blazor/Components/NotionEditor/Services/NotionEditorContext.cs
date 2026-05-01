using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Cascades all NotionEditor provider references to the component tree.
/// Child components receive this via <c>[CascadingParameter] NotionEditorContext Context</c>
/// and do not require individual DI registrations.
/// </summary>
public sealed class NotionEditorContext
{
    public INotionDataProvider         DataProvider          { get; init; } = default!;
    public INotionBlockProvider        BlockProvider         { get; init; } = default!;
    public INotionSearchProvider?      SearchProvider        { get; init; }
    public INotionDatabaseProvider?    DatabaseProvider      { get; init; }
    public INotionCommentProvider?     CommentProvider       { get; init; }
    public INotionHistoryProvider?     HistoryProvider       { get; init; }
    public INotionCollaborationProvider? CollaborationProvider { get; init; }
    public INotionMentionProvider?     MentionProvider       { get; init; }
    public INotionBookmarkProvider?      BookmarkProvider         { get; init; }
    public INotionFileProvider?          FileProvider             { get; init; }
    public INotionImportExportProvider?  ImportExportProvider     { get; init; }
    public IDiagramDocumentProvider?     DiagramDocumentProvider  { get; init; }
    public IWireframeDocumentProvider?   WireframeDocumentProvider{ get; init; }
    public INotionSyncedBlockProvider?    SyncedBlockProvider      { get; init; }

    /// <summary>
    /// Navigates to the given page by ID. Supplied by TmNotionEditor and used by
    /// child page / linked page / breadcrumb blocks to trigger in-editor navigation.
    /// </summary>
    public Func<string, Task>?            NavigateTo               { get; init; }
}
