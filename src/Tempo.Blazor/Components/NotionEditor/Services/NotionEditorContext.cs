using Tempo.Blazor.Interfaces;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Services;

/// <summary>
/// Cascades all NotionEditor provider references to the component tree.
/// Child components receive this via <c>[CascadingParameter] NotionEditorContext Context</c>
/// and do not require individual DI registrations.
/// </summary>
public sealed class NotionEditorContext
{
    public string                      CurrentUserId        { get; init; } = "demo";
    public string?                     CurrentPageId        { get; init; }
    public INotionDataProvider         DataProvider          { get; init; } = default!;
    public INotionBlockProvider        BlockProvider         { get; init; } = default!;
    public INotionSearchProvider?      SearchProvider        { get; init; }
    public INotionDatabaseProvider?    DatabaseProvider      { get; init; }
    public INotionCommentProvider?     CommentProvider       { get; init; }
    public INotionHistoryProvider?     HistoryProvider       { get; init; }
    public INotionCollaborationProvider? CollaborationProvider { get; init; }
    public INotionMentionProvider?     MentionProvider       { get; init; }
    public INotionAIProvider?          AIProvider            { get; init; }
    public INotionTaskProvider?        TaskProvider          { get; init; }
    public WorkItemProviderRegistry?   WorkItemProviders     { get; init; }
    public INotionReactionProvider?    ReactionProvider      { get; init; }
    public INotionAnalyticsProvider?   AnalyticsProvider     { get; init; }
    public INotionBlogProvider?        BlogProvider          { get; init; }
    public INotionWatchProvider?       WatchProvider         { get; init; }
    public INotionSpaceProvider?       SpaceProvider         { get; init; }
    public INotionPagePropertiesProvider? PagePropertiesProvider { get; init; }
    public INotionTemplateProvider?      TemplateProvider         { get; init; }
    public ISmartLinkProvider?           SmartLinkProvider        { get; init; }
    public INotionPermissionProvider?    PermissionProvider       { get; init; }
    public INotionPublicShareProvider?   PublicShareProvider      { get; init; }
    public INotionAuditProvider?         AuditProvider            { get; init; }
    public IReadOnlyList<string>         CurrentUserGroupIds      { get; init; } = [];
    public INotionBookmarkProvider?      BookmarkProvider         { get; init; }
    public INotionFileProvider?          FileProvider             { get; init; }
    public INotionImportExportProvider?  ImportExportProvider     { get; init; }
    public IDiagramDocumentProvider?     DiagramDocumentProvider  { get; init; }
    public IWireframeDocumentProvider?    WireframeDocumentProvider  { get; init; }
    public ISpreadsheetDocumentProvider?  SpreadsheetDocumentProvider{ get; init; }
    public INotionSyncedBlockProvider?    SyncedBlockProvider        { get; init; }
    public INotionMediaLibraryProvider?   MediaLibraryProvider       { get; init; }
    public ITokenDataProvider?            TokenProvider              { get; init; }

    /// <summary>
    /// Optional document library used by the Tempo blocks (wireframe/diagram/spreadsheet) to offer
    /// "Insert existing…", render previews and refresh them. When null, only "Create" is offered.
    /// </summary>
    public Tempo.Blazor.DocumentLibrary.ITempoDocumentLibraryProvider? DocumentLibraryProvider { get; init; }

    /// <summary>
    /// Optional live-change notifier. When present, embedded Tempo blocks subscribe to their
    /// linked document and refresh automatically when it changes elsewhere.
    /// </summary>
    public Tempo.Blazor.DocumentLibrary.ITempoDocumentChangeNotifier? DocumentChangeNotifier { get; init; }

    /// <summary>Active collaboration sync service (null when CollaborationProvider is absent).</summary>
    public NotionCollaborationSync?        CollaborationSync         { get; init; }

    /// <summary>
    /// Navigates to the given page by ID. Supplied by TmNotionEditor and used by
    /// child page / linked page / breadcrumb blocks to trigger in-editor navigation.
    /// </summary>
    public Func<string, Task>?            NavigateTo               { get; init; }

    /// <summary>Currently selected workspace/space identifier for sidebar-scoped navigation.</summary>
    public string?                         SelectedSpaceId          { get; init; }

    /// <summary>Raised by the sidebar space switcher when the active space changes.</summary>
    public Func<string?, Task>?            SelectSpace              { get; init; }

    /// <summary>Raised after the active page is moved to another space.</summary>
    public Func<string, Task>?             CurrentPageMovedToSpace  { get; init; }

    /// <summary>Opens the new-page template gallery from the sidebar.</summary>
    public Func<Task>?                     OpenTemplateGallery      { get; init; }

    /// <summary>
    /// Raised when a block type is converted (e.g. via slash menu or toolbar).
    /// Nested components such as column blocks subscribe to keep their local
    /// child cache in sync with the remote store.
    /// </summary>
    public Action<IPageBlock>? BlockConverted;

    /// <summary>Invokes <see cref="BlockConverted"/> if any subscriber is attached.</summary>
    public void RaiseBlockConverted(IPageBlock block) => BlockConverted?.Invoke(block);

    /// <summary>Raised by nested blocks when they create a sibling block through the shared provider.</summary>
    public Func<IPageBlock, Task>? BlockCreated { get; set; }

    /// <summary>Invokes <see cref="BlockCreated"/> if the active page subscribed to sibling creation events.</summary>
    public Task RaiseBlockCreatedAsync(IPageBlock block) => BlockCreated?.Invoke(block) ?? Task.CompletedTask;

    /// <summary>
    /// When non-null, only these block types are available in the slash menu and type
    /// conversion ("Turn Into") menus. Existing blocks of other types still render normally.
    /// When null, all block types are allowed (default).
    /// </summary>
    public IReadOnlySet<BlockType>? AllowedBlockTypes { get; init; }

    /// <summary>Returns true when the given block type may be created or converted to.</summary>
    public bool IsBlockTypeAllowed(BlockType type)
        => AllowedBlockTypes is null || AllowedBlockTypes.Contains(type);
}
