using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.Notifications;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor;

/// <summary>
/// Root shell for the Notion-style editor.
/// Provides all NotionEditor providers as a <see cref="NotionEditorContext"/> cascade,
/// manages top-level navigation, sidebar visibility, and loading state.
/// </summary>
public partial class TmNotionEditor : ComponentBase, IAsyncDisposable
{
    // ── DI ───────────────────────────────────────────────────────────────────

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Required parameters ──────────────────────────────────────────────────

    /// <summary>Core page data provider (required).</summary>
    [Parameter, EditorRequired]
    public INotionDataProvider DataProvider { get; set; } = default!;

    /// <summary>Block read/write provider (required).</summary>
    [Parameter, EditorRequired]
    public INotionBlockProvider BlockProvider { get; set; } = default!;

    // ── Optional providers ───────────────────────────────────────────────────

    [Parameter] public INotionSearchProvider?        SearchProvider        { get; set; }
    [Parameter] public INotionDatabaseProvider?      DatabaseProvider      { get; set; }
    [Parameter] public INotionCommentProvider?       CommentProvider       { get; set; }
    [Parameter] public INotionHistoryProvider?       HistoryProvider       { get; set; }
    [Parameter] public INotionCollaborationProvider? CollaborationProvider { get; set; }
    [Parameter] public INotionMentionProvider?       MentionProvider       { get; set; }
    [Parameter] public INotionBookmarkProvider?      BookmarkProvider       { get; set; }
    [Parameter] public INotionFileProvider?          FileProvider           { get; set; }
    [Parameter] public INotionImportExportProvider?  ImportExportProvider   { get; set; }
    [Parameter] public IDiagramDocumentProvider?     DiagramDocumentProvider  { get; set; }
    [Parameter] public IWireframeDocumentProvider?   WireframeDocumentProvider   { get; set; }
    [Parameter] public ISpreadsheetDocumentProvider? SpreadsheetDocumentProvider { get; set; }
    [Parameter] public INotionSyncedBlockProvider?   SyncedBlockProvider         { get; set; }
    [Parameter] public INotionMediaLibraryProvider?  MediaLibraryProvider        { get; set; }
    [Parameter] public ITokenDataProvider?           TokenProvider               { get; set; }

    /// <summary>
    /// When non-null, restricts the slash menu and Turn Into menu to only these block types.
    /// Existing blocks of other types are still displayed. When null, all types are available.
    /// </summary>
    [Parameter] public IReadOnlySet<BlockType>? AllowedBlockTypes { get; set; }

    // ── Behaviour parameters ─────────────────────────────────────────────────

    /// <summary>Page ID to open on first render. Supports @bind-InitialPageId.</summary>
    [Parameter] public string? InitialPageId { get; set; }

    /// <summary>Show the left navigation sidebar.</summary>
    [Parameter] public bool ShowSidebar { get; set; } = true;

    /// <summary>Prevents all editing interactions.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Additional CSS class on the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised whenever the active page changes.</summary>
    [Parameter] public EventCallback<INotionPage> OnPageChanged { get; set; }

    /// <summary>Raised when the user requests to open the trash panel from the sidebar.</summary>
    [Parameter] public EventCallback OnTrashRequested { get; set; }

    /// <summary>
    /// Called when the user clicks "Create token" in the token dropdown.
    /// Arg = current search query (may be empty). Return the newly created
    /// token (Key, DisplayName, ColorClass) so the editor can insert it
    /// automatically, or <c>null</c> if the user cancelled.
    /// </summary>
    [Parameter] public Func<string, Task<(string Key, string DisplayName, string? ColorClass)?>?>? OnCreateTokenRequested { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private INotionPage?               _currentPage;
    private string?                    _currentPageId;
    private readonly Stack<string>     _navStack      = new();
    private bool                       _isLoading;
    private string?                    _loadError;
    private bool                       _sidebarVisible = true;
    private bool                       _sidebarOverlay;
    private bool                       _topbarScrolled;
    private NotionEditorContext        _context        = default!;
    private ElementReference           _rootRef;
    private ElementReference           _mainRef;
    private IJSObjectReference?        _jsScrollListener;
    private NotionCollaborationSync?   _collabSync;
    private TmNotificationBell?        _notificationBell;
    private bool                       _notificationPanelOpen;

    // ── Computed ─────────────────────────────────────────────────────────────

    private string _editorModifiers => string.Concat(
        _currentPage?.IsFullWidth == true ? " tm-notion-editor--full-width" : string.Empty,
        ReadOnly                           ? " tm-notion-editor--locked"     : string.Empty
    ).TrimStart();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        if (CollaborationProvider is not null)
            _collabSync = new NotionCollaborationSync();

        _context = BuildContext();
    }

