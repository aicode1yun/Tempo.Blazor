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
    public INotionBookmarkProvider?    BookmarkProvider      { get; init; }
    public INotionFileProvider?        FileProvider          { get; init; }
    public INotionImportExportProvider? ImportExportProvider { get; init; }
}