    protected override void OnParametersSet()
    {
        _context = BuildContext();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitScrollListenerAsync();

            if (InitialPageId is not null)
            {
                await NavigateToPageAsync(InitialPageId);
            }
        }
    }

    // ── Public navigation API ────────────────────────────────────────────────

    /// <summary>
    /// Navigates to the given page and pushes it onto the navigation stack.
    /// Calling with the current page ID is a no-op.
    /// </summary>
    public async Task NavigateToPageAsync(string pageId)
    {
        if (_isLoading || pageId == _currentPageId) return;

        _isLoading  = true;
        _loadError  = null;
        StateHasChanged();

        try
        {
            var page = await DataProvider.GetPageAsync(pageId);
            _navStack.Push(pageId);
            _currentPageId = pageId;
            _currentPage   = page;

            if (_collabSync is not null && CollaborationProvider is not null)
                await _collabSync.JoinAsync(CollaborationProvider, pageId, "demo");

            await OnPageChanged.InvokeAsync(page);
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // ── Internal handlers ────────────────────────────────────────────────────

    private async Task NavigateBackAsync()
    {
        if (_navStack.Count <= 1) return;
        _navStack.Pop();
        var prevId = _navStack.Peek();
        _navStack.Pop();
        await NavigateToPageAsync(prevId);
    }

    private async Task RetryLoadAsync()
    {
        if (_currentPageId is null) return;
        var id = _currentPageId;
        _currentPageId = null;
        await NavigateToPageAsync(id);
    }

    private void ToggleSidebar()
    {
        _sidebarVisible = !_sidebarVisible;
        StateHasChanged();
    }

    private void OnNotificationDropdownOpenChanged(bool isOpen)
    {
        _notificationPanelOpen = isOpen;
        StateHasChanged();
    }

    private async Task OnChildPageUpdatedAsync(INotionPage updatedPage)
    {
        _currentPage = updatedPage;
        await OnPageChanged.InvokeAsync(updatedPage);
        StateHasChanged();
    }

    private async Task OnTrashRequestedAsync()
    {
        await OnTrashRequested.InvokeAsync();
    }

    // ── JS scroll spy ────────────────────────────────────────────────────────

    private async Task InitScrollListenerAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("tmNotionEditor.initSmoothScrollSpy",
                _mainRef,
                DotNetObjectReference.Create(this));
        }
        catch { /* JS not available (SSR / test) */ }
    }

    [JSInvokable]
    public void OnScrollSpyBlockChanged(string? blockId) { }

    // ── Topbar scroll detection ───────────────────────────────────────────────

    [JSInvokable]
    public void OnMainScrolled(bool scrolled)
    {
        if (_topbarScrolled == scrolled) return;
        _topbarScrolled = scrolled;
        StateHasChanged();
    }

    // ── Context builder ──────────────────────────────────────────────────────

    private NotionEditorContext BuildContext() => new()
    {
        DataProvider          = DataProvider,
        BlockProvider         = BlockProvider,
        SearchProvider        = SearchProvider,
        DatabaseProvider      = DatabaseProvider,
        CommentProvider       = CommentProvider,
        HistoryProvider       = HistoryProvider,
        CollaborationProvider = CollaborationProvider,
        CollaborationSync     = _collabSync,
        MentionProvider       = MentionProvider,
        BookmarkProvider          = BookmarkProvider,
        FileProvider              = FileProvider,
        ImportExportProvider      = ImportExportProvider,
        DiagramDocumentProvider   = DiagramDocumentProvider,
        WireframeDocumentProvider   = WireframeDocumentProvider,
        SpreadsheetDocumentProvider = SpreadsheetDocumentProvider,
        SyncedBlockProvider         = SyncedBlockProvider,
        MediaLibraryProvider        = MediaLibraryProvider,
        TokenProvider               = TokenProvider,
        AllowedBlockTypes           = AllowedBlockTypes,
        NavigateTo                = pageId => NavigateToPageAsync(pageId)
    };

    // ── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_collabSync is not null)
        {
            try { await _collabSync.DisposeAsync(); } catch { }
        }
        if (_jsScrollListener is not null)
        {
            try { await _jsScrollListener.DisposeAsync(); } catch { }
        }
    }
}
