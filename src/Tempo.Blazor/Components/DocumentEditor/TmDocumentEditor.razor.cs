using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.DocumentEditor.Clipboard;
using Tempo.Blazor.Components.DocumentEditor.Commands;
using Tempo.Blazor.Components.DocumentEditor.Features;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Word-like document editor shell backed by the document editor JSON provider contracts.</summary>
public partial class TmDocumentEditor : ComponentBase, IDisposable, IAsyncDisposable
{
    private readonly DocumentEditorCommandStack _commandStack = new();
    private readonly DocumentEditorKeyboardManager _keyboardManager = new();
    private readonly DocumentFloatingLayerStack _floatingLayerStack = new();
    private readonly DocumentEditorFocusManager _focusManager = new();
    private readonly DocumentEditorAnnouncer _announcer = new();
    private readonly DocumentPendingActionService _pendingActions = new();
    private readonly DocumentAutosaveStateMachine _autosave = new();
    private readonly DocumentEditorFeatureRegistry _featureRegistry = DocumentEditorBuiltInFeatures.CreateDefaultRegistry();
    private const long MaxDocumentFormatImportSize = 20 * 1024 * 1024;
    private Timer? _autoSaveTimer;
    private Timer? _collaborationTimer;
    private TimeSpan? _configuredAutoSaveInterval;
    private TimeSpan? _configuredCollaborationInterval;

    /// <summary>Stable document id to load from the provider.</summary>
    [Parameter, EditorRequired] public string DocumentId { get; set; } = string.Empty;

    /// <summary>Document editor provider used for load/save/version/comment operations.</summary>
    [Parameter] public IDocumentEditorProvider? Provider { get; set; }

    /// <summary>Whether the editor is read-only.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Host-controlled permissions for the current user.</summary>
    [Parameter] public DocumentEditorPermissions Permissions { get; set; } = new();

    /// <summary>Current editor mode.</summary>
    [Parameter] public DocumentEditorMode Mode { get; set; } = DocumentEditorMode.Edit;

    /// <summary>Whether the Word-like toolbar/ribbon is displayed.</summary>
    [Parameter] public bool ShowToolbar { get; set; } = true;

    /// <summary>Visual mode used by the document editor toolbar.</summary>
    [Parameter] public DocumentToolbarMode ToolbarMode { get; set; } = DocumentToolbarMode.Ribbon;

    /// <summary>Whether the comments rail is displayed.</summary>
    [Parameter] public bool ShowComments { get; set; } = true;

    /// <summary>Whether the version history panel is displayed.</summary>
    [Parameter] public bool ShowVersionHistory { get; set; } = true;

    /// <summary>Whether debug tools (View JSON etc.) are shown in the toolbar. Intended for development only.</summary>
    [Parameter] public bool ShowDebugTools { get; set; }

    /// <summary>Feature names that should be disabled for this editor instance.</summary>
    [Parameter] public IReadOnlyCollection<string>? DisabledFeatures { get; set; }

    /// <summary>Optional resolver for provider-managed document image assets.</summary>
    [Parameter] public IDocumentImageUrlResolver? ImageUrlResolver { get; set; }

    /// <summary>Optional image provider used by upload and clipboard image flows.</summary>
    [Parameter] public IDocumentImageProvider? ImageProvider { get; set; }

    /// <summary>Provider-managed image assets that can be inserted from the editor image menu.</summary>
    [Parameter] public IReadOnlyList<DocumentImageAsset> ImageAssetOptions { get; set; } = [];

    /// <summary>Canvas content-control rendering mode. Use <c>form</c> for plain fill mode or <c>design</c> for structured-tag chrome.</summary>
    [Parameter] public string? ContentControlRenderMode { get; set; }

    /// <summary>Image validation options used by upload and clipboard image flows.</summary>
    [Parameter] public DocumentImageValidationOptions ImageValidation { get; set; } = new();

    /// <summary>Whether track changes starts enabled.</summary>
    [Parameter] public bool TrackChangesEnabled { get; set; }

    /// <summary>Whether provider-backed suggestions start enabled.</summary>
    [Parameter] public bool SuggestionsEnabled { get; set; }

    /// <summary>Optional autosave interval. Set to <c>null</c> to disable autosave.</summary>
    [Parameter] public TimeSpan? AutoSaveInterval { get; set; }

    /// <summary>Whether major versions require a non-empty description.</summary>
    [Parameter] public bool RequireMajorVersionDescription { get; set; }

    /// <summary>Whether the current user can add comments and replies.</summary>
    [Parameter] public bool CanComment { get; set; } = true;

    /// <summary>Whether the current user can resolve or reopen comment threads.</summary>
    [Parameter] public bool CanResolveComments { get; set; } = true;

    /// <summary>Whether the current user can delete their own comments.</summary>
    [Parameter] public bool CanDeleteOwnComments { get; set; }

    /// <summary>Optional mention provider reused by the comment composer.</summary>
    [Parameter] public IMentionDataProvider? MentionProvider { get; set; }

    /// <summary>Optional token provider reused by the document token autocomplete menu.</summary>
    [Parameter] public ITokenDataProvider? TokenProvider { get; set; }

    /// <summary>Optional provider used to resolve template token values for preview.</summary>
    [Parameter] public IDocumentTokenValueProvider? TokenValueProvider { get; set; }

    /// <summary>Optional provider for available editor font families.</summary>
    [Parameter] public IDocumentFontProvider? FontProvider { get; set; }

    /// <summary>Optional host provider used to export the current document as PDF.</summary>
    [Parameter] public IDocumentPdfExportProvider? PdfExportProvider { get; set; }

    /// <summary>Optional host provider used to import and export external document formats.</summary>
    [Parameter] public IDocumentFormatProvider? FormatProvider { get; set; }

    /// <summary>Optional host provider used to compare arbitrary document sources.</summary>
    [Parameter] public IDocumentComparisonProvider? ComparisonProvider { get; set; }

    /// <summary>Whether local comparison should be used if the comparison provider fails.</summary>
    [Parameter] public bool UseLocalComparisonFallback { get; set; } = true;

    /// <summary>Optional provider used to store and review document suggestions.</summary>
    [Parameter] public IDocumentSuggestionProvider? SuggestionProvider { get; set; }

    /// <summary>Optional proofing options used by the canvas engine spelling overlay.</summary>
    [Parameter] public DocumentProofingOptions? ProofingOptions { get; set; }

    /// <summary>Optional provider used to synchronize realtime collaborative edits.</summary>
    [Parameter] public IDocumentCollaborationProvider? CollaborationProvider { get; set; }

    /// <summary>Stable collaboration client id for the current editor instance.</summary>
    [Parameter] public string? CollaborationClientId { get; set; }

    /// <summary>Interval used to poll provider-backed collaboration updates.</summary>
    [Parameter] public TimeSpan CollaborationSyncInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Offline draft behavior for the editor. Defaults to disabled.</summary>
    [Parameter] public DocumentEditorOfflineMode OfflineMode { get; set; } = DocumentEditorOfflineMode.Disabled;

    /// <summary>
    /// Selects the rendering/editing engine. As of the canvas phase 25 cutover the default is
    /// <see cref="DocumentEditorRenderEngine.CanvasEnginePreview"/> after the phase 24 parity
    /// gate passed for legacy 0-23 plus E1-E12. Pass
    /// <see cref="DocumentEditorRenderEngine.Legacy"/> for the reversible rollback path during
    /// soak; <see cref="DocumentEditorRenderEngine.CoreEnginePreview"/> remains available as a
    /// diagnostic engine until phase 26 makes an explicit removal/retention decision.
    /// </summary>
    [Parameter] public DocumentEditorRenderEngine RenderEngine { get; set; } = DocumentEditorRenderEngine.CanvasEnginePreview;

    /// <summary>
    /// The engine actually in effect. Until the core engine reaches hosted-interop parity
    /// (the R.4.8 cutover gate), <see cref="DocumentEditorRenderEngine.CoreEnginePreview"/>
    /// resolves to <see cref="DocumentEditorRenderEngine.Legacy"/> so requesting the
    /// preview can never leave the editor non-functional.
    /// </summary>
    internal DocumentEditorRenderEngine EffectiveRenderEngine =>
        DocumentEditorRenderEngineFlag.Resolve(RenderEngine, CoreEngineHostedInteropReady);

    /// <summary>
    /// Whether the core engine is wired into the editor's render path well enough to honor
    /// an explicit <see cref="DocumentEditorRenderEngine.CoreEnginePreview"/> request: it
    /// renders the document (positioned-DOM), accepts keyboard/IME editing, and round-trips
    /// the model back to C# (verified by the <c>R49</c> WASM bridge E2E). It is still a
    /// PREVIEW — toolbar command routing, autosave, collaboration, and advanced features are
    /// not yet wired through the new engine, so it must not become the default until full
    /// parity (the R.4.8 finale; see <c>planning/r48-cutover-plan.md</c>). The default
    /// remains <see cref="DocumentEditorRenderEngine.Legacy"/>, so nothing changes unless a
    /// host explicitly opts in.
    /// </summary>
    internal const bool CoreEngineHostedInteropReady = true;

    /// <summary>Optional offline draft store used when <see cref="OfflineMode"/> is enabled.</summary>
    [Parameter] public IDocumentOfflineStore? OfflineStore { get; set; }

    /// <summary>Optional provider used to submit an offline draft back to the authoritative store.</summary>
    [Parameter] public IDocumentSyncProvider? SyncProvider { get; set; }

    /// <summary>Whether a newer local draft should replace the server snapshot during load.</summary>
    [Parameter] public bool PreferLocalDraft { get; set; }

    /// <summary>Author used for save requests and audit events.</summary>
    [Parameter] public DocumentEditorAuthor? Author { get; set; }

    /// <summary>Optional audit sink used to record document save events.</summary>
    [Parameter] public IDocumentAuditSink? AuditSink { get; set; }

    /// <summary>Determines whether audit sink failures block editor workflows.</summary>
    [Parameter] public DocumentEditorAuditFailureMode AuditFailureMode { get; set; } = DocumentEditorAuditFailureMode.NonBlocking;

    /// <summary>Raised immediately before a save request is sent to the provider.</summary>
    [Parameter] public EventCallback<DocumentEditorSaveRequest> OnSaveRequested { get; set; }

    /// <summary>Additional CSS class for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Raised after a document was loaded successfully.</summary>
    [Parameter] public EventCallback<DocumentEditorDocument> OnDocumentLoaded { get; set; }

    /// <summary>Raised after a document version was created successfully.</summary>
    [Parameter] public EventCallback<DocumentVersion> OnVersionCreated { get; set; }

    /// <summary>Raised after the optional PDF export provider returns a result.</summary>
    [Parameter] public EventCallback<DocumentPdfExportResult> OnPdfExported { get; set; }

    /// <summary>Raised after the optional document format provider returns an export result.</summary>
    [Parameter] public EventCallback<DocumentFormatExportProviderResult> OnDocumentFormatExported { get; set; }

    /// <summary>Raised after a document comparison completes.</summary>
    [Parameter] public EventCallback<DocumentCompareResult> OnDocumentCompared { get; set; }

    /// <summary>Additional HTML attributes for the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>JavaScript runtime used by provider-backed download bridges.</summary>
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private DocumentEditorDocument? _document;
    private TmDocumentEditorToolbar? _toolbar;
    private TmDocumentWysiwygHost? _wysiwygHost;
    private TmDocumentCoreEngineHost? _coreHost; // R.4.8 — used when RenderEngine == CoreEnginePreview
    private TmDocumentCanvasEngineHost? _canvasHost;
    private TmDocumentSidePanel? _sidePanel;
    private bool _coreCanUndo;                   // R.4.8 — core host undo/redo state, synced into EffectiveUndoState
    private bool _coreCanRedo;
    private bool _canvasCanUndo;
    private bool _canvasCanRedo;
    private bool _coreImageDialogOpen;           // R.4.8 — minimal image-URL dialog for the core engine
    private string _coreImageUrl = string.Empty;
    private TmDocumentCoreEngineHost.CoreObjectSelection? _coreSelectedObject; // R.4.8 — engine image selection
    private ImageBlockContent? _coreActiveImage;  // R.4.8 — drives the inspector in core mode
    private string _coreBlockStyle = "Normal";    // R.4.8 — block-style picker value (core engine)
    private TmDocumentCoreEngineHost.CoreContextMenuRequest? _coreContextMenu; // R.5.23 — right-click menu
    private DocumentEditorSelectionState _selection = new();
    private DocumentEditorSelectionContext _selectionContext = DocumentEditorSelectionContext.Empty;
    private string? _activeImageInspectorBlockId;
    private string? _activeCanvasImageObjectId;
    private string? _activeCanvasImageBlockId;
    private string? _activeCanvasImageRunId;
    private string? _errorMessage;
    private string? _saveMessage;
    private string? _versionMessage;
    private string? _commentMessage;
    private string? _suggestionMessage;
    private string? _revisionMessage;
    private string? _templatePreviewMessage;
    private string? _runtimeMessage;
    private bool _runtimeFailed;
    private WysiwygRuntimeRecoveryDetail? _lastRuntimeRecoveryDetail;
    private WysiwygFormattingState _formattingState = new();
    private long _lastFormattingStateVersion;
    private DocumentTextAlignment? _pendingParagraphAlignment;
    private string? _pendingParagraphAlignmentBlockId;
    private DateTimeOffset _pendingParagraphAlignmentExpiresAt;
    private double? _pendingLineSpacing;
    private string? _pendingLineSpacingBlockId;
    private DateTimeOffset _pendingLineSpacingExpiresAt;
    private string? _concurrencyToken;
    private DateTimeOffset? _lastSavedAt;
    private DocumentEditorDocument? _currentDocument;
    private DocumentEditorDocument? _compareDocumentSnapshot;
    private DocumentEditorDocument? _templatePreviewDocument;
    private IReadOnlyList<DocumentVersion> _versions = [];
    private IReadOnlyList<DocumentComment> _comments = [];
    private IReadOnlyList<DocumentSuggestion> _suggestions = [];
    private IReadOnlyList<DocumentFontFamily> _fontFamilies = [];
    private DocumentVersion? _previewVersion;
    private DocumentCommentAnchor? _draftCommentAnchor;
    private bool _isLoading;
    private bool _isSaving;
    private bool _saveAgainRequested;
    private bool _canRetrySave;
    private DocumentEditorSaveTrigger _lastFailedSaveTrigger = DocumentEditorSaveTrigger.Explicit;
    private bool _isCreatingVersion;
    private bool _isExportingPdf;
    private bool _isImportingDocx;
    private bool _isExportingDocx;
    private bool _isLoadingVersions;
    private bool _isLoadingComments;
    private bool _isLoadingSuggestions;
    private bool _isSubmittingComment;
    private bool _isReviewingSuggestion;
    private bool _isReviewingRevision;
    private bool _isDirty;
    private bool _trackChangesEnabled;
    private bool _suggestionsEnabled;
    private bool _templatePreviewEnabled;
    private bool _versionDialogOpen;
    private bool _compareDialogOpen;
    private bool _sidePanelOpen = true;
    private DocumentSidePanelTab _activeSidePanelTab = DocumentSidePanelTab.Versions;
    private bool _sidePanelTabManuallySelected;
    private bool _commentComposerOpen;
    private string? _selectedCommentId;
    private string? _selectedRevisionId;
    private DocumentCommentFilter _commentFilter = DocumentCommentFilter.All;
    private DocumentCommentSortMode _commentSortMode = DocumentCommentSortMode.Position;
    private string? _loadedDocumentId;
    private IDocumentEditorProvider? _loadedProvider;
    private DocumentOfflineDraft? _offlineDraft;
    private DocumentSyncConflict? _offlineConflict;
    private DocumentSyncStatus _offlineStatus = DocumentSyncStatus.Online;
    private string? _offlineMessage;
    private bool _isSyncingOfflineDraft;
    private DocumentCollaborationSync? _collaborationSync;
    private IDocumentCollaborationProvider? _loadedCollaborationProvider;
    private IDocumentCollaborationRealtimeProvider? _realtimeCollaborationProvider;
    private IDocumentSuggestionProvider? _loadedSuggestionProvider;
    private IDocumentFormatProvider? _loadedFormatProvider;
    private IDocumentFontProvider? _loadedFontProvider;
    private IReadOnlyList<DocumentFormatProviderCapability> _formatCapabilities = [];
    private List<DocumentFormatProviderWarning> _formatWarnings = [];
    private string? _formatMessage;
    private DocumentEditorDocument? _collaborationSnapshot;
    private IReadOnlyList<DocumentCollaborationCursor> _remoteCursors = [];
    private readonly string _generatedCollaborationClientId = Guid.NewGuid().ToString("N");
    private string? _activeCollaborationClientId;
    private bool _isRefreshingCollaboration;
    private bool _suppressCollaborationBroadcast;
    private DocumentEditorDocument? _suggestionSnapshot;
    private bool _disposed;
    private string? _activeWysiwygTransactionId;
    private bool _suppressCommandStackChangedRender;
    private DocumentReviewDisplayMode _reviewDisplayMode = DocumentReviewDisplayMode.AllMarkup;
    private readonly IDocumentFontProvider _fallbackFontProvider = new InMemoryDocumentFontProvider();
    private string _activeWysiwygRegion = "Body";
    private WysiwygSelectionSnapshot? _lastWysiwygSelectionSnapshot;
    private WysiwygSelectionSnapshot? _lastBodySelectionSnapshot;
    private WysiwygSelectionSnapshot? _lastBodyRangeSelectionSnapshot;
    private WysiwygSelectionSnapshot? _pendingLinkSelectionSnapshot;
    private bool _showRuler = true;
    private int _zoomPercent = 100;
    private bool _zoomPageWidth = true;
    private string _canvasViewMode = "print";
    private bool _canvasPrintPreviewActive;
    private bool _ribbonKeyboardMode;
    private bool _findPanelOpen;
    private bool _findReplaceMode;
    private bool _commandPaletteOpen;
    private TmDocumentFindPanel? _findPanel;
    private bool _focusSidePanelOnRender;
    private bool _focusDocumentOnRender;
    private WysiwygTextContextMenuRequest? _textContextMenu;
    private WysiwygTableContextMenuRequest? _tableContextMenu;
    private WysiwygMiniToolbarRequest? _miniToolbar;
    private WysiwygMiniToolbarRequest? _lastMiniToolbarRequest;
    private CanvasContentControlPopoverState? _activeCanvasContentControl;
    private bool _miniToolbarColorPickerOpen;
    private DateTimeOffset _keepMiniToolbarVisibleUntil;
    private DateTimeOffset _ignoreFloatingCollapsedSelectionUntil;
    private string? _optimisticFloatingTextColor;
    private string? _optimisticFloatingHighlightColor;
    private string? _optimisticFloatingFontSize;
    private string? _optimisticFloatingFormattingSelectionKey;
    private DateTimeOffset _optimisticFloatingFormattingExpiresAt;
    private WysiwygUndoState _wysiwygUndoState = new();
    private WysiwygDirtyState _wysiwygDirtyState = new();
    private string? _runtimeDraftStateJson;
    private bool _isDocumentProtected;
    private bool _isCaretInEditableRegion;
    private bool _showBlocks;
    private bool _showNonPrintingCharacters;
    private bool _isFullscreen;
    private IReadOnlyList<DocumentOutlineItem> _documentOutline = [];
    private WysiwygPageMetrics _pageMetrics = new() { TotalPages = 1, RenderedPages = 1, Pages = [new WysiwygPageMetric { PageIndex = 0, PageNumber = 1 }] };
    private int _activePageIndex;
    private string? _activeHeadingBlockId;
    private readonly DocumentOutlineService _outlineService = new();
    private bool _jsonDebugModalOpen;
    private bool _clipboardHtmlModalOpen;
    private string? _clipboardHtmlSnapshot;
    private string? _runtimeDebugJson;
    private string? _clipboardDebugRawHtml;
    private string? _clipboardDebugNormalizedJson;
    private string? _clipboardDebugWarningsJson;
    private int _blazorRenderCount;
    private bool _suppressNextWysiwygStateRender;
    private string? _lastCollapsedSelectionRenderKey;

    private int NextBlazorRenderCount => ++_blazorRenderCount;

    private string? LiveRegionMessage => _announcer.CurrentMessage;

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        if (_suppressNextWysiwygStateRender)
        {
            _suppressNextWysiwygStateRender = false;
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusSidePanelOnRender && _sidePanelOpen && _sidePanel is not null)
        {
            _focusSidePanelOnRender = false;
            await _sidePanel.FocusActiveTabAsync();
        }

        if (_focusDocumentOnRender)
        {
            _focusDocumentOnRender = false;
            await FocusDocumentAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _commandStack.OnStackChanged += HandleCommandStackChanged;
        _focusManager.Register(new DocumentEditorFocusTarget
        {
            Id = "surface",
            Kind = DocumentEditorFocusTargetKind.Surface,
            Selector = "[data-testid='document-wysiwyg-host']"
        });
        _focusManager.Register(new DocumentEditorFocusTarget
        {
            Id = "toolbar",
            Kind = DocumentEditorFocusTargetKind.Toolbar,
            Selector = "[data-testid='document-toolbar']"
        });
        InitializeCommandRegistry();
    }

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-document-editor" };
            if (EffectiveReadOnly)
            {
                classes.Add("tm-document-editor--readonly");
            }

            if (!ShowToolbar || CanvasReadingMode)
            {
                classes.Add("tm-document-editor--no-toolbar");
            }

            if (CanvasReadingMode)
            {
                classes.Add("tm-document-editor--canvas-reading");
            }

            if (CanvasPrintPreviewActive)
            {
                classes.Add("tm-document-editor--canvas-print-preview");
            }

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class!);
            }

            return string.Join(" ", classes);
        }
    }

    private string DocumentTitle => string.IsNullOrWhiteSpace(_document?.Metadata.Title)
        ? Loc["TmDocumentEditor_UntitledDocument"]
        : _document!.Metadata.Title;

    private bool IsDocumentEmpty => _document is null || _document.Blocks.Count == 0;

    private bool IsVersionPreview => _previewVersion is not null;

    private DocumentEditorDocument? DisplayedDocument => _templatePreviewEnabled
        ? _templatePreviewDocument ?? _document
        : _document;

    private DocumentDrawingRun? ActiveImageDrawingRun => FindDrawingRunByObjectId(DisplayedDocument, ActiveImageInspectorObjectId);

    private ImageBlockContent? ActiveImageContent => CreateImageBlockContentFromDrawingRun(ActiveImageDrawingRun);

    private string? ActiveImageInspectorObjectId =>
        !string.IsNullOrWhiteSpace(_selection.ObjectSelection?.ObjectId)
            ? _selection.ObjectSelection.ObjectId
            : !string.IsNullOrWhiteSpace(_selection.ActiveObjectId)
                ? _selection.ActiveObjectId
                : _activeCanvasImageObjectId;

    private string? ActiveImageInspectorBlockId =>
        !string.IsNullOrWhiteSpace(_selection.ObjectSelection?.AnchorBlockId)
            ? _selection.ObjectSelection.AnchorBlockId
            : !string.IsNullOrWhiteSpace(_activeImageInspectorBlockId)
                ? _activeImageInspectorBlockId
                : _activeCanvasImageBlockId;

    private string? ActiveImageCommandObjectId =>
        UsingCanvasEngine && !string.IsNullOrWhiteSpace(_activeCanvasImageObjectId)
            ? _activeCanvasImageObjectId
            : ActiveImageInspectorObjectId;

    private string? ActiveImageCommandBlockId =>
        UsingCanvasEngine && !string.IsNullOrWhiteSpace(_activeCanvasImageBlockId)
            ? _activeCanvasImageBlockId
            : ActiveImageInspectorBlockId;

    private string? ActiveImageCommandRunId =>
        UsingCanvasEngine && !string.IsNullOrWhiteSpace(_activeCanvasImageRunId)
            ? _activeCanvasImageRunId
            : null;

    private DocumentBlock? ActiveTableBlock =>
        DisplayedDocument?.Blocks.FirstOrDefault(block =>
            block.Content is TableBlockContent table
            && (string.Equals(block.Id, _selectionContext.ActiveTableId, StringComparison.Ordinal)
                || string.Equals(block.Id, _selection.ActiveTableId, StringComparison.Ordinal)
                || string.Equals(block.Id, _selection.ActiveBlockId, StringComparison.Ordinal)
                || TableContainsCell(table, _selectionContext.ActiveTableCellId ?? _selection.ActiveTableCellId)));

    private TableBlockContent? ActiveTableContent => ActiveTableBlock?.Content as TableBlockContent;

    private TableCellContent? ActiveTableCell => string.IsNullOrWhiteSpace(_selectionContext.ActiveTableCellId ?? _selection.ActiveTableCellId)
        ? null
        : ActiveTableContent?.Rows
            .SelectMany(row => row.Cells)
            .FirstOrDefault(cell => string.Equals(cell.Id, _selectionContext.ActiveTableCellId ?? _selection.ActiveTableCellId, StringComparison.Ordinal));

    private bool IsTemplatePreview => _templatePreviewEnabled;

    private DocumentEditorPermissions EffectivePermissions => Permissions ?? new DocumentEditorPermissions();

    private bool CanReadDocument => EffectivePermissions.CanRead;

    private bool CanEditDocument => EffectivePermissions.CanEdit && !ReadOnly && !IsVersionPreview && !IsTemplatePreview;

    private bool EffectiveReadOnly => !CanEditDocument;

    private IReadOnlyCollection<string> EffectiveDisabledFeatures => DisabledFeatures ?? [];

    private bool IsFeatureEnabled(string featureName) =>
        _featureRegistry.TryGet(featureName, out _)
        && (DisabledFeatures is null
            || !DisabledFeatures.Any(disabledFeature => string.Equals(disabledFeature, featureName, StringComparison.OrdinalIgnoreCase)));

    private bool IsCommandEnabled(string commandName, bool fallback = true) =>
        _commandRegistry.GetState(commandName)?.IsEnabled ?? fallback;

    private bool CanCreateVersion => EffectivePermissions.CanCreateVersion
        && Provider is not null
        && _currentDocument is not null
        && !IsVersionPreview
        && !IsTemplatePreview;

    private bool CanViewAudit => EffectivePermissions.CanViewAudit;

    private string DocumentStatus => IsVersionPreview && _previewVersion is not null
        ? Loc["TmDocumentEditor_PreviewingVersion", GetVersionTitle(_previewVersion)]
        : IsTemplatePreview
            ? Loc["TmDocumentEditor_TemplatePreviewOn"]
            : Loc["TmDocumentEditor_StatusLoaded"];

    private bool CanUseComments => ShowComments
        && CanComment
        && CanEditDocument
        && IsFeatureEnabled(DocumentEditorFeatureNames.Comments)
        && EffectivePermissions.CanComment
        && Provider is not null
        && _document is not null
        && !IsVersionPreview
        && !IsTemplatePreview;

    private bool CanStartComment => CanUseComments && (_selection.ActiveBlockId is not null || _document?.Blocks.Count > 0);

    private bool CanPreviewTemplate => TokenValueProvider is not null && _document is not null && !IsVersionPreview;

    private bool CanExportPdf => EffectivePermissions.CanExport && PdfExportProvider is not null && _document is not null && !IsVersionPreview;

    private bool CanImportDocx => FormatProvider is not null
        && EffectivePermissions.CanImport
        && CanEditDocument
        && _document is not null
        && _formatCapabilities.Any(capability =>
            capability.Format == DocumentFormatProviderKind.Docx && capability.CanImport);

    private bool CanExportDocx => FormatProvider is not null
        && EffectivePermissions.CanExport
        && _document is not null
        && !IsVersionPreview
        && !IsTemplatePreview
        && _formatCapabilities.Any(capability =>
            capability.Format == DocumentFormatProviderKind.Docx && capability.CanExport);

    private bool CanCompareDocuments => _document is not null
        && EffectivePermissions.CanRead
        && Provider is not null
        && !IsVersionPreview
        && !IsTemplatePreview;

    private bool CanCreateSuggestions => SuggestionProvider is not null
        && EffectivePermissions.CanSuggest
        && EffectivePermissions.CanComment
        && _document is not null
        && CanEditDocument
        && !IsVersionPreview
        && !IsTemplatePreview;

    private bool CanReviewSuggestions => SuggestionProvider is not null
        && EffectivePermissions.CanReviewSuggestions
        && EffectivePermissions.CanComment
        && CanEditDocument
        && _document is not null
        && !IsVersionPreview
        && !IsTemplatePreview;

    private bool CanUseSuggestions => CanCreateSuggestions;

    private bool CanReviewRevisions => CanEditDocument
        && _document is not null
        && !IsVersionPreview
        && !IsTemplatePreview;

    private bool HasPendingRevisions => _document?.Revisions.Any(revision => revision.Action == DocumentRevisionAction.Pending) == true;

    private int PendingRevisionCount => _document?.Revisions.Count(revision => revision.Action == DocumentRevisionAction.Pending) ?? 0;

    private int OpenCommentCount => _comments.Count(comment => comment.Status == DocumentCommentStatus.Open);

    private string WorkspaceClass => _sidePanelOpen
        ? "tm-document-editor__workspace tm-document-editor__workspace--side-panel-open"
        : "tm-document-editor__workspace tm-document-editor__workspace--side-panel-closed";

    private DocumentSidePanelTab ActiveSidePanelTab => NormalizeSidePanelTab(_activeSidePanelTab);

    private string ActiveSidePanelDataValue => ActiveSidePanelTab.ToString().ToLowerInvariant();

    private bool EffectiveTrackChangesEnabled => _trackChangesEnabled;

    private DocumentSection? ActiveSection => DisplayedDocument?.Sections.OrderBy(section => section.Order).FirstOrDefault();

    private bool DifferentFirstPageHeaderFooter => ActiveSection?.Properties.DifferentFirstPage == true;

    private bool DifferentOddAndEvenHeaderFooter => ActiveSection?.Properties.DifferentOddAndEvenPages == true;

    private DocumentPageSettings ActivePageSettings =>
        ActiveSection?.Properties.PageSettings
        ?? DisplayedDocument?.PageSettings
        ?? new DocumentPageSettings();

    private DocumentSectionColumns ActiveSectionColumns =>
        ActiveSection?.Properties.Columns
        ?? new DocumentSectionColumns();

    private DocumentLineNumbering ActiveLineNumbering =>
        ActiveSection?.Properties.LineNumbering
        ?? new DocumentLineNumbering();

    private WysiwygUndoState EffectiveUndoState => new()
    {
        CanUndo = _wysiwygUndoState.CanUndo || _commandStack.CanUndo || _coreCanUndo || _canvasCanUndo,
        CanRedo = _wysiwygUndoState.CanRedo || _commandStack.CanRedo || _coreCanRedo || _canvasCanRedo,
        UndoDepth = _wysiwygUndoState.UndoDepth + (_commandStack.CanUndo ? 1 : 0),
        RedoDepth = _wysiwygUndoState.RedoDepth + (_commandStack.CanRedo ? 1 : 0),
        NextUndoDescription = _wysiwygUndoState.CanUndo
            ? _wysiwygUndoState.NextUndoDescription
            : _commandStack.NextUndoDescription,
        NextRedoDescription = _wysiwygUndoState.CanRedo
            ? _wysiwygUndoState.NextRedoDescription
            : _commandStack.NextRedoDescription,
        Epoch = _wysiwygUndoState.Epoch,
        PendingTransactionId = _wysiwygUndoState.PendingTransactionId,
        LastTransactionId = _wysiwygUndoState.LastTransactionId,
        JsOwnedUndo = _wysiwygUndoState.JsOwnedUndo
    };

    private string ActiveHeaderFooterScopeLabel
    {
        get
        {
            var labelPrefix = string.Equals(_activeWysiwygRegion, "Footer", StringComparison.OrdinalIgnoreCase)
                ? Loc["TmDocumentEditor_RegionFooter"].ToString()
                : Loc["TmDocumentEditor_RegionHeader"].ToString();
            var headerFooter = _document is null
                ? null
                : DocumentHeaderFooterResolver.FindById(_document, _selection.HeaderFooterId);
            var scope = headerFooter?.Scope ?? DocumentHeaderFooterScope.Primary;
            var scopeLabel = scope switch
            {
                DocumentHeaderFooterScope.FirstPage => Loc["TmDocumentEditor_HeaderFooterScopeFirstPage"].ToString(),
                DocumentHeaderFooterScope.EvenPages => Loc["TmDocumentEditor_HeaderFooterScopeEvenPages"].ToString(),
                DocumentHeaderFooterScope.OddPages => Loc["TmDocumentEditor_HeaderFooterScopeOddPages"].ToString(),
                _ => Loc["TmDocumentEditor_HeaderFooterScopePrimary"].ToString()
            };

            return string.Create(CultureInfo.CurrentCulture, $"{labelPrefix} - {scopeLabel}");
        }
    }

    private int DocumentWordCount => CountWords(DisplayedDocument);

    private int DocumentPageCount => CountPages(DisplayedDocument);

    private string ActiveRegionLabel => _activeWysiwygRegion switch
    {
        "Header" => Loc["TmDocumentEditor_RegionHeader"],
        "Footer" => Loc["TmDocumentEditor_RegionFooter"],
        "Caption" => Loc["TmDocumentEditor_RegionCaption"],
        "Footnote" => Loc["TmDocumentEditor_RegionFootnote"],
        "Endnote" => Loc["TmDocumentEditor_RegionEndnote"],
        _ => Loc["TmDocumentEditor_RegionBody"]
    };

    private string ZoomStatusLabel => _zoomPageWidth
        ? Loc["TmDocumentEditor_ZoomPageWidth"]
        : string.Create(CultureInfo.InvariantCulture, $"{_zoomPercent}%");

    private bool CanvasReadingMode
        => EffectiveRenderEngine == DocumentEditorRenderEngine.CanvasEnginePreview
            && string.Equals(_canvasViewMode, "reading", StringComparison.OrdinalIgnoreCase);

    private bool CanvasPrintPreviewActive
        => EffectiveRenderEngine == DocumentEditorRenderEngine.CanvasEnginePreview && _canvasPrintPreviewActive;

    private bool CanvasContentControlPopoverVisible
        => _activeCanvasContentControl is not null
           && UsingCanvasEngine
           && !CanvasReadingMode
           && !CanvasPrintPreviewActive;

    private bool OfflineEnabled => OfflineMode == DocumentEditorOfflineMode.Enabled && OfflineStore is not null;

    private bool ShowOfflineBanner => OfflineEnabled
        && (_offlineDraft is not null || _offlineConflict is not null || _offlineStatus != DocumentSyncStatus.Online);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        _trackChangesEnabled = TrackChangesEnabled || _trackChangesEnabled;
        _suggestionsEnabled = SuggestionProvider is not null && (SuggestionsEnabled || _suggestionsEnabled);
        if (SuggestionProvider is null)
        {
            _suggestionsEnabled = false;
        }

        await RefreshFormatCapabilitiesAsync();
        await RefreshFontFamiliesAsync();
        ConfigureAutoSaveTimer();

        if (Provider is null)
        {
            await StopCollaborationAsync();
            _document = null;
            _errorMessage = null;
            _loadedDocumentId = null;
            _loadedProvider = null;
            _currentDocument = null;
            _previewVersion = null;
            _versions = [];
            _comments = [];
            _suggestions = [];
            _suggestionSnapshot = null;
            _draftCommentAnchor = null;
            _isLoading = false;
            _isDirty = false;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _revisionMessage = null;
            _formatMessage = null;
            _formatWarnings = [];
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _loadedSuggestionProvider = null;
            _loadedFormatProvider = FormatProvider;
            _loadedFontProvider = null;
            _compareDialogOpen = false;
            _compareDocumentSnapshot = null;
            _lastSavedAt = null;
            _offlineDraft = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _offlineMessage = null;
            return;
        }

        if (!CanReadDocument)
        {
            await StopCollaborationAsync();
            _document = null;
            _errorMessage = Loc["TmDocumentEditor_ReadDenied"];
            _loadedDocumentId = null;
            _loadedProvider = null;
            _currentDocument = null;
            _previewVersion = null;
            _versions = [];
            _comments = [];
            _suggestions = [];
            _suggestionSnapshot = null;
            _draftCommentAnchor = null;
            _isLoading = false;
            _isDirty = false;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _revisionMessage = null;
            _formatMessage = null;
            _formatWarnings = [];
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _loadedSuggestionProvider = null;
            _loadedFormatProvider = FormatProvider;
            _loadedFontProvider = null;
            _compareDialogOpen = false;
            _compareDocumentSnapshot = null;
            _lastSavedAt = null;
            _offlineDraft = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _offlineMessage = null;
            await RecordOpenAuditAsync(DocumentId, DocumentEditorAuditResult.Denied, _errorMessage);
            return;
        }

        if (_loadedDocumentId == DocumentId && ReferenceEquals(_loadedProvider, Provider))
        {
            if (!ReferenceEquals(_loadedSuggestionProvider, SuggestionProvider))
            {
                await RefreshSuggestionsAsync();
                _loadedSuggestionProvider = SuggestionProvider;
            }

            await EnsureCollaborationStartedAsync();
            return;
        }

        await LoadDocumentAsync();
    }

    private async Task RefreshFormatCapabilitiesAsync()
    {
        if (FormatProvider is null)
        {
            _formatCapabilities = [];
            _loadedFormatProvider = null;
            return;
        }

        if (ReferenceEquals(_loadedFormatProvider, FormatProvider))
        {
            return;
        }

        try
        {
            _formatCapabilities = await FormatProvider.GetCapabilitiesAsync();
            _loadedFormatProvider = FormatProvider;
        }
        catch
        {
            _formatCapabilities = [];
            _loadedFormatProvider = null;
            _formatMessage = Loc["TmDocumentEditor_FormatProviderUnavailable"];
        }
    }

    private async Task RefreshFontFamiliesAsync()
    {
        var provider = FontProvider ?? _fallbackFontProvider;
        if (ReferenceEquals(_loadedFontProvider, provider) && _fontFamilies.Count > 0)
        {
            return;
        }

        try
        {
            var query = new DocumentFontQuery
            {
                DocumentId = DocumentId,
                CultureName = CultureInfo.CurrentUICulture.Name
            };
            var fonts = await provider.GetFontFamiliesAsync(query);
            var fallback = await provider.GetFallbackFontAsync(query);
            _fontFamilies = fonts.Count > 0
                ? fonts
                : [fallback];
            _loadedFontProvider = provider;
        }
        catch
        {
            _fontFamilies = (await _fallbackFontProvider.GetFontFamiliesAsync(new DocumentFontQuery
            {
                DocumentId = DocumentId,
                CultureName = CultureInfo.CurrentUICulture.Name
            })).ToList();
            _loadedFontProvider = _fallbackFontProvider;
        }
    }

    private async Task RetryAsync()
    {
        await LoadDocumentAsync(force: true);
    }

    private async Task LoadDocumentAsync(bool force = false)
    {
        if (Provider is null)
        {
            return;
        }

        if (!CanReadDocument)
        {
            await StopCollaborationAsync();
            _document = null;
            _errorMessage = Loc["TmDocumentEditor_ReadDenied"];
            _isLoading = false;
            await RecordOpenAuditAsync(DocumentId, DocumentEditorAuditResult.Denied, _errorMessage);
            return;
        }

        if (!force && _loadedDocumentId == DocumentId && ReferenceEquals(_loadedProvider, Provider))
        {
            return;
        }

        _isLoading = true;
        _errorMessage = null;

        try
        {
            var result = await Provider.LoadAsync(DocumentId, new DocumentEditorLoadOptions
            {
                IncludeDocument = true,
                IncludeJson = false
            });

            if (!result.Found || result.Document is null)
            {
                await StopCollaborationAsync();
                _document = null;
                _errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? Loc["TmDocumentEditor_DocumentNotFound"]
                    : result.ErrorMessage;
                return;
            }

            _document = result.Document;
            _documentOutline = _outlineService.GetOutline(_document);
            DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(_document);
            _currentDocument = _document;
            _concurrencyToken = result.ConcurrencyToken;
            _selection = new DocumentEditorSelectionState();
            _activeImageInspectorBlockId = null;
            _activeWysiwygRegion = "Body";
            _lastBodySelectionSnapshot = null;
            _lastBodyRangeSelectionSnapshot = null;
            _isDirty = false;
            _saveMessage = null;
            _versionMessage = null;
            _commentMessage = null;
            _suggestionMessage = null;
            _revisionMessage = null;
            _templatePreviewMessage = null;
            _formatMessage = null;
            _formatWarnings = [];
            _lastSavedAt = null;
            _previewVersion = null;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _comments = [];
            _draftCommentAnchor = null;
            _versionDialogOpen = false;
            _compareDialogOpen = false;
            _compareDocumentSnapshot = null;
            _sidePanelOpen = ShowComments || ShowVersionHistory || _trackChangesEnabled;
            _activeSidePanelTab = _trackChangesEnabled
                ? DocumentSidePanelTab.Revisions
                : ShowVersionHistory
                ? DocumentSidePanelTab.Versions
                : ShowComments
                    ? DocumentSidePanelTab.Comments
                    : DocumentSidePanelTab.Properties;
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _offlineMessage = null;
            _runtimeDraftStateJson = null;
            await LoadOfflineDraftIfNeededAsync(result.Document);
            ApplyCommentMarksFromComments(_document);
            await _commandStack.ClearAsync();
            _loadedDocumentId = DocumentId;
            _loadedProvider = Provider;
            await RefreshVersionsAsync();
            await RefreshCommentsAsync();
            await RefreshSuggestionsAsync();
            _loadedSuggestionProvider = SuggestionProvider;
            _suggestionSnapshot = Clone(_document);
            await EnsureCollaborationStartedAsync();
            await RefreshCommandRegistryAsync();
            await RecordOpenAuditAsync(_document.DocumentId, DocumentEditorAuditResult.Success, null);
            await OnDocumentLoaded.InvokeAsync(_document);
        }
        catch (Exception ex)
        {
            await StopCollaborationAsync();
            _document = null;
            _errorMessage = Loc["TmDocumentEditor_LoadErrorMessage"];
            await RecordOpenAuditAsync(DocumentId, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private Task SaveAsync()
    {
        return SaveAsync(DocumentEditorSaveTrigger.Explicit);
    }

    private async Task SaveAsync(DocumentEditorSaveTrigger trigger)
    {
        if (_isSaving)
        {
            if (_isDirty)
            {
                _saveAgainRequested = true;
                _autosave.RegisterLocalChange();
                SyncAutosavePendingAction();
                await UpdateBeforeUnloadGuardAsync();
            }

            return;
        }

        var currentTrigger = trigger;
        do
        {
            _saveAgainRequested = false;
            await SaveCoreAsync(currentTrigger);
            currentTrigger = DocumentEditorSaveTrigger.AutoSave;
        }
        while (!_disposed && _saveAgainRequested && _isDirty);
    }

    private async Task SaveCoreAsync(DocumentEditorSaveTrigger trigger)
    {
        if (Provider is null || _document is null || !CanEditDocument || _isSaving)
        {
            return;
        }

        if (IsVersionPreview)
        {
            return;
        }

        if (trigger == DocumentEditorSaveTrigger.AutoSave && !_isDirty)
        {
            return;
        }

        _isSaving = true;
        _saveMessage = null;
        _canRetrySave = false;
        _autosave.DebounceElapsed();
        _pendingActions.Remove(PendingActionId.AutosaveWaiting);
        _pendingActions.Add(PendingActionId.Save, Loc["TmDocumentEditor_Saving"]);
        await UpdateBeforeUnloadGuardAsync();

        var documentToSave = _currentDocument ?? _document;
        var saveUndoEpoch = _wysiwygUndoState.Epoch;
        var saveDirtyEpoch = _wysiwygDirtyState.DirtyEpoch;

        try
        {
            if (UsingCoreEngine)
            {
                // R.4.8 — pull the live model from the core engine and persist it.
                var coreDoc = await _coreHost!.RequestDocumentAsync();
                documentToSave = CreateProviderBoundarySnapshot(coreDoc ?? documentToSave);
                _document = documentToSave;
                _currentDocument = documentToSave;
            }
            else if (UsingCanvasEngine)
            {
                var canvasDoc = await _canvasHost!.RequestDocumentAsync();
                documentToSave = CreateProviderBoundarySnapshot(canvasDoc ?? documentToSave, preserveImageBlocks: true);
                _document = documentToSave;
                _currentDocument = documentToSave;
            }
            else if (_wysiwygHost is not null)
            {
                var undoState = await _wysiwygHost.RequestUndoStateAsync();
                if (undoState is not null)
                {
                    _wysiwygUndoState = undoState;
                    saveUndoEpoch = undoState.Epoch;
                }

                var dirtyState = await _wysiwygHost.RequestDirtyStateAsync();
                if (dirtyState is not null)
                {
                    _wysiwygDirtyState = dirtyState;
                    _isDirty = dirtyState.IsDirty;
                    saveDirtyEpoch = dirtyState.DirtyEpoch;
                }

                var jsSnapshot = await _wysiwygHost.RequestSnapshotAsync();
                if (jsSnapshot is not null)
                {
                    documentToSave = CreateProviderBoundarySnapshot(documentToSave, jsSnapshot);
                    _document = documentToSave;
                    _currentDocument = documentToSave;
                }
            }
            else
            {
                documentToSave = CreateProviderBoundarySnapshot(documentToSave);
            }

            var request = new DocumentEditorSaveRequest
            {
                DocumentId = documentToSave.DocumentId,
                Document = documentToSave,
                JsonSnapshot = DocumentEditorJson.Serialize(documentToSave),
                BaseConcurrencyToken = _concurrencyToken,
                ConcurrencyMode = DocumentEditorConcurrencyMode.Optional,
                Author = Author,
                IsAutosave = trigger == DocumentEditorSaveTrigger.AutoSave,
                VersionKind = trigger == DocumentEditorSaveTrigger.AutoSave ? DocumentVersionKind.Autosave : null,
                PreserveImageBlocks = UsingCanvasEngine
            };

            await OnSaveRequested.InvokeAsync(request);
            var result = await Provider.SaveAsync(request);

            if (result.Success)
            {
                var saveIsCurrent = true;
                DocumentEditorDocument? currentRuntimeDocument = null;
                if (_wysiwygHost is not null)
                {
                    var undoState = await _wysiwygHost.RequestUndoStateAsync();
                    if (undoState is not null)
                    {
                        _wysiwygUndoState = undoState;
                        saveIsCurrent = undoState.Epoch == saveUndoEpoch;
                    }

                    var dirtyState = await _wysiwygHost.RequestDirtyStateAsync();
                    if (dirtyState is not null)
                    {
                        _wysiwygDirtyState = dirtyState;
                        saveIsCurrent = saveIsCurrent && dirtyState.DirtyEpoch == saveDirtyEpoch;
                    }

                    if (!saveIsCurrent)
                    {
                        currentRuntimeDocument = await _wysiwygHost.RequestSnapshotAsync();
                    }
                }

                _document = saveIsCurrent
                    ? result.Document ?? _document
                    : currentRuntimeDocument ?? _document;
                _currentDocument = _document;
                _concurrencyToken = result.ConcurrencyToken;
                _isDirty = !saveIsCurrent;
                if (!saveIsCurrent)
                {
                    _saveAgainRequested = true;
                    _autosave.RegisterLocalChange();
                }

                _lastSavedAt = DateTimeOffset.Now;
                if (saveIsCurrent && _wysiwygHost is not null)
                {
                    await _wysiwygHost.MarkSavedAsync(_concurrencyToken);
                    var dirtyState = await _wysiwygHost.RequestDirtyStateAsync();
                    if (dirtyState is not null)
                    {
                        _wysiwygDirtyState = dirtyState;
                        _isDirty = dirtyState.IsDirty;
                    }
                }
                else if (saveIsCurrent && UsingCoreEngine)
                {
                    // R.4.8 — baseline the core engine's dirty tracking at the saved version.
                    await _coreHost!.MarkSavedAsync();
                    _isDirty = await _coreHost.IsDirtyAsync();
                    _coreCanUndo = await _coreHost.CanUndoAsync();
                    _coreCanRedo = await _coreHost.CanRedoAsync();
                }
                else if (saveIsCurrent && UsingCanvasEngine)
                {
                    await _canvasHost!.MarkSavedAsync();
                    _isDirty = await _canvasHost.IsDirtyAsync();
                    var canvasUndo = await _canvasHost.GetUndoStateAsync();
                    _canvasCanUndo = canvasUndo.CanUndo;
                    _canvasCanRedo = canvasUndo.CanRedo;
                }

                _saveMessage = trigger == DocumentEditorSaveTrigger.AutoSave
                    ? Loc["TmDocumentEditor_AutoSaveComplete"]
                    : Loc["TmDocumentEditor_SaveComplete"];
                if (!_isDirty)
                {
                    _autosave.SaveSucceeded();
                }

                Announce(_saveMessage);
                if (_offlineDraft is not null && OfflineStore is not null)
                {
                    await OfflineStore.DeleteDraftAsync(_offlineDraft.Id);
                }

                _offlineDraft = null;
                _offlineConflict = null;
                _offlineStatus = DocumentSyncStatus.Online;
                _offlineMessage = null;
                _runtimeDraftStateJson = null;
                await RecordSaveAuditAsync(trigger, DocumentEditorAuditResult.Success, null);
            }
            else
            {
                _saveMessage = result.Conflict
                    ? Loc["TmDocumentEditor_SaveConflict"]
                    : string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? Loc["TmDocumentEditor_SaveFailed"]
                        : result.ErrorMessage;
                if (result.Conflict)
                {
                    await SaveOfflineDraftAsync(documentToSave, DocumentOfflineDraftState.Conflict, DocumentSyncStatus.Conflict);
                    _offlineConflict = new DocumentSyncConflict
                    {
                        DocumentId = documentToSave.DocumentId,
                        LocalBaseVersionId = _concurrencyToken,
                        ServerVersionId = result.ConcurrencyToken,
                        Reason = _saveMessage
                    };
                    _offlineStatus = DocumentSyncStatus.Conflict;
                    _offlineMessage = Loc["TmDocumentEditor_OfflineConflict"];
                }
                else
                {
                    await SaveOfflineDraftAsync(documentToSave);
                }

                _lastFailedSaveTrigger = trigger;
                _canRetrySave = IsRecoverableSaveFailure(result);
                _autosave.SaveFailed(_saveMessage, _canRetrySave);
                Announce(_saveMessage, DocumentEditorAnnouncementPoliteness.Assertive);
                await RecordSaveAuditAsync(trigger, DocumentEditorAuditResult.Failure, _saveMessage);
            }
        }
        catch (Exception ex)
        {
            _saveMessage = Loc["TmDocumentEditor_SaveFailed"];
            await SaveOfflineDraftAsync(documentToSave);
            _lastFailedSaveTrigger = trigger;
            _canRetrySave = true;
            _autosave.SaveFailed(_saveMessage, recoverable: true);
            Announce(_saveMessage, DocumentEditorAnnouncementPoliteness.Assertive);
            await RecordSaveAuditAsync(trigger, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSaving = false;
            _pendingActions.Remove(PendingActionId.Save);
            SyncAutosavePendingAction();
            await UpdateBeforeUnloadGuardAsync();
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task RetrySaveAsync()
    {
        _autosave.Retry();
        _canRetrySave = false;
        return SaveAsync(_lastFailedSaveTrigger);
    }

    private static bool IsRecoverableSaveFailure(DocumentEditorSaveResult result)
    {
        if (result.Success || result.Conflict)
        {
            return false;
        }

        return result.ErrorKind is DocumentEditorSaveErrorKind.None or DocumentEditorSaveErrorKind.Recoverable;
    }

    private async Task ToggleInsertPanelAsync()
    {
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.OpenTokenMenuAsync();
        }
    }

    private async Task OpenCompareDialogAsync()
    {
        if (!CanCompareDocuments)
        {
            return;
        }

        var documentToCompare = await CreateCanvasExportBridge().RequestSnapshotAsync();
        _compareDocumentSnapshot = CloneForEditor(documentToCompare);
        _compareDialogOpen = true;
        _floatingLayerStack.Push(new DocumentFloatingLayerState
        {
            LayerId = FloatingLayerId.CompareDialog,
            Kind = DocumentFloatingLayerKind.CompareDialog,
            ZIndex = 50,
            CloseAsync = () =>
            {
                _compareDialogOpen = false;
                _compareDocumentSnapshot = null;
                return Task.CompletedTask;
            }
        });
    }

    private void CloseCompareDialog()
    {
        _compareDialogOpen = false;
        _compareDocumentSnapshot = null;
        _floatingLayerStack.Remove(FloatingLayerId.CompareDialog);
    }

    private async Task HandleDocumentComparedAsync(DocumentCompareResult result)
    {
        await RecordCompareAuditAsync(result, DocumentEditorAuditResult.Success, null);
        await OnDocumentCompared.InvokeAsync(result);
    }

    private Task HandleDocumentCompareFailedAsync(string message)
        => RecordCompareAuditAsync(null, DocumentEditorAuditResult.Failure, message);

    private async Task ExportPdfAsync()
    {
        if (_document is null || PdfExportProvider is null || _isExportingPdf || IsVersionPreview || !EffectivePermissions.CanExport)
        {
            return;
        }

        _isExportingPdf = true;
        _saveMessage = null;
        _pendingActions.Add(PendingActionId.ExportPdf, Loc["TmDocumentEditor_ExportingPdf"]);
        try
        {
            var result = await CreateCanvasExportBridge().ExportPdfAsync(
                PdfExportProvider,
                Author,
                documentToExport => CreatePdfExportOptions(documentToExport, _reviewDisplayMode));

            if (result.Content.Length == 0)
            {
                _saveMessage = Loc["TmDocumentEditor_ExportPdfFailed"];
                await RecordExportAuditAsync(result, DocumentEditorAuditResult.Failure, _saveMessage);
                return;
            }

            _saveMessage = Loc["TmDocumentEditor_ExportPdfComplete"];
            await RecordExportAuditAsync(result, DocumentEditorAuditResult.Success, null);
            await OnPdfExported.InvokeAsync(result);
            await DownloadFileAsync(result.FileName, result.ContentType, result.Content);
        }
        catch (Exception ex)
        {
            _saveMessage = Loc["TmDocumentEditor_ExportPdfFailed"];
            await RecordExportAuditAsync(null, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isExportingPdf = false;
            _pendingActions.Remove(PendingActionId.ExportPdf);
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ImportDocxAsync(InputFileChangeEventArgs args)
    {
        if (_document is null || FormatProvider is null || !CanImportDocx || _isImportingDocx)
        {
            return;
        }

        _isImportingDocx = true;
        _formatMessage = null;
        _formatWarnings = [];
        _pendingActions.Add(PendingActionId.ImportDocx, Loc["TmDocumentEditor_ImportingDocx"]);
        var file = args.File;

        try
        {
            using var memory = new MemoryStream();
            await using (var stream = file.OpenReadStream(MaxDocumentFormatImportSize))
            {
                await stream.CopyToAsync(memory);
            }

            var result = await FormatProvider.ImportAsync(new DocumentFormatImportProviderRequest
            {
                DocumentId = _document.DocumentId,
                Format = DocumentFormatProviderKind.Docx,
                FileName = file.Name,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                Content = memory.ToArray(),
                Author = Author
            });

            _formatWarnings = result.Warnings.ToList();
            if (!result.Success || result.Document is null)
            {
                _formatMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? Loc["TmDocumentEditor_ImportDocxFailed"]
                    : result.ErrorMessage;
                await RecordFormatImportAuditAsync(DocumentEditorAuditResult.Failure, _formatMessage);
                return;
            }

            var imported = result.Document;
            imported.DocumentId = _document.DocumentId;
            DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(imported);
            new DocumentEditorPostFixer().Fix(imported);
            _document = imported;
            _documentOutline = _outlineService.GetOutline(_document);
            _currentDocument = imported;
            _selection = new DocumentEditorSelectionState();
            _activeImageInspectorBlockId = null;
            _activeWysiwygRegion = "Body";
            _lastBodySelectionSnapshot = null;
            _lastBodyRangeSelectionSnapshot = null;
            _previewVersion = null;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _isDirty = true;
            ApplyCommentMarksFromComments(_document);
            await _commandStack.ClearAsync();
            _suggestionSnapshot = Clone(_document);
            _formatMessage = Loc["TmDocumentEditor_ImportDocxComplete"];
            if (_collaborationSync is not null || CollaborationProvider is not null)
            {
                await StopCollaborationAsync();
                await EnsureCollaborationStartedAsync();
            }
            else
            {
                _collaborationSnapshot = Clone(_document);
            }

            await RecordFormatImportAuditAsync(DocumentEditorAuditResult.Success, file.Name);
            await OnDocumentLoaded.InvokeAsync(_document);
            if (_canvasHost is not null)
            {
                await _canvasHost.ReplaceDocumentAsync(_document);
            }
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.RefreshSnapshotAsync(_document);
            }
        }
        catch (Exception ex)
        {
            _formatMessage = Loc["TmDocumentEditor_ImportDocxFailed"];
            _formatWarnings = [];
            await RecordFormatImportAuditAsync(DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isImportingDocx = false;
            _pendingActions.Remove(PendingActionId.ImportDocx);
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ExportDocxAsync()
    {
        if (_document is null || FormatProvider is null || !CanExportDocx || _isExportingDocx)
        {
            return;
        }

        _isExportingDocx = true;
        _formatMessage = null;
        _formatWarnings = [];
        _pendingActions.Add(PendingActionId.ExportDocx, Loc["TmDocumentEditor_ExportingDocx"]);

        try
        {
            var result = await CreateCanvasExportBridge().ExportFormatAsync(
                FormatProvider,
                DocumentFormatProviderKind.Docx,
                Author);

            _formatWarnings = result.Warnings.ToList();
            if (!result.Success || result.Content.Length == 0)
            {
                _formatMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? Loc["TmDocumentEditor_ExportDocxFailed"]
                    : result.ErrorMessage;
                await RecordFormatExportAuditAsync(result, DocumentEditorAuditResult.Failure, _formatMessage);
                return;
            }

            _formatMessage = Loc["TmDocumentEditor_ExportDocxComplete"];
            await RecordFormatExportAuditAsync(result, DocumentEditorAuditResult.Success, null);
            await OnDocumentFormatExported.InvokeAsync(result);
            await DownloadFormatExportAsync(result);
        }
        catch (Exception ex)
        {
            _formatMessage = Loc["TmDocumentEditor_ExportDocxFailed"];
            _formatWarnings = [];
            await RecordFormatExportAuditAsync(null, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isExportingDocx = false;
            _pendingActions.Remove(PendingActionId.ExportDocx);
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<DocumentEditorDocument> GetCurrentDocumentForProviderExportAsync()
    {
        var documentToExport = _currentDocument ?? _document ?? DocumentEditorDocument.Empty();
        if (UsingCoreEngine && _coreHost is not null)
        {
            // R.5.21 — pull the live model from the core engine so DOCX/ODT/PDF exports reflect
            // unsaved edits (tables/images/text round-trip via CoreEngineModelConverter).
            var coreDoc = await _coreHost.RequestDocumentAsync();
            documentToExport = CreateProviderBoundarySnapshot(coreDoc ?? documentToExport);
            documentToExport = await EnrichProviderBoundarySnapshotAsync(documentToExport);
            _document = documentToExport;
            _currentDocument = documentToExport;
            return documentToExport;
        }
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            var canvasDoc = await _canvasHost.RequestDocumentAsync();
            documentToExport = CreateProviderBoundarySnapshot(canvasDoc ?? documentToExport, preserveImageBlocks: true);
            documentToExport = await EnrichProviderBoundarySnapshotAsync(documentToExport);
            _document = documentToExport;
            _currentDocument = documentToExport;
            return documentToExport;
        }
        if (_wysiwygHost is not null)
        {
            var jsSnapshot = await _wysiwygHost.RequestSnapshotAsync();
            if (jsSnapshot is not null)
            {
                documentToExport = CreateProviderBoundarySnapshot(documentToExport, jsSnapshot);
                _document = documentToExport;
                _currentDocument = documentToExport;
            }
        }

        return await EnrichProviderBoundarySnapshotAsync(CreateProviderBoundarySnapshot(documentToExport));
    }

    private CanvasExportBridge CreateCanvasExportBridge()
        => new(_ => GetCurrentDocumentForProviderExportAsync());

    private async Task<DocumentEditorDocument> EnrichProviderBoundarySnapshotAsync(DocumentEditorDocument snapshot)
    {
        if (_comments.Count > 0)
        {
            snapshot.Comments = _comments.Select(CloneForEditor).ToList();
        }

        if (Provider is not null && snapshot.Comments.Count == 0)
        {
            var providerComments = await Provider.GetCommentsAsync(snapshot.DocumentId);
            if (providerComments.Count > 0)
            {
                snapshot.Comments = providerComments.Select(CloneForEditor).ToList();
            }
        }

        if (snapshot.Comments.Count > 0)
        {
            ApplyCommentMarksFromComments(snapshot);
        }

        return snapshot;
    }

    private static DocumentEditorDocument CreateProviderBoundarySnapshot(
        DocumentEditorDocument currentDocument,
        DocumentEditorDocument? wysiwygSnapshot = null,
        bool preserveImageBlocks = false)
    {
        var snapshot = wysiwygSnapshot is null
            ? CloneForEditor(currentDocument)
            : CloneForEditor(wysiwygSnapshot);

        snapshot.DocumentId = currentDocument.DocumentId;
        snapshot.SchemaVersion = currentDocument.SchemaVersion;
        snapshot.Metadata = CloneForEditor(currentDocument.Metadata);
        snapshot.PageSettings = CloneForEditor(currentDocument.PageSettings);
        snapshot.Theme = CloneForEditor(currentDocument.Theme);
        snapshot.Sections = CloneForEditor(currentDocument.Sections);
        snapshot.Notes = CloneForEditor(currentDocument.Notes);
        snapshot.Assets = CloneForEditor(currentDocument.Assets);
        snapshot.Anchors = CloneForEditor(currentDocument.Anchors);

        if (snapshot.Comments.Count == 0 && currentDocument.Comments.Count > 0)
        {
            snapshot.Comments = CloneForEditor(currentDocument.Comments);
        }

        if (snapshot.HeadersFooters.Count == 0 && currentDocument.HeadersFooters.Count > 0)
        {
            snapshot.HeadersFooters = CloneForEditor(currentDocument.HeadersFooters);
        }

        if (snapshot.Revisions.Count == 0 && currentDocument.Revisions.Count > 0)
        {
            snapshot.Revisions = CloneForEditor(currentDocument.Revisions);
        }

        DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(snapshot);
        new DocumentEditorPostFixer().Fix(snapshot);
        if (!preserveImageBlocks)
        {
            DocumentImagePersistence.ConvertImageBlocksToDrawingRuns(snapshot);
        }

        RemoveTransientDisplayData(snapshot);
        return snapshot;
    }

    private static void RemoveTransientDisplayData(DocumentEditorDocument document)
    {
        DocumentImagePersistence.Sanitize(document);
    }

    private Task DownloadFormatExportAsync(DocumentFormatExportProviderResult result)
        => DownloadFileAsync(result.FileName, result.ContentType, result.Content);

    private Task DownloadFileAsync(string fileName, string contentType, byte[] content)
    {
        if (content.Length == 0 || string.IsNullOrWhiteSpace(fileName))
        {
            return Task.CompletedTask;
        }

        return JSRuntime.InvokeVoidAsync(
            "tmDocumentEditor.downloadFile",
            fileName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Convert.ToBase64String(content)).AsTask();
    }

    private static DocumentPdfExportOptions CreatePdfExportOptions(
        DocumentEditorDocument document,
        DocumentReviewDisplayMode reviewDisplayMode = DocumentReviewDisplayMode.AllMarkup)
    {
        var pageSettings = document.PageSettings ?? new DocumentPageSettings();
        return new DocumentPdfExportOptions
        {
            IncludeComments = true,
            IncludeSuggestions = reviewDisplayMode is DocumentReviewDisplayMode.AllMarkup or DocumentReviewDisplayMode.SimpleMarkup,
            ReviewDisplayMode = reviewDisplayMode,
            PageSetup = new DocumentPdfPageSetupOptions
            {
                PageSize = CloneForEditor(pageSettings.Size ?? DocumentPageSize.A4),
                Orientation = pageSettings.Landscape
                    ? DocumentPdfPageOrientation.Landscape
                    : DocumentPdfPageOrientation.Portrait,
                Margins = CloneForEditor(pageSettings.Margins ?? DocumentPageMargins.Default)
            }
        };
    }

    private async Task OpenImageDialogAsync()
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Image))
        {
            return;
        }

        // R.4.8 — the legacy image dialog lives inside the legacy JS host; the core engine
        // gets a small Blazor URL dialog that routes the chosen image into engine.insertImage.
        if (UsingCoreEngine || UsingCanvasEngine)
        {
            _coreImageUrl = string.Empty;
            _coreImageDialogOpen = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.OpenImageDialogAsync();
        }
    }

    // R.4.8 — inserts the composed image URL into the core engine (floating drawing with
    // wrapping + resize handles from R.4.6d). The engine anchors it at its live caret.
    private async Task InsertCoreImageAsync()
    {
        if (string.IsNullOrWhiteSpace(_coreImageUrl))
        {
            return;
        }

        var url = _coreImageUrl.Trim();
        if (!EffectiveReadOnly && UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync("insertImage", new { url, width = 240, height = 160 });
            _coreImageDialogOpen = false;
            _coreImageUrl = string.Empty;
            await SyncCanvasEngineStateAsync();
            await _canvasHost.FocusAsync();
            return;
        }

        if (_coreHost is null)
        {
            return;
        }

        await _coreHost.ExecCommandAsync("insertImage", new { url, width = 240, height = 160 });
        _coreImageDialogOpen = false;
        _coreImageUrl = string.Empty;
        await SyncCoreEngineStateAsync();
    }

    private void CancelCoreImageDialog()
    {
        _coreImageDialogOpen = false;
        _coreImageUrl = string.Empty;
    }

    // R.4.8 — uploads an image into the core engine by reading the picked file client-side as
    // a data URL (the preview engine needs no ImageProvider) and routing it to insertImage.
    private async Task HandleCoreImageFileAsync(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null || (!UsingCoreEngine && !UsingCanvasEngine))
        {
            return;
        }

        const long maxBytes = 8 * 1024 * 1024;
        if (file.Size <= 0 || file.Size > maxBytes)
        {
            return;
        }

        if (!EffectiveReadOnly && UsingCanvasEngine && _canvasHost is not null)
        {
            await InsertCanvasUploadedFileAsync(file);
            return;
        }

        if (_coreHost is null)
        {
            return;
        }

        string dataUrl;
        await using (var stream = file.OpenReadStream(maxBytes))
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType;
            dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }

        await _coreHost.ExecCommandAsync("insertImage", new { url = dataUrl, width = 240, height = 160 });
        _coreImageDialogOpen = false;
        _coreImageUrl = string.Empty;
        await SyncCoreEngineStateAsync();
    }

    private async Task InsertCanvasUploadedFileAsync(IBrowserFile file)
    {
        if (_canvasHost is null || ImageProvider is null)
        {
            return;
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType;
        if (!ImageValidation.IsAllowed(contentType, file.Size))
        {
            _runtimeMessage = Loc["TmDocumentEditor_ImagePasteRejected"];
            await InvokeAsync(StateHasChanged);
            return;
        }

        await HandleImageUploadStateChangedAsync(true);
        try
        {
            await using var stream = file.OpenReadStream(ImageValidation.MaxFileSizeBytes);
            var upload = await ImageProvider.UploadAsync(new DocumentImageUploadRequest
            {
                DocumentId = _document?.DocumentId ?? DocumentId,
                FileName = string.IsNullOrWhiteSpace(file.Name) ? "image.png" : file.Name,
                ContentType = contentType,
                SizeBytes = file.Size
            }, stream);

            if (!upload.Success || string.IsNullOrWhiteSpace(upload.AssetId))
            {
                _runtimeMessage = string.IsNullOrWhiteSpace(upload.ErrorMessage)
                    ? Loc["TmDocumentEditor_ImageUploadFailed"]
                    : upload.ErrorMessage;
                await InvokeAsync(StateHasChanged);
                return;
            }

            await InsertCanvasImageAsync(new
            {
                assetId = upload.AssetId,
                url = upload.Url,
                altText = file.Name,
                width = 240,
                height = 160
            });
        }
        finally
        {
            await HandleImageUploadStateChangedAsync(false);
        }
    }

    private async Task InsertCanvasImageAssetAsync(DocumentImageAsset asset)
    {
        if (_canvasHost is null)
        {
            return;
        }

        await InsertCanvasImageAsync(new
        {
            assetId = asset.Id,
            url = asset.Url,
            altText = asset.AltText ?? asset.FileName,
            caption = asset.Caption,
            width = asset.ImageSize.Width ?? 240,
            height = asset.ImageSize.Height ?? 160
        });
    }

    private async Task InsertCanvasImageAsync(object payload)
    {
        if (_canvasHost is null)
        {
            return;
        }

        await _canvasHost.ExecCommandAsync("insertImage", payload);
        _coreImageDialogOpen = false;
        _coreImageUrl = string.Empty;
        await SyncCanvasEngineStateAsync();
        await _canvasHost.FocusAsync();
    }

    private async Task InsertEquationAsync(string preset)
    {
        if (EffectiveReadOnly || !UsingCanvasEngine || _canvasHost is null)
        {
            return;
        }

        var (command, payload) = BuildEquationCommandPayload(preset);
        await RouteToCanvasEngineAsync(command, payload, focus: true);
    }

    private static (string Command, object Payload) BuildEquationCommandPayload(string? preset)
    {
        var key = string.Concat((preset ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return key switch
        {
            "fraction" => ("insertFraction", new { activateFirstSlot = true }),
            "radical" => ("insertRadical", new { activateFirstSlot = true }),
            "superscript" => ("insertSuperscript", new { activateFirstSlot = true }),
            "subscript" => ("insertSubscript", new { activateFirstSlot = true }),
            "sum" => ("insertEquation", new { linear = "\\sum", displayMode = "display" }),
            "matrix" => ("insertMatrix", new { rows = 2, columns = 2, values = new[] { "1", "0", "0", "1" }, displayMode = "display" }),
            "limit" => ("insertLimit", new { lowerText = "x→0", text = "f(x)", displayMode = "display" }),
            "accent" => ("insertAccent", new { accent = "̂", baseText = "x" }),
            "borderbox" => ("insertBorderBox", new { text = "x+y" }),
            "alpha" => ("insertMathSymbol", new { symbol = "\\alpha" }),
            "beta" => ("insertMathSymbol", new { symbol = "\\beta" }),
            "pi" => ("insertMathSymbol", new { symbol = "\\pi" }),
            "infinity" => ("insertMathSymbol", new { symbol = "\\infty" }),
            "plusminus" => ("insertMathSymbol", new { symbol = "±" }),
            "integral" => ("insertMathSymbol", new { symbol = "\\int" }),
            "gamma" => ("insertMathSymbol", new { symbol = "\\gamma" }),
            "delta" => ("insertMathSymbol", new { symbol = "\\Delta" }),
            "theta" => ("insertMathSymbol", new { symbol = "\\theta" }),
            "lambda" => ("insertMathSymbol", new { symbol = "\\lambda" }),
            "rightarrow" => ("insertMathSymbol", new { symbol = "→" }),
            "lessequal" => ("insertMathSymbol", new { symbol = "≤" }),
            "greaterequal" => ("insertMathSymbol", new { symbol = "≥" }),
            "notequal" => ("insertMathSymbol", new { symbol = "≠" }),
            "product" => ("insertEquation", new { linear = "\\prod", displayMode = "display" }),
            "quadratic" => ("insertEquation", new { linear = "x=(-b±sqrt(b^2-4ac))/(2a)", displayMode = "display" }),
            _ => ("insertEquation", new { linear = "x" })
        };
    }

    private async Task InsertSymbolAsync(string preset)
    {
        if (EffectiveReadOnly || !UsingCanvasEngine || _canvasHost is null)
        {
            return;
        }

        var (command, payload) = BuildSymbolCommandPayload(preset);
        await RouteToCanvasEngineAsync(command, payload, focus: true);
    }

    private static (string Command, object Payload) BuildSymbolCommandPayload(string? preset)
    {
        var key = string.Concat((preset ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return key switch
        {
            "emdash" => ("insertEmDash", new { }),
            "endash" => ("insertEnDash", new { }),
            "nonbreakingspace" or "nbsp" => ("insertNonBreakingSpace", new { }),
            "optionalhyphen" or "softhyphen" => ("insertOptionalHyphen", new { }),
            "section" => ("insertSymbol", new { codePoint = 0x00A7 }),
            "copyright" => ("insertSymbol", new { codePoint = 0x00A9 }),
            "registered" => ("insertSymbol", new { codePoint = 0x00AE }),
            "check" => ("insertEmoji", new { codePoint = 0x2713 }),
            "sparkles" => ("insertEmoji", new { codePoint = 0x2728 }),
            "warning" => ("insertEmoji", new { codePoint = 0x26A0 }),
            "rocket" => ("insertEmoji", new { codePoint = 0x1F680 }),
            "calendar" => ("insertEmoji", new { codePoint = 0x1F4C5 }),
            _ => ("insertEmDash", new { })
        };
    }

    private async Task InsertImageAssetAsync()
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Image))
        {
            return;
        }

        var asset = ImageAssetOptions.FirstOrDefault();
        if (asset is null)
        {
            return;
        }

        // R.4.8 — the core engine inserts the asset's URL directly (no legacy host).
        if (UsingCoreEngine && _coreHost is not null)
        {
            if (!string.IsNullOrWhiteSpace(asset.Url))
            {
                await _coreHost.ExecCommandAsync("insertImage", new { url = asset.Url, width = 240, height = 160, altText = asset.AltText, caption = asset.Caption });
                await SyncCoreEngineStateAsync();
            }
            return;
        }

        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await InsertCanvasImageAssetAsync(asset);
            return;
        }

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.InsertImageAssetAsync(asset);
        }
    }

    private async Task ExecuteImageRuntimeCommandAsync(string command, object? payload = null)
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Image))
        {
            return;
        }

        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await RouteToCanvasEngineAsync(command, payload, focus: true);
            return;
        }

        if (_wysiwygHost is null)
        {
            return;
        }

        await _wysiwygHost.ExecuteRuntimeCommandAsync(command, payload);
    }

    private async Task SetActiveImageAltTextFromPanelAsync(string altText)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SetObjectAltTextAsync(altText ?? string.Empty);
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageAltText", new
        {
            AltText = altText,
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private Task SetActiveImageDecorativeFromPanelAsync(bool isDecorative) =>
        ExecuteImageRuntimeCommandAsync("setImageDecorative", new
        {
            IsDecorative = isDecorative,
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });

    private async Task ToggleActiveImageCaptionFromPanelAsync()
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            // Toggle: clear an existing caption, otherwise add a default one.
            var current = _coreActiveImage?.Caption ?? string.Empty;
            await _coreHost.SetObjectCaptionAsync(string.IsNullOrEmpty(current) ? Loc["TmDocumentEditor_ImageCaption"] : string.Empty);
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("toggleImageCaption", new
        {
            Caption = Loc["TmDocumentEditor_ImageCaption"],
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private async Task SetActiveImageCaptionFromPanelAsync(string caption)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SetObjectCaptionAsync(caption ?? string.Empty);
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageCaption", new
        {
            Caption = caption,
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private Task SetActiveImageUrlFromPanelAsync(string? imageUrl) =>
        ExecuteImageRuntimeCommandAsync("setImageUrl", new
        {
            Url = imageUrl,
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });

    private async Task SetActiveImageWrapModeFromPanelAsync(DocumentWrapMode wrapMode)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SetObjectWrapModeAsync(EngineWrapMode(wrapMode));
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageWrapMode", new
        {
            WrapMode = wrapMode.ToString(),
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    // R.4.8 — paragraph (block) style picker. The core engine applies a named style
    // (Normal/Title/Heading1-6 → setParagraphStyle); the legacy host has no named-style
    // command, so the picker is only shown for the core engine preview.
    private async Task ApplyBlockStyleAsync(string styleName)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return;
        }

        if (await RouteToCoreEngineAsync(styleName))
        {
            _coreBlockStyle = styleName;
            return;
        }

        if (await RouteToCanvasEngineAsync("blockStyle", styleName))
        {
            _coreBlockStyle = styleName;
        }
    }

    private async Task ModifyDocumentStyleAsync(DocumentStyleDefinition style)
    {
        if (style is null || string.IsNullOrWhiteSpace(style.Id))
        {
            return;
        }

        UpsertDocumentStyle(_document, style);
        UpsertDocumentStyle(_currentDocument, style);

        if (await RouteToCanvasEngineAsync("modifyStyle", style))
        {
            _coreBlockStyle = style.Name;
        }
    }

    private async Task CreateDocumentStyleFromSelectionAsync(DocumentStyleDefinition style)
    {
        if (style is null || string.IsNullOrWhiteSpace(style.Id))
        {
            return;
        }

        UpsertDocumentStyle(_document, style);
        UpsertDocumentStyle(_currentDocument, style);

        if (await RouteToCanvasEngineAsync("createStyleFromSelection", style))
        {
            _coreBlockStyle = style.Name;
        }
    }

    private async Task RenameDocumentStyleAsync(DocumentStyleDefinition style)
    {
        if (style is null || string.IsNullOrWhiteSpace(style.Id) || string.IsNullOrWhiteSpace(style.Name))
        {
            return;
        }

        RenameDocumentStyle(_document, style);
        RenameDocumentStyle(_currentDocument, style);

        if (await RouteToCanvasEngineAsync("renameStyle", style))
        {
            _coreBlockStyle = style.Name;
        }
    }

    private async Task DeleteDocumentStyleAsync(DocumentStyleDefinition style)
    {
        if (style is null || string.IsNullOrWhiteSpace(style.Id))
        {
            return;
        }

        DeleteDocumentStyle(_document, style);
        DeleteDocumentStyle(_currentDocument, style);

        if (await RouteToCanvasEngineAsync("deleteStyle", style))
        {
            _coreBlockStyle = "Normal";
        }
    }

    private async Task ResetDocumentStyleFormattingAsync()
    {
        if (await RouteToCanvasEngineAsync("resetStyleFormatting"))
        {
            await SyncCanvasEngineStateAsync();
        }
    }

    private static void UpsertDocumentStyle(DocumentEditorDocument? document, DocumentStyleDefinition style)
    {
        if (document is null)
        {
            return;
        }

        var index = document.Styles.FindIndex(item =>
            string.Equals(item.Id, style.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, style.Name, StringComparison.OrdinalIgnoreCase));

        var clone = CloneForEditor(style);
        if (index < 0)
        {
            document.Styles.Add(clone);
        }
        else
        {
            document.Styles[index] = clone;
        }
    }

    private static void RenameDocumentStyle(DocumentEditorDocument? document, DocumentStyleDefinition style)
    {
        if (document is null)
        {
            return;
        }

        var index = document.Styles.FindIndex(item =>
            string.Equals(item.Id, style.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, style.Id, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            UpsertDocumentStyle(document, style);
            return;
        }

        document.Styles[index].Name = style.Name;
    }

    private static void DeleteDocumentStyle(DocumentEditorDocument? document, DocumentStyleDefinition style)
    {
        if (document is null)
        {
            return;
        }

        document.Styles.RemoveAll(item =>
            string.Equals(item.Id, style.Id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, style.Name, StringComparison.OrdinalIgnoreCase));
    }

    // R.4.8 — JS→.NET: the engine selected/deselected an image; drive the inspector panel.
    private async Task HandleCoreObjectSelectedAsync(TmDocumentCoreEngineHost.CoreObjectSelection? selection)
    {
        _coreSelectedObject = selection;
        if (selection is null || string.IsNullOrEmpty(selection.ObjectId))
        {
            _coreActiveImage = null;
        }
        else
        {
            _coreActiveImage = new ImageBlockContent
            {
                Url = selection.Url,
                AltText = selection.AltText ?? string.Empty,
                Caption = selection.Caption ?? string.Empty,
                Layout = new DocumentObjectLayout
                {
                    Wrap = new DocumentObjectWrap { Mode = ParseEngineWrapMode(selection.WrapMode) },
                    Transform = new DocumentObjectTransform { Width = selection.Width, Height = selection.Height },
                    Position = new DocumentObjectPosition { X = selection.X, Y = selection.Y },
                },
            };
            OpenSidePanel(DocumentSidePanelTab.Properties);
        }
        await InvokeAsync(StateHasChanged);
    }

    private static DocumentWrapMode ParseEngineWrapMode(string? mode) => (mode ?? "inline").ToLowerInvariant() switch
    {
        "square" => DocumentWrapMode.Square,
        "tight" => DocumentWrapMode.Tight,
        "through" => DocumentWrapMode.Through,
        "topbottom" => DocumentWrapMode.TopBottom,
        "topandbottom" => DocumentWrapMode.TopBottom,
        "behindtext" => DocumentWrapMode.BehindText,
        "infrontoftext" => DocumentWrapMode.InFrontOfText,
        _ => DocumentWrapMode.Inline,
    };

    private static string EngineWrapMode(DocumentWrapMode mode) => mode switch
    {
        DocumentWrapMode.Square => "square",
        DocumentWrapMode.Tight => "tight",
        DocumentWrapMode.Through => "through",
        DocumentWrapMode.TopBottom => "topAndBottom",
        DocumentWrapMode.BehindText => "behindText",
        DocumentWrapMode.InFrontOfText => "inFrontOfText",
        _ => "inline",
    };

    private async Task SetActiveImageAlignmentFromPanelAsync(DocumentImageAlignment alignment)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SetObjectAlignmentAsync(ToHorizontalPosition(alignment).ToString());
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImagePosition", new
        {
            HorizontalPosition = ToHorizontalPosition(alignment).ToString(),
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private async Task SetActiveImageSizeFromPanelAsync(DocumentImageSize size)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SetObjectSizeAsync(size.Width ?? 0, size.Height ?? 0);
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageSize", new
        {
            Width = size.Width,
            Height = size.Height,
            LockAspectRatio = size.LockAspectRatio,
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    // R.5.3 — autosave for the core engine. The engine fires OnModelChanged (debounced in JS by
    // CoreChangeDebounceMs) after the user stops editing; we mark the document dirty (so the
    // before-unload guard + manual save reflect it) and, when autosave is enabled, persist.
    private int CoreChangeDebounceMs =>
        AutoSaveInterval is { } interval && interval > TimeSpan.Zero
            ? (int)Math.Clamp(interval.TotalMilliseconds, 250, 10000)
            : 800;

    private async Task HandleCoreModelChangedAsync()
    {
        if (_disposed || !UsingCoreEngine)
        {
            return;
        }
        _isDirty = true;
        _autosave.RegisterLocalChange();
        SyncAutosavePendingAction();
        await UpdateBeforeUnloadGuardAsync();
        await InvokeAsync(StateHasChanged);

        if (AutoSaveInterval is { } interval && interval > TimeSpan.Zero && !_isSaving && CanEditDocument)
        {
            // The JS side already debounced; persist now (SaveCoreAsync pulls the live model).
            await SaveAsync(DocumentEditorSaveTrigger.AutoSave);
        }
    }

    private async Task SetActiveImagePositionFromPanelAsync(DocumentObjectPosition position)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SetObjectPositionAsync(position.X, position.Y);
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageObjectPosition", new
        {
            X = position.X,
            Y = position.Y,
            HorizontalRelativeTo = position.HorizontalRelativeTo.ToString(),
            VerticalRelativeTo = position.VerticalRelativeTo.ToString(),
            HorizontalPosition = position.HorizontalAlignment?.ToString(),
            VerticalAlignment = position.VerticalAlignment.ToString(),
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private Task SetActiveImageLockAnchorFromPanelAsync(bool lockAnchor) =>
        ExecuteImageRuntimeCommandAsync("setImageAnchorMode", new
        {
            LockAnchor = lockAnchor,
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });

    private async Task BringActiveImageForwardFromPanelAsync()
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.BringObjectForwardAsync();
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageZOrder", new
        {
            Direction = "Forward",
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private async Task SendActiveImageBackwardFromPanelAsync()
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.SendObjectBackwardAsync();
            await SyncCoreEngineStateAsync();
            return;
        }

        await ExecuteImageRuntimeCommandAsync("setImageZOrder", new
        {
            Direction = "Backward",
            ObjectId = ActiveImageCommandObjectId,
            BlockId = ActiveImageCommandBlockId,
            RunId = ActiveImageCommandRunId
        });
    }

    private Task SetActiveTablePropertiesFromPanelAsync(TableLayoutContent layout) =>
        _wysiwygHost is null
            ? Task.CompletedTask
            : _wysiwygHost.ExecuteRuntimeCommandAsync("setTableProperties", new
            {
                CellId = _selectionContext.ActiveTableCellId ?? _selection.ActiveTableCellId,
                layout.Width,
                Alignment = layout.Alignment.ToString(),
                layout.CellPadding,
                layout.BackgroundColor,
                layout.Borders
            });

    private Task SetActiveCellPropertiesFromPanelAsync(TableCellContent cell) =>
        _wysiwygHost is null
            ? Task.CompletedTask
            : _wysiwygHost.ExecuteRuntimeCommandAsync("setCellProperties", new
            {
                CellId = _selectionContext.ActiveTableCellId ?? _selection.ActiveTableCellId,
                cell.Width,
                cell.BackgroundColor,
                VerticalAlignment = cell.VerticalAlignment.ToString(),
                cell.Padding,
                cell.Borders
            });

    private static DocumentImageHorizontalPosition ToHorizontalPosition(DocumentImageAlignment alignment) =>
        alignment switch
        {
            DocumentImageAlignment.Start => DocumentImageHorizontalPosition.Left,
            DocumentImageAlignment.End => DocumentImageHorizontalPosition.Right,
            _ => DocumentImageHorizontalPosition.Center
        };

    private async Task ExecuteTableRuntimeCommandAsync(string command, object? payload = null)
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Table) || _wysiwygHost is null)
        {
            return;
        }

        await _wysiwygHost.ExecuteRuntimeCommandAsync(command, payload);
    }

    private async Task UploadDemoImageAsync()
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Image))
        {
            return;
        }

        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await InsertCanvasProviderDemoImageAsync();
            return;
        }

        if (_wysiwygHost is null)
        {
            return;
        }

        await _wysiwygHost.UploadDemoImageAsync();
    }

    private async Task InsertCanvasProviderDemoImageAsync()
    {
        if (ImageProvider is null)
        {
            _runtimeMessage = Loc["TmDocumentEditor_ImageProviderMissing"];
            await InvokeAsync(StateHasChanged);
            return;
        }

        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        const string contentType = "image/png";
        await HandleImageUploadStateChangedAsync(true);
        try
        {
            await using var stream = new MemoryStream(bytes);
            var upload = await ImageProvider.UploadAsync(new DocumentImageUploadRequest
            {
                DocumentId = _document?.DocumentId ?? DocumentId,
                FileName = "demo-image.png",
                ContentType = contentType,
                SizeBytes = bytes.Length
            }, stream);

            if (!upload.Success || string.IsNullOrWhiteSpace(upload.AssetId))
            {
                _runtimeMessage = string.IsNullOrWhiteSpace(upload.ErrorMessage)
                    ? Loc["TmDocumentEditor_ImageUploadFailed"]
                    : upload.ErrorMessage;
                await InvokeAsync(StateHasChanged);
                return;
            }

            await InsertCanvasImageAsync(new
            {
                assetId = upload.AssetId,
                url = upload.Url,
                altText = Loc["TmDocumentEditor_ImageCaption"],
                width = 240,
                height = 160
            });
        }
        finally
        {
            await HandleImageUploadStateChangedAsync(false);
        }
    }

    private async Task InsertTableAsync(int rows = 2, int columns = 2)
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Table))
        {
            return;
        }

        if (UsingCoreEngine)
        {
            await _coreHost!.ExecCommandAsync("insertTable", new { rows, cols = columns });
            await SyncCoreEngineStateAsync();
            return;
        }

        if (_wysiwygHost is not null)
        {
            if (_lastBodySelectionSnapshot is not null)
            {
                await _wysiwygHost.RestoreSelectionAsync(_lastBodySelectionSnapshot);
            }

            await _wysiwygHost.ExecuteEditorCommandAsync("insertTable", new { rows, columns });
        }
    }

    private async Task InsertPageBreakAsync()
    {
        if (_wysiwygHost is null || EffectiveReadOnly)
        {
            return;
        }

        var selection = _lastBodySelectionSnapshot;
        if (selection is null)
        {
            await GetCurrentDocumentForProviderExportAsync();
            if (_document is not null)
            {
                selection = CreateFirstBodySelectionSnapshot(_document);
                _lastBodySelectionSnapshot = selection;
            }
        }

        if (selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
        }

        await GetCurrentDocumentForProviderExportAsync();
        if (_document is null)
        {
            return;
        }

        if (selection is null || !IsBodySelection(selection))
        {
            selection = CreateFirstBodySelectionSnapshot(_document);
        }

        if (selection is null || !IsBodySelection(selection))
        {
            return;
        }

        var anchorBlockIndex = _document.Blocks.FindIndex(block =>
            string.Equals(block.Id, selection.AnchorBlockId, StringComparison.Ordinal));
        if (anchorBlockIndex < 0)
        {
            return;
        }

        var before = DocumentEditorCommandCloner.Clone(_document);
        var sectionId = _document.Blocks[anchorBlockIndex].SectionId;
        var pageBreak = new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            SectionId = sectionId,
            Type = DocumentBlockType.PageBreak,
            Order = _document.Blocks.Count,
            Content = new PageBreakBlockContent()
        };
        var paragraph = new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            SectionId = sectionId,
            Type = DocumentBlockType.Paragraph,
            Order = _document.Blocks.Count + 1,
            Content = new ParagraphBlockContent
            {
                Inlines = [new TextRun { Id = Guid.NewGuid().ToString("N"), Text = string.Empty }]
            }
        };
        _document.Blocks.Insert(anchorBlockIndex + 1, pageBreak);
        _document.Blocks.Insert(anchorBlockIndex + 2, paragraph);
        NormalizeBlockOrder(_document.Blocks);
        _document.BumpVersion();  // Phase C1

        var afterSelection = new WysiwygSelectionSnapshot
        {
            Region = "Body",
            PageIndex = (selection.PageIndex ?? 0) + 1,
            AnchorBlockId = paragraph.Id,
            FocusBlockId = paragraph.Id,
            AnchorInlineId = ((ParagraphBlockContent)paragraph.Content).Inlines.First().Id,
            FocusInlineId = ((ParagraphBlockContent)paragraph.Content).Inlines.First().Id,
            IsCollapsed = true
        };

        var after = DocumentEditorCommandCloner.Clone(_document);
        await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(_document, before, after, "Insert page break"));
        _currentDocument = _document;
        _lastWysiwygSelectionSnapshot = afterSelection;
        _lastBodySelectionSnapshot = afterSelection;
        MarkDirtyAfterCommand();
        await _wysiwygHost.ExecuteEditorCommandAsync("syncHeaderFooterLayout", new { Document = _document, Selection = afterSelection });
        await _wysiwygHost.RestoreSelectionAsync(afterSelection);
        await RefreshCommandRegistryAsync();
    }

    private Task InsertFootnoteAsync() => InsertNoteAsync(DocumentNoteType.Footnote);

    private Task InsertEndnoteAsync() => InsertNoteAsync(DocumentNoteType.Endnote);

    private async Task InsertNoteAsync(DocumentNoteType noteType)
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await RouteToCanvasEngineAsync(noteType == DocumentNoteType.Endnote ? "insertEndnote" : "insertFootnote", null, focus: true);
            return;
        }

        if (EffectiveReadOnly || _wysiwygHost is null)
        {
            return;
        }

        var selection = await _wysiwygHost.RequestRuntimeSelectionAsync()
            ?? await _wysiwygHost.RequestSelectionSnapshotAsync()
            ?? _lastBodySelectionSnapshot;
        if (selection is null || !IsBodySelection(selection))
        {
            if (_lastBodySelectionSnapshot is not null)
            {
                await _wysiwygHost.RestoreSelectionAsync(_lastBodySelectionSnapshot);
            }

            selection = await _wysiwygHost.RequestRuntimeSelectionAsync()
                ?? await _wysiwygHost.RequestSelectionSnapshotAsync()
                ?? _lastBodySelectionSnapshot;
        }

        if (selection is null || !IsBodySelection(selection))
        {
            selection = _lastBodySelectionSnapshot;
        }

        await GetCurrentDocumentForProviderExportAsync();
        if (_document is null)
        {
            return;
        }

        if (selection is null || !IsBodySelection(selection))
        {
            selection = CreateFirstBodySelectionSnapshot(_document);
        }

        if (selection is null || !IsBodySelection(selection))
        {
            return;
        }

        var block = FindBlockForSelection(_document, selection.AnchorBlockId, selection);
        var inlines = block is null ? null : GetEditableInlines(block.Content);
        if (block is null || inlines is null)
        {
            return;
        }

        var before = DocumentEditorCommandCloner.Clone(_document);
        var marker = CreateNoteMarker(_document, noteType);
        var note = new DocumentNote
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = noteType,
            SectionId = ActiveSection?.Id ?? _document.Sections.OrderBy(section => section.Order).FirstOrDefault()?.Id,
            Marker = marker,
            ReferenceIds = []
        };

        var reference = new DocumentNoteReferenceRun
        {
            Id = Guid.NewGuid().ToString("N"),
            NoteId = note.Id,
            NoteType = noteType,
            DisplayMarker = marker
        };
        note.ReferenceIds.Add(reference.Id!);
        note.Blocks.Add(CreateDefaultNoteBlock(noteType));

        var insertOffset = ResolveNoteInsertionOffset(selection, inlines);
        InsertInlineAtBlockOffset(inlines, reference, insertOffset);
        _document.Notes.Add(note);
        _document.BumpVersion();  // Phase C1

        var afterSelection = CloneForEditor(selection);
        afterSelection.Region = "Body";
        afterSelection.AnchorBlockId = block.Id;
        afterSelection.FocusBlockId = block.Id;
        afterSelection.AnchorInlineId = reference.Id;
        afterSelection.FocusInlineId = reference.Id;
        afterSelection.AnchorBlockOffset = insertOffset + marker.Length;
        afterSelection.FocusBlockOffset = afterSelection.AnchorBlockOffset;
        afterSelection.AnchorOffset = marker.Length;
        afterSelection.FocusOffset = marker.Length;
        afterSelection.IsCollapsed = true;

        var after = DocumentEditorCommandCloner.Clone(_document);
        await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
            _document,
            before,
            after,
            noteType == DocumentNoteType.Endnote ? "Insert endnote" : "Insert footnote"));

        _currentDocument = _document;
        _lastWysiwygSelectionSnapshot = afterSelection;
        _lastBodySelectionSnapshot = afterSelection;
        MarkDirtyAfterCommand();
        await _wysiwygHost.ExecuteEditorCommandAsync("syncHeaderFooterLayout", new { Document = _document, Selection = afterSelection });
        await _wysiwygHost.RestoreSelectionAsync(afterSelection);
        await RefreshCommandRegistryAsync();
    }

    private async Task InsertHeaderFooterFieldAsync(DocumentFieldType fieldType)
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await RouteToCanvasEngineAsync("insertField", new
            {
                FieldType = (int)fieldType,
                FallbackText = GetDocumentFieldFallbackText(fieldType)
            }, focus: true);
            return;
        }

        if (_wysiwygHost is null
            || EffectiveReadOnly)
        {
            return;
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("insertField", new
        {
            FieldType = (int)fieldType,
            FallbackText = GetDocumentFieldFallbackText(fieldType),
            Selection = _lastWysiwygSelectionSnapshot
        });
    }

    private Task InsertPageNumberFieldAsync() =>
        InsertHeaderFooterFieldAsync(DocumentFieldType.PageNumber);

    private Task InsertPageCountFieldAsync() =>
        InsertHeaderFooterFieldAsync(DocumentFieldType.PageCount);

    private Task InsertPageXOfYFieldAsync() =>
        InsertHeaderFooterFieldAsync(DocumentFieldType.PageXOfY);

    private Task InsertDateFieldAsync() =>
        InsertHeaderFooterFieldAsync(DocumentFieldType.Date);

    private Task InsertDocumentTitleFieldAsync() =>
        InsertHeaderFooterFieldAsync(DocumentFieldType.DocumentTitle);

    private Task InsertAuthorFieldAsync() =>
        InsertHeaderFooterFieldAsync(DocumentFieldType.Author);

    private async Task InsertCaptionAsync()
    {
        if (EffectiveReadOnly || !UsingCanvasEngine)
        {
            return;
        }

        await RouteToCanvasEngineAsync("insertCaption", new
        {
            Kind = "figure",
            Label = Loc["TmDocumentEditor_FigureLabel"],
            Text = Loc["TmDocumentEditor_DefaultCaptionText"]
        }, focus: true);
    }

    private async Task InsertCrossReferenceAsync()
    {
        if (EffectiveReadOnly || !UsingCanvasEngine)
        {
            return;
        }

        await RouteToCanvasEngineAsync("insertCrossReference", new
        {
            ReferenceFormat = "full"
        }, focus: true);
    }

    private async Task InsertTableOfFiguresAsync()
    {
        if (EffectiveReadOnly || !UsingCanvasEngine)
        {
            return;
        }

        await RouteToCanvasEngineAsync("insertTableOfFigures", new
        {
            Kind = "figure"
        }, focus: true);
    }

    private async Task InsertTableOfContentsAsync()
    {
        if (EffectiveReadOnly || !UsingCanvasEngine)
        {
            return;
        }

        await RouteToCanvasEngineAsync("insertTableOfContents", new
        {
            Levels = 3
        }, focus: true);
    }

    private async Task InsertBibliographyAsync()
    {
        if (EffectiveReadOnly || !UsingCanvasEngine)
        {
            return;
        }

        await RouteToCanvasEngineAsync("insertBibliography", null, focus: true);
    }

    private async Task UpdateFieldsAsync()
    {
        if (EffectiveReadOnly || !UsingCanvasEngine)
        {
            return;
        }

        await RouteToCanvasEngineAsync("updateTableOfContents");
        await RouteToCanvasEngineAsync("updateAllFields", null, focus: true);
    }

    private string GetDocumentFieldFallbackText(DocumentFieldType fieldType)
    {
        var metadata = _document?.Metadata;
        return fieldType switch
        {
            DocumentFieldType.PageNumber => "1",
            DocumentFieldType.PageCount => "1",
            DocumentFieldType.PageXOfY => "1 / 1",
            DocumentFieldType.Date => DateTime.Today.ToShortDateString(),
            DocumentFieldType.DocumentTitle => string.IsNullOrWhiteSpace(metadata?.Title) ? Loc["TmDocumentEditor_DocumentTitle"] : metadata.Title,
            DocumentFieldType.Author => string.IsNullOrWhiteSpace(metadata?.Author?.DisplayName) ? Loc["TmDocumentEditor_Author"] : metadata.Author.DisplayName,
            DocumentFieldType.LastSaved => (metadata?.ModifiedAt ?? metadata?.CreatedAt ?? DateTimeOffset.Now).ToLocalTime().ToString("d", CultureInfo.CurrentCulture),
            DocumentFieldType.SectionPageNumber => "1",
            DocumentFieldType.SectionPageCount => "1",
            DocumentFieldType.FileName => string.IsNullOrWhiteSpace(metadata?.Title) ? Loc["TmDocumentEditor_FileName"] : metadata.Title,
            DocumentFieldType.RevisionNumber => "1",
            _ => string.Empty
        };
    }

    private async Task ApplyHeaderFooterPresetAsync(string preset)
    {
        if (_document is null || EffectiveReadOnly)
        {
            return;
        }

        var activeHeaderFooterSelection = CreateActiveHeaderFooterSelectionSnapshot();
        await GetCurrentDocumentForProviderExportAsync();
        if (_document is null)
        {
            return;
        }

        DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(_document);
        var before = DocumentEditorCommandCloner.Clone(_document);
        var section = ActiveSection ?? _document.Sections.OrderBy(item => item.Order).FirstOrDefault();
        if (section is null)
        {
            return;
        }

        switch (preset)
        {
            case "footer-page-number-right":
                AppendHeaderFooterPresetBlock(section, DocumentHeaderFooterType.Footer, DocumentTextAlignment.Right, [CreateField(DocumentFieldType.PageNumber)]);
                break;
            case "footer-page-number-center":
                AppendHeaderFooterPresetBlock(section, DocumentHeaderFooterType.Footer, DocumentTextAlignment.Center, [CreateField(DocumentFieldType.PageNumber)]);
                break;
            case "header-title-page-number":
                AppendHeaderFooterPresetBlock(section, DocumentHeaderFooterType.Header, DocumentTextAlignment.Left, [
                    CreateField(DocumentFieldType.DocumentTitle),
                    new TextRun { Id = Guid.NewGuid().ToString("N"), Text = "    " },
                    CreateField(DocumentFieldType.PageNumber)
                ]);
                break;
            case "footer-page-x-of-y-right":
                AppendHeaderFooterPresetBlock(section, DocumentHeaderFooterType.Footer, DocumentTextAlignment.Right, [CreateField(DocumentFieldType.PageXOfY)]);
                break;
            default:
                return;
        }

        var after = DocumentEditorCommandCloner.Clone(_document);
        await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
            _document,
            before,
            after,
            "Apply header/footer preset"));
        _currentDocument = _document;
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync(
                "syncHeaderFooterLayout",
                new { Document = _document, Selection = activeHeaderFooterSelection ?? _lastWysiwygSelectionSnapshot });
            if (activeHeaderFooterSelection is not null)
            {
                await _wysiwygHost.RestoreSelectionAsync(activeHeaderFooterSelection);
            }
        }
    }

    private WysiwygSelectionSnapshot? CreateActiveHeaderFooterSelectionSnapshot()
    {
        if (!string.Equals(_activeWysiwygRegion, "Header", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_activeWysiwygRegion, "Footer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var snapshot = _lastWysiwygSelectionSnapshot is null
            ? new WysiwygSelectionSnapshot()
            : CloneForEditor(_lastWysiwygSelectionSnapshot);
        snapshot.Region = _activeWysiwygRegion;
        snapshot.HeaderFooterId = string.IsNullOrWhiteSpace(_selection.HeaderFooterId)
            ? snapshot.HeaderFooterId
            : _selection.HeaderFooterId;
        snapshot.PageIndex = _selection.PageIndex ?? snapshot.PageIndex ?? 0;
        return snapshot;
    }

    private void AppendHeaderFooterPresetBlock(
        DocumentSection section,
        DocumentHeaderFooterType type,
        DocumentTextAlignment alignment,
        IReadOnlyList<InlineContent> inlines)
    {
        if (_document is null)
        {
            return;
        }

        var activeHeaderFooter = DocumentHeaderFooterResolver.FindById(_document, _selection.HeaderFooterId);
        var scope = activeHeaderFooter?.Type == type
            ? activeHeaderFooter.Scope
            : DocumentHeaderFooterScope.Primary;
        var headerFooter = DocumentHeaderFooterResolver.Ensure(_document, section, type, scope);
        headerFooter.Blocks.Add(new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DocumentBlockType.Paragraph,
            Order = headerFooter.Blocks.Count == 0 ? 10 : headerFooter.Blocks.Max(item => item.Order) + 10,
            ParagraphProperties = new DocumentParagraphProperties { Alignment = alignment },
            Content = new ParagraphBlockContent { Inlines = [.. inlines] }
        });
    }

    private DocumentFieldRun CreateField(DocumentFieldType type) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            FieldType = type,
            FallbackText = GetDocumentFieldFallbackText(type)
        };

    private Task SetPageSettingsAsync(DocumentPageSettings settings)
        => SetPageSetupAsync(new DocumentSectionPageSetup
        {
            SectionId = ActiveSection?.Id,
            PageSettings = CloneForEditor(settings),
            Columns = CloneForEditor(ActiveSectionColumns),
            LineNumbering = CloneForEditor(ActiveLineNumbering)
        });

    private async Task SetPageSetupAsync(DocumentSectionPageSetup setup)
    {
        if (_document is null || EffectiveReadOnly)
        {
            return;
        }

        var activeSectionId = string.IsNullOrWhiteSpace(setup.SectionId) ? ActiveSection?.Id : setup.SectionId;
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            var command = await _canvasHost.ExecCommandAsync("setPageSetup", new
            {
                sectionId = activeSectionId,
                pageSettings = CloneForEditor(setup.PageSettings),
                columns = CloneForEditor(setup.Columns),
                lineNumbering = CloneForEditor(setup.LineNumbering)
            });
            if (command.Handled)
            {
                var canvasDocument = await _canvasHost.RequestDocumentAsync();
                if (canvasDocument is not null)
                {
                    _document = CreateProviderBoundarySnapshot(canvasDocument, preserveImageBlocks: true);
                    _currentDocument = _document;
                }

                await SyncCanvasEngineStateAsync();
                await _canvasHost.FocusAsync();
                await InvokeAsync(StateHasChanged);
                return;
            }
        }

        await GetCurrentDocumentForProviderExportAsync();
        if (_document is null)
        {
            return;
        }

        var before = DocumentEditorCommandCloner.Clone(_document);
        _document.PageSettings = CloneForEditor(setup.PageSettings);
        _document.BumpVersion();  // Phase C1
        var section = !string.IsNullOrWhiteSpace(activeSectionId)
            ? _document.Sections.FirstOrDefault(item => string.Equals(item.Id, activeSectionId, StringComparison.Ordinal))
            : null;
        section ??= _document.Sections.OrderBy(item => item.Order).FirstOrDefault();
        if (section is not null)
        {
            section.Properties.PageSettings = CloneForEditor(setup.PageSettings);
            section.Properties.Columns = CloneForEditor(setup.Columns);
            section.Properties.LineNumbering = CloneForEditor(setup.LineNumbering);
        }

        var after = DocumentEditorCommandCloner.Clone(_document);
        await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
            _document,
            before,
            after,
            "Change page setup"));
        _currentDocument = _document;
        MarkDirtyAfterCommand();

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync(
                "syncHeaderFooterLayout",
                new
                {
                    Document = _document,
                    Selection = _lastWysiwygSelectionSnapshot,
                    PreferBodySelectionWhenFocusOutside = true
                });
        }
    }

    private async Task ToggleTemplatePreviewAsync()
    {
        if (_templatePreviewEnabled)
        {
            _templatePreviewEnabled = false;
            _templatePreviewDocument = null;
            _templatePreviewMessage = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_document is null || TokenValueProvider is null)
        {
            return;
        }

        try
        {
            var service = new DocumentTemplatePreviewService(TokenValueProvider);
            _templatePreviewDocument = await service.CreatePreviewAsync(
                _document,
                new DocumentTokenResolutionContext
                {
                    DocumentId = DocumentId,
                    CultureName = CultureInfo.CurrentUICulture.Name,
                    Author = Author
                });
            _templatePreviewEnabled = true;
            _templatePreviewMessage = Loc["TmDocumentEditor_TemplatePreviewOn"];
        }
        catch
        {
            _templatePreviewEnabled = false;
            _templatePreviewDocument = null;
            _templatePreviewMessage = Loc["TmDocumentEditor_TemplatePreviewFailed"];
        }

        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleDocumentChangedAsync(DocumentEditorDocument document)
    {
        var suggestionBefore = _suggestionSnapshot is not null ? Clone(_suggestionSnapshot) : null;
        if (await TryCreateSuggestionAsync(suggestionBefore, document))
        {
            return;
        }

        var before = _collaborationSnapshot is not null
            ? Clone(_collaborationSnapshot)
            : _document is not null
                ? Clone(_document)
                : null;

        _document = document;
        DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(_document);
        _currentDocument = _document;
        _templatePreviewDocument = null;
        _templatePreviewEnabled = false;
        _templatePreviewMessage = null;
        _isDirty = true;
        if (before is not null)
        {
            await BroadcastLocalCollaborationChangeAsync(before, _document);
        }

        _suggestionSnapshot = Clone(_document);
    }

    private async Task HandleWysiwygPatchAsync(WysiwygPatch patch)
    {
        if (_document is null || patch is null)
        {
            return;
        }

        var deferRenderUntilTransactionCommit = IsLiveWysiwygPatch(patch);
        try
        {
            var before = DocumentEditorCommandCloner.Clone(_document);
            var handledAsRevision = _trackChangesEnabled && TryApplyTrackedRevisionPatch(_document, patch);
            var handledAsTrackedStructuralChange = _trackChangesEnabled && IsTrackedStructuralPatch(patch);
            if (!handledAsRevision)
            {
                var applier = new WysiwygPatchApplier();
                applier.ApplyPatch(_document, patch);
                if (handledAsTrackedStructuralChange)
                {
                    handledAsRevision = TryUpsertTrackedStructuralRevision(_document, patch);
                }
            }

            var after = DocumentEditorCommandCloner.Clone(_document);
            if (!handledAsRevision && !handledAsTrackedStructuralChange && await TryCreateSuggestionAsync(before, after))
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            _currentDocument = _document;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _isDirty = true;
            await BroadcastLocalCollaborationChangeAsync(before, after, patch);
            _suggestionSnapshot = Clone(after);
            if (handledAsRevision || handledAsTrackedStructuralChange)
            {
                OpenSidePanel(DocumentSidePanelTab.Revisions);
                await InvokeAsync(StateHasChanged);
            }
            else if (IsImageInsertPatch(patch))
            {
                _activeImageInspectorBlockId = GetImageInsertAnchorBlockId(patch);
                OpenSidePanel(DocumentSidePanelTab.Properties);
                await InvokeAsync(StateHasChanged);
            }
            else if (!deferRenderUntilTransactionCommit)
            {
                if (_wysiwygHost is not null)
                {
                    await _wysiwygHost.RefreshSnapshotAsync();
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            _errorMessage = Loc["TmDocumentEditor_WysiwygPatchFailed"];
            await RecordSaveAuditAsync(DocumentEditorSaveTrigger.Explicit, DocumentEditorAuditResult.Failure, ex.Message);
        }
    }

    private async Task HandleWysiwygSnapshotAsync(DocumentEditorDocument document)
    {
        var suggestionBefore = _suggestionSnapshot is not null ? Clone(_suggestionSnapshot) : null;
        if (await TryCreateSuggestionAsync(suggestionBefore, document))
        {
            return;
        }

        var before = _collaborationSnapshot is not null
            ? Clone(_collaborationSnapshot)
            : _document is not null
                ? Clone(_document)
                : null;

        _document = document;
        _currentDocument = document;
        _templatePreviewDocument = null;
        _templatePreviewEnabled = false;
        _templatePreviewMessage = null;
        _isDirty = true;
        if (before is not null)
        {
            await BroadcastLocalCollaborationChangeAsync(before, document);
        }

        _suggestionSnapshot = Clone(document);
    }

    private async Task HandleWysiwygSelectionChangedAsync(WysiwygSelectionSnapshot? snapshot)
    {
        var keepMiniToolbarVisible = _miniToolbarColorPickerOpen || DateTimeOffset.UtcNow <= _keepMiniToolbarVisibleUntil;
        if (snapshot?.IsCollapsed != false && !keepMiniToolbarVisible)
        {
            _miniToolbar = null;
        }

        if (ShouldIgnoreTransientFloatingCollapsedSelection(snapshot))
        {
            _formattingState.CurrentSelection = _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
            ApplyPendingFloatingFormattingOverride();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_miniToolbarColorPickerOpen && snapshot?.IsCollapsed != false)
        {
            _formattingState.CurrentSelection = _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
            ApplyPendingFloatingFormattingOverride();
            await RefreshCommandRegistryAsync();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (snapshot is null)
        {
            _selection = new DocumentEditorSelectionState();
            _selectionContext = DocumentEditorSelectionContext.Empty;
            _activeWysiwygRegion = "Body";
            _lastWysiwygSelectionSnapshot = null;
            _lastCollapsedSelectionRenderKey = null;
            if (_optimisticFloatingFormattingExpiresAt != default)
            {
                _formattingState.CurrentSelection = _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
                ApplyPendingFloatingFormattingOverride();
                await RefreshCommandRegistryAsync();
                await InvokeAsync(StateHasChanged);
                await BroadcastCollaborationCursorAsync();
                return;
            }

            _formattingState = new WysiwygFormattingState();
            await BroadcastCollaborationCursorAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(snapshot.ActiveObjectId)
            && !string.IsNullOrWhiteSpace(snapshot.ObjectSelection?.ObjectId))
        {
            snapshot.ActiveObjectId = snapshot.ObjectSelection.ObjectId;
        }

        if (string.IsNullOrWhiteSpace(snapshot.SelectionMode))
        {
            snapshot.SelectionMode = !string.IsNullOrWhiteSpace(snapshot.ObjectSelection?.ObjectId) ? "Object" : "Text";
        }

        _activeWysiwygRegion = string.IsNullOrWhiteSpace(snapshot.Region) ? "Body" : snapshot.Region;
        _lastWysiwygSelectionSnapshot = snapshot;
        var collapsedRenderKey = snapshot.IsCollapsed ? GetCollapsedSelectionRenderKey(snapshot) : null;
        _lastCollapsedSelectionRenderKey = collapsedRenderKey;
        if (string.Equals(_activeWysiwygRegion, "Body", StringComparison.OrdinalIgnoreCase))
        {
            _lastBodySelectionSnapshot = snapshot;
            if (snapshot.IsCollapsed == false)
            {
                _lastBodyRangeSelectionSnapshot = snapshot;
            }
            else if (_miniToolbar is null
                && !_miniToolbarColorPickerOpen
                && DateTimeOffset.UtcNow > _ignoreFloatingCollapsedSelectionUntil)
            {
                _lastBodyRangeSelectionSnapshot = null;
            }
        }

        var range = new DocumentEditorInlineRange
        {
            BlockId = snapshot.AnchorBlockId,
            StartOffset = snapshot.AnchorOffset,
            EndOffset = snapshot.IsCollapsed ? snapshot.AnchorOffset : snapshot.FocusOffset
        };

        if (!string.IsNullOrWhiteSpace(snapshot.AnchorBlockId) && _document is not null)
        {
            var block = FindBlockForSelection(_document, snapshot.AnchorBlockId, snapshot);
            var inlines = GetEditableInlines(block?.Content);
            if (inlines is not null && !string.IsNullOrWhiteSpace(snapshot.AnchorInlineId))
            {
                range.StartInlineIndex = inlines.FindIndex(i => i.Id == snapshot.AnchorInlineId);
                if (range.StartInlineIndex < 0) range.StartInlineIndex = 0;
            }
            if (inlines is not null && !string.IsNullOrWhiteSpace(snapshot.FocusInlineId) && !snapshot.IsCollapsed)
            {
                range.EndInlineIndex = inlines.FindIndex(i => i.Id == snapshot.FocusInlineId);
                if (range.EndInlineIndex < 0) range.EndInlineIndex = range.StartInlineIndex;
            }
            else
            {
                range.EndInlineIndex = range.StartInlineIndex;
            }
        }

        _selection = new DocumentEditorSelectionState
        {
            ActiveBlockId = snapshot.AnchorBlockId,
            SelectionMode = string.IsNullOrWhiteSpace(snapshot.SelectionMode) ? "Text" : snapshot.SelectionMode,
            TextSelection = snapshot.TextSelection,
            ObjectSelection = snapshot.ObjectSelection,
            FocusedInlineRange = range,
            ActiveTableCellId = snapshot.ActiveTableCellId,
            ActiveTableId = snapshot.ActiveTableId,
            ActiveImageBlockId = snapshot.ActiveImageBlockId,
            ActiveCommentId = snapshot.ActiveCommentId,
            ActiveRevisionId = snapshot.ActiveRevisionId,
            LayoutLineId = snapshot.LayoutLineId,
            LayoutSegmentId = snapshot.LayoutSegmentId,
            VisualLineIndex = snapshot.VisualLineIndex,
            ActiveObjectId = !string.IsNullOrWhiteSpace(snapshot.ObjectSelection?.ObjectId) ? snapshot.ObjectSelection.ObjectId : snapshot.ActiveObjectId,
            HitTargetKind = snapshot.HitTargetKind,
            Region = _activeWysiwygRegion,
            HeaderFooterId = snapshot.HeaderFooterId,
            PageIndex = snapshot.PageIndex
        };

        _formattingState = await ResolveRuntimeFormattingStateAsync(snapshot);
        _selectionContext = DocumentEditorSelectionContext.FromSnapshot(snapshot, _formattingState, GetActiveObjectPropertiesSnapshot(snapshot));
        ApplySelectionContextToSidePanel(_selectionContext);
        ApplyPendingFloatingFormattingOverride();
        ApplyPendingParagraphFormattingOverride(snapshot);
        await RefreshCommandRegistryAsync();
        await BroadcastCollaborationCursorAsync();
        await InvokeAsync(StateHasChanged);
    }

    private static bool SelectionTargetsDocumentContent(WysiwygSelectionSnapshot snapshot) =>
        !string.IsNullOrWhiteSpace(snapshot.AnchorBlockId)
        || !string.IsNullOrWhiteSpace(snapshot.FocusBlockId)
        || !string.IsNullOrWhiteSpace(snapshot.ActiveTableCellId);

    private static bool TableContainsCell(TableBlockContent table, string? cellId) =>
        !string.IsNullOrWhiteSpace(cellId)
        && table.Rows.Any(row => row.Cells.Any(cell => string.Equals(cell.Id, cellId, StringComparison.Ordinal)));

    private void ApplySelectionContextToSidePanel(DocumentEditorSelectionContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ActiveImageId))
        {
            _activeImageInspectorBlockId = context.Selection.ObjectSelection?.AnchorBlockId ?? context.Selection.AnchorBlockId;
            OpenSidePanel(DocumentSidePanelTab.Properties, preserveManualChoice: false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(context.ActiveTableCellId) || !string.IsNullOrWhiteSpace(context.ActiveTableId))
        {
            _activeImageInspectorBlockId = null;
            OpenSidePanel(DocumentSidePanelTab.Properties, preserveManualChoice: false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(context.ActiveCommentId))
        {
            _activeImageInspectorBlockId = null;
            _selectedCommentId = context.ActiveCommentId;
            OpenSidePanel(DocumentSidePanelTab.Comments, preserveManualChoice: false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(context.ActiveRevisionId))
        {
            _activeImageInspectorBlockId = null;
            _selectedRevisionId = context.ActiveRevisionId;
            OpenSidePanel(DocumentSidePanelTab.Revisions, preserveManualChoice: false);
            return;
        }

        if (SelectionTargetsDocumentContent(context.Selection))
        {
            _activeImageInspectorBlockId = null;
        }

        if (_sidePanelTabManuallySelected)
        {
            return;
        }
    }

    private IReadOnlyDictionary<string, object?> GetActiveObjectPropertiesSnapshot(WysiwygSelectionSnapshot snapshot)
    {
        var activeObjectId = !string.IsNullOrWhiteSpace(snapshot.ObjectSelection?.ObjectId)
            ? snapshot.ObjectSelection.ObjectId
            : snapshot.ActiveObjectId;
        var drawing = FindDrawingRunByObjectId(DisplayedDocument, activeObjectId);
        if (drawing is not null)
        {
            return new Dictionary<string, object?>
            {
                ["kind"] = "image",
                ["objectId"] = drawing.ObjectId,
                ["altText"] = drawing.AltText,
                ["wrapMode"] = drawing.Layout.Wrap.Mode.ToString(),
                ["width"] = drawing.Layout.Transform.Width,
                ["height"] = drawing.Layout.Transform.Height
            };
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveTableCellId) || !string.IsNullOrWhiteSpace(snapshot.ActiveTableId))
        {
            return new Dictionary<string, object?>
            {
                ["kind"] = "table",
                ["tableId"] = snapshot.ActiveTableId,
                ["cellId"] = snapshot.ActiveTableCellId
            };
        }

        return new Dictionary<string, object?>();
    }

    private static ImageBlockContent? CreateImageBlockContentFromDrawingRun(DocumentDrawingRun? drawing)
    {
        if (drawing is null)
        {
            return null;
        }

        return new ImageBlockContent
        {
            Source = drawing.Source,
            Url = drawing.Url,
            AssetId = drawing.AssetId,
            AltText = drawing.AltText,
            IsDecorative = drawing.IsDecorative,
            Caption = drawing.Caption,
            Size = drawing.Size,
            NaturalSize = drawing.NaturalSize,
            Layout = drawing.Layout,
            LinkUrl = drawing.LinkUrl
        };
    }

    private static DocumentDrawingRun? FindDrawingRunByObjectId(DocumentEditorDocument? document, string? objectId)
    {
        if (document is null || string.IsNullOrWhiteSpace(objectId))
        {
            return null;
        }

        foreach (var block in EnumerateDocumentBlocksForObjectSelection(document))
        {
            var inlines = GetEditableInlines(block.Content);
            var drawing = inlines?.OfType<DocumentDrawingRun>()
                .FirstOrDefault(run => string.Equals(run.ObjectId, objectId, StringComparison.Ordinal));
            if (drawing is not null)
            {
                return drawing;
            }
        }

        return null;
    }

    private static IEnumerable<DocumentBlock> EnumerateDocumentBlocksForObjectSelection(DocumentEditorDocument document)
    {
        foreach (var block in document.Blocks)
        {
            yield return block;

            if (block.Content is TableBlockContent table)
            {
                foreach (var nestedBlock in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks))
                {
                    yield return nestedBlock;
                }
            }
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            foreach (var block in headerFooter.Blocks)
            {
                yield return block;
            }
        }
    }

    private static string GetCollapsedSelectionRenderKey(WysiwygSelectionSnapshot snapshot)
        => string.Join('|',
            string.IsNullOrWhiteSpace(snapshot.Region) ? "Body" : snapshot.Region,
            snapshot.AnchorBlockId ?? string.Empty,
            snapshot.AnchorInlineId ?? string.Empty,
            snapshot.AnchorOffset.ToString(CultureInfo.InvariantCulture),
            snapshot.ActiveTableCellId ?? string.Empty,
            snapshot.ActiveImageBlockId ?? string.Empty,
            snapshot.LayoutLineId ?? string.Empty,
            snapshot.LayoutSegmentId ?? string.Empty,
            snapshot.VisualLineIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            snapshot.ActiveObjectId ?? string.Empty,
            snapshot.HitTargetKind ?? string.Empty,
            snapshot.HeaderFooterId ?? string.Empty,
            snapshot.PageIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private void UpdateCollapsedSelectionWithoutRender(WysiwygSelectionSnapshot snapshot)
    {
        _activeWysiwygRegion = string.IsNullOrWhiteSpace(snapshot.Region) ? "Body" : snapshot.Region;
        if (string.Equals(_activeWysiwygRegion, "Body", StringComparison.OrdinalIgnoreCase))
        {
            _lastBodySelectionSnapshot = snapshot;
        }

        _selection.ActiveBlockId = snapshot.AnchorBlockId;
        _selection.SelectionMode = string.IsNullOrWhiteSpace(snapshot.SelectionMode) ? "Text" : snapshot.SelectionMode;
        _selection.TextSelection = snapshot.TextSelection;
        _selection.ObjectSelection = snapshot.ObjectSelection;
        _selection.ActiveTableCellId = snapshot.ActiveTableCellId;
        _selection.ActiveTableId = snapshot.ActiveTableId;
        _selection.ActiveImageBlockId = snapshot.ActiveImageBlockId;
        _selection.ActiveCommentId = snapshot.ActiveCommentId;
        _selection.ActiveRevisionId = snapshot.ActiveRevisionId;
        _selection.LayoutLineId = snapshot.LayoutLineId;
        _selection.LayoutSegmentId = snapshot.LayoutSegmentId;
        _selection.VisualLineIndex = snapshot.VisualLineIndex;
        _selection.ActiveObjectId = !string.IsNullOrWhiteSpace(snapshot.ObjectSelection?.ObjectId) ? snapshot.ObjectSelection.ObjectId : snapshot.ActiveObjectId;
        _selection.HitTargetKind = snapshot.HitTargetKind;
        _selection.Region = _activeWysiwygRegion;
        _selection.HeaderFooterId = snapshot.HeaderFooterId;
        _selection.PageIndex = snapshot.PageIndex;
        _selection.FocusedInlineRange ??= new DocumentEditorInlineRange();
        _selection.FocusedInlineRange.BlockId = snapshot.AnchorBlockId;
        _selection.FocusedInlineRange.StartOffset = snapshot.AnchorOffset;
        _selection.FocusedInlineRange.EndOffset = snapshot.AnchorOffset;
    }

    private async Task<WysiwygFormattingState> ResolveRuntimeFormattingStateAsync(WysiwygSelectionSnapshot snapshot)
    {
        if (_wysiwygHost is not null)
        {
            var runtimeState = await _wysiwygHost.RequestRuntimeSelectionStateAsync();
            if (runtimeState is not null)
            {
                runtimeState.CurrentSelection ??= snapshot;
                if (string.IsNullOrWhiteSpace(runtimeState.ActiveRegion))
                {
                    runtimeState.ActiveRegion = _activeWysiwygRegion;
                }

                return runtimeState;
            }
        }

        var fallback = ComputeFormattingState(snapshot);
        fallback.CurrentSelection = snapshot;
        fallback.ActiveRegion = _activeWysiwygRegion;
        return fallback;
    }

    private async Task HandleWysiwygFormattingStateChangedAsync(WysiwygFormattingState state)
    {
        if (state.Version > 0 && state.Version < _lastFormattingStateVersion)
        {
            return;
        }

        if (state.Version > 0)
        {
            _lastFormattingStateVersion = state.Version;
        }

        state.CurrentSelection ??= _lastWysiwygSelectionSnapshot ?? _lastBodySelectionSnapshot;
        if (string.IsNullOrWhiteSpace(state.ActiveRegion))
        {
            state.ActiveRegion = _activeWysiwygRegion;
        }

        _formattingState = state;
        if (state.CurrentSelection is not null)
        {
            _activeWysiwygRegion = string.IsNullOrWhiteSpace(state.CurrentSelection.Region)
                ? state.ActiveRegion
                : state.CurrentSelection.Region;
            if (IsBodySelection(state.CurrentSelection))
            {
                _lastBodySelectionSnapshot = state.CurrentSelection;
                if (state.CurrentSelection.IsCollapsed == false)
                {
                    _lastBodyRangeSelectionSnapshot = state.CurrentSelection;
                }
                else if (_miniToolbar is null
                    && !_miniToolbarColorPickerOpen
                    && DateTimeOffset.UtcNow > _ignoreFloatingCollapsedSelectionUntil)
                {
                    _lastBodyRangeSelectionSnapshot = null;
                }
            }

            ApplyPendingParagraphFormattingOverride(state.CurrentSelection);
        }

        _selectionContext = state.CurrentSelection is null
            ? _selectionContext
            : DocumentEditorSelectionContext.FromSnapshot(state.CurrentSelection, _formattingState, GetActiveObjectPropertiesSnapshot(state.CurrentSelection));
        ApplyPendingFloatingFormattingOverride();
        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshFormattingStateFromRuntimeAsync(WysiwygSelectionSnapshot? selection)
    {
        if (_wysiwygHost is null)
        {
            return;
        }

        var runtimeState = await _wysiwygHost.RequestRuntimeSelectionStateAsync();
        if (runtimeState is null)
        {
            return;
        }

        runtimeState.CurrentSelection ??= selection
            ?? _lastBodyRangeSelectionSnapshot
            ?? _lastBodySelectionSnapshot
            ?? _lastWysiwygSelectionSnapshot;
        if (string.IsNullOrWhiteSpace(runtimeState.ActiveRegion))
        {
            runtimeState.ActiveRegion = _activeWysiwygRegion;
        }

        await HandleWysiwygFormattingStateChangedAsync(runtimeState);
    }

    private void ApplyPendingParagraphFormattingOverride(WysiwygSelectionSnapshot snapshot)
    {
        if (!snapshot.IsCollapsed)
        {
            return;
        }

        var blockId = snapshot.AnchorBlockId;

        if (_pendingParagraphAlignment is not null)
        {
            if (DateTimeOffset.UtcNow > _pendingParagraphAlignmentExpiresAt
                || (!string.IsNullOrWhiteSpace(_pendingParagraphAlignmentBlockId)
                    && !string.Equals(_pendingParagraphAlignmentBlockId, blockId, StringComparison.Ordinal)))
            {
                _pendingParagraphAlignment = null;
                _pendingParagraphAlignmentBlockId = null;
            }
            else
            {
                _formattingState.ParagraphAlignment = _pendingParagraphAlignment.Value;
                _formattingState.ParagraphAlignmentMixed = false;
            }
        }

        if (_pendingLineSpacing is not null)
        {
            if (DateTimeOffset.UtcNow > _pendingLineSpacingExpiresAt
                || (!string.IsNullOrWhiteSpace(_pendingLineSpacingBlockId)
                    && !string.Equals(_pendingLineSpacingBlockId, blockId, StringComparison.Ordinal)))
            {
                _pendingLineSpacing = null;
                _pendingLineSpacingBlockId = null;
            }
            else
            {
                _formattingState.LineSpacing = _pendingLineSpacing.Value;
                _formattingState.LineSpacingMixed = false;
            }
        }
    }

    private Task HandleTextContextMenuRequestedAsync(WysiwygTextContextMenuRequest request)
    {
        _textContextMenu = request;
        _tableContextMenu = null;
        _miniToolbar = null;
        _floatingLayerStack.Remove(FloatingLayerId.TableContextMenu);
        _floatingLayerStack.Remove(FloatingLayerId.MiniToolbar);
        _floatingLayerStack.Push(new DocumentFloatingLayerState
        {
            LayerId = FloatingLayerId.TextContextMenu,
            Kind = DocumentFloatingLayerKind.TextContextMenu,
            ZIndex = 20,
            Priority = 20,
            Anchor = new DocumentFloatingLayerAnchor { X = request.Left, Y = request.Top, Width = request.Width, Height = request.Height },
            RestoreFocusTarget = "surface",
            CloseAsync = () => { CloseFloatingUi(); return Task.CompletedTask; }
        });
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleTableContextMenuRequestedAsync(WysiwygTableContextMenuRequest request)
    {
        if (!IsFeatureEnabled(DocumentEditorFeatureNames.Table))
        {
            return Task.CompletedTask;
        }

        _tableContextMenu = request;
        _textContextMenu = null;
        _miniToolbar = null;
        _floatingLayerStack.Remove(FloatingLayerId.TextContextMenu);
        _floatingLayerStack.Remove(FloatingLayerId.MiniToolbar);
        _floatingLayerStack.Push(new DocumentFloatingLayerState
        {
            LayerId = FloatingLayerId.TableContextMenu,
            Kind = DocumentFloatingLayerKind.TableContextMenu,
            ZIndex = 20,
            Priority = 20,
            Anchor = new DocumentFloatingLayerAnchor { X = request.Left, Y = request.Top, Width = request.Width, Height = request.Height },
            RestoreFocusTarget = "surface",
            CloseAsync = () => { CloseFloatingUi(); return Task.CompletedTask; }
        });
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleMiniToolbarChangedAsync(WysiwygMiniToolbarRequest? request)
    {
        if (ShouldPreserveMiniToolbarDuringFloatingInteraction(request))
        {
            return Task.CompletedTask;
        }

        _miniToolbar = IsVisibleRangeMiniToolbarRequest(request) ? request : null;
        if (_miniToolbar is not null)
        {
            _lastMiniToolbarRequest = _miniToolbar;
            if (_miniToolbar.Selection is not null)
            {
                RememberBodySelection(_miniToolbar.Selection);
            }

            _textContextMenu = null;
            _tableContextMenu = null;
            _floatingLayerStack.Remove(FloatingLayerId.TextContextMenu);
            _floatingLayerStack.Remove(FloatingLayerId.TableContextMenu);
            _floatingLayerStack.Push(new DocumentFloatingLayerState
            {
                LayerId = FloatingLayerId.MiniToolbar,
                Kind = DocumentFloatingLayerKind.MiniToolbar,
                ZIndex = 15,
                Priority = 15,
                Anchor = new DocumentFloatingLayerAnchor { X = _miniToolbar.Left, Y = _miniToolbar.Top, Width = _miniToolbar.Width, Height = _miniToolbar.Height },
                RestoreFocusTarget = "surface",
                CloseAsync = () => { CloseFloatingUi(); return Task.CompletedTask; }
            });
        }
        else
        {
            _lastMiniToolbarRequest = null;
            _floatingLayerStack.Remove(FloatingLayerId.MiniToolbar);
        }

        return InvokeAsync(StateHasChanged);
    }

    private static bool IsVisibleRangeMiniToolbarRequest(WysiwygMiniToolbarRequest? request)
        => request?.IsVisible == true
        && request.Selection?.IsCollapsed == false;

    private bool ShouldPreserveMiniToolbarDuringFloatingInteraction(WysiwygMiniToolbarRequest? request)
    {
        if (IsVisibleRangeMiniToolbarRequest(request))
        {
            return false;
        }

        if (_miniToolbar is null && _lastMiniToolbarRequest is null)
        {
            return false;
        }

        if (!_miniToolbarColorPickerOpen && DateTimeOffset.UtcNow > _keepMiniToolbarVisibleUntil)
        {
            return false;
        }

        var reason = request?.Reason ?? string.Empty;
        if (reason.Contains("editable-pointerdown", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("outside-pointerdown", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("api-hide", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return FirstRangeSelection(
            _miniToolbar?.Selection,
            _lastMiniToolbarRequest?.Selection,
            _lastBodyRangeSelectionSnapshot) is not null;
    }

    private static readonly int[] MiniToolbarFontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];
    private static readonly string[] MiniToolbarTextColors =
    [
        "#111827", "#374151", "#6b7280", "#000000", "#ffffff", "#dc2626",
        "#ea580c", "#d97706", "#059669", "#2563eb", "#4f46e5", "#7c3aed"
    ];
    private static readonly string[] MiniToolbarHighlightColors =
    [
        "#ffffff", "#fef3c7", "#fde68a", "#fee2e2", "#ffedd5", "#dcfce7",
        "#dbeafe", "#e0e7ff", "#f3e8ff", "#f3f4f6", "#d1d5db", "#9ca3af"
    ];

    private string NormalizeMiniToolbarFontSize()
    {
        var value = _formattingState.FontSize;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "11";
        }

        var normalized = value.Trim().Replace("pt", string.Empty, StringComparison.OrdinalIgnoreCase);
        return double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var size)
            ? size.ToString("0.##", CultureInfo.InvariantCulture)
            : "11";
    }

    private string NormalizeMiniToolbarTextColor()
        => NormalizeMiniToolbarColorValue(_formattingState.TextColor, string.Empty);

    private string NormalizeMiniToolbarHighlightColor()
        => NormalizeMiniToolbarColorValue(_formattingState.HighlightColor, string.Empty);

    private static string GetMiniToolbarColorPickerClass(bool mixed)
        => mixed
            ? "tm-document-editor__mini-color-picker tm-document-editor__mini-color-picker--mixed"
            : "tm-document-editor__mini-color-picker";

    private Task HandleMiniToolbarFontSizeChangedAsync(ChangeEventArgs args)
    {
        var selection = GetMiniToolbarSelectionSnapshot();
        return double.TryParse(args.Value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var size)
            ? RunFloatingSelectionRegistryCommandAsync(selection, "fontSize", size, false)
            : Task.CompletedTask;
    }

    private Task HandleMiniToolbarTextColorAppliedAsync(string? value)
    {
        var normalized = NormalizeMiniToolbarColorValue(value, string.Empty);
        var selection = GetMiniToolbarSelectionSnapshot();
        return string.IsNullOrWhiteSpace(normalized)
            ? Task.CompletedTask
            : RunFloatingSelectionRegistryCommandAsync(selection, "textColor", normalized, false);
    }

    private Task HandleMiniToolbarHighlightColorAppliedAsync(string? value)
    {
        var normalized = NormalizeMiniToolbarColorValue(value, string.Empty);
        var selection = GetMiniToolbarSelectionSnapshot();
        return string.IsNullOrWhiteSpace(normalized)
            ? Task.CompletedTask
            : RunFloatingSelectionRegistryCommandAsync(selection, "highlightColor", normalized, false);
    }

    private Task HandleMiniToolbarColorPickerOpenChangedAsync(bool isOpen)
    {
        _miniToolbarColorPickerOpen = isOpen;
        _keepMiniToolbarVisibleUntil = isOpen
            ? DateTimeOffset.UtcNow.AddMinutes(10)
            : DateTimeOffset.UtcNow.AddSeconds(1);

        if (isOpen && _miniToolbar is null && IsVisibleRangeMiniToolbarRequest(_lastMiniToolbarRequest))
        {
            _lastMiniToolbarRequest.IsVisible = true;
            return HandleMiniToolbarChangedAsync(_lastMiniToolbarRequest);
        }

        return Task.CompletedTask;
    }

    private WysiwygSelectionSnapshot? GetMiniToolbarSelectionSnapshot()
        => FirstRangeSelection(
            _miniToolbar?.Selection,
            _lastMiniToolbarRequest?.Selection,
            _lastBodyRangeSelectionSnapshot);

    private static WysiwygSelectionSnapshot? FirstRangeSelection(params WysiwygSelectionSnapshot?[] selections)
        => selections.FirstOrDefault(selection => selection?.IsCollapsed == false);

    private static string NormalizeMiniToolbarColorValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = $"#{trimmed}";
        }

        if (trimmed.Length == 4 && trimmed.Skip(1).All(Uri.IsHexDigit))
        {
            return $"#{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}{trimmed[3]}{trimmed[3]}".ToLowerInvariant();
        }

        return trimmed.Length == 7 && trimmed.Skip(1).All(Uri.IsHexDigit)
            ? trimmed.ToLowerInvariant()
            : fallback;
    }

    private bool IsPageBreakContextMenu =>
        _textContextMenu is { BlockId: { Length: > 0 } }
        && string.Equals(_textContextMenu.BlockType, "PageBreak", StringComparison.OrdinalIgnoreCase);

    private bool CanCopyTextContextSelection =>
        _textContextMenu?.Selection is { IsCollapsed: false }
        && _wysiwygHost is not null;

    private async Task DeletePageBreakFromContextAsync()
    {
        if (_wysiwygHost is not null && _textContextMenu is { BlockId: { Length: > 0 } blockId })
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("deletePageBreak", new { BlockId = blockId });
        }

        CloseFloatingUi();
    }

    private async Task RemoveLinkFromTextContextAsync()
    {
        var selection = _textContextMenu?.Selection;
        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            await _wysiwygHost.ExecuteEditorCommandAsync("removeLink", new
            {
                Selection = selection
            });
        }

        CloseFloatingUi();
    }

    private async Task CopyTextContextSelectionAsync()
    {
        var selection = _textContextMenu?.Selection;
        if (_wysiwygHost is not null && selection is { IsCollapsed: false })
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            await _wysiwygHost.CopySelectionToClipboardAsync();
        }

        CloseFloatingUi();
    }

    private Task ClearFormattingFromTextContextAsync()
    {
        var selection = _textContextMenu?.Selection;
        return RunFloatingSelectionCommandAsync(selection, () => ClearInlineFormattingAsync(selection));
    }

    private Task InsertTableRowBeforeFromContextAsync()
        => RunTableContextCommandAsync("insertTableRowBefore");

    private Task InsertTableRowFromContextAsync()
        => RunTableContextCommandAsync("insertTableRowAfter");

    private Task InsertTableColumnBeforeFromContextAsync()
        => RunTableContextCommandAsync("insertTableColumnBefore");

    private Task InsertTableColumnFromContextAsync()
        => RunTableContextCommandAsync("insertTableColumnAfter");

    private Task DeleteTableRowFromContextAsync()
        => RunTableContextCommandAsync("deleteTableRow");

    private Task DeleteTableColumnFromContextAsync()
        => RunTableContextCommandAsync("deleteTableColumn");

    private Task DeleteTableFromContextAsync()
        => RunTableContextCommandAsync("deleteTable");

    private Task MergeTableCellsFromContextAsync()
        => RunTableContextCommandAsync("mergeTableCells");

    private Task SplitTableCellFromContextAsync()
        => RunTableContextCommandAsync("splitTableCell");

    private Task ToggleTableHeaderRowFromContextAsync()
        => RunTableContextCommandAsync("toggleTableHeaderRow");

    private Task OpenTablePropertiesFromContextAsync()
        => RunTableContextPanelCommandAsync(openTableProperties: true);

    private Task OpenCellPropertiesFromContextAsync()
        => RunTableContextPanelCommandAsync(openTableProperties: false);

    private async Task RunTableContextPanelCommandAsync(bool openTableProperties)
    {
        var selection = NormalizeTableContextSelection();
        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            selection = await ResolveActiveTableSelectionAsync(selection);
            if (openTableProperties)
            {
                await _wysiwygHost.OpenTablePropertiesPanelAsync(selection);
            }
            else
            {
                await _wysiwygHost.OpenCellPropertiesPanelAsync(selection);
            }
        }

        CloseFloatingUi();
    }

    private async Task RunTableContextCommandAsync(string command)
    {
        var selection = NormalizeTableContextSelection();
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync(command, new
            {
                cellId = selection?.ActiveTableCellId,
                tableId = selection?.ActiveTableId
            });
            await SyncCanvasEngineStateAsync();
            CloseFloatingUi();
            await _canvasHost.FocusAsync();
            return;
        }

        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            selection = await ResolveActiveTableSelectionAsync(selection);
            await _wysiwygHost.ExecuteEditorCommandAsync(command, new
            {
                selection.ActiveTableCellId,
                selection.AnchorBlockId,
                selection.FocusBlockId
            });
        }

        CloseFloatingUi();
    }

    private WysiwygSelectionSnapshot? NormalizeTableContextSelection()
    {
        var selection = _tableContextMenu?.Selection;
        if (selection is not null
            && string.IsNullOrWhiteSpace(selection.ActiveTableCellId)
            && !string.IsNullOrWhiteSpace(_tableContextMenu?.CellId))
        {
            selection.ActiveTableCellId = _tableContextMenu.CellId;
            if (string.IsNullOrWhiteSpace(selection.Region))
            {
                selection.Region = "TableCell";
            }
        }

        return selection;
    }

    private async Task<WysiwygSelectionSnapshot> ResolveActiveTableSelectionAsync(WysiwygSelectionSnapshot fallback)
    {
        if (_wysiwygHost is null)
        {
            return fallback;
        }

        var runtimeSelection = await _wysiwygHost.RequestRuntimeSelectionAsync();
        return string.IsNullOrWhiteSpace(runtimeSelection?.ActiveTableCellId)
            ? fallback
            : runtimeSelection;
    }

    private async Task RunFloatingSelectionCommandAsync(WysiwygSelectionSnapshot? selection, Func<Task> command, bool closeAfterCommand = true)
    {
        var previousMiniToolbar = _miniToolbar ?? _lastMiniToolbarRequest;
        if (!closeAfterCommand)
        {
            _keepMiniToolbarVisibleUntil = DateTimeOffset.UtcNow.AddSeconds(4);
            _ignoreFloatingCollapsedSelectionUntil = DateTimeOffset.UtcNow.AddSeconds(1);
        }

        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            RememberBodySelection(selection);
        }

        await command();
        if (closeAfterCommand)
        {
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.ExecuteEditorCommandAsync("hideMiniToolbar");
            }

            CloseFloatingUi();
        }
        else
        {
            await RestoreMiniToolbarAfterFloatingCommandAsync(previousMiniToolbar, selection);
            await RefreshFormattingStateAfterFloatingCommandAsync(selection, null, null);
        }
    }

    private async Task RunFloatingSelectionRegistryCommandAsync(
        WysiwygSelectionSnapshot? selection,
        string commandName,
        object? payload = null,
        bool closeAfterCommand = true)
    {
        var previousMiniToolbar = _miniToolbar ?? _lastMiniToolbarRequest;
        if (!IsCommandEnabled(commandName))
        {
            return;
        }

        if (!closeAfterCommand)
        {
            _keepMiniToolbarVisibleUntil = DateTimeOffset.UtcNow.AddSeconds(4);
            _ignoreFloatingCollapsedSelectionUntil = DateTimeOffset.UtcNow.AddSeconds(1);
        }

        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            RememberBodySelection(selection);
        }

        ApplyFloatingFormattingOptimisticState(selection, commandName, payload);
        ApplyPendingFloatingFormattingOverride();
        await InvokeAsync(StateHasChanged);

        var commandPayload = commandName == "clearFormatting" && payload is null && selection is not null
            ? selection
            : payload;
        await _commandRegistry.ExecuteAsync(commandName, BuildCommandContext(), commandPayload);
        if (closeAfterCommand)
        {
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.ExecuteEditorCommandAsync("hideMiniToolbar");
            }

            CloseFloatingUi();
        }
        else
        {
            await RestoreMiniToolbarAfterFloatingCommandAsync(previousMiniToolbar, selection);
            await RefreshFormattingStateAfterFloatingCommandAsync(selection, commandName, payload);
        }
    }

    private Task ApplyDefaultFloatingLinkAsync()
    {
        return ApplyLinkAsync("https://example.com");
    }

    private static string FormattingAriaPressed(WysiwygFormattingValue value)
        => value switch
        {
            WysiwygFormattingValue.Active => "true",
            WysiwygFormattingValue.Mixed => "mixed",
            _ => "false"
        };

    private async Task RefreshFormattingStateAfterFloatingCommandAsync(
        WysiwygSelectionSnapshot? selection,
        string? commandName,
        object? payload)
    {
        if (selection is null)
        {
            return;
        }

        _formattingState = await ResolveRuntimeFormattingStateAsync(selection);
        ApplyFloatingFormattingOptimisticState(selection, commandName, payload);
        ApplyPendingFloatingFormattingOverride();
        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task RestoreMiniToolbarAfterFloatingCommandAsync(
        WysiwygMiniToolbarRequest? previousMiniToolbar,
        WysiwygSelectionSnapshot? selection)
    {
        if (previousMiniToolbar is null)
        {
            return;
        }

        previousMiniToolbar.Selection = FirstRangeSelection(selection, previousMiniToolbar.Selection);
        if (previousMiniToolbar.Selection is null)
        {
            _miniToolbar = null;
            _floatingLayerStack.Remove(FloatingLayerId.MiniToolbar);
            await InvokeAsync(StateHasChanged);
            return;
        }

        previousMiniToolbar.IsVisible = true;
        await HandleMiniToolbarChangedAsync(previousMiniToolbar);
    }

    private void ApplyFloatingFormattingOptimisticState(WysiwygSelectionSnapshot? selection, string? commandName, object? payload)
    {
        if (commandName is null)
        {
            return;
        }

        if (selection is not null)
        {
            _formattingState.CurrentSelection = selection;
        }

        switch (commandName)
        {
            case "textColor" when payload is string textColor:
                _optimisticFloatingTextColor = textColor;
                _optimisticFloatingFormattingSelectionKey = GetFloatingFormattingSelectionKey(selection);
                _optimisticFloatingFormattingExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
                _formattingState.TextColor = textColor;
                _formattingState.TextColorMixed = false;
                break;
            case "highlightColor" when payload is string highlightColor:
                _optimisticFloatingHighlightColor = highlightColor;
                _optimisticFloatingFormattingSelectionKey = GetFloatingFormattingSelectionKey(selection);
                _optimisticFloatingFormattingExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
                _formattingState.HighlightColor = highlightColor;
                _formattingState.HighlightColorMixed = false;
                break;
            case "fontSize" when payload is double fontSize:
                _optimisticFloatingFontSize = $"{fontSize.ToString("0.##", CultureInfo.InvariantCulture)}pt";
                _optimisticFloatingFormattingSelectionKey = GetFloatingFormattingSelectionKey(selection);
                _optimisticFloatingFormattingExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
                _formattingState.FontSize = _optimisticFloatingFontSize;
                _formattingState.FontSizeMixed = false;
                break;
            case "fontSize" when payload is int fontSize:
                _optimisticFloatingFontSize = $"{fontSize.ToString(CultureInfo.InvariantCulture)}pt";
                _optimisticFloatingFormattingSelectionKey = GetFloatingFormattingSelectionKey(selection);
                _optimisticFloatingFormattingExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
                _formattingState.FontSize = _optimisticFloatingFontSize;
                _formattingState.FontSizeMixed = false;
                break;
            case "fontSize" when payload is string fontSize:
                _optimisticFloatingFontSize = fontSize.EndsWith("pt", StringComparison.OrdinalIgnoreCase) ? fontSize : $"{fontSize}pt";
                _optimisticFloatingFormattingSelectionKey = GetFloatingFormattingSelectionKey(selection);
                _optimisticFloatingFormattingExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
                _formattingState.FontSize = _optimisticFloatingFontSize;
                _formattingState.FontSizeMixed = false;
                break;
            case "clearFormatting":
                _optimisticFloatingTextColor = null;
                _optimisticFloatingHighlightColor = null;
                _optimisticFloatingFontSize = null;
                _optimisticFloatingFormattingSelectionKey = null;
                _optimisticFloatingFormattingExpiresAt = default;
                _formattingState.Bold = WysiwygFormattingValue.Inactive;
                _formattingState.Italic = WysiwygFormattingValue.Inactive;
                _formattingState.Underline = WysiwygFormattingValue.Inactive;
                _formattingState.Strikethrough = WysiwygFormattingValue.Inactive;
                _formattingState.FontSize = null;
                _formattingState.FontSizeMixed = false;
                _formattingState.TextColor = null;
                _formattingState.TextColorMixed = false;
                _formattingState.HighlightColor = null;
                _formattingState.HighlightColorMixed = false;
                break;
        }
    }

    private void ApplyPendingFloatingFormattingOverride()
    {
        if (_optimisticFloatingFormattingExpiresAt == default)
        {
            return;
        }

        var currentSelectionKey = GetFloatingFormattingSelectionKey(_formattingState.CurrentSelection);
        var lastBodySelectionKey = GetFloatingFormattingSelectionKey(_lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot);
        var selectionMatches = string.IsNullOrWhiteSpace(_optimisticFloatingFormattingSelectionKey)
            || string.Equals(_optimisticFloatingFormattingSelectionKey, currentSelectionKey, StringComparison.Ordinal)
            || string.Equals(_optimisticFloatingFormattingSelectionKey, lastBodySelectionKey, StringComparison.Ordinal);
        if (DateTimeOffset.UtcNow > _optimisticFloatingFormattingExpiresAt
            || !selectionMatches)
        {
            _optimisticFloatingTextColor = null;
            _optimisticFloatingHighlightColor = null;
            _optimisticFloatingFontSize = null;
            _optimisticFloatingFormattingSelectionKey = null;
            _optimisticFloatingFormattingExpiresAt = default;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_optimisticFloatingFontSize))
        {
            _formattingState.FontSize = _optimisticFloatingFontSize;
            _formattingState.FontSizeMixed = false;
        }

        if (!string.IsNullOrWhiteSpace(_optimisticFloatingTextColor))
        {
            _formattingState.TextColor = _optimisticFloatingTextColor;
            _formattingState.TextColorMixed = false;
        }

        if (!string.IsNullOrWhiteSpace(_optimisticFloatingHighlightColor))
        {
            _formattingState.HighlightColor = _optimisticFloatingHighlightColor;
            _formattingState.HighlightColorMixed = false;
        }
    }

    private bool ShouldIgnoreTransientFloatingCollapsedSelection(WysiwygSelectionSnapshot? snapshot)
    {
        if (DateTimeOffset.UtcNow > _ignoreFloatingCollapsedSelectionUntil)
        {
            return false;
        }

        if (snapshot?.IsCollapsed == false)
        {
            return false;
        }

        return _lastBodyRangeSelectionSnapshot is not null
            && (_miniToolbar is not null || _lastMiniToolbarRequest is not null);
    }

    private static string? GetFloatingFormattingSelectionKey(WysiwygSelectionSnapshot? selection)
    {
        if (selection is null)
        {
            return null;
        }

        return string.Join('|',
            string.IsNullOrWhiteSpace(selection.Region) ? "Body" : selection.Region,
            selection.HeaderFooterId ?? string.Empty,
            selection.AnchorBlockId ?? string.Empty,
            selection.FocusBlockId ?? string.Empty,
            selection.AnchorBlockOffset.ToString(CultureInfo.InvariantCulture),
            selection.FocusBlockOffset.ToString(CultureInfo.InvariantCulture),
            selection.IsCollapsed ? "1" : "0");
    }

    private void CloseFloatingUi()
    {
        if (_textContextMenu is null && _tableContextMenu is null && _miniToolbar is null)
        {
            return;
        }

        _textContextMenu = null;
        _tableContextMenu = null;
        _miniToolbar = null;
        _lastMiniToolbarRequest = null;
        _miniToolbarColorPickerOpen = false;
        _keepMiniToolbarVisibleUntil = default;
        _ignoreFloatingCollapsedSelectionUntil = default;
        _floatingLayerStack.Remove(FloatingLayerId.TextContextMenu);
        _floatingLayerStack.Remove(FloatingLayerId.TableContextMenu);
        _floatingLayerStack.Remove(FloatingLayerId.MiniToolbar);
        _focusManager.PushRestoreTarget("surface");
    }

    private static string FloatingStyle(WysiwygFloatingUiPosition position)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: {position.Left:0.##}px; top: {position.Top:0.##}px;");
    }

    private static string MiniToolbarFloatingStyle(WysiwygMiniToolbarRequest position)
    {
        var width = Math.Max(240, position.Width);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: {position.Left:0.##}px; top: {position.Top:0.##}px; inline-size: min({width:0.##}px, calc(100vw - 1rem));");
    }

    private WysiwygFormattingState ComputeFormattingState(WysiwygSelectionSnapshot snapshot)
    {
        var (paragraphAlignment, paragraphAlignmentMixed) = ComputeParagraphAlignmentState(snapshot);
        return new WysiwygFormattingState
        {
            Bold = ComputeMarkState(snapshot, InlineMarkType.Bold),
            Italic = ComputeMarkState(snapshot, InlineMarkType.Italic),
            Underline = ComputeMarkState(snapshot, InlineMarkType.Underline),
            Strikethrough = ComputeMarkState(snapshot, InlineMarkType.Strikethrough),
            ParagraphAlignment = paragraphAlignment,
            ParagraphAlignmentMixed = paragraphAlignmentMixed
        };
    }

    private (DocumentTextAlignment Alignment, bool Mixed) ComputeParagraphAlignmentState(WysiwygSelectionSnapshot snapshot)
    {
        var blocks = ResolveSelectedBlocks(snapshot).ToList();
        if (blocks.Count == 0)
        {
            return (DocumentTextAlignment.Left, false);
        }

        var first = blocks[0].ParagraphProperties?.Alignment ?? DocumentTextAlignment.Left;
        var mixed = blocks.Any(block => (block.ParagraphProperties?.Alignment ?? DocumentTextAlignment.Left) != first);
        return (first, mixed);
    }

    private IEnumerable<DocumentBlock> ResolveSelectedBlocks(WysiwygSelectionSnapshot snapshot)
    {
        var document = DisplayedDocument;
        if (document is null || string.IsNullOrWhiteSpace(snapshot.AnchorBlockId))
        {
            return [];
        }

        var blocks = ResolveSelectionBlocks(document, snapshot);
        var anchorIndex = blocks.FindIndex(block => block.Id == snapshot.AnchorBlockId);
        if (anchorIndex < 0)
        {
            return [];
        }

        var focusBlockId = string.IsNullOrWhiteSpace(snapshot.FocusBlockId)
            ? snapshot.AnchorBlockId
            : snapshot.FocusBlockId;
        var focusIndex = blocks.FindIndex(block => block.Id == focusBlockId);
        if (focusIndex < 0)
        {
            focusIndex = anchorIndex;
        }

        var start = Math.Min(anchorIndex, focusIndex);
        var end = Math.Max(anchorIndex, focusIndex);
        return blocks
            .Skip(start)
            .Take(end - start + 1)
            .Where(block => block.Content is ParagraphBlockContent or HeadingBlockContent or ListBlockContent or QuoteBlockContent)
            .ToList();
    }

    private WysiwygFormattingValue ComputeMarkState(WysiwygSelectionSnapshot snapshot, InlineMarkType markType)
    {
        var document = DisplayedDocument;
        var block = document is null
            ? null
            : FindBlockForSelection(document, snapshot.AnchorBlockId, snapshot);
        if (block is null)
        {
            return WysiwygFormattingValue.Inactive;
        }

        var inlines = GetEditableInlines(block.Content);
        if (inlines is null || inlines.Count == 0)
        {
            return WysiwygFormattingValue.Inactive;
        }

        var textLength = inlines.Sum(inline => GetInlineText(inline).Length);
        var start = Math.Min(snapshot.AnchorOffset, snapshot.FocusOffset);
        var end = Math.Max(snapshot.AnchorOffset, snapshot.FocusOffset);
        start = Math.Clamp(start, 0, textLength);
        end = Math.Clamp(end, start, textLength);

        if (snapshot.IsCollapsed || start == end)
        {
            var inline = ResolveInlineAtOffset(inlines, snapshot.AnchorInlineId, start);
            return inline?.Marks.Any(mark => mark.Type == markType) == true
                ? WysiwygFormattingValue.Active
                : WysiwygFormattingValue.Inactive;
        }

        var current = 0;
        var any = false;
        var all = true;
        foreach (var inline in inlines)
        {
            var length = GetInlineText(inline).Length;
            var inlineStart = current;
            var inlineEnd = current + length;
            current = inlineEnd;

            if (inlineEnd <= start || inlineStart >= end)
            {
                continue;
            }

            any |= inline.Marks.Any(mark => mark.Type == markType);
            all &= inline.Marks.Any(mark => mark.Type == markType);
        }

        return any switch
        {
            true when all => WysiwygFormattingValue.Active,
            true => WysiwygFormattingValue.Mixed,
            _ => WysiwygFormattingValue.Inactive
        };
    }

    private static List<DocumentBlock> ResolveSelectionBlocks(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot snapshot)
    {
        if ((string.Equals(snapshot.Region, "Header", StringComparison.OrdinalIgnoreCase)
                || string.Equals(snapshot.Region, "Footer", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(snapshot.HeaderFooterId))
        {
            var headerFooter = document.HeadersFooters.FirstOrDefault(item =>
                string.Equals(item.Id, snapshot.HeaderFooterId, StringComparison.Ordinal));
            if (headerFooter is not null)
            {
                return headerFooter.Blocks;
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.AnchorBlockId)
            && !document.Blocks.Any(block => block.Id == snapshot.AnchorBlockId))
        {
            var headerFooter = document.HeadersFooters.FirstOrDefault(item =>
                item.Blocks.Any(block => block.Id == snapshot.AnchorBlockId));
            if (headerFooter is not null)
            {
                return headerFooter.Blocks;
            }
        }

        return document.Blocks;
    }

    private static DocumentBlock? FindBlockForSelection(
        DocumentEditorDocument document,
        string? blockId,
        WysiwygSelectionSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return null;
        }

        return ResolveSelectionBlocks(document, snapshot).FirstOrDefault(block => block.Id == blockId)
            ?? document.Blocks.FirstOrDefault(block => block.Id == blockId)
            ?? document.HeadersFooters.SelectMany(headerFooter => headerFooter.Blocks).FirstOrDefault(block => block.Id == blockId);
    }

    private static InlineContent? ResolveInlineAtOffset(List<InlineContent> inlines, string? inlineId, int offset)
    {
        if (!string.IsNullOrWhiteSpace(inlineId))
        {
            var exact = inlines.FirstOrDefault(inline => inline.Id == inlineId);
            if (exact is not null)
            {
                return exact;
            }
        }

        var current = 0;
        foreach (var inline in inlines)
        {
            var length = GetInlineText(inline).Length;
            if (offset <= current + Math.Max(length, 1))
            {
                return inline;
            }

            current += length;
        }

        return inlines.LastOrDefault();
    }


    private async Task HandleWysiwygTransactionCommittedAsync()
    {
        _activeWysiwygTransactionId = null;
        if (_wysiwygHost is null)
        {
            return;
        }

        var runtimeSelection = await _wysiwygHost.RequestRuntimeSelectionAsync();
        if (runtimeSelection is null)
        {
            return;
        }

        var runtimeRegion = string.IsNullOrWhiteSpace(runtimeSelection.Region)
            ? "Body"
            : runtimeSelection.Region;
        if (string.Equals(runtimeRegion, _activeWysiwygRegion, StringComparison.Ordinal)
            && string.Equals(runtimeSelection.HeaderFooterId, _selection.HeaderFooterId, StringComparison.Ordinal)
            && runtimeSelection.PageIndex == _selection.PageIndex)
        {
            return;
        }

        _activeWysiwygRegion = runtimeRegion;
        _lastWysiwygSelectionSnapshot = runtimeSelection;
        _selection.Region = runtimeRegion;
        _selection.HeaderFooterId = runtimeSelection.HeaderFooterId;
        _selection.PageIndex = runtimeSelection.PageIndex;
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleWysiwygUndoStateChangedAsync(WysiwygUndoState state)
    {
        _wysiwygUndoState = state ?? new WysiwygUndoState();
        await RefreshCommandRegistryAsync();
        _suppressNextWysiwygStateRender = true;
    }

    private async Task HandleWysiwygDirtyStateChangedAsync(WysiwygDirtyState state)
    {
        _wysiwygDirtyState = state ?? new WysiwygDirtyState();
        var wasDirty = _isDirty;
        _isDirty = _wysiwygDirtyState.IsDirty;
        if (_isDirty && _document is not null)
        {
            _suggestionSnapshot = Clone(_document);
            _autosave.RegisterLocalChange();
            if (_isSaving)
            {
                _saveAgainRequested = true;
            }

            ScheduleAutoSave();
        }
        else if (!_isDirty)
        {
            _autosave.ResetSynchronized();
        }

        SyncAutosavePendingAction();
        await UpdateBeforeUnloadGuardAsync();

        if (wasDirty == _isDirty)
        {
            _suppressNextWysiwygStateRender = true;
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateBeforeUnloadGuardAsync()
    {
        if (ReadOnly || _disposed) return;
        var shouldGuard = _isDirty || _pendingActions.HasAny;
        try
        {
            if (shouldGuard)
                await JSRuntime.InvokeVoidAsync("tmDocumentEditor.enableBeforeUnloadGuard");
            else
                await JSRuntime.InvokeVoidAsync("tmDocumentEditor.disableBeforeUnloadGuard");
        }
        catch
        {
            // JS interop may fail during prerender or dispose — ignore silently.
        }
    }

    private Task HandleImageUploadStateChangedAsync(bool isUploading)
    {
        if (isUploading)
            _pendingActions.Add(PendingActionId.ImageUpload, Loc["TmDocumentEditor_UploadingImage"]);
        else
            _pendingActions.Remove(PendingActionId.ImageUpload);
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleRuntimeRecoveredAsync()
    {
        _runtimeMessage = Loc["TmDocumentEditor_RuntimeRecovered"];
        _runtimeFailed = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleRuntimeRecoveryFailedAsync()
    {
        _runtimeMessage = Loc["TmDocumentEditor_RuntimeRecoveryFailed"];
        _runtimeFailed = true;
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleRuntimeRecoveryDetailAsync(WysiwygRuntimeRecoveryDetail detail)
    {
        _lastRuntimeRecoveryDetail = detail;
        _runtimeFailed = string.Equals(detail.Event, "runtimeRecoveryFailed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(detail.State, "failed", StringComparison.OrdinalIgnoreCase);
        _runtimeMessage = BuildRuntimeRecoveryMessage(detail);
        return InvokeAsync(StateHasChanged);
    }

    private string BuildRuntimeRecoveryMessage(WysiwygRuntimeRecoveryDetail detail)
    {
        var source = NormalizeRuntimeRecoverySource(detail.Source);
        var message = _runtimeFailed
            ? Loc["TmDocumentEditor_RuntimeRecoveryFailed"]
            : source switch
            {
                "remoteOperation" => Loc["TmDocumentEditor_RuntimeRecoveredRemoteOperation"],
                "render" => Loc["TmDocumentEditor_RuntimeRecoveredRender"],
                "serialization" => Loc["TmDocumentEditor_RuntimeRecoveredSerialization"],
                _ => Loc["TmDocumentEditor_RuntimeRecoveredCommand"]
            };

        return detail.UsedSnapshotFallback
            ? $"{message} {Loc["TmDocumentEditor_RuntimeRecoveredSnapshotFallback"]}"
            : message;
    }

    private static string NormalizeRuntimeRecoverySource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "command";
        }

        return source.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant() switch
            {
                "remoteoperation" or "remoteoperations" or "remotebatch" => "remoteOperation",
                "render" or "rendering" => "render",
                "serialization" or "serialize" => "serialization",
                _ => "command"
            };
    }

    private async Task ToggleDocumentProtectionAsync()
    {
        if (_document is null) return;
        _isDocumentProtected = !_isDocumentProtected;
        _document.IsProtected = _isDocumentProtected;
        if (!_isDocumentProtected)
        {
            _isCaretInEditableRegion = false;
            _document.RestrictedMarkers.Clear();
        }
        _document.BumpVersion();  // Phase C1
        if (_wysiwygHost is not null)
            await _wysiwygHost.SetProtectionModeAsync(_isDocumentProtected, BuildMarkerPayloads());
        await RefreshCommandRegistryAsync();
    }

    private async Task MarkEditableRegionAsync()
    {
        if (_document is null || !_isDocumentProtected) return;
        var snap = _lastBodySelectionSnapshot;
        if (snap is null || string.IsNullOrEmpty(snap.AnchorBlockId)) return;

        var startBlockId = snap.AnchorBlockId;
        var endBlockId = string.IsNullOrEmpty(snap.FocusBlockId) ? startBlockId : snap.FocusBlockId;
        var startOffset = Math.Min(snap.AnchorOffset, snap.AnchorOffset == snap.FocusOffset ? snap.FocusOffset : snap.AnchorOffset);
        var endOffset = Math.Max(snap.AnchorOffset, snap.FocusOffset);

        if (startOffset == endOffset && startBlockId == endBlockId)
            endOffset = startOffset + 1;

        var marker = new DocumentRestrictedMarker
        {
            StartBlockId = startBlockId,
            StartOffset = startOffset,
            EndBlockId = endBlockId,
            EndOffset = endOffset
        };
        _document.RestrictedMarkers.Add(marker);
        _document.BumpVersion();  // Phase C1

        if (_wysiwygHost is not null)
            await _wysiwygHost.SetProtectionModeAsync(true, BuildMarkerPayloads());
        await RefreshCommandRegistryAsync();
    }

    private IEnumerable<object> BuildMarkerPayloads() =>
        _document?.RestrictedMarkers.Select(m => (object)new
        {
            startBlockId = m.StartBlockId,
            startOffset = m.StartOffset,
            endBlockId = m.EndBlockId,
            endOffset = m.EndOffset
        }) ?? [];

    private async Task ToggleShowBlocksAsync()
    {
        _showBlocks = !_showBlocks;
        if (await RouteToCanvasEngineAsync("showBlocks", _showBlocks))
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_wysiwygHost is not null)
            await _wysiwygHost.SetShowBlocksAsync(_showBlocks);
        await RefreshCommandRegistryAsync();
    }

    private async Task ToggleNonPrintingCharactersAsync()
    {
        _showNonPrintingCharacters = !_showNonPrintingCharacters;
        if (await RouteToCanvasEngineAsync("toggleNonPrintingCharacters", _showNonPrintingCharacters))
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_wysiwygHost is not null)
            await _wysiwygHost.SetShowNonPrintingCharactersAsync(_showNonPrintingCharacters);
        await RefreshCommandRegistryAsync();
    }

    private async Task ToggleFullscreenAsync()
    {
        _isFullscreen = !_isFullscreen;
        if (_wysiwygHost is not null)
            await _wysiwygHost.SetFullscreenAsync(_isFullscreen);
        await RefreshCommandRegistryAsync();
    }

    private async Task NavigateToBlockAsync(string blockId)
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync("gotoHeading", new { blockId });
            await SyncCanvasEngineStateAsync();
            return;
        }

        if (_wysiwygHost is not null)
            await _wysiwygHost.ScrollToBlockAsync(blockId);
    }

    private async Task NavigateToPageAsync(int pageIndex)
    {
        _activePageIndex = Math.Max(0, pageIndex);
        if (_wysiwygHost is not null)
            await _wysiwygHost.ScrollToPageAsync(_activePageIndex);
    }

    private Task HandlePageMetricsChangedAsync(WysiwygPageMetrics metrics)
    {
        _pageMetrics = metrics ?? new WysiwygPageMetrics();
        _activePageIndex = Math.Max(0, _pageMetrics.ActivePageIndex);
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleActiveHeadingChangedAsync(string? blockId)
    {
        _activeHeadingBlockId = string.IsNullOrWhiteSpace(blockId) ? null : blockId;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ViewDocumentJsonAsync()
    {
        if (!ShowDebugTools) return;
        await RefreshDocumentDebugSnapshotAsync();
        _jsonDebugModalOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    private Task CloseJsonDebugModalAsync()
    {
        _jsonDebugModalOpen = false;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ViewClipboardHtmlAsync()
    {
        if (!ShowDebugTools) return;
        var debugSnapshot = _wysiwygHost?.GetClipboardDebugSnapshot()
            ?? (_canvasHost is not null
                ? await _canvasHost.GetClipboardDebugSnapshotAsync()
                : new DocumentClipboardDebugSnapshot());
        _clipboardDebugRawHtml = debugSnapshot.RawHtml;
        _clipboardDebugNormalizedJson = debugSnapshot.NormalizedJson;
        _clipboardDebugWarningsJson = JsonSerializer.Serialize(debugSnapshot.Warnings, new JsonSerializerOptions(DocumentEditorJson.Options)
        {
            WriteIndented = true
        });
        _clipboardHtmlSnapshot = string.IsNullOrWhiteSpace(debugSnapshot.RawHtml) && _wysiwygHost is not null
            ? await _wysiwygHost.GetBodyHtmlAsync()
            : debugSnapshot.RawHtml;
        _clipboardHtmlModalOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    private Task CloseClipboardHtmlModalAsync()
    {
        _clipboardHtmlModalOpen = false;
        return InvokeAsync(StateHasChanged);
    }

    private string GetDocumentJson()
    {
        if (_document is null) return string.Empty;
        return JsonSerializer.Serialize(_document, new JsonSerializerOptions(DocumentEditorJson.Options)
        {
            WriteIndented = true
        });
    }

    private async Task CopyDocumentDebugJsonAsync()
    {
        if (!ShowDebugTools) return;
        try
        {
            await RefreshDocumentDebugSnapshotAsync();
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", BuildDocumentDebugJson());
        }
        catch (JSException)
        {
            // Clipboard access can be denied outside a trusted user gesture.
        }
    }

    private async Task RefreshDocumentDebugSnapshotAsync()
    {
        await GetCurrentDocumentForProviderExportAsync();
        _runtimeDebugJson = _canvasHost is not null
            ? await _canvasHost.GetRuntimeDebugSnapshotJsonAsync()
            : _wysiwygHost is not null
                ? await _wysiwygHost.GetRuntimeDebugSnapshotJsonAsync()
                : string.Empty;
    }

    private string BuildDocumentDebugJson()
    {
        JsonElement? runtimeDebug = string.IsNullOrWhiteSpace(_runtimeDebugJson)
            ? null
            : JsonSerializer.Deserialize<JsonElement>(_runtimeDebugJson);

        return JsonSerializer.Serialize(new
        {
            canonicalDocument = _document,
            runtimeDebug,
            docxDrawingMetadata = BuildDocxDrawingMetadataDebug(),
            runtimeRecovery = _lastRuntimeRecoveryDetail
        }, new JsonSerializerOptions(DocumentEditorJson.Options)
        {
            WriteIndented = true
        });
    }

    private string? GetDocxDrawingMetadataDebugJson()
    {
        var metadata = BuildDocxDrawingMetadataDebug();
        if (metadata.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions(DocumentEditorJson.Options)
        {
            WriteIndented = true
        });
    }

    private List<object> BuildDocxDrawingMetadataDebug()
    {
        if (_document is null)
        {
            return [];
        }

        var drawings = new List<object>();
        foreach (var drawing in DocumentImagePersistence.EnumerateDrawingRuns(_document))
        {
            var docx = drawing.Docx;
            drawings.Add(new
            {
                HasDocxMetadata = docx is not null,
                drawing.ObjectId,
                RunId = drawing.Id,
                drawing.AltText,
                drawing.Caption,
                Anchor = new
                {
                    drawing.Layout.Anchor.BlockId,
                    drawing.Layout.Anchor.Region,
                    drawing.Layout.Anchor.TableId,
                    drawing.Layout.Anchor.CellId,
                    drawing.Layout.Anchor.HeaderFooterId,
                    drawing.Layout.Anchor.InlineIndex,
                    drawing.Layout.Anchor.Offset,
                    drawing.Layout.Anchor.MoveWithText,
                    drawing.Layout.Anchor.FixedOnPage,
                    drawing.Layout.Anchor.LockAnchor
                },
                Wrap = new
                {
                    drawing.Layout.Wrap.Mode,
                    drawing.Layout.Wrap.Side,
                    drawing.Layout.Wrap.DistanceLeft,
                    drawing.Layout.Wrap.DistanceRight,
                    drawing.Layout.Wrap.DistanceTop,
                    drawing.Layout.Wrap.DistanceBottom
                },
                Transform = new
                {
                    drawing.Layout.Transform.Width,
                    drawing.Layout.Transform.Height,
                    drawing.Layout.Transform.NaturalWidth,
                    drawing.Layout.Transform.NaturalHeight,
                    drawing.Layout.Transform.Rotation,
                    drawing.Layout.Transform.LockAspectRatio,
                    Crop = new
                    {
                        drawing.Layout.Transform.Crop.Left,
                        drawing.Layout.Transform.Crop.Top,
                        drawing.Layout.Transform.Crop.Right,
                        drawing.Layout.Transform.Crop.Bottom
                    },
                    Flip = drawing.Layout.Transform.Flip
                },
                DocPr = new
                {
                    docx?.DocPrId,
                    docx?.DocPrName,
                    docx?.DocPrTitle,
                    docx?.DocPrDescription
                },
                Picture = new
                {
                    docx?.PictureNonVisualId,
                    docx?.PictureName,
                    docx?.PictureDescription
                },
                Image = new
                {
                    docx?.RelationshipId,
                    docx?.BlipLinkRelationshipId,
                    docx?.ImageReferenceMode,
                    docx?.BlipCompressionState,
                    docx?.BlipFillMode,
                    docx?.PresetGeometry
                },
                Media = docx?.Media,
                EffectExtent = docx?.EffectExtent,
                AnchorXml = new
                {
                    docx?.LayoutInCell,
                    docx?.Hidden,
                    docx?.UsesSimplePosition,
                    docx?.SimplePosition,
                    docx?.AnchorId,
                    docx?.EditId,
                    docx?.RelativeWidth,
                    docx?.RelativeHeight
                }
            });
        }

        return drawings;
    }

    private string? GetRuntimeRecoveryDetailJson()
    {
        if (_lastRuntimeRecoveryDetail is null)
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Serialize(_lastRuntimeRecoveryDetail, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private Task HandleWysiwygRevisionsChangedAsync(IReadOnlyList<DocumentRevision> runtimeRevisions)
    {
        if (_document is null || runtimeRevisions.Count == 0)
        {
            return Task.CompletedTask;
        }

        var changed = false;
        foreach (var runtimeRevision in runtimeRevisions)
        {
            if (string.IsNullOrWhiteSpace(runtimeRevision.Id))
            {
                continue;
            }

            var existing = _document.Revisions.FirstOrDefault(revision => revision.Id == runtimeRevision.Id);
            if (existing is null)
            {
                _document.Revisions.Add(Clone(runtimeRevision));
                changed = true;
                continue;
            }

            existing.Type = runtimeRevision.Type;
            existing.Range = ChooseRuntimeRevisionRange(existing, runtimeRevision);
            existing.Author = Clone(runtimeRevision.Author);
            existing.CreatedAt = runtimeRevision.CreatedAt;
            existing.Action = runtimeRevision.Action;
            existing.PayloadJson = ChooseRuntimeRevisionPayload(existing, runtimeRevision);
            changed = true;
        }

        if (!changed)
        {
            return Task.CompletedTask;
        }

        _document.BumpVersion();  // Phase C1
        _currentDocument = _document;
        return InvokeAsync(StateHasChanged);
    }

    private static string? ChooseRuntimeRevisionPayload(DocumentRevision existing, DocumentRevision runtimeRevision)
    {
        var current = existing.PayloadJson;
        var incoming = runtimeRevision.PayloadJson;
        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(incoming))
        {
            return string.IsNullOrWhiteSpace(incoming) ? current : incoming;
        }

        if (existing.Action == DocumentRevisionAction.Pending
            && runtimeRevision.Action == DocumentRevisionAction.Pending
            && existing.Type == runtimeRevision.Type
            && current.Contains(incoming, StringComparison.Ordinal)
            && current.Length > incoming.Length)
        {
            return current;
        }

        return incoming;
    }

    private static DocumentRevisionRange ChooseRuntimeRevisionRange(DocumentRevision existing, DocumentRevision runtimeRevision)
    {
        var current = existing.Range;
        var incoming = runtimeRevision.Range;
        if (existing.Action == DocumentRevisionAction.Pending
            && runtimeRevision.Action == DocumentRevisionAction.Pending
            && existing.Type == runtimeRevision.Type
            && string.Equals(current.BlockId, incoming.BlockId, StringComparison.Ordinal)
            && (current.EndOffset ?? 0) > (incoming.EndOffset ?? 0))
        {
            return Clone(current);
        }

        return Clone(incoming);
    }

    private Task HandleWysiwygCommentsChangedAsync(IReadOnlyList<DocumentComment> runtimeComments)
    {
        if (_document is null)
        {
            return Task.CompletedTask;
        }

        _document.Comments = runtimeComments.Select(Clone).ToList();
        if (_currentDocument is not null && !ReferenceEquals(_currentDocument, _document))
        {
            _currentDocument.Comments = runtimeComments.Select(Clone).ToList();
        }

        _comments = runtimeComments.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    private void SyncCommentsFromRuntimeDocument(DocumentEditorDocument document)
    {
        _comments = document.Comments.Select(CloneForEditor).ToList();
        ApplyCommentMarksFromComments(document);
    }

    private static string GetPatchDescription(WysiwygPatch patch)
    {
        return patch.Type switch
        {
            "InsertText" => "Type text",
            "InsertInline" => "Insert inline content",
            "DeleteRange" or "DeleteContentBackward" or "DeleteContentForward" => "Delete",
            "ToggleMark" => $"Apply {patch.MarkType}",
            "SetMarks" => $"Apply {patch.MarkType}",
            "ClearFormatting" => "Clear formatting",
            "SetParagraphProperties" => "Format paragraph",
            "InsertParagraph" or "SplitBlock" => "Insert paragraph",
            "InsertLineBreak" or "InsertSoftBreak" => "Insert line break",
            "InsertBlock" => $"Insert {patch.BlockType}",
            "RemoveBlock" => "Remove block",
            "MoveBlock" => "Move block",
            "UpdateBlock" => "Update block",
            "Paste" => "Paste",
            _ => "Edit"
        };
    }

    private async Task LoadOfflineDraftIfNeededAsync(DocumentEditorDocument serverDocument)
    {
        if (!OfflineEnabled || OfflineStore is null)
        {
            _offlineDraft = null;
            return;
        }

        var drafts = await OfflineStore.ListPendingDraftsAsync(DocumentId);
        _offlineDraft = drafts.FirstOrDefault();
        if (_offlineDraft is null)
        {
            return;
        }

        _offlineStatus = _offlineDraft.SyncStatus;
        if (PreferLocalDraft && IsDraftNewerThanServer(_offlineDraft, serverDocument))
        {
            try
            {
                _document = DocumentEditorJson.Deserialize(_offlineDraft.JsonSnapshot);
                _currentDocument = _document;
                _isDirty = true;
                _runtimeDraftStateJson = _offlineDraft.RuntimeStateJson;
                _offlineMessage = Loc["TmDocumentEditor_OfflineDraftLoaded"];
            }
            catch
            {
                _offlineMessage = Loc["TmDocumentEditor_OfflineDraftLoadFailed"];
            }
        }
        else
        {
            _offlineMessage = Loc["TmDocumentEditor_OfflineDraftAvailable"];
        }
    }

    private static bool IsDraftNewerThanServer(DocumentOfflineDraft draft, DocumentEditorDocument serverDocument)
    {
        var serverTimestamp = serverDocument.Metadata.ModifiedAt ?? serverDocument.Metadata.CreatedAt;
        return draft.UpdatedAt > serverTimestamp;
    }

    private static (int DirtyEpoch, int UndoEpoch) ReadRuntimeDraftEpochs(string? runtimeStateJson)
    {
        if (string.IsNullOrWhiteSpace(runtimeStateJson))
        {
            return (0, 0);
        }

        try
        {
            using var document = JsonDocument.Parse(runtimeStateJson);
            var root = document.RootElement;
            var dirtyEpoch = root.TryGetProperty("dirtyState", out var dirtyState)
                && dirtyState.TryGetProperty("DirtyEpoch", out var dirtyEpochElement)
                    ? dirtyEpochElement.GetInt32()
                    : root.TryGetProperty("dirtyEpoch", out dirtyEpochElement)
                        ? dirtyEpochElement.GetInt32()
                        : 0;
            var undoEpoch = root.TryGetProperty("runtimeUndoEpoch", out var undoEpochElement)
                ? undoEpochElement.GetInt32()
                : root.TryGetProperty("dirtyState", out dirtyState)
                    && dirtyState.TryGetProperty("UndoEpoch", out var dirtyUndoEpochElement)
                        ? dirtyUndoEpochElement.GetInt32()
                        : root.TryGetProperty("undoEpoch", out undoEpochElement)
                            ? undoEpochElement.GetInt32()
                            : 0;
            return (dirtyEpoch, undoEpoch);
        }
        catch
        {
            return (0, 0);
        }
    }

    private Task SaveOfflineDraftAsync()
    {
        return SaveOfflineDraftAsync(_currentDocument);
    }

    private async Task SaveOfflineDraftAsync(
        DocumentEditorDocument? document,
        DocumentOfflineDraftState state = DocumentOfflineDraftState.PendingSync,
        DocumentSyncStatus syncStatus = DocumentSyncStatus.Offline)
    {
        if (!OfflineEnabled || OfflineStore is null || document is null)
        {
            return;
        }

        var documentToDraft = document;
        string? runtimeStateJson = null;
        var runtimeDirtyEpoch = 0;
        var runtimeUndoEpoch = 0;
        if (_wysiwygHost is not null)
        {
            var jsSnapshot = await _wysiwygHost.RequestSnapshotAsync();
            if (jsSnapshot is not null)
            {
                documentToDraft = CreateProviderBoundarySnapshot(documentToDraft, jsSnapshot);
                _document = documentToDraft;
                _currentDocument = documentToDraft;
            }

            runtimeStateJson = await _wysiwygHost.RequestOfflineStateJsonAsync();
            (runtimeDirtyEpoch, runtimeUndoEpoch) = ReadRuntimeDraftEpochs(runtimeStateJson);
        }
        else if (_canvasHost is not null)
        {
            var canvasDocument = await _canvasHost.RequestDocumentAsync();
            if (canvasDocument is not null)
            {
                documentToDraft = CreateProviderBoundarySnapshot(canvasDocument, preserveImageBlocks: true);
                _document = documentToDraft;
                _currentDocument = documentToDraft;
            }

            runtimeStateJson = await _canvasHost.RequestOfflineStateJsonAsync();
            (runtimeDirtyEpoch, runtimeUndoEpoch) = ReadRuntimeDraftEpochs(runtimeStateJson);
        }

        var pendingAssets = CollectPendingAssets(documentToDraft);
        var draft = new DocumentOfflineDraft
        {
            Id = _offlineDraft?.Id ?? Guid.NewGuid().ToString("N"),
            DocumentId = documentToDraft.DocumentId,
            BaseVersionId = _concurrencyToken,
            JsonSnapshot = DocumentEditorJson.Serialize(documentToDraft),
            RuntimeStateJson = runtimeStateJson,
            RuntimeDirtyEpoch = runtimeDirtyEpoch,
            RuntimeUndoEpoch = runtimeUndoEpoch,
            State = state,
            SyncStatus = syncStatus,
            UpdatedAt = DateTimeOffset.UtcNow,
            PendingAssets = pendingAssets,
            PendingClipboardImages = pendingAssets
                .Where(asset => asset.Source == DocumentImageSource.Clipboard)
                .Select(CreatePendingClipboardImage)
                .Where(image => image is not null)
                .Select(image => image!)
                .ToList()
        };

        await OfflineStore.SaveDraftAsync(draft);
        _offlineDraft = draft;
        _offlineStatus = syncStatus;
        _runtimeDraftStateJson = runtimeStateJson;
        _offlineMessage = syncStatus == DocumentSyncStatus.Conflict
            ? Loc["TmDocumentEditor_OfflineConflict"]
            : Loc["TmDocumentEditor_OfflineDraftSaved"];
    }

    private static List<DocumentImageAsset> CollectPendingAssets(DocumentEditorDocument document)
    {
        var assets = document.Assets
            .Where(asset => asset.IsLocalDraft || asset.Source == DocumentImageSource.Clipboard)
            .Select(Clone)
            .ToList();

        var knownIds = assets.Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var image in document.Blocks.Select(block => block.Content).OfType<ImageBlockContent>())
        {
            if (image.Source != DocumentImageSource.Clipboard || string.IsNullOrWhiteSpace(image.AssetId) || knownIds.Contains(image.AssetId))
            {
                continue;
            }

            assets.Add(new DocumentImageAsset
            {
                Id = image.AssetId,
                DocumentId = document.DocumentId,
                Source = DocumentImageSource.Clipboard,
                Url = image.Url,
                ContentType = GetContentTypeFromDataUrl(image.Url),
                FileName = image.AltText,
                AltText = image.AltText,
                IsLocalDraft = true
            });
            knownIds.Add(image.AssetId);
        }

        foreach (var drawing in DocumentImagePersistence.EnumerateDrawingRuns(document))
        {
            if (drawing.Source != DocumentImageSource.Clipboard || string.IsNullOrWhiteSpace(drawing.AssetId) || knownIds.Contains(drawing.AssetId))
            {
                continue;
            }

            assets.Add(new DocumentImageAsset
            {
                Id = drawing.AssetId,
                DocumentId = document.DocumentId,
                Source = DocumentImageSource.Clipboard,
                Url = drawing.Url,
                ContentType = GetContentTypeFromDataUrl(drawing.Url),
                FileName = drawing.AltText,
                AltText = drawing.AltText,
                IsLocalDraft = true
            });
            knownIds.Add(drawing.AssetId);
        }

        return assets;
    }

    private static DocumentClipboardImage? CreatePendingClipboardImage(DocumentImageAsset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Url) || !asset.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var comma = asset.Url.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            return null;
        }

        return new DocumentClipboardImage
        {
            LocalAssetId = asset.Id,
            ContentType = asset.ContentType,
            FileName = asset.FileName,
            Bytes = Convert.FromBase64String(asset.Url[(comma + 1)..])
        };
    }

    private static string GetContentTypeFromDataUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        var semicolon = url.IndexOf(';', StringComparison.Ordinal);
        return semicolon > 5 ? url[5..semicolon] : "image/png";
    }

    private async Task SyncOfflineDraftAsync()
    {
        if (_offlineDraft is null || Provider is null || _isSyncingOfflineDraft)
        {
            return;
        }

        _isSyncingOfflineDraft = true;
        _offlineStatus = DocumentSyncStatus.Syncing;
        _offlineMessage = Loc["TmDocumentEditor_OfflineSyncing"];
        _pendingActions.Add(PendingActionId.OfflineSync, Loc["TmDocumentEditor_OfflineSyncing"]);
        try
        {
            var syncProvider = SyncProvider ?? new InMemoryDocumentSyncProvider(Provider, OfflineStore);
            var result = await syncProvider.SyncAsync(new DocumentSyncRequest { Draft = _offlineDraft });
            if (result.Success)
            {
                _offlineDraft = null;
                _offlineConflict = null;
                _offlineStatus = DocumentSyncStatus.Online;
                _isDirty = false;
                _saveMessage = Loc["TmDocumentEditor_OfflineSyncComplete"];
                _offlineMessage = null;
                _runtimeDraftStateJson = null;
                if (result.SaveResult?.Document is not null)
                {
                    _document = result.SaveResult.Document;
                    _currentDocument = _document;
                    _concurrencyToken = result.SaveResult.ConcurrencyToken;
                }

                if (_wysiwygHost is not null)
                {
                    await _wysiwygHost.MarkSavedAsync(_concurrencyToken);
                }

                if (_canvasHost is not null && _document is not null)
                {
                    await _canvasHost.ReplaceDocumentAsync(_document);
                    await _canvasHost.MarkSavedAsync();
                }
            }
            else if (result.Conflict is not null)
            {
                _offlineConflict = result.Conflict;
                _offlineStatus = DocumentSyncStatus.Conflict;
                _offlineMessage = Loc["TmDocumentEditor_OfflineConflict"];
            }
            else
            {
                _offlineStatus = DocumentSyncStatus.Failed;
                _offlineMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? Loc["TmDocumentEditor_OfflineSyncFailed"]
                    : result.ErrorMessage;
            }
        }
        finally
        {
            _isSyncingOfflineDraft = false;
            _pendingActions.Remove(PendingActionId.OfflineSync);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task DiscardOfflineDraftAsync()
    {
        if (_offlineDraft is not null && OfflineStore is not null)
        {
            await OfflineStore.DeleteDraftAsync(_offlineDraft.Id);
        }

        _offlineDraft = null;
        _offlineConflict = null;
        _offlineStatus = DocumentSyncStatus.Online;
        _offlineMessage = null;
        _runtimeDraftStateJson = null;
        await LoadDocumentAsync(force: true);
    }

    private async Task AcceptLocalConflictAsync()
    {
        if (Provider is null || _currentDocument is null)
        {
            return;
        }

        var result = await Provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = _currentDocument.DocumentId,
            Document = _currentDocument,
            BaseConcurrencyToken = _concurrencyToken,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force,
            Author = Author
        });

        if (result.Success)
        {
            if (_offlineDraft is not null && OfflineStore is not null)
            {
                await OfflineStore.DeleteDraftAsync(_offlineDraft.Id);
            }

            _offlineDraft = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _isDirty = false;
            _saveMessage = Loc["TmDocumentEditor_OfflineLocalAccepted"];
            _concurrencyToken = result.ConcurrencyToken;
            _runtimeDraftStateJson = null;
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.MarkSavedAsync(_concurrencyToken);
            }
        }
    }

    private Task AcceptServerConflictAsync()
    {
        return DiscardOfflineDraftAsync();
    }

    private async Task CreateOfflineCopyAsync()
    {
        if (Provider is null || _currentDocument is null)
        {
            return;
        }

        var copy = Clone(_currentDocument);
        copy.DocumentId = $"{_currentDocument.DocumentId}-copy-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        copy.Metadata.Title = string.IsNullOrWhiteSpace(copy.Metadata.Title)
            ? Loc["TmDocumentEditor_OfflineCopyTitle"]
            : Loc["TmDocumentEditor_OfflineCopyTitleWithName", copy.Metadata.Title];

        var result = await Provider.SaveAsync(new DocumentEditorSaveRequest
        {
            DocumentId = copy.DocumentId,
            Document = copy,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Force,
            Author = Author
        });

        if (result.Success)
        {
            if (_offlineDraft is not null && OfflineStore is not null)
            {
                await OfflineStore.DeleteDraftAsync(_offlineDraft.Id);
            }

            _offlineDraft = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _saveMessage = Loc["TmDocumentEditor_OfflineCopyCreated"];
        }
    }

    private static T Clone<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)!;
    }

    private async Task HandleSelectionChangedAsync(DocumentEditorSelectionState selection)
    {
        _selection = selection;
        await BroadcastCollaborationCursorAsync();
    }

    private Task SetSidePanelTabAsync(DocumentSidePanelTab tab)
    {
        OpenSidePanel(tab, manual: true);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenSidePanelAsync()
    {
        _sidePanelOpen = true;
        _activeSidePanelTab = NormalizeSidePanelTab(_activeSidePanelTab);
        _focusSidePanelOnRender = true;
        return InvokeAsync(StateHasChanged);
    }

    private async Task OpenCommentsPanelAsync()
    {
        OpenSidePanel(DocumentSidePanelTab.Comments, manual: true);
        // R.5.12 — the engine owns its comments; refresh the rail from the engine when opening.
        if (UsingCoreEngine)
        {
            await SyncCoreEngineCommentsAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task OpenRevisionsPanelAsync()
    {
        OpenSidePanel(DocumentSidePanelTab.Revisions, manual: true);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenVersionsPanelAsync()
    {
        OpenSidePanel(DocumentSidePanelTab.Versions, manual: true);
        return InvokeAsync(StateHasChanged);
    }

    private Task SetCommentFilterAsync(DocumentCommentFilter filter)
    {
        _commentFilter = filter;
        return InvokeAsync(StateHasChanged);
    }

    private Task SetCommentSortModeAsync(DocumentCommentSortMode sortMode)
    {
        _commentSortMode = sortMode;
        return InvokeAsync(StateHasChanged);
    }

    private async Task SetDifferentFirstPageHeaderFooterAsync(bool enabled)
    {
        if (_document is null || EffectiveReadOnly)
        {
            return;
        }

        await GetCurrentDocumentForProviderExportAsync();
        if (_document is null)
        {
            return;
        }

        var before = DocumentEditorCommandCloner.Clone(_document);
        DocumentHeaderFooterResolver.SetDifferentFirstPage(_document, enabled);
        var after = DocumentEditorCommandCloner.Clone(_document);
        await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
            _document,
            before,
            after,
            "Change header/footer first page setting"));
        _currentDocument = _document;
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("syncHeaderFooterLayout", new
            {
                Document = _document
            });
        }
    }

    private async Task SetDifferentOddAndEvenHeaderFooterAsync(bool enabled)
    {
        if (_document is null || EffectiveReadOnly)
        {
            return;
        }

        await GetCurrentDocumentForProviderExportAsync();
        if (_document is null)
        {
            return;
        }

        var before = DocumentEditorCommandCloner.Clone(_document);
        DocumentHeaderFooterResolver.SetDifferentOddAndEvenPages(_document, enabled);
        var after = DocumentEditorCommandCloner.Clone(_document);
        await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
            _document,
            before,
            after,
            "Change header/footer odd-even setting"));
        _currentDocument = _document;
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("syncHeaderFooterLayout", new
            {
                Document = _document
            });
        }
    }

    private async Task CloseHeaderFooterAsync()
    {
        _activeWysiwygRegion = "Body";
        _selection.Region = "Body";
        _selection.HeaderFooterId = null;
        _selection.PageIndex = null;

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.CloseHeaderFooterAsync();

            if (_lastBodySelectionSnapshot is not null)
            {
                await _wysiwygHost.RestoreSelectionAsync(_lastBodySelectionSnapshot);
            }
            else
            {
                await _wysiwygHost.FocusAsync();
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetRulerVisibleAsync(bool visible)
    {
        _showRuler = visible;
        if (await RouteToCanvasEngineAsync("showRuler", visible))
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetZoomPercentAsync(int percent)
    {
        _zoomPercent = Math.Clamp(percent, 50, 200);
        _zoomPageWidth = false;
        if (await RouteToCanvasEngineAsync("setZoom", new { percent = _zoomPercent }, focus: true))
        {
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetZoomPageWidthAsync()
    {
        _zoomPageWidth = true;
        _zoomPercent = 100;
        if (await RouteToCanvasEngineAsync("fitWidth", null, focus: true))
        {
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCanvasPrintPreviewAsync()
    {
        if (!UsingCanvasEngine || _canvasHost is null)
        {
            return;
        }

        await RouteToCanvasEngineAsync("openPrintPreview", null, focus: true);
        await InvokeAsync(StateHasChanged);
    }

    private async Task CloseCanvasPrintPreviewAsync()
    {
        if (!UsingCanvasEngine || _canvasHost is null)
        {
            return;
        }

        await _canvasHost.ExecCommandAsync("closePrintPreview");
        _canvasPrintPreviewActive = false;
        await SyncCanvasEngineStateAsync();
        await _canvasHost.FocusAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task PrintCanvasDocumentAsync()
    {
        if (!UsingCanvasEngine || _canvasHost is null)
        {
            return;
        }

        await _canvasHost.ExecCommandAsync("printDocument");
        await SyncCanvasEngineStateAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void OpenSidePanel(DocumentSidePanelTab tab, bool preserveManualChoice = true, bool manual = false)
    {
        _activeSidePanelTab = NormalizeSidePanelTab(tab);
        _sidePanelOpen = true;
        if (manual)
        {
            _sidePanelTabManuallySelected = true;
            _focusSidePanelOnRender = true;
        }
        else if (!preserveManualChoice)
        {
            _sidePanelTabManuallySelected = false;
        }
    }

    private void CloseSidePanel()
    {
        _sidePanelOpen = false;
        _focusDocumentOnRender = true;
    }

    private DocumentSidePanelTab NormalizeSidePanelTab(DocumentSidePanelTab tab)
    {
        return tab switch
        {
            DocumentSidePanelTab.Comments when ShowComments => DocumentSidePanelTab.Comments,
            DocumentSidePanelTab.Revisions => DocumentSidePanelTab.Revisions,
            DocumentSidePanelTab.Versions when ShowVersionHistory => DocumentSidePanelTab.Versions,
            DocumentSidePanelTab.Properties => DocumentSidePanelTab.Properties,
            DocumentSidePanelTab.Outline => DocumentSidePanelTab.Outline,
            DocumentSidePanelTab.Pages => DocumentSidePanelTab.Pages,
            _ when ShowVersionHistory => DocumentSidePanelTab.Versions,
            _ when ShowComments => DocumentSidePanelTab.Comments,
            _ => DocumentSidePanelTab.Properties
        };
    }

    private async Task BeginCommentFromToolbarAsync()
    {
        if (!CanUseComments || _document is null)
        {
            return;
        }

        var selectionAnchor = _wysiwygHost is not null
            ? await _wysiwygHost.CaptureTextSelectionAnchorAsync()
            : null;
        selectionAnchor ??= UsingCanvasEngine && _canvasHost is not null
            ? await _canvasHost.CaptureCommentAnchorAsync()
            : null;

        if (selectionAnchor is not null && !string.IsNullOrWhiteSpace(selectionAnchor.BlockId))
        {
            await BeginCommentAsync(selectionAnchor);
            return;
        }

        var blockId = _selection.ActiveBlockId
            ?? _document.Blocks.OrderBy(block => block.Order).FirstOrDefault()?.Id;

        if (!string.IsNullOrWhiteSpace(blockId))
        {
            await BeginCommentAsync(new DocumentCommentAnchor
            {
                Type = DocumentCommentAnchorType.Block,
                BlockId = blockId
            });
        }
    }

    private Task BeginCommentAsync(DocumentCommentAnchor anchor)
    {
        if (!CanUseComments)
        {
            return Task.CompletedTask;
        }

        OpenSidePanel(DocumentSidePanelTab.Comments);
        _draftCommentAnchor = anchor;
        _commentComposerOpen = true;
        _commentMessage = anchor.Type == DocumentCommentAnchorType.TextRange
            ? Loc["TmDocumentEditor_TextCommentReady"]
            : Loc["TmDocumentEditor_BlockCommentReady"];
        return InvokeAsync(StateHasChanged);
    }

    private void CancelCommentComposer()
    {
        _commentComposerOpen = false;
        _draftCommentAnchor = null;
        _commentMessage = null;
    }

    private async Task CreateCommentAsync(DocumentCommentCreateRequest request)
    {
        // R.4.8 — in the core engine the comment is stored in the engine model (not the
        // provider): route the composed text to the engine, which highlights the range.
        if (UsingCoreEngine)
        {
            await CreateCommentInCoreEngineAsync(request);
            return;
        }

        if (Provider is null || _document is null || !CanUseComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        _commentMessage = null;

        try
        {
            var comment = new DocumentComment
            {
                Anchor = request.Anchor,
                Visibility = DocumentCommentVisibility.Internal,
                Entries =
                [
                    new DocumentCommentEntry
                    {
                        Author = Author ?? new DocumentEditorAuthor(),
                        Text = request.Text
                    }
                ]
            };

            var created = await Provider.CreateCommentAsync(DocumentId, comment);
            UpsertComment(created);
            ApplyCommentAnchorMark(created);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.UpsertCommentAsync(created);
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.UpsertCommentAsync(created);
                await _canvasHost.SelectCommentAsync(created.Id);
            }

            var undoComment = CloneForEditor(created);
            await _commandStack.PushAsync(new CallbackDocumentEditorCommand(
                "Add comment",
                execute: () => RestoreCommentForUndoAsync(undoComment),
                undo: () => DeleteCommentForUndoAsync(created.Id),
                skipInitialExecute: true));

            _selectedCommentId = created.Id;
            OpenSidePanel(DocumentSidePanelTab.Comments);
            _commentComposerOpen = false;
            _draftCommentAnchor = null;
            _commentMessage = Loc["TmDocumentEditor_CommentCreated"];
            await RecordCommentAuditAsync(created.Id, DocumentEditorAuditResult.Success, null);
        }
        catch (Exception ex)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentCreateFailed"];
            await RecordCommentAuditAsync(null, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    // R.4.8 — comment compose routed to the core engine. The engine owns comment storage
    // (model.comments + anchor mark highlight) and undo; the provider is bypassed for now.
    private async Task CreateCommentInCoreEngineAsync(DocumentCommentCreateRequest request)
    {
        if (_coreHost is null || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        _commentMessage = null;
        try
        {
            var created = await _coreHost.AddCommentAsync(request.Text, Author?.DisplayName);
            if (created)
            {
                await SyncCoreEngineCommentsAsync();
                _commentComposerOpen = false;
                _draftCommentAnchor = null;
                _commentMessage = Loc["TmDocumentEditor_CommentCreated"];
                await SyncCoreEngineStateAsync();
            }
            else
            {
                // No selection range to anchor to (engine.addComment needs a non-collapsed selection).
                _commentMessage = Loc["TmDocumentEditor_CommentCreateFailed"];
            }
        }
        catch (Exception)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentCreateFailed"];
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    // R.5.12 — pulls the engine's comments into the rail's model (root entry + reply thread +
    // anchor text + resolved status) so the comments sidebar reflects engine-owned comments.
    private async Task SyncCoreEngineCommentsAsync()
    {
        if (_coreHost is null)
        {
            return;
        }

        var core = await _coreHost.GetCommentsAsync();
        _comments = core.Select(c =>
        {
            var entries = new List<DocumentCommentEntry>
            {
                new()
                {
                    Author = new DocumentEditorAuthor { DisplayName = c.Author ?? "You" },
                    Text = c.Text ?? string.Empty,
                },
            };
            foreach (var reply in c.Replies)
            {
                entries.Add(new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { DisplayName = reply.Author ?? "You" },
                    Text = reply.Text ?? string.Empty,
                });
            }

            return new DocumentComment
            {
                Id = string.IsNullOrEmpty(c.Id) ? Guid.NewGuid().ToString("N") : c.Id!,
                Anchor = new DocumentCommentAnchor
                {
                    Type = DocumentCommentAnchorType.TextRange,
                    BlockId = c.AnchorBlockId,
                    StartOffset = c.AnchorOffset,
                },
                Status = c.Resolved ? DocumentCommentStatus.Resolved : DocumentCommentStatus.Open,
                Entries = entries,
            };
        }).ToList();
    }

    private async Task ReplyToCommentAsync(DocumentEditorCommentReplyRequest request)
    {
        // R.5.12 — the core engine owns its comment threads; route the reply to the engine.
        if (UsingCoreEngine)
        {
            await ReplyToCoreEngineCommentAsync(request);
            return;
        }

        if (Provider is null || _document is null || !CanUseComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        _commentMessage = null;

        try
        {
            var updated = await Provider.AddCommentReplyAsync(DocumentId, request.CommentId, new DocumentCommentEntry
            {
                Author = Author ?? new DocumentEditorAuthor(),
                Text = request.Text
            });
            UpsertComment(updated);
            _selectedCommentId = updated.Id;
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.UpsertCommentAsync(updated);
                await _canvasHost.SelectCommentAsync(updated.Id);
            }
            _commentMessage = Loc["TmDocumentEditor_CommentReplyAdded"];
            await RecordCommentAuditAsync(updated.Id, DocumentEditorAuditResult.Success, null);
        }
        catch (Exception ex)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentReplyFailed"];
            await RecordCommentAuditAsync(request.CommentId, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task EditCommentAsync(DocumentEditorCommentEditRequest request)
    {
        if (Provider is null || _document is null || !CanUseComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        _commentMessage = null;

        try
        {
            var updated = await Provider.UpdateCommentEntryAsync(
                DocumentId,
                request.CommentId,
                request.EntryId,
                request.Text,
                Author ?? new DocumentEditorAuthor());

            UpsertComment(updated);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.UpsertCommentAsync(updated);
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.UpsertCommentAsync(updated);
                await _canvasHost.SelectCommentAsync(updated.Id);
            }

            _selectedCommentId = updated.Id;
            _commentMessage = Loc["TmDocumentEditor_CommentEdited"];
            await RecordCommentAuditAsync(updated.Id, DocumentEditorAuditResult.Success, null);
        }
        catch (Exception ex)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentEditFailed"];
            await RecordCommentAuditAsync(request.CommentId, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    // R.5.12 — engine-owned reply/resolve/reopen/delete + navigation for the core engine.
    private async Task ReplyToCoreEngineCommentAsync(DocumentEditorCommentReplyRequest request)
    {
        if (_coreHost is null || !CanUseComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        _commentMessage = null;
        try
        {
            var ok = await _coreHost.ReplyToCommentAsync(request.CommentId, request.Text, Author?.DisplayName);
            if (ok)
            {
                await SyncCoreEngineCommentsAsync();
                _selectedCommentId = request.CommentId;
                _commentMessage = Loc["TmDocumentEditor_CommentReplyAdded"];
            }
            else
            {
                _commentMessage = Loc["TmDocumentEditor_CommentReplyFailed"];
            }
        }
        catch (Exception)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentReplyFailed"];
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task ResolveOrReopenCoreEngineCommentAsync(string commentId, bool resolve)
    {
        if (_coreHost is null || !CanResolveComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        try
        {
            var ok = resolve
                ? await _coreHost.ResolveCommentAsync(commentId)
                : await _coreHost.ReopenCommentAsync(commentId);
            if (ok)
            {
                await SyncCoreEngineCommentsAsync();
                _selectedCommentId = commentId;
                _commentMessage = Loc[resolve ? "TmDocumentEditor_CommentResolvedMessage" : "TmDocumentEditor_CommentReopenedMessage"];
            }
        }
        catch (Exception)
        {
            _commentMessage = Loc[resolve ? "TmDocumentEditor_CommentResolveFailed" : "TmDocumentEditor_CommentReopenFailed"];
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task DeleteCoreEngineCommentAsync(string commentId)
    {
        if (_coreHost is null || !CanDeleteOwnComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        _commentMessage = null;
        try
        {
            var ok = await _coreHost.RemoveCommentAsync(commentId);
            if (ok)
            {
                await SyncCoreEngineCommentsAsync();
                if (_selectedCommentId == commentId)
                {
                    _selectedCommentId = null;
                }

                _commentMessage = Loc["TmDocumentEditor_CommentDeleted"];
            }
            else
            {
                _commentMessage = Loc["TmDocumentEditor_CommentDeleteFailed"];
            }
        }
        catch (Exception)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentDeleteFailed"];
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task ResolveCommentAsync(string commentId)
    {
        // R.5.12 — route to the engine when the core engine owns the comment store.
        if (UsingCoreEngine)
        {
            await ResolveOrReopenCoreEngineCommentAsync(commentId, resolve: true);
            return;
        }

        if (Provider is null || !CanResolveComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        try
        {
            var updated = await Provider.ResolveCommentAsync(DocumentId, commentId, Author ?? new DocumentEditorAuthor());
            UpsertComment(updated);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.UpsertCommentAsync(updated);
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.UpsertCommentAsync(updated);
                await _canvasHost.SelectCommentAsync(updated.Id);
            }

            _selectedCommentId = updated.Id;
            _commentMessage = Loc["TmDocumentEditor_CommentResolvedMessage"];
            await RecordCommentAuditAsync(updated.Id, DocumentEditorAuditResult.Success, null);
        }
        catch (Exception ex)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentResolveFailed"];
            await RecordCommentAuditAsync(commentId, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task ReopenCommentAsync(string commentId)
    {
        // R.5.12 — route to the engine when the core engine owns the comment store.
        if (UsingCoreEngine)
        {
            await ResolveOrReopenCoreEngineCommentAsync(commentId, resolve: false);
            return;
        }

        if (Provider is null || !CanResolveComments || _isSubmittingComment)
        {
            return;
        }

        _isSubmittingComment = true;
        try
        {
            var updated = await Provider.ReopenCommentAsync(DocumentId, commentId, Author ?? new DocumentEditorAuthor());
            UpsertComment(updated);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.UpsertCommentAsync(updated);
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.UpsertCommentAsync(updated);
                await _canvasHost.SelectCommentAsync(updated.Id);
            }

            _selectedCommentId = updated.Id;
            _commentMessage = Loc["TmDocumentEditor_CommentReopenedMessage"];
            await RecordCommentAuditAsync(updated.Id, DocumentEditorAuditResult.Success, null);
        }
        catch (Exception ex)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentReopenFailed"];
            await RecordCommentAuditAsync(commentId, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task DeleteCommentAsync(string commentId)
    {
        // R.5.12 — route to the engine when the core engine owns the comment store.
        if (UsingCoreEngine)
        {
            await DeleteCoreEngineCommentAsync(commentId);
            return;
        }

        if (Provider is null || _document is null || !CanDeleteOwnComments || _isSubmittingComment)
        {
            return;
        }

        var comment = _comments.FirstOrDefault(item => item.Id == commentId);
        if (!CanDeleteComment(comment))
        {
            return;
        }

        var undoComment = CloneForEditor(comment!);
        _isSubmittingComment = true;
        _commentMessage = null;

        try
        {
            await Provider.DeleteCommentAsync(DocumentId, commentId, Author ?? new DocumentEditorAuthor());
            RemoveComment(commentId);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.RemoveCommentAsync(commentId);
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.RemoveCommentAsync(commentId);
            }

            _commentMessage = Loc["TmDocumentEditor_CommentDeleted"];
            await RecordCommentAuditAsync(commentId, DocumentEditorAuditResult.Success, null);
            await _commandStack.PushAsync(new CallbackDocumentEditorCommand(
                "Delete comment",
                execute: () => DeleteCommentForUndoAsync(commentId),
                undo: () => RestoreCommentForUndoAsync(undoComment),
                skipInitialExecute: true));
        }
        catch (Exception ex)
        {
            _commentMessage = Loc["TmDocumentEditor_CommentDeleteFailed"];
            await RecordCommentAuditAsync(commentId, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSubmittingComment = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task RestoreCommentForUndoAsync(DocumentComment comment)
    {
        if (Provider is null)
        {
            return;
        }

        var restored = await Provider.CreateCommentAsync(DocumentId, CloneForEditor(comment));
        UpsertComment(restored);
        ApplyCommentAnchorMark(restored);
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.UpsertCommentAsync(restored);
        }
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.UpsertCommentAsync(restored);
            await _canvasHost.SelectCommentAsync(restored.Id);
        }

        _selectedCommentId = restored.Id;
        OpenSidePanel(DocumentSidePanelTab.Comments);
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteCommentForUndoAsync(string commentId)
    {
        if (Provider is null || string.IsNullOrWhiteSpace(commentId))
        {
            return;
        }

        await Provider.DeleteCommentAsync(DocumentId, commentId, Author ?? new DocumentEditorAuthor());
        RemoveComment(commentId);
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.RemoveCommentAsync(commentId);
        }
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.RemoveCommentAsync(commentId);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SelectCommentAsync(string commentId)
    {
        _selectedCommentId = commentId;
        _selection.ActiveCommentId = commentId;
        OpenSidePanel(DocumentSidePanelTab.Comments);
        // R.5.12 — in the core engine, selecting a comment navigates the caret to its anchor.
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.GoToCommentAsync(commentId);
        }
        else if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ScrollToCommentAsync(commentId);
        }
        else if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.SelectCommentAsync(commentId);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleTrackChanges()
    {
        if (EffectiveReadOnly || !IsFeatureEnabled(DocumentEditorFeatureNames.TrackChanges))
        {
            return;
        }

        _trackChangesEnabled = !_trackChangesEnabled;
        _revisionMessage = _trackChangesEnabled
            ? Loc["TmDocumentEditor_TrackChangesOn"]
            : Loc["TmDocumentEditor_TrackChangesOff"];
        if (_trackChangesEnabled)
        {
            OpenSidePanel(DocumentSidePanelTab.Revisions);
        }

        if (UsingCoreEngine)
        {
            await _coreHost!.ExecCommandAsync("trackChanges", _trackChangesEnabled);
        }
        else if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.SetTrackChangesEnabledAsync(_trackChangesEnabled);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetReviewDisplayModeAsync(DocumentReviewDisplayMode mode)
    {
        _reviewDisplayMode = mode;
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.SetReviewDisplayModeAsync(mode);
        }
        else if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.SetReviewDisplayModeAsync(mode);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void SelectSuggestion(DocumentSuggestion suggestion)
    {
        _selection.ActiveBlockId = suggestion.Range.BlockId;
        _selection.FocusedInlineRange = new DocumentEditorInlineRange
        {
            BlockId = suggestion.Range.BlockId,
            StartInlineIndex = suggestion.Range.StartInlineIndex ?? 0,
            StartOffset = suggestion.Range.StartOffset ?? 0,
            EndInlineIndex = suggestion.Range.EndInlineIndex ?? suggestion.Range.StartInlineIndex ?? 0,
            EndOffset = suggestion.Range.EndOffset ?? suggestion.Range.StartOffset ?? 0
        };
    }

    private Task AcceptSuggestionAsync(DocumentSuggestion suggestion)
        => ReviewSuggestionAsync(suggestion, DocumentSuggestionStatus.Accepted);

    private Task RejectSuggestionAsync(DocumentSuggestion suggestion)
        => ReviewSuggestionAsync(suggestion, DocumentSuggestionStatus.Rejected);

    private async Task SelectRevision(DocumentRevision revision)
    {
        await SelectRevisionAsync(revision.Id);
    }

    private async Task SelectRevisionAsync(string revisionId)
    {
        var revision = _document?.Revisions.FirstOrDefault(item => item.Id == revisionId);
        if (revision is null)
        {
            return;
        }

        _selectedRevisionId = revision.Id;
        _selection.ActiveRevisionId = revision.Id;
        OpenSidePanel(DocumentSidePanelTab.Revisions);
        _selection.ActiveBlockId = revision.Range.BlockId;
        _selection.FocusedInlineRange = new DocumentEditorInlineRange
        {
            BlockId = revision.Range.BlockId,
            StartInlineIndex = revision.Range.StartInlineIndex ?? 0,
            StartOffset = revision.Range.StartOffset ?? 0,
            EndInlineIndex = revision.Range.EndInlineIndex ?? revision.Range.StartInlineIndex ?? 0,
            EndOffset = revision.Range.EndOffset ?? revision.Range.StartOffset ?? 0
        };

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ScrollToRevisionAsync(revision.Id);
        }
        else if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.SelectRevisionAsync(revision.Id);
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task HandleCanvasAnnotationSelectedAsync(TmDocumentCanvasEngineHost.CanvasEngineAnnotationSelection selection)
    {
        if (selection.Kind.Equals("comment", StringComparison.OrdinalIgnoreCase))
        {
            _selectedCommentId = selection.Id;
            _selection.ActiveCommentId = selection.Id;
            OpenSidePanel(DocumentSidePanelTab.Comments);
        }
        else if (selection.Kind.Equals("revision", StringComparison.OrdinalIgnoreCase))
        {
            _selectedRevisionId = selection.Id;
            _selection.ActiveRevisionId = selection.Id;
            OpenSidePanel(DocumentSidePanelTab.Revisions);
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task AcceptRevisionAsync(DocumentRevision revision)
        => ReviewRevisionAsync(revision, DocumentRevisionAction.Accepted);

    private Task RejectRevisionAsync(DocumentRevision revision)
        => ReviewRevisionAsync(revision, DocumentRevisionAction.Rejected);

    private Task AcceptAllRevisionsAsync(DocumentRevisionFilter filter)
        => ReviewAllRevisionsAsync(DocumentRevisionAction.Accepted, filter);

    private Task RejectAllRevisionsAsync(DocumentRevisionFilter filter)
        => ReviewAllRevisionsAsync(DocumentRevisionAction.Rejected, filter);

    private async Task ReviewAllRevisionsAsync(DocumentRevisionAction action, DocumentRevisionFilter? filter = null)
    {
        if (_document is null || _isReviewingRevision || !CanReviewRevisions)
        {
            return;
        }

        filter ??= new DocumentRevisionFilter();
        var revisions = _document.Revisions
            .Where(revision => revision.Action == DocumentRevisionAction.Pending)
            .Where(filter.Matches)
            .ToList();
        if (revisions.Count == 0)
        {
            return;
        }

        _isReviewingRevision = true;
        _revisionMessage = null;
        var before = DocumentEditorCommandCloner.Clone(_document);

        try
        {
            foreach (var revision in revisions)
            {
                var removeContent = (revision.Type == DocumentRevisionType.Insertion && action == DocumentRevisionAction.Rejected)
                    || (revision.Type == DocumentRevisionType.Deletion && action == DocumentRevisionAction.Accepted);

                if (revision.Type == DocumentRevisionType.Formatting)
                {
                    ApplyFormattingRevisionDecision(_document, revision, action);
                }
                else if (removeContent)
                {
                    RemoveRevisionContent(_document, revision.Id);
                }
                else
                {
                    RemoveRevisionMarks(_document, revision.Id);
                }

                revision.Action = action;
            }

            var after = DocumentEditorCommandCloner.Clone(_document);
            await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
                _document,
                before,
                after,
                action == DocumentRevisionAction.Accepted ? "Accept all revisions" : "Reject all revisions"));

            _currentDocument = _document;
            _isDirty = true;
            _suggestionSnapshot = Clone(after);
            await BroadcastLocalCollaborationChangeAsync(before, after);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.ExecuteEditorCommandAsync(
                    action == DocumentRevisionAction.Accepted ? "acceptAllRevisions" : "rejectAllRevisions",
                    new
                    {
                        RevisionIds = revisions.Select(revision => revision.Id).ToList()
                    });
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.ReplaceDocumentAsync(_document);
            }

            _revisionMessage = action == DocumentRevisionAction.Accepted
                ? Loc["TmDocumentEditor_AllRevisionsAccepted", revisions.Count]
                : Loc["TmDocumentEditor_AllRevisionsRejected", revisions.Count];
        }
        finally
        {
            _isReviewingRevision = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task ReviewInlineRevisionAsync(WysiwygRevisionReviewRequest request)
    {
        if (_document is null || string.IsNullOrWhiteSpace(request.RevisionId))
        {
            return Task.CompletedTask;
        }

        var revision = _document.Revisions.FirstOrDefault(item => item.Id == request.RevisionId);
        if (revision is null || request.Action is not (DocumentRevisionAction.Accepted or DocumentRevisionAction.Rejected))
        {
            return Task.CompletedTask;
        }

        return ReviewRevisionAsync(revision, request.Action);
    }

    private async Task ReviewRevisionAsync(DocumentRevision revision, DocumentRevisionAction action)
    {
        if (_document is null || _isReviewingRevision || !CanReviewRevisions || revision.Action != DocumentRevisionAction.Pending)
        {
            return;
        }

        var target = _document.Revisions.FirstOrDefault(item => item.Id == revision.Id);
        if (target is null)
        {
            return;
        }

        _isReviewingRevision = true;
        _revisionMessage = null;
        var before = DocumentEditorCommandCloner.Clone(_document);

        try
        {
            var removeContent = (target.Type == DocumentRevisionType.Insertion && action == DocumentRevisionAction.Rejected)
                || (target.Type == DocumentRevisionType.Deletion && action == DocumentRevisionAction.Accepted);

            if (target.Type == DocumentRevisionType.Formatting)
            {
                ApplyFormattingRevisionDecision(_document, target, action);
            }
            else if (removeContent)
            {
                RemoveRevisionContent(_document, target.Id);
            }
            else
            {
                RemoveRevisionMarks(_document, target.Id);
            }

            target.Action = action;
            var after = DocumentEditorCommandCloner.Clone(_document);
            await _commandStack.PushAsync(new DocumentEditorSnapshotCommand(
                _document,
                before,
                after,
                action == DocumentRevisionAction.Accepted ? "Accept revision" : "Reject revision"));

            _currentDocument = _document;
            _isDirty = true;
            _suggestionSnapshot = Clone(after);
            await BroadcastLocalCollaborationChangeAsync(before, after);
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.ReviewRevisionAsync(target.Id, action);
            }
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                await _canvasHost.ReplaceDocumentAsync(_document);
            }

            _revisionMessage = action == DocumentRevisionAction.Accepted
                ? Loc["TmDocumentEditor_RevisionAccepted"]
                : Loc["TmDocumentEditor_RevisionRejected"];
        }
        finally
        {
            _isReviewingRevision = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ReviewSuggestionAsync(DocumentSuggestion suggestion, DocumentSuggestionStatus status)
    {
        if (SuggestionProvider is null || _document is null || _isReviewingSuggestion || !CanReviewSuggestions)
        {
            return;
        }

        _isReviewingSuggestion = true;
        _suggestionMessage = null;
        var before = Clone(_document);
        if (status == DocumentSuggestionStatus.Accepted
            && !string.IsNullOrWhiteSpace(suggestion.BaseSnapshotHash)
            && !string.Equals(suggestion.BaseSnapshotHash, ComputeSnapshotHash(_document), StringComparison.Ordinal))
        {
            _suggestionMessage = Loc["TmDocumentEditor_SuggestionConflict"];
            _isReviewingSuggestion = false;
            return;
        }

        try
        {
            var reviewed = await SuggestionProvider.ReviewSuggestionAsync(new DocumentSuggestionReviewRequest
            {
                DocumentId = suggestion.DocumentId,
                SuggestionId = suggestion.Id,
                Status = status,
                Reviewer = Author ?? new DocumentEditorAuthor()
            });

            if (status == DocumentSuggestionStatus.Accepted && suggestion.Operations.Count > 0)
            {
                var result = new DocumentOperationApplier().Apply(_document, new DocumentOperationBatch
                {
                    DocumentId = _document.DocumentId,
                    Operations = suggestion.Operations.Select(Clone).ToList()
                });
                if (!result.IsValid)
                {
                    _document = before;
                    _currentDocument = _document;
                    _suggestionMessage = Loc["TmDocumentEditor_SuggestionReviewFailed"];
                    return;
                }

                _currentDocument = _document;
                _isDirty = true;
                _suggestionSnapshot = Clone(_document);
                await BroadcastLocalCollaborationChangeAsync(before, _document);
            }

            _suggestions = _suggestions
                .Where(item => item.Id != suggestion.Id)
                .Append(reviewed)
                .Where(item => item.Status == DocumentSuggestionStatus.Pending)
                .OrderByDescending(item => item.CreatedAt)
                .ToList();
            _suggestionMessage = status == DocumentSuggestionStatus.Accepted
                ? Loc["TmDocumentEditor_SuggestionAccepted"]
                : Loc["TmDocumentEditor_SuggestionRejected"];
        }
        catch
        {
            _document = before;
            _currentDocument = _document;
            _suggestionMessage = Loc["TmDocumentEditor_SuggestionReviewFailed"];
        }
        finally
        {
            _isReviewingSuggestion = false;
        }
    }

    private async Task UndoAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (await RouteToCoreEngineAsync("undo"))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("undo"))
        {
            return;
        }

        ClearPendingParagraphFormattingOverrides();
        if (_wysiwygHost is not null && _wysiwygUndoState.CanUndo)
        {
            var runtimeDescription = _wysiwygUndoState.NextUndoDescription;
            var mirrorCommandStackReview = IsMirroredReviewUndoRedo(runtimeDescription, _commandStack.NextUndoDescription);
            var runtimeChanged = await _wysiwygHost.UndoRuntimeAsync();
            var runtimeTransaction = runtimeChanged
                ? await _wysiwygHost.RequestLastCommandTransactionAsync()
                : null;
            if (mirrorCommandStackReview && _commandStack.CanUndo)
            {
                await _commandStack.UndoAsync();
                MarkDirtyAfterCommand();
                await SyncCurrentDocumentToWysiwygAsync();
            }
            else if (runtimeChanged)
            {
                if (await TryApplyRuntimeUndoRedoDocumentTransactionAsync(runtimeTransaction, forward: false))
                {
                    await SyncCurrentDocumentToWysiwygAsync();
                }
                else
                {
                    await SyncCurrentDocumentFromWysiwygRuntimeAsync();
                }
            }

            await RefreshRuntimeUndoDirtyStateAsync();
            return;
        }

        if (!_commandStack.CanUndo)
        {
            return;
        }

        await _commandStack.UndoAsync();
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await SyncCurrentDocumentToWysiwygAsync();
        }

        await RefreshCommandRegistryAsync();
    }

    private async Task RedoAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (await RouteToCoreEngineAsync("redo"))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("redo"))
        {
            return;
        }

        ClearPendingParagraphFormattingOverrides();
        if (_wysiwygHost is not null && _wysiwygUndoState.CanRedo)
        {
            var runtimeDescription = _wysiwygUndoState.NextRedoDescription;
            var mirrorCommandStackReview = IsMirroredReviewUndoRedo(runtimeDescription, _commandStack.NextRedoDescription);
            var runtimeChanged = await _wysiwygHost.RedoRuntimeAsync();
            var runtimeTransaction = runtimeChanged
                ? await _wysiwygHost.RequestLastCommandTransactionAsync()
                : null;
            if (mirrorCommandStackReview && _commandStack.CanRedo)
            {
                await _commandStack.RedoAsync();
                MarkDirtyAfterCommand();
                await SyncCurrentDocumentToWysiwygAsync();
            }
            else if (runtimeChanged)
            {
                if (await TryApplyRuntimeUndoRedoDocumentTransactionAsync(runtimeTransaction, forward: true))
                {
                    await SyncCurrentDocumentToWysiwygAsync();
                }
                else
                {
                    await SyncCurrentDocumentFromWysiwygRuntimeAsync();
                }
            }

            await RefreshRuntimeUndoDirtyStateAsync();
            return;
        }

        if (!_commandStack.CanRedo)
        {
            return;
        }

        await _commandStack.RedoAsync();
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await SyncCurrentDocumentToWysiwygAsync();
        }

        await RefreshCommandRegistryAsync();
    }

    private void ClearPendingParagraphFormattingOverrides()
    {
        _pendingParagraphAlignment = null;
        _pendingParagraphAlignmentBlockId = null;
        _pendingLineSpacing = null;
        _pendingLineSpacingBlockId = null;
    }

    private static bool IsMirroredReviewUndoRedo(string? runtimeDescription, string? commandDescription)
    {
        if (string.IsNullOrWhiteSpace(runtimeDescription) || string.IsNullOrWhiteSpace(commandDescription))
        {
            return false;
        }

        return string.Equals(runtimeDescription, commandDescription, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(runtimeDescription, "Accept revision", StringComparison.OrdinalIgnoreCase)
                || string.Equals(runtimeDescription, "Reject revision", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> TryApplyRuntimeUndoRedoDocumentTransactionAsync(JsonElement? transaction, bool forward)
    {
        if (_document is null || transaction is null)
        {
            return false;
        }

        if (!TryGetJsonProperty(transaction.Value, out var operationsElement, "operations", "Operations")
            || operationsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var before = _collaborationSnapshot is not null
            ? Clone(_collaborationSnapshot)
            : Clone(_document);
        var changed = false;
        var operations = operationsElement.EnumerateArray().ToArray();
        IEnumerable<JsonElement> orderedOperations = forward
            ? operations
            : operations.AsEnumerable().Reverse();

        foreach (var operation in orderedOperations)
        {
            var type = GetJsonString(operation, "type", "Type");
            if (!forward && string.Equals(type, "SplitBlock", StringComparison.OrdinalIgnoreCase))
            {
                changed |= TryUndoRuntimeSplitBlock(operation);
            }
            else if (!forward && string.Equals(type, "InsertBlock", StringComparison.OrdinalIgnoreCase))
            {
                changed |= TryUndoRuntimeInsertBlock(operation);
            }
            else if (forward && string.Equals(type, "InsertBlock", StringComparison.OrdinalIgnoreCase))
            {
                changed |= TryRedoRuntimeInsertBlock(operation);
            }
        }

        if (!changed)
        {
            return false;
        }

        DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(_document);
        new DocumentEditorPostFixer().Fix(_document);
        _currentDocument = _document;
        _templatePreviewDocument = null;
        _templatePreviewEnabled = false;
        _templatePreviewMessage = null;
        _isDirty = true;
        _suggestionSnapshot = Clone(_document);
        await BroadcastLocalCollaborationChangeAsync(before, _document);
        return true;
    }

    private bool TryUndoRuntimeInsertBlock(JsonElement operation)
    {
        if (_document is null || !TryGetJsonProperty(operation, out var blockElement, "block", "Block"))
        {
            return false;
        }

        var blockId = GetJsonString(blockElement, "id", "Id");
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return false;
        }

        var blocks = FindMutableBlockListContainingBlock(_document, blockId);
        if (blocks is null)
        {
            return false;
        }

        var index = blocks.FindIndex(block => string.Equals(block.Id, blockId, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        blocks.RemoveAt(index);
        _document.Anchors.RemoveAll(anchor => string.Equals(anchor.ObjectBlockId, blockId, StringComparison.Ordinal));
        _document.BumpVersion();  // Phase C1
        return true;
    }

    private bool TryRedoRuntimeInsertBlock(JsonElement operation)
    {
        if (_document is null)
        {
            return false;
        }

        try
        {
            var options = new JsonSerializerOptions(DocumentEditorJson.Options)
            {
                PropertyNameCaseInsensitive = true
            };
            var patch = JsonSerializer.Deserialize<WysiwygPatch>(operation.GetRawText(), options);
            if (patch is null || !string.Equals(patch.Type, "InsertBlock", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            new WysiwygPatchApplier().ApplyPatch(_document, patch);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryUndoRuntimeSplitBlock(JsonElement operation)
    {
        if (_document is null
            || !TryGetJsonProperty(operation, out var selection, "selection", "Selection", "beforeSelection", "BeforeSelection"))
        {
            return false;
        }

        var blockId = GetJsonString(selection, "anchorBlockId", "AnchorBlockId");
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return false;
        }

        var splitBlockId = TryGetJsonProperty(operation, out var blockElement, "block", "Block")
            ? GetJsonString(blockElement, "id", "Id")
            : null;
        var blocks = FindMutableBlockListContainingBlock(_document, blockId);
        if (blocks is null)
        {
            return false;
        }

        var currentIndex = blocks.FindIndex(block => string.Equals(block.Id, blockId, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            return false;
        }

        var splitIndex = string.IsNullOrWhiteSpace(splitBlockId)
            ? -1
            : blocks.FindIndex(block => string.Equals(block.Id, splitBlockId, StringComparison.Ordinal));
        if (splitIndex < 0 && currentIndex + 1 < blocks.Count)
        {
            splitIndex = currentIndex + 1;
        }

        if (splitIndex <= currentIndex || splitIndex >= blocks.Count)
        {
            return false;
        }

        var currentInlines = GetEditableInlines(blocks[currentIndex].Content);
        var splitInlines = GetEditableInlines(blocks[splitIndex].Content);
        if (currentInlines is null || splitInlines is null)
        {
            return false;
        }

        currentInlines.AddRange(splitInlines.Select(CloneForEditor));
        blocks.RemoveAt(splitIndex);
        return true;
    }

    private static List<DocumentBlock>? FindMutableBlockListContainingBlock(DocumentEditorDocument document, string blockId)
    {
        return FindMutableBlockListContainingBlock(document.Blocks, blockId)
            ?? document.HeadersFooters
                .Select(headerFooter => FindMutableBlockListContainingBlock(headerFooter.Blocks, blockId))
                .FirstOrDefault(blocks => blocks is not null)
            ?? document.Notes
                .Select(note => FindMutableBlockListContainingBlock(note.Blocks, blockId))
                .FirstOrDefault(blocks => blocks is not null);
    }

    private static List<DocumentBlock>? FindMutableBlockListContainingBlock(List<DocumentBlock> blocks, string blockId)
    {
        if (blocks.Any(block => string.Equals(block.Id, blockId, StringComparison.Ordinal)))
        {
            return blocks;
        }

        foreach (var table in blocks.Select(block => block.Content).OfType<TableBlockContent>())
        {
            foreach (var cell in table.Rows.SelectMany(row => row.Cells))
            {
                var nested = FindMutableBlockListContainingBlock(cell.Blocks, blockId);
                if (nested is not null)
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static bool TryGetJsonProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetJsonString(JsonElement element, params string[] names)
    {
        return TryGetJsonProperty(element, out var value, names) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private async Task SyncCurrentDocumentFromWysiwygRuntimeAsync()
    {
        if (_wysiwygHost is null || _document is null)
        {
            return;
        }

        var runtimeDocument = await _wysiwygHost.RequestSnapshotAsync();
        if (runtimeDocument is null)
        {
            return;
        }

        var before = _collaborationSnapshot is not null
            ? Clone(_collaborationSnapshot)
            : Clone(_document);
        var synchronizedDocument = CreateProviderBoundarySnapshot(_document, runtimeDocument);
        _document = synchronizedDocument;
        _currentDocument = synchronizedDocument;
        _wysiwygHost.MarkRuntimeDocumentSynchronized(synchronizedDocument);
        _templatePreviewDocument = null;
        _templatePreviewEnabled = false;
        _templatePreviewMessage = null;
        _isDirty = true;
        _suggestionSnapshot = Clone(synchronizedDocument);
        await BroadcastLocalCollaborationChangeAsync(before, synchronizedDocument);
    }

    private async Task SyncCurrentDocumentToWysiwygAsync()
    {
        if (_wysiwygHost is null || _document is null)
        {
            return;
        }

        await _wysiwygHost.ExecuteEditorCommandAsync(
            "syncHeaderFooterLayout",
            new { Document = _document, Selection = _lastWysiwygSelectionSnapshot });
        if (_lastWysiwygSelectionSnapshot is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(_lastWysiwygSelectionSnapshot);
        }
    }

    private async Task RefreshRuntimeUndoDirtyStateAsync()
    {
        if (_wysiwygHost is null)
        {
            return;
        }

        var undoState = await _wysiwygHost.RequestUndoStateAsync();
        if (undoState is not null)
        {
            _wysiwygUndoState = undoState;
        }

        var dirtyState = await _wysiwygHost.RequestDirtyStateAsync();
        if (dirtyState is not null)
        {
            _wysiwygDirtyState = dirtyState;
            _isDirty = dirtyState.IsDirty;
        }
        else if (undoState is not null)
        {
            _isDirty = undoState.CanUndo;
        }

        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    // R.4.8 step 3b — when the core engine preview hosts the editor, toolbar commands route
    // to it (its host owns the model/undo/dirty) instead of the absent legacy wysiwyg host.
    private bool UsingCoreEngine => EffectiveRenderEngine == DocumentEditorRenderEngine.CoreEnginePreview && _coreHost is not null;

    private bool UsingCanvasEngine => EffectiveRenderEngine == DocumentEditorRenderEngine.CanvasEnginePreview && _canvasHost is not null;

    private async Task<bool> RouteToCoreEngineAsync(string command, object? argument = null)
    {
        if (!UsingCoreEngine)
        {
            return false;
        }
        await _coreHost!.ExecCommandAsync(command, argument);
        await SyncCoreEngineStateAsync();
        return true;
    }

    private Task<bool> RouteToCanvasEngineAsync(string command, object? argument = null)
        => RouteToCanvasEngineAsync(command, argument, focus: false);

    private async Task<bool> RouteToCanvasEngineAsync(string command, object? argument, bool focus)
    {
        if (!UsingCanvasEngine)
        {
            return false;
        }

        await _canvasHost!.ExecCommandAsync(command, argument);
        await SyncCanvasEngineStateAsync();
        if (focus)
        {
            await _canvasHost.FocusAsync();
        }

        return true;
    }

    // R.5.23 — the engine raised a right-click; show the core context menu at the pointer.
    private Task HandleCoreContextMenuAsync(TmDocumentCoreEngineHost.CoreContextMenuRequest request)
    {
        _coreContextMenu = request;
        return InvokeAsync(StateHasChanged);
    }

    private string CoreContextMenuStyle()
        => _coreContextMenu is null
            ? string.Empty
            : FormattableString.Invariant($"position: fixed; left: {_coreContextMenu.X}px; top: {_coreContextMenu.Y}px;");

    private void CloseCoreContextMenu() => _coreContextMenu = null;

    // Runs a core-engine command from the context menu, then dismisses it.
    private async Task RunCoreContextCommandAsync(string command)
    {
        CloseCoreContextMenu();
        if (await RouteToCanvasEngineAsync(command, null, focus: true))
        {
            return;
        }

        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.ExecCommandAsync(command);
            await SyncCoreEngineStateAsync();
            await _coreHost.FocusAsync();
        }
    }

    private async Task BeginCoreContextCommentAsync()
    {
        CloseCoreContextMenu();
        await BeginCommentFromToolbarAsync();
    }

    private async Task AddCoreContextLinkAsync()
    {
        CloseCoreContextMenu();
        await ApplyLinkAsync("https://example.com");
    }

    // R.5.23c — replace the misspelled word under the pointer with the chosen suggestion.
    private async Task ApplyCoreSpellSuggestionAsync(string suggestion)
    {
        var misspelling = _coreContextMenu?.Info.Misspelling;
        CloseCoreContextMenu();
        if (misspelling is null || _coreHost is null || string.IsNullOrEmpty(misspelling.BlockId))
        {
            return;
        }

        await _coreHost.ExecCommandAsync("replacerange", new
        {
            blockId = misspelling.BlockId,
            start = misspelling.Start,
            end = misspelling.End,
            text = suggestion,
        });
        await SyncCoreEngineStateAsync();
        await _coreHost.FocusAsync();
    }

    private async Task HandleCanvasMiniToolbarChangedAsync(WysiwygMiniToolbarRequest? request)
    {
        await HandleMiniToolbarChangedAsync(request);
        if (!_disposed && UsingCanvasEngine)
        {
            await SyncCanvasEngineStateAsync();
            await BroadcastCollaborationCursorAsync();
        }
    }

    private Task HandleCanvasContextMenuAsync(TmDocumentCanvasEngineHost.CanvasEngineContextMenuRequest request)
    {
        var selection = request.Selection;
        if (request.Misspelling is not null || !request.InTable)
        {
            return HandleTextContextMenuRequestedAsync(new WysiwygTextContextMenuRequest
            {
                Left = request.X,
                Top = request.Y,
                Width = 280,
                Height = request.Misspelling is null ? 280 : 420,
                ClientX = request.X,
                ClientY = request.Y,
                ViewportWidth = 0,
                ViewportHeight = 0,
                Selection = selection,
                BlockId = request.BlockId,
                BlockType = string.IsNullOrWhiteSpace(request.ImageBlockId) ? null : "Image",
                Misspelling = request.Misspelling is null
                    ? null
                    : new WysiwygMisspelling
                    {
                        Word = request.Misspelling.Word,
                        Start = request.Misspelling.Start,
                        End = request.Misspelling.End,
                        BlockId = request.Misspelling.BlockId,
                        Suggestions = request.Misspelling.Suggestions,
                        CanApplyFix = request.Misspelling.CanApplyFix
                    }
            });
        }

        return HandleTableContextMenuRequestedAsync(new WysiwygTableContextMenuRequest
        {
            Left = request.X,
            Top = request.Y,
            Width = 260,
            Height = 360,
            ClientX = request.X,
            ClientY = request.Y,
            Selection = selection,
            CellId = string.IsNullOrWhiteSpace(request.CellId) ? selection?.ActiveTableCellId ?? string.Empty : request.CellId
        });
    }

    private async Task ApplyTextContextSpellSuggestionAsync(string suggestion)
    {
        var misspelling = _textContextMenu?.Misspelling;
        CloseFloatingUi();
        if (misspelling is null || !UsingCanvasEngine || _canvasHost is null || string.IsNullOrWhiteSpace(misspelling.BlockId))
        {
            return;
        }

        await RouteToCanvasEngineAsync("replacerange", new
        {
            blockId = misspelling.BlockId,
            start = misspelling.Start,
            end = misspelling.End,
            text = suggestion
        }, focus: true);
    }

    private async Task RunTextContextSpellCommandAsync(string command)
    {
        var misspelling = _textContextMenu?.Misspelling;
        CloseFloatingUi();
        if (misspelling is null || !UsingCanvasEngine || _canvasHost is null)
        {
            return;
        }

        await RouteToCanvasEngineAsync(command, new
        {
            word = misspelling.Word,
            blockId = misspelling.BlockId,
            start = misspelling.Start,
            end = misspelling.End
        }, focus: true);
    }

    private async Task SyncCoreEngineStateAsync()
    {
        if (_coreHost is null)
        {
            return;
        }
        _isDirty = await _coreHost.IsDirtyAsync();
        _coreCanUndo = await _coreHost.CanUndoAsync();
        _coreCanRedo = await _coreHost.CanRedoAsync();

        // Reflect the engine's active formatting in the toolbar (after a command). Live
        // read-back on caret moves needs a JS→.NET selection event (follow-up).
        var fmt = await _coreHost.GetFormattingStateAsync();
        if (fmt is not null)
        {
            _formattingState.Bold = fmt.Bold ? WysiwygFormattingValue.Active : WysiwygFormattingValue.Inactive;
            _formattingState.Italic = fmt.Italic ? WysiwygFormattingValue.Active : WysiwygFormattingValue.Inactive;
            _formattingState.Underline = fmt.Underline ? WysiwygFormattingValue.Active : WysiwygFormattingValue.Inactive;
            _formattingState.Strikethrough = fmt.Strikethrough ? WysiwygFormattingValue.Active : WysiwygFormattingValue.Inactive;
            _formattingState.ParagraphAlignment = fmt.Alignment switch
            {
                "center" => DocumentTextAlignment.Center,
                "right" => DocumentTextAlignment.Right,
                "justify" => DocumentTextAlignment.Justify,
                _ => DocumentTextAlignment.Left,
            };
            _formattingState.ParagraphAlignmentMixed = false;
            _formattingState.IsBulletList = fmt.BulletList;
            _formattingState.IsNumberedList = fmt.NumberedList;
            _formattingState.ListMixed = false;
            if (!string.IsNullOrEmpty(fmt.ParagraphStyle))
            {
                _coreBlockStyle = fmt.ParagraphStyle;
            }
        }

        await RefreshCommandRegistryAsync();
        StateHasChanged();
    }

    private async Task SyncCanvasEngineStateAsync()
    {
        if (_canvasHost is null)
        {
            return;
        }

        _isDirty = await _canvasHost.IsDirtyAsync();
        var undoState = await _canvasHost.GetUndoStateAsync();
        _canvasCanUndo = undoState.CanUndo;
        _canvasCanRedo = undoState.CanRedo;

        var fmt = await _canvasHost.GetFormattingStateAsync();
        if (fmt is not null)
        {
            _formattingState.Bold = ToFormattingValue(fmt.Bold, fmt.BoldMixed);
            _formattingState.Italic = ToFormattingValue(fmt.Italic, fmt.ItalicMixed);
            _formattingState.Underline = ToFormattingValue(fmt.Underline, fmt.UnderlineMixed);
            _formattingState.Strikethrough = ToFormattingValue(fmt.Strikethrough, fmt.StrikethroughMixed);
            _formattingState.Superscript = ToFormattingValue(fmt.Superscript, fmt.SuperscriptMixed);
            _formattingState.Subscript = ToFormattingValue(fmt.Subscript, fmt.SubscriptMixed);
            _formattingState.SmallCaps = ToFormattingValue(fmt.SmallCaps, fmt.SmallCapsMixed);
            _formattingState.AllCaps = ToFormattingValue(fmt.AllCaps, fmt.AllCapsMixed);
            _formattingState.DoubleStrikethrough = ToFormattingValue(fmt.DoubleStrikethrough, fmt.DoubleStrikethroughMixed);
            _formattingState.FontFamily = string.IsNullOrWhiteSpace(fmt.FontFamily) ? null : fmt.FontFamily;
            _formattingState.FontFamilyMixed = fmt.FontFamilyMixed;
            _formattingState.FontSize = string.IsNullOrWhiteSpace(fmt.FontSize) ? null : fmt.FontSize;
            _formattingState.FontSizeMixed = fmt.FontSizeMixed;
            _formattingState.TextColor = string.IsNullOrWhiteSpace(fmt.TextColor) ? null : fmt.TextColor;
            _formattingState.TextColorMixed = fmt.TextColorMixed;
            _formattingState.HighlightColor = string.IsNullOrWhiteSpace(fmt.HighlightColor) ? null : fmt.HighlightColor;
            _formattingState.HighlightColorMixed = fmt.HighlightColorMixed;
            _formattingState.ParagraphAlignment = ParseCanvasAlignment(fmt.Alignment);
            _formattingState.ParagraphAlignmentMixed = fmt.AlignmentMixed;
            _formattingState.LineSpacing = fmt.LineSpacing;
            _formattingState.LineSpacingMixed = fmt.LineSpacingMixed;
            _formattingState.SpacingBefore = fmt.SpacingBefore;
            _formattingState.SpacingBeforeMixed = fmt.SpacingBeforeMixed;
            _formattingState.SpacingAfter = fmt.SpacingAfter;
            _formattingState.SpacingAfterMixed = fmt.SpacingAfterMixed;
            _formattingState.LeftIndent = fmt.LeftIndent;
            _formattingState.LeftIndentMixed = fmt.LeftIndentMixed;
            _formattingState.IsBulletList = fmt.BulletList;
            _formattingState.IsNumberedList = fmt.NumberedList;
            _formattingState.ListMixed = fmt.ListMixed;
            if (!string.IsNullOrWhiteSpace(fmt.BlockStyle))
            {
                _coreBlockStyle = fmt.BlockStyle;
            }

            _showRuler = fmt.ShowRuler;
            _showBlocks = fmt.ShowBlocks;
            _showNonPrintingCharacters = fmt.ShowNonPrintingCharacters;
            if (!string.IsNullOrWhiteSpace(fmt.ViewMode))
            {
                _canvasViewMode = fmt.ViewMode;
            }

            if (fmt.ZoomPercent > 0)
            {
                _zoomPercent = Math.Clamp(fmt.ZoomPercent, 25, 400);
            }

            _zoomPageWidth = string.Equals(fmt.ZoomPreset, "fitWidth", StringComparison.OrdinalIgnoreCase);
            _canvasPrintPreviewActive = fmt.PrintPreviewActive;
            SyncCanvasActiveImage(fmt.Image);
        }

        var navigation = await _canvasHost.GetNavigationStateAsync();
        if (navigation.Outline.Count > 0)
        {
            _documentOutline = navigation.Outline;
            if (string.IsNullOrWhiteSpace(_activeHeadingBlockId))
            {
                _activeHeadingBlockId = _documentOutline[0].BlockId;
            }
        }

        await SyncCanvasContentControlPopoverAsync();
        await RefreshCommandRegistryAsync();
        StateHasChanged();
    }

    private void SyncCanvasActiveImage(TmDocumentCanvasEngineHost.CanvasEngineImageState? image)
    {
        if (image is null || string.IsNullOrWhiteSpace(image.ObjectId))
        {
            _activeCanvasImageObjectId = null;
            _activeCanvasImageBlockId = null;
            _activeCanvasImageRunId = null;
            _coreActiveImage = null;
            return;
        }

        _activeCanvasImageObjectId = image.ObjectId;
        _activeCanvasImageBlockId = image.BlockId;
        _activeCanvasImageRunId = image.RunId;
        _coreActiveImage = new ImageBlockContent
        {
            Source = string.IsNullOrWhiteSpace(image.AssetId) ? DocumentImageSource.Url : DocumentImageSource.Asset,
            Url = image.Url,
            AssetId = image.AssetId,
            AltText = image.AltText,
            Caption = image.Caption,
            IsDecorative = image.IsDecorative,
            Size = new DocumentImageSize { Width = image.Width, Height = image.Height, LockAspectRatio = true },
            Layout = new DocumentObjectLayout
            {
                Wrap = new DocumentObjectWrap { Mode = ParseEngineWrapMode(image.WrapMode) },
                Transform = new DocumentObjectTransform { Width = image.Width, Height = image.Height },
                Position = new DocumentObjectPosition { X = image.X, Y = image.Y },
                Stacking = new DocumentObjectStacking { ZIndex = (int)Math.Round(image.ZIndex) }
            }
        };
        OpenSidePanel(DocumentSidePanelTab.Properties);
    }

    private async Task SyncCanvasContentControlPopoverAsync()
    {
        if (_canvasHost is null || !UsingCanvasEngine || EffectiveReadOnly)
        {
            _activeCanvasContentControl = null;
            return;
        }

        var selection = await _canvasHost.GetSelectionStateAsync();
        if (string.IsNullOrWhiteSpace(selection.FocusBlockId))
        {
            _activeCanvasContentControl = null;
            return;
        }

        var runtimeDocument = await _canvasHost.RequestDocumentAsync();
        _activeCanvasContentControl = FindCanvasContentControlAtSelection(runtimeDocument, selection);
    }

    private async Task CloseCanvasContentControlPopoverAsync()
    {
        _activeCanvasContentControl = null;
        if (_canvasHost is not null)
        {
            await _canvasHost.FocusAsync();
        }
    }

    private Task SetCanvasContentControlDateAsync(ChangeEventArgs args)
        => RunCanvasContentControlCommandAsync("setContentControlDate", new
        {
            controlId = _activeCanvasContentControl?.ControlId,
            dateIso = Convert.ToString(args.Value, CultureInfo.InvariantCulture) ?? string.Empty
        });

    private Task SetCanvasContentControlChoiceAsync(ChangeEventArgs args)
        => RunCanvasContentControlCommandAsync("selectContentControlOption", new
        {
            controlId = _activeCanvasContentControl?.ControlId,
            selectedValue = Convert.ToString(args.Value, CultureInfo.InvariantCulture) ?? string.Empty
        });

    private Task SetCanvasContentControlComboTextAsync(ChangeEventArgs args)
        => RunCanvasContentControlCommandAsync("setContentControlComboText", new
        {
            controlId = _activeCanvasContentControl?.ControlId,
            text = Convert.ToString(args.Value, CultureInfo.InvariantCulture) ?? string.Empty
        });

    private Task SetCanvasContentControlPictureAsync(ChangeEventArgs args)
        => RunCanvasContentControlCommandAsync("setContentControlPicture", new
        {
            controlId = _activeCanvasContentControl?.ControlId,
            assetId = Convert.ToString(args.Value, CultureInfo.InvariantCulture) ?? string.Empty
        });

    private async Task RunCanvasContentControlCommandAsync(string command, object payload)
    {
        if (_activeCanvasContentControl is null || _activeCanvasContentControl.LockContent || _canvasHost is null || EffectiveReadOnly)
        {
            return;
        }

        await RouteToCanvasEngineAsync(command, payload, focus: true);
    }

    private static CanvasContentControlPopoverState? FindCanvasContentControlAtSelection(
        DocumentEditorDocument? document,
        TmDocumentCanvasEngineHost.CanvasEngineSelectionState selection)
    {
        if (document is null || string.IsNullOrWhiteSpace(selection.FocusBlockId))
        {
            return null;
        }

        foreach (var block in EnumerateBlocks(document.Blocks))
        {
            if (!string.Equals(block.Id, selection.FocusBlockId, StringComparison.Ordinal))
            {
                continue;
            }

            var offset = 0;
            foreach (var inline in GetInlineContent(block.Content))
            {
                var length = InlineDisplayLength(inline);
                var start = offset;
                var end = offset + length;
                if (inline is DocumentContentControlRun controlRun
                    && selection.FocusOffset >= start
                    && (selection.FocusOffset < end || start == end)
                    && IsPopoverContentControlKind(controlRun.Control.Kind))
                {
                    return CanvasContentControlPopoverState.From(controlRun.Control);
                }

                offset = end;
            }
        }

        return null;
    }

    private static IEnumerable<DocumentBlock> EnumerateBlocks(IEnumerable<DocumentBlock> blocks)
    {
        var stack = new Stack<DocumentBlock>(blocks.Reverse());
        while (stack.Count > 0)
        {
            var block = stack.Pop();
            yield return block;

            if (block.Content is TableBlockContent table)
            {
                for (var rowIndex = table.Rows.Count - 1; rowIndex >= 0; rowIndex--)
                {
                    var row = table.Rows[rowIndex];
                    for (var cellIndex = row.Cells.Count - 1; cellIndex >= 0; cellIndex--)
                    {
                        var cell = row.Cells[cellIndex];
                        for (var nestedIndex = cell.Blocks.Count - 1; nestedIndex >= 0; nestedIndex--)
                        {
                            stack.Push(cell.Blocks[nestedIndex]);
                        }
                    }
                }
            }

            if (block.Content is ContentControlBlockContent contentControl)
            {
                for (var nestedIndex = contentControl.Blocks.Count - 1; nestedIndex >= 0; nestedIndex--)
                {
                    stack.Push(contentControl.Blocks[nestedIndex]);
                }
            }
        }
    }

    private static IReadOnlyList<InlineContent> GetInlineContent(DocumentBlockContent content)
        => content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => []
        };

    private static int InlineDisplayLength(InlineContent inline)
        => Math.Max(0, inline switch
        {
            TextRun text => text.Text.Length,
            TokenRun token => FirstNonEmpty(token.DisplayName, token.FallbackText, token.Key).Length,
            DocumentFieldRun field => FirstNonEmpty(field.DisplayText, field.CachedResult, field.FallbackText, field.InstrText).Length,
            DocumentNoteReferenceRun note => FirstNonEmpty(note.DisplayMarker, note.NoteId, "1").Length,
            DocumentDrawingRun => 1,
            DocumentMathRun math => FirstNonEmpty(math.AltText, math.MathId).Length,
            DocumentContentControlRun control => ContentControlDisplayText(control.Control).Length,
            _ => 0
        });

    private static string ContentControlDisplayText(DocumentContentControl control)
    {
        var value = control.Value;
        return control.Kind switch
        {
            DocumentContentControlKind.Checkbox => value.Checked == true ? "☑" : "☐",
            DocumentContentControlKind.DropDown or DocumentContentControlKind.ComboBox =>
                FirstNonEmpty(
                    control.Items.FirstOrDefault(item => string.Equals(item.Value, value.SelectedValue, StringComparison.Ordinal))?.DisplayText,
                    value.Text,
                    control.PlaceholderText),
            DocumentContentControlKind.Date => FirstNonEmpty(value.DateIso, value.Text, control.PlaceholderText),
            DocumentContentControlKind.Picture => FirstNonEmpty(value.AssetId, control.PlaceholderText),
            _ => FirstNonEmpty(value.Text, control.PlaceholderText)
        };
    }

    private static bool IsPopoverContentControlKind(DocumentContentControlKind kind)
        => kind is DocumentContentControlKind.Date
            or DocumentContentControlKind.ComboBox
            or DocumentContentControlKind.DropDown
            or DocumentContentControlKind.Picture;

    private string ContentControlKindLabel(DocumentContentControlKind kind)
        => kind switch
        {
            DocumentContentControlKind.Date => Loc["TmDocumentEditor_ContentControlKindDate"],
            DocumentContentControlKind.ComboBox => Loc["TmDocumentEditor_ContentControlKindCombo"],
            DocumentContentControlKind.DropDown => Loc["TmDocumentEditor_ContentControlKindDropdown"],
            DocumentContentControlKind.Picture => Loc["TmDocumentEditor_ContentControlKindPicture"],
            _ => Loc["TmDocumentEditor_ContentControlKindField"]
        };

    private IReadOnlyList<DocumentImageAsset> CanvasContentControlImageAssetOptions
    {
        get
        {
            var assets = new List<DocumentImageAsset>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            AddCanvasContentControlImageAssets(ImageAssetOptions, assets, ids);
            AddCanvasContentControlImageAssets(_document?.Assets, assets, ids);
            AddCanvasContentControlImageAssets(_currentDocument?.Assets, assets, ids);
            return assets;
        }
    }

    private static void AddCanvasContentControlImageAssets(
        IEnumerable<DocumentImageAsset>? source,
        ICollection<DocumentImageAsset> target,
        ISet<string> ids)
    {
        if (source is null)
        {
            return;
        }

        foreach (var asset in source)
        {
            if (string.IsNullOrWhiteSpace(asset.Id) || !ids.Add(asset.Id))
            {
                continue;
            }

            target.Add(asset);
        }
    }

    private static string ContentControlAssetLabel(DocumentImageAsset asset)
        => FirstNonEmpty(asset.Caption, asset.AltText, asset.FileName, asset.Id);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed class CanvasContentControlPopoverState
    {
        public string ControlId { get; init; } = string.Empty;

        public DocumentContentControlKind Kind { get; init; }

        public string Title { get; init; } = string.Empty;

        public bool IsRequired { get; init; }

        public bool LockContent { get; init; }

        public string Text { get; init; } = string.Empty;

        public string SelectedValue { get; init; } = string.Empty;

        public string DateIso { get; init; } = string.Empty;

        public string AssetId { get; init; } = string.Empty;

        public IReadOnlyList<DocumentContentControlItem> Items { get; init; } = [];

        public static CanvasContentControlPopoverState From(DocumentContentControl control)
            => new()
            {
                ControlId = control.ControlId,
                Kind = control.Kind,
                Title = FirstNonEmpty(control.Alias, control.Tag, control.ControlId),
                IsRequired = control.IsRequired,
                LockContent = control.LockContent,
                Text = control.Value.Text ?? string.Empty,
                SelectedValue = control.Value.SelectedValue ?? string.Empty,
                DateIso = control.Value.DateIso ?? control.Value.Text ?? string.Empty,
                AssetId = control.Value.AssetId ?? string.Empty,
                Items = control.Items
                    .Select(item => new DocumentContentControlItem
                    {
                        Value = item.Value,
                        DisplayText = item.DisplayText
                    })
                    .ToList()
            };
    }

    private Task HandleCanvasEngineReadyAsync(TmDocumentCanvasEngineHost _)
        => SyncCanvasEngineStateAsync();

    private async Task HandleCanvasEngineChangedAsync(TmDocumentCanvasEngineHost.CanvasEngineChangedState _)
    {
        if (_disposed || !UsingCanvasEngine)
        {
            return;
        }

        var collaborationBefore = !_suppressCollaborationBroadcast && _document is not null
            ? Clone(_collaborationSnapshot ?? _document)
            : null;
        await SyncCanvasEngineStateAsync();
        if (_canvasHost is not null)
        {
            var canvasDocument = await _canvasHost.RequestDocumentAsync();
            if (canvasDocument is not null)
            {
                var synchronizedDocument = CreateProviderBoundarySnapshot(canvasDocument, preserveImageBlocks: true);
                _document = synchronizedDocument;
                _currentDocument = synchronizedDocument;
                SyncCommentsFromRuntimeDocument(synchronizedDocument);
                if (collaborationBefore is not null && !DocumentsEqual(collaborationBefore, synchronizedDocument))
                {
                    await BroadcastLocalCollaborationChangeAsync(collaborationBefore, synchronizedDocument);
                }
            }
        }

        if (_isDirty && _document is not null && !EffectiveReadOnly)
        {
            _autosave.RegisterLocalChange();
            if (_isSaving)
            {
                _saveAgainRequested = true;
            }

            ScheduleAutoSave();
        }
        else if (!_isDirty)
        {
            _autosave.ResetSynchronized();
        }

        SyncAutosavePendingAction();
        await UpdateBeforeUnloadGuardAsync();
        await InvokeAsync(StateHasChanged);
    }

    private static DocumentTextAlignment ParseCanvasAlignment(string? alignment)
        => (alignment ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "center" or "middle" => DocumentTextAlignment.Center,
            "right" or "end" => DocumentTextAlignment.Right,
            "justify" or "justified" or "block" => DocumentTextAlignment.Justify,
            _ => DocumentTextAlignment.Left
        };

    private static WysiwygFormattingValue ToFormattingValue(bool active, bool mixed)
        => mixed
            ? WysiwygFormattingValue.Mixed
            : active
                ? WysiwygFormattingValue.Active
                : WysiwygFormattingValue.Inactive;

    private static double NextFontSizeStep(string? currentValue, int direction)
    {
        ReadOnlySpan<double> steps = [6, 7, 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 32, 36, 40, 44, 48, 54, 60, 66, 72, 80, 88, 96];
        var current = ParseFontSizePoints(currentValue, 11);
        if (direction >= 0)
        {
            foreach (var step in steps)
            {
                if (step > current + 0.001)
                {
                    return step;
                }
            }

            return steps[^1];
        }

        for (var index = steps.Length - 1; index >= 0; index--)
        {
            if (steps[index] < current - 0.001)
            {
                return steps[index];
            }
        }

        return steps[0];
    }

    private static double ParseFontSizePoints(string? value, double fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^2].Trim();
        }

        return double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 6 and <= 96
            ? parsed
            : fallback;
    }

    private async Task ToggleInlineMarkAsync(InlineMarkType markType)
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        var commandId = MarkCommandId(markType);
        if (await RouteToCoreEngineAsync(commandId))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync(commandId))
        {
            return;
        }

        if (_wysiwygHost is not null)
        {
            var selection = await ResolveInlineCommandSelectionAsync();
            if (selection is not null)
            {
                RememberBodySelection(selection);
            }

            var command = markType switch
            {
                InlineMarkType.Bold => "toggleBold",
                InlineMarkType.Italic => "toggleItalic",
                InlineMarkType.Underline => "toggleUnderline",
                InlineMarkType.Strikethrough => "toggleStrikethrough",
                _ => "toggleMark"
            };
            var payload = WithSelectionPayload(
                command == "toggleMark"
                    ? new WysiwygMarkPayload { MarkType = markType.ToString() }
                    : null,
                selection);

            await _wysiwygHost.ExecuteEditorCommandAsync(command, payload);
            await RefreshFormattingStateFromRuntimeAsync(selection);
        }
    }

    private static string MarkCommandId(InlineMarkType markType) => markType switch
    {
        InlineMarkType.Bold => "bold",
        InlineMarkType.Italic => "italic",
        InlineMarkType.Underline => "underline",
        InlineMarkType.Strikethrough => "strikethrough",
        InlineMarkType.Superscript => "superscript",
        InlineMarkType.Subscript => "subscript",
        InlineMarkType.SmallCaps => "smallCaps",
        InlineMarkType.AllCaps => "allCaps",
        InlineMarkType.DoubleStrikethrough => "doubleStrikethrough",
        _ => "bold",
    };

    private async Task ApplyFontFamilyAsync(string cssFamily)
    {
        if (EffectiveReadOnly || string.IsNullOrWhiteSpace(cssFamily))
        {
            return;
        }

        if (!_fontFamilies.Any(font => string.Equals(font.CssFamily, cssFamily, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (await RouteToCoreEngineAsync("fontfamily", cssFamily))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("fontfamily", cssFamily))
        {
            return;
        }

        if (_wysiwygHost is null)
        {
            return;
        }

        var selection = await ResolveInlineCommandSelectionAsync();
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("setFontFamily", WithSelectionPayload(new
        {
            Value = cssFamily
        }, selection));
        await RefreshFormattingStateFromRuntimeAsync(selection);
    }

    private async Task ApplyFontSizeAsync(double sizePt)
    {
        if (EffectiveReadOnly || sizePt is < 6 or > 96)
        {
            return;
        }

        if (await RouteToCoreEngineAsync("fontsize", FormattableString.Invariant($"{sizePt:0.##}pt")))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("fontsize", FormattableString.Invariant($"{sizePt:0.##}pt")))
        {
            return;
        }

        if (_wysiwygHost is null)
        {
            return;
        }

        var selection = await ResolveInlineCommandSelectionAsync();
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("setFontSize", WithSelectionPayload(new
        {
            Value = FormattableString.Invariant($"{sizePt:0.##}pt")
        }, selection));
        await RefreshFormattingStateFromRuntimeAsync(selection);
    }

    private async Task IncreaseFontSizeAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("increaseFontSize"))
        {
            return;
        }

        await ApplyFontSizeAsync(NextFontSizeStep(_formattingState.FontSize, 1));
    }

    private async Task DecreaseFontSizeAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("decreaseFontSize"))
        {
            return;
        }

        await ApplyFontSizeAsync(NextFontSizeStep(_formattingState.FontSize, -1));
    }

    private async Task ChangeCaseAsync(string variant)
    {
        if (EffectiveReadOnly || string.IsNullOrWhiteSpace(variant))
        {
            return;
        }

        await RouteToCanvasEngineAsync("changeCase", new { variant });
    }

    private async Task ApplyTextColorAsync(string color)
    {
        if (UsingCoreEngine)
        {
            await RouteToCoreEngineAsync("textcolor", color);
            return;
        }

        if (UsingCanvasEngine)
        {
            await RouteToCanvasEngineAsync("textcolor", color);
            return;
        }

        await ApplyColorMarkAsync(InlineMarkType.TextColor, color);
    }

    private async Task ApplyHighlightColorAsync(string color)
    {
        if (UsingCoreEngine)
        {
            await RouteToCoreEngineAsync("highlight", string.IsNullOrWhiteSpace(color) ? null : color);
            return;
        }

        if (UsingCanvasEngine)
        {
            await RouteToCanvasEngineAsync("highlight", string.IsNullOrWhiteSpace(color) ? null : color);
            return;
        }
        if (string.IsNullOrWhiteSpace(color))
        {
            await ClearColorMarkAsync(InlineMarkType.Highlight);
            return;
        }
        await ApplyColorMarkAsync(InlineMarkType.Highlight, color);
    }

    private async Task ApplyParagraphAlignmentAsync(DocumentTextAlignment alignment)
    {
        if (await RouteToCoreEngineAsync("align", alignment.ToString().ToLowerInvariant()))
        {
            _formattingState.ParagraphAlignment = alignment;
            _formattingState.ParagraphAlignmentMixed = false;
            return;
        }

        if (await RouteToCanvasEngineAsync("align", alignment.ToString().ToLowerInvariant()))
        {
            _formattingState.ParagraphAlignment = alignment;
            _formattingState.ParagraphAlignmentMixed = false;
            return;
        }

        var selection = await ResolveParagraphCommandSelectionAsync();
        _pendingParagraphAlignment = alignment;
        _pendingParagraphAlignmentBlockId = selection?.AnchorBlockId;
        _pendingParagraphAlignmentExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
        ApplyParagraphAlignmentToLocalSnapshots(selection, alignment);

        await ExecuteParagraphCommandAsync("setParagraphAlignment", new { Alignment = alignment }, selection);
        _formattingState.ParagraphAlignment = alignment;
        _formattingState.ParagraphAlignmentMixed = false;
        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearColorMarkAsync(InlineMarkType markType)
    {
        if (_wysiwygHost is null || EffectiveReadOnly)
        {
            return;
        }

        var command = markType == InlineMarkType.TextColor
            ? "textColor"
            : "backgroundColor";
        var selection = await ResolveInlineCommandSelectionAsync();
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync(command, WithSelectionPayload(new
        {
            Value = (string?)null
        }, selection));
        await RefreshFormattingStateFromRuntimeAsync(selection);
        ApplyFloatingFormattingOptimisticState(
            selection,
            markType == InlineMarkType.TextColor ? "textColor" : "highlightColor",
            string.Empty);
        await InvokeAsync(StateHasChanged);
    }

    private void ApplyParagraphAlignmentToLocalSnapshots(WysiwygSelectionSnapshot? selection, DocumentTextAlignment alignment)
    {
        if (selection is null)
        {
            return;
        }

        if (_document is not null)
        {
            ApplyParagraphAlignmentToDocument(_document, selection, alignment);
        }

        if (_currentDocument is not null && !ReferenceEquals(_currentDocument, _document))
        {
            ApplyParagraphAlignmentToDocument(_currentDocument, selection, alignment);
        }
    }

    private static void ApplyParagraphAlignmentToDocument(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot selection,
        DocumentTextAlignment alignment)
    {
        var blocks = ResolveSelectionBlocks(document, selection);
        var anchorIndex = blocks.FindIndex(block => block.Id == selection.AnchorBlockId);
        if (anchorIndex < 0)
        {
            return;
        }

        var focusBlockId = string.IsNullOrWhiteSpace(selection.FocusBlockId)
            ? selection.AnchorBlockId
            : selection.FocusBlockId;
        var focusIndex = blocks.FindIndex(block => block.Id == focusBlockId);
        if (focusIndex < 0)
        {
            focusIndex = anchorIndex;
        }

        var start = Math.Min(anchorIndex, focusIndex);
        var end = Math.Max(anchorIndex, focusIndex);
        foreach (var block in blocks.Skip(start).Take(end - start + 1))
        {
            if (block.Content is not (ParagraphBlockContent or HeadingBlockContent or ListBlockContent or QuoteBlockContent))
            {
                continue;
            }

            block.ParagraphProperties ??= new DocumentParagraphProperties();
            block.ParagraphProperties.Alignment = alignment;
        }
    }

    private async Task ApplyLineSpacingAsync(double lineSpacing)
    {
        if (lineSpacing is < 0.8 or > 3)
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("lineSpacing", lineSpacing))
        {
            _formattingState.LineSpacing = lineSpacing;
            _formattingState.LineSpacingMixed = false;
            return;
        }

        var selection = await ResolveParagraphCommandSelectionAsync();
        _pendingLineSpacing = lineSpacing;
        _pendingLineSpacingBlockId = selection?.AnchorBlockId;
        _pendingLineSpacingExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);

        await ExecuteParagraphCommandAsync("setLineSpacing", new { LineSpacing = lineSpacing });
        _formattingState.LineSpacing = lineSpacing;
        _formattingState.LineSpacingMixed = false;
        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplySpacingBeforeAsync(double spacingBefore)
    {
        if (spacingBefore is < 0 or > 144)
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("spacingBefore", spacingBefore))
        {
            _formattingState.SpacingBefore = spacingBefore;
            _formattingState.SpacingBeforeMixed = false;
            return;
        }

        await ApplyParagraphPropertiesAsync(new DocumentParagraphPropertiesPatch { SpacingBefore = spacingBefore });
    }

    private async Task ApplySpacingAfterAsync(double spacingAfter)
    {
        if (spacingAfter is < 0 or > 144)
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("spacingAfter", spacingAfter))
        {
            _formattingState.SpacingAfter = spacingAfter;
            _formattingState.SpacingAfterMixed = false;
            return;
        }

        await ApplyParagraphPropertiesAsync(new DocumentParagraphPropertiesPatch { SpacingAfter = spacingAfter });
    }

    private async Task IncreaseParagraphIndentAsync()
    {
        if (await RouteToCanvasEngineAsync("increaseIndent"))
        {
            return;
        }

        await ExecuteParagraphCommandAsync("increaseIndent");
    }

    private async Task DecreaseParagraphIndentAsync()
    {
        if (await RouteToCanvasEngineAsync("decreaseIndent"))
        {
            return;
        }

        await ExecuteParagraphCommandAsync("decreaseIndent");
    }

    private async Task ToggleBulletListAsync()
    {
        if (await RouteToCoreEngineAsync("bulletList"))
        {
            return;
        }
        if (await RouteToCanvasEngineAsync("bulletList"))
        {
            return;
        }
        await ExecuteParagraphCommandAsync("toggleBulletList");
    }

    private async Task ToggleNumberedListAsync()
    {
        if (await RouteToCoreEngineAsync("numberedList"))
        {
            return;
        }
        if (await RouteToCanvasEngineAsync("numberedList"))
        {
            return;
        }
        await ExecuteParagraphCommandAsync("toggleNumberedList");
    }

    private async Task ApplyParagraphPropertiesAsync(DocumentParagraphPropertiesPatch properties)
    {
        if (_wysiwygHost is null || EffectiveReadOnly)
        {
            return;
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("setParagraphProperties", new
        {
            ParagraphProperties = properties
        });
    }

    private async Task ExecuteParagraphCommandAsync(string command, object? payload = null, WysiwygSelectionSnapshot? resolvedSelection = null)
    {
        if (_wysiwygHost is null || EffectiveReadOnly)
        {
            return;
        }

        var selection = resolvedSelection ?? await ResolveParagraphCommandSelectionAsync();
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync(command, WithSelectionPayload(payload, selection));
    }

    private async Task<WysiwygSelectionSnapshot?> ResolveParagraphCommandSelectionAsync()
    {
        if (_wysiwygHost is not null)
        {
            var runtime = await _wysiwygHost.RequestRuntimeSelectionAsync();
            if (runtime is not null && IsBodySelection(runtime))
            {
                RememberBodySelection(runtime);
                return runtime;
            }
        }

        var current = _formattingState.CurrentSelection;
        if (current is not null && IsBodySelection(current))
        {
            return current;
        }

        return _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot ?? current;
    }

    private async Task<WysiwygSelectionSnapshot?> ResolveInlineCommandSelectionAsync()
    {
        var current = _formattingState.CurrentSelection;
        var currentCollapsedBodySelection = current is not null
            && current.IsCollapsed
            && IsBodySelection(current)
                ? current
                : null;

        if (current is not null && current.IsCollapsed == false && IsBodySelection(current))
        {
            RememberBodySelection(current);
            return current;
        }

        if (_wysiwygHost is not null)
        {
            var runtime = await _wysiwygHost.RequestRuntimeSelectionAsync();
            if (runtime is not null && IsBodySelection(runtime))
            {
                RememberBodySelection(runtime);
                if (runtime.IsCollapsed == false)
                {
                    return runtime;
                }

                if (currentCollapsedBodySelection is not null || _lastBodyRangeSelectionSnapshot is null)
                {
                    _lastBodyRangeSelectionSnapshot = null;
                    return runtime;
                }
            }
        }

        if (currentCollapsedBodySelection is not null)
        {
            _lastBodyRangeSelectionSnapshot = null;
            RememberBodySelection(currentCollapsedBodySelection);
            return currentCollapsedBodySelection;
        }

        return _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot ?? current;
    }

    private static bool IsBodySelection(WysiwygSelectionSnapshot snapshot)
        => !string.IsNullOrWhiteSpace(snapshot.AnchorBlockId)
            && (string.Equals(snapshot.Region, "Body", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(snapshot.Region));

    private void RememberBodySelection(WysiwygSelectionSnapshot snapshot)
    {
        _lastBodySelectionSnapshot = snapshot;
        if (snapshot.IsCollapsed == false)
        {
            _lastBodyRangeSelectionSnapshot = snapshot;
        }
    }

    private async Task ApplyColorMarkAsync(InlineMarkType markType, string color)
    {
        var normalizedColor = NormalizeHexColor(color);
        if (_wysiwygHost is null || EffectiveReadOnly || normalizedColor is null)
        {
            return;
        }

        var command = markType == InlineMarkType.TextColor
            ? "textColor"
            : "backgroundColor";
        var selection = await ResolveInlineCommandSelectionAsync();
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync(command, WithSelectionPayload(new
        {
            Value = normalizedColor
        }, selection));
        await RefreshFormattingStateFromRuntimeAsync(selection);
        ApplyFloatingFormattingOptimisticState(
            selection,
            markType == InlineMarkType.TextColor ? "textColor" : "highlightColor",
            normalizedColor);
        await InvokeAsync(StateHasChanged);
    }

    private static string? NormalizeHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var trimmed = color.Trim();
        if (trimmed.Length == 4
            && trimmed[0] == '#'
            && trimmed.Skip(1).All(Uri.IsHexDigit))
        {
            return string.Create(7, trimmed, static (span, source) =>
            {
                span[0] = '#';
                span[1] = char.ToLowerInvariant(source[1]);
                span[2] = char.ToLowerInvariant(source[1]);
                span[3] = char.ToLowerInvariant(source[2]);
                span[4] = char.ToLowerInvariant(source[2]);
                span[5] = char.ToLowerInvariant(source[3]);
                span[6] = char.ToLowerInvariant(source[3]);
            });
        }

        return trimmed.Length == 7
            && trimmed[0] == '#'
            && trimmed.Skip(1).All(Uri.IsHexDigit)
                ? trimmed.ToLowerInvariant()
                : null;
    }

    private static IDictionary<string, object?> WithSelectionPayload(object? payload, WysiwygSelectionSnapshot? selection)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (payload is not null)
        {
            foreach (var property in payload.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length == 0)
                {
                    values[property.Name] = property.GetValue(payload);
                }
            }
        }

        if (selection is not null)
        {
            values["Selection"] = selection;
            var selectionToken = string.IsNullOrWhiteSpace(selection.SelectionToken)
                ? selection.StableSelectionToken
                : selection.SelectionToken;
            if (!string.IsNullOrWhiteSpace(selectionToken))
            {
                values["SelectionToken"] = selectionToken;
                values["StableSelectionToken"] = selectionToken;
            }

            if (selection.SelectionTokenData is not null)
            {
                values["SelectionTokenData"] = selection.SelectionTokenData;
            }
        }

        return values;
    }

    private Task ClearInlineFormattingAsync()
        => ClearInlineFormattingAsync(null);

    private async Task ClearInlineFormattingAsync(WysiwygSelectionSnapshot? explicitSelection)
    {
        if (!EffectiveReadOnly && await RouteToCanvasEngineAsync("clearFormatting"))
        {
            return;
        }

        if (_wysiwygHost is not null && !EffectiveReadOnly)
        {
            var selection = explicitSelection ?? _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
            if (selection is not null)
            {
                RememberBodySelection(selection);
            }

            await _wysiwygHost.ExecuteEditorCommandAsync("clearFormatting", WithSelectionPayload(null, selection));
        }
    }

    private async Task ApplyLinkAsync(string href)
        => await ApplyLinkAsync(new WysiwygLinkPayload { Href = href });

    private async Task ApplyLinkAsync(WysiwygLinkPayload payload)
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (await RouteToCoreEngineAsync("link", payload?.Href))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("link", payload?.Href))
        {
            return;
        }

        if (_wysiwygHost is null)
        {
            return;
        }

        var href = DocumentLinkUtility.NormalizeHref(payload.Href);
        if (!DocumentLinkUtility.IsSafeHref(href))
        {
            return;
        }

        var selection = _pendingLinkSelectionSnapshot?.IsCollapsed == false
            ? _pendingLinkSelectionSnapshot
            : _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
        _pendingLinkSelectionSnapshot = null;
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("insertLink", WithSelectionPayload(new
        {
            Href = href,
            Title = string.IsNullOrWhiteSpace(payload.Title) ? null : payload.Title.Trim()
        }, selection));
    }

    private async Task RemoveLinkAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (await RouteToCoreEngineAsync("removelink"))
        {
            return;
        }

        if (await RouteToCanvasEngineAsync("removelink"))
        {
            return;
        }

        if (_wysiwygHost is null)
        {
            return;
        }

        var selection = _pendingLinkSelectionSnapshot?.IsCollapsed == false
            ? _pendingLinkSelectionSnapshot
            : _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
        _pendingLinkSelectionSnapshot = null;
        if (selection is not null)
        {
            RememberBodySelection(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("removeLink", WithSelectionPayload(null, selection));
    }

    private async Task<WysiwygLinkInfo?> GetCurrentLinkInfoAsync()
    {
        if (_wysiwygHost is null)
        {
            return null;
        }

        var selection = await _wysiwygHost.RequestSelectionSnapshotAsync();
        _pendingLinkSelectionSnapshot = selection?.IsCollapsed == false
            ? selection
            : _lastBodyRangeSelectionSnapshot;
        return await _wysiwygHost.RequestLinkInfoAsync();
    }

    private async Task HandleEditorKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && (_textContextMenu is not null || _tableContextMenu is not null || _miniToolbar is not null))
        {
            CloseFloatingUi();
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.FocusAsync();
            }

            return;
        }

        if (args.Key == "Escape" && _commandPaletteOpen)
        {
            await CloseCommandPaletteAsync();
            return;
        }

        // Registry-routed shortcuts: check CanExecute before dispatching.
        var registryName = _keyboardManager.GetRegistryCommandName(args);
        if (registryName is not null)
        {
            if (registryName == "commandPalette")
            {
                await OpenCommandPaletteAsync();
                return;
            }

            var state = _commandRegistry.GetState(registryName);
            if (state?.IsEnabled != true)
            {
                return;
            }

            if (registryName == "undo")
            {
                await UndoAsync();
                return;
            }

            if (registryName == "redo")
            {
                await RedoAsync();
                return;
            }

            await _commandRegistry.ExecuteAsync(registryName, BuildCommandContext());
            return;
        }

        switch (_keyboardManager.GetCommand(args))
        {
            case DocumentEditorKeyboardCommand.OpenVersions:
                OpenSidePanel(DocumentSidePanelTab.Versions, manual: true);
                await InvokeAsync(StateHasChanged);
                break;
            case DocumentEditorKeyboardCommand.ActivateRibbon:
                await ActivateRibbonKeyboardModeAsync();
                break;
            case DocumentEditorKeyboardCommand.ClosePanel:
                await CloseTopmostEditorLayerAsync();
                break;
            case DocumentEditorKeyboardCommand.OpenFind:
                await OpenFindPanelAsync(replaceMode: false);
                break;
            case DocumentEditorKeyboardCommand.OpenReplace:
                await OpenFindPanelAsync(replaceMode: true);
                break;
        }
    }

    private async Task HandleWysiwygKeyboardCommandRequestedAsync(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return;
        }

        var state = _commandRegistry.GetState(commandName);
        if (state?.IsEnabled != true)
        {
            return;
        }

        await _commandRegistry.ExecuteAsync(commandName, BuildCommandContext());
        await RefreshCommandRegistryAsync();
    }

    private Task HandleWysiwygAccessibilityAnnouncementAsync(string message)
    {
        Announce(message);
        return InvokeAsync(StateHasChanged);
    }

    private async Task OpenFindPanelAsync(bool replaceMode)
    {
        _findReplaceMode = replaceMode;
        _findPanelOpen = true;
        _floatingLayerStack.Push(new DocumentFloatingLayerState
        {
            LayerId = FloatingLayerId.FindPanel,
            Kind = DocumentFloatingLayerKind.FindPanel,
            ZIndex = 25,
            Priority = 25,
            RestoreFocusTarget = "surface",
            CloseAsync = async () =>
            {
                _findPanelOpen = false;
                await ClearSearchMarkersAsync();
            }
        });
        await InvokeAsync(StateHasChanged);

        if (_findPanel is not null && _wysiwygHost is not null)
        {
            var selected = await _wysiwygHost.GetSelectedTextAsync();
            if (!string.IsNullOrEmpty(selected))
                await _findPanel.SetQueryAsync(selected);
        }
    }

    private async Task OpenCommandPaletteAsync()
    {
        await RefreshCommandRegistryAsync();
        if (!_commandRegistry.CurrentState.Values.Any(command => command.IsVisible))
        {
            return;
        }

        _commandPaletteOpen = true;
        await InvokeAsync(StateHasChanged);
    }

    private Task CloseCommandPaletteAsync()
    {
        _commandPaletteOpen = false;
        _focusDocumentOnRender = true;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ExecuteCommandPaletteCommandAsync(string commandName)
    {
        var state = _commandRegistry.GetState(commandName);
        var selection = _lastBodyRangeSelectionSnapshot ?? _lastBodySelectionSnapshot;
        if (state?.AffectsData == true && _wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
        }

        _commandPaletteOpen = false;
        await _commandRegistry.ExecuteAsync(commandName, BuildCommandContext());
        await RefreshCommandRegistryAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CloseFindPanelAsync()
    {
        _findPanelOpen = false;
        _floatingLayerStack.Remove(FloatingLayerId.FindPanel);
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.ExecCommandAsync("clearFind");
        }
        await ClearSearchMarkersAsync();
        await InvokeAsync(StateHasChanged);
        await FocusDocumentAsync();
    }

    private async Task HandleFindResultsChangedAsync(IReadOnlyList<DocumentSearchResult> results)
    {
        if (UsingCanvasEngine)
        {
            Announce(results.Count == 0
                ? Loc["TmDocumentEditor_FindNoResults"]
                : string.Format(CultureInfo.CurrentCulture, Loc["TmDocumentEditor_FindResultCount"], 1, results.Count));
            return;
        }

        if (_wysiwygHost is null) return;
        var ids = results.Select(r => r.BlockId).ToArray();
        var offsets = results.Select(r => r.BlockTextOffset).ToArray();
        var lengths = results.Select(r => r.Length).ToArray();
        await _wysiwygHost.SetSearchMarkersAsync(ids, offsets, lengths);
        Announce(results.Count == 0
            ? Loc["TmDocumentEditor_FindNoResults"]
            : string.Format(CultureInfo.CurrentCulture, Loc["TmDocumentEditor_FindResultCount"], 1, results.Count));
    }

    private async Task HandleActiveSearchResultChangedAsync(DocumentSearchResult result)
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync("gotoSearchResult", new { index = result.Index });
            return;
        }

        if (_wysiwygHost is null) return;
        await _wysiwygHost.ScrollToSearchResultAsync(result.BlockId, result.BlockTextOffset, result.Length);
    }

    // R.4.8 — the find panel's query changed; the core engine runs its own search + highlights
    // all matches (R.4.6h-2). Empty query clears the highlight.
    private async Task HandleFindSearchRequestedAsync(DocumentSearchQuery query)
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            if (string.IsNullOrEmpty(query.Text))
            {
                await _canvasHost.ExecCommandAsync("clearFind");
                return;
            }

            await _canvasHost.ExecCommandAsync("find", new
            {
                query = query.Text,
                options = new { caseSensitive = query.CaseSensitive, wholeWord = query.WholeWord, regex = query.UseRegex },
            });
            return;
        }

        if (!UsingCoreEngine || _coreHost is null) return;

        if (string.IsNullOrEmpty(query.Text))
        {
            await _coreHost.ExecCommandAsync("clearFind");
            return;
        }

        await _coreHost.ExecCommandAsync("find", new
        {
            query = query.Text,
            options = new { caseSensitive = query.CaseSensitive, wholeWord = query.WholeWord, regex = query.UseRegex },
        });
    }

    // R.4.8 — next(+1)/previous(-1) moves the core engine's current highlight.
    private async Task HandleFindNavigateAsync(int direction)
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync(direction < 0 ? "findPrev" : "findNext");
            return;
        }

        if (!UsingCoreEngine || _coreHost is null) return;
        await _coreHost.ExecCommandAsync(direction < 0 ? "findPrev" : "findNext");
    }

    private async Task HandleFindReplaceOneRequestedAsync(DocumentFindReplaceRequest request)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.ExecCommandAsync("replaceCurrent", new { replacement = request.Replacement });
            await SyncCoreEngineStateAsync();
            return;
        }

        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync("replaceCurrent", new { replacement = request.Replacement });
            await SyncCanvasEngineStateAsync();
            return;
        }

        if (_wysiwygHost is null || request.ActiveResult is null) return;

        await _wysiwygHost.ExecuteRuntimeCommandAsync("replaceOne", new
        {
            replacement = request.Replacement,
            query = request.Query.Text,
            caseSensitive = request.Query.CaseSensitive,
            wholeWord = request.Query.WholeWord,
            regex = request.Query.UseRegex,
            scope = request.Query.Scope.ToString(),
            markerId = request.ActiveResult.MarkerId,
            blockId = request.ActiveResult.BlockId,
            offset = request.ActiveResult.BlockTextOffset,
            length = request.ActiveResult.Length
        });
        await RefreshRuntimeUndoDirtyStateAsync();
    }

    private async Task HandleFindReplaceAllRequestedAsync(DocumentFindReplaceRequest request)
    {
        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.ExecCommandAsync("replaceall", new
            {
                query = request.Query.Text,
                replacement = request.Replacement,
                options = new { caseSensitive = request.Query.CaseSensitive, wholeWord = request.Query.WholeWord, regex = request.Query.UseRegex },
            });
            await SyncCoreEngineStateAsync();
            return;
        }

        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync("replaceAll", new
            {
                query = request.Query.Text,
                replacement = request.Replacement,
                options = new { caseSensitive = request.Query.CaseSensitive, wholeWord = request.Query.WholeWord, regex = request.Query.UseRegex },
            });
            await SyncCanvasEngineStateAsync();
            return;
        }

        if (_wysiwygHost is null) return;

        await _wysiwygHost.ExecuteRuntimeCommandAsync("replaceAll", new
        {
            replacement = request.Replacement,
            query = request.Query.Text,
            caseSensitive = request.Query.CaseSensitive,
            wholeWord = request.Query.WholeWord,
            regex = request.Query.UseRegex,
            scope = request.Query.Scope.ToString(),
            markerId = request.ActiveResult?.MarkerId,
            blockId = request.ActiveResult?.BlockId,
            offset = request.ActiveResult?.BlockTextOffset,
            length = request.ActiveResult?.Length
        });
        await RefreshRuntimeUndoDirtyStateAsync();
    }

    private async Task ClearSearchMarkersAsync()
    {
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.ExecCommandAsync("clearFind");
        }

        if (_wysiwygHost is not null)
            await _wysiwygHost.ClearSearchMarkersAsync();
    }

    private async Task ActivateRibbonKeyboardModeAsync()
    {
        if (!ShowToolbar || _toolbar is null)
        {
            return;
        }

        _ribbonKeyboardMode = true;
        await _toolbar.FocusActiveTabAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CloseTopmostEditorLayerAsync()
    {
        if (_floatingLayerStack.HasOpenLayers)
        {
            await _floatingLayerStack.CloseTopmostAsync();
            await FocusDocumentAsync();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_sidePanelOpen)
        {
            CloseSidePanel();
            await FocusDocumentAsync();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (_isFullscreen)
        {
            await ToggleFullscreenAsync();
        }
    }

    private async Task FocusDocumentAsync()
    {
        _focusManager.PopRestoreTarget();
        if (UsingCanvasEngine && _canvasHost is not null)
        {
            await _canvasHost.FocusAsync();
            return;
        }

        if (UsingCoreEngine && _coreHost is not null)
        {
            await _coreHost.FocusAsync();
            return;
        }

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.FocusAsync();
        }
    }

    private void Announce(string? message, DocumentEditorAnnouncementPoliteness politeness = DocumentEditorAnnouncementPoliteness.Polite)
        => _announcer.Announce(message, politeness);

    private void MarkDirtyAfterCommand()
    {
        if (_document is not null)
        {
            _isDirty = true;
            _suggestionSnapshot = Clone(_document);
        }

        StateHasChanged();
    }

    private void HandleCommandStackChanged()
    {
        if (_suppressCommandStackChangedRender)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await RefreshCommandRegistryAsync();
            StateHasChanged();
        });
    }

    private static bool IsLiveWysiwygPatch(WysiwygPatch patch)
    {
        if (!string.IsNullOrWhiteSpace(patch.TransactionId))
        {
            return true;
        }

        return patch.Type is "InsertText"
            or "DeleteRange"
            or "DeleteContentBackward"
            or "DeleteContentForward"
            or "InsertParagraph"
            or "SplitBlock"
            or "InsertLineBreak"
            or "InsertSoftBreak"
            or "MergeWithPreviousBlock"
            or "ToggleMark"
            or "SetMarks"
            or "ClearFormatting"
            or "SetParagraphProperties";
    }

    private static bool IsImageInsertPatch(WysiwygPatch patch) =>
        string.Equals(patch.Type, "InsertInline", StringComparison.Ordinal)
        && patch.Inline is DocumentDrawingRun;

    private static string? GetImageInsertAnchorBlockId(WysiwygPatch patch) =>
        patch.AfterSelection?.ObjectSelection?.AnchorBlockId
        ?? patch.AfterSelection?.AnchorBlockId
        ?? patch.Selection?.ObjectSelection?.AnchorBlockId
        ?? patch.Selection?.AnchorBlockId;

    private static bool IsTrackedStructuralPatch(WysiwygPatch patch)
    {
        return string.Equals(patch.RevisionType, "Structural", StringComparison.Ordinal)
            || patch.Type is "InsertParagraph" or "SplitBlock";
    }

    private bool TryApplyTrackedRevisionPatch(DocumentEditorDocument document, WysiwygPatch patch)
    {
        return patch.Type switch
        {
            "InsertText" => TryApplyTrackedInsertion(document, patch),
            "DeleteRange" => TryApplyTrackedDeletion(document, patch, patch.DeleteLength),
            "DeleteContentBackward" => TryApplyTrackedDeletion(
                document,
                patch,
                string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length,
                backward: true),
            "DeleteContentForward" => TryApplyTrackedDeletion(
                document,
                patch,
                string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length),
            "DeleteWordBackward" => TryApplyTrackedDeletion(
                document,
                patch,
                string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length,
                backward: true),
            "DeleteWordForward" => TryApplyTrackedDeletion(
                document,
                patch,
                string.IsNullOrEmpty(patch.Data) ? 1 : patch.Data.Length),
            "ToggleMark" => TryApplyTrackedFormatting(document, patch),
            "SetMarks" => TryApplyTrackedFormatting(document, patch),
            "ClearFormatting" => TryApplyTrackedFormatting(document, patch),
            _ => false
        };
    }

    private bool TryUpsertTrackedStructuralRevision(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var revisionId = string.IsNullOrWhiteSpace(patch.RevisionId) ? Guid.NewGuid().ToString("N") : patch.RevisionId;
        var revision = document.Revisions.FirstOrDefault(candidate => candidate.Id == revisionId);
        if (revision is null)
        {
            revision = CreateRevision(
                DocumentRevisionType.Structure,
                patch.Selection?.AnchorBlockId ?? patch.Block?.Id,
                patch.Type,
                revisionId);
            document.Revisions.Add(revision);
        }

        revision.Type = DocumentRevisionType.Structure;
        revision.Action = DocumentRevisionAction.Pending;
        revision.PayloadJson = string.IsNullOrWhiteSpace(patch.Data) ? patch.Type : patch.Data;
        revision.Range.BlockId = patch.Selection?.AnchorBlockId ?? patch.Block?.Id ?? revision.Range.BlockId;
        revision.Range.StartOffset = patch.Selection?.AnchorOffset ?? revision.Range.StartOffset;
        revision.Range.EndOffset = patch.AfterSelection?.AnchorOffset ?? revision.Range.EndOffset;
        return true;
    }

    private bool TryApplyTrackedInsertion(DocumentEditorDocument document, WysiwygPatch patch)
    {
        var context = ResolveEditableInlineContext(document, patch.Selection);
        if (context.Inlines is null || context.Inline is not TextRun textRun || string.IsNullOrEmpty(patch.Data))
        {
            return false;
        }

        var suppliedRevisionId = string.IsNullOrWhiteSpace(patch.RevisionId) ? null : patch.RevisionId;
        if (!string.IsNullOrWhiteSpace(suppliedRevisionId)
            && TryFindRevisionTextRun(document, suppliedRevisionId, DocumentRevisionType.Insertion, out var existingRun))
        {
            existingRun.TextRun.Text += patch.Data;
            var existingRevision = document.Revisions.FirstOrDefault(revision => revision.Id == suppliedRevisionId);
            if (existingRevision is not null)
            {
                existingRevision.PayloadJson = GetRevisionPayload(existingRevision.PayloadJson, patch.Data);
                UpdateRevisionRange(existingRevision, existingRun.InlineIndex, 0, existingRun.TextRun.Text.Length);
            }

            return true;
        }

        var existingInsertionRevisionId = GetPendingRevisionMark(textRun, DocumentRevisionType.Insertion);
        if (!string.IsNullOrWhiteSpace(existingInsertionRevisionId))
        {
            var offset = Math.Clamp(patch.Selection?.AnchorOffset ?? textRun.Text.Length, 0, textRun.Text.Length);
            textRun.Text = textRun.Text.Insert(offset, patch.Data);
            var existing = document.Revisions.FirstOrDefault(revision => revision.Id == existingInsertionRevisionId);
            if (existing is not null)
            {
                existing.PayloadJson = GetRevisionPayload(existing.PayloadJson, patch.Data);
            }

            return true;
        }

        var revision = document.Revisions.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(suppliedRevisionId)
            && candidate.Id == suppliedRevisionId)
            ?? CreateRevision(DocumentRevisionType.Insertion, context.Block?.Id, patch.Data, suppliedRevisionId);
        if (!document.Revisions.Contains(revision))
        {
            document.Revisions.Add(revision);
        }

        revision.Type = DocumentRevisionType.Insertion;
        revision.Action = DocumentRevisionAction.Pending;
        if (string.IsNullOrWhiteSpace(revision.PayloadJson))
        {
            revision.PayloadJson = patch.Data;
        }
        else if (!string.Equals(revision.PayloadJson, patch.Data, StringComparison.Ordinal))
        {
            revision.PayloadJson = GetRevisionPayload(revision.PayloadJson, patch.Data);
        }

        var offsetToInsert = Math.Clamp(patch.Selection?.AnchorOffset ?? textRun.Text.Length, 0, textRun.Text.Length);
        var replacement = new List<InlineContent>();
        AddTextSlice(replacement, textRun, 0, offsetToInsert);
        replacement.Add(new TextRun
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = patch.Data,
            Marks = CopyMarks(textRun.Marks)
                .Where(mark => mark.Type != InlineMarkType.Revision)
                .Append(CreateRevisionMark(revision))
                .ToList()
        });
        AddTextSlice(replacement, textRun, offsetToInsert, textRun.Text.Length);

        context.Inlines.RemoveAt(context.InlineIndex);
        context.Inlines.InsertRange(context.InlineIndex, MergeAdjacentTextRuns(replacement));
        UpdateRevisionRange(revision, context.InlineIndex, 0, patch.Data.Length);
        return true;
    }

    private bool TryApplyTrackedDeletion(DocumentEditorDocument document, WysiwygPatch patch, int length, bool backward = false)
    {
        var context = ResolveEditableInlineContext(document, patch.Selection);
        if (context.Inlines is null || context.Inline is not TextRun textRun)
        {
            return false;
        }

        var offset = Math.Clamp(patch.Selection?.AnchorOffset ?? 0, 0, textRun.Text.Length);
        var start = backward ? Math.Clamp(offset - length, 0, textRun.Text.Length) : offset;
        var end = backward ? offset : Math.Clamp(offset + Math.Max(length, 0), start, textRun.Text.Length);
        if (end <= start)
        {
            return true;
        }

        var deletedText = textRun.Text[start..end];
        var insertionRevisionId = GetPendingRevisionMark(textRun, DocumentRevisionType.Insertion);
        var replacement = new List<InlineContent>();
        AddTextSlice(replacement, textRun, 0, start);

        if (string.IsNullOrWhiteSpace(insertionRevisionId))
        {
            var suppliedRevisionId = string.IsNullOrWhiteSpace(patch.RevisionId) ? null : patch.RevisionId;
            var revision = document.Revisions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, suppliedRevisionId, StringComparison.Ordinal)
                && candidate.Type == DocumentRevisionType.Deletion
                && candidate.Action == DocumentRevisionAction.Pending);
            if (revision is null)
            {
                revision = CreateRevision(DocumentRevisionType.Deletion, context.Block?.Id, deletedText, suppliedRevisionId);
                document.Revisions.Add(revision);
            }
            else if (string.IsNullOrWhiteSpace(revision.PayloadJson))
            {
                revision.PayloadJson = deletedText;
            }
            else if (!string.Equals(revision.PayloadJson, deletedText, StringComparison.Ordinal))
            {
                revision.PayloadJson = GetRevisionPayload(revision.PayloadJson, deletedText);
            }

            replacement.Add(new TextRun
            {
                Id = Guid.NewGuid().ToString("N"),
                Text = deletedText,
                Marks = CopyMarks(textRun.Marks)
                    .Where(mark => mark.Type != InlineMarkType.Revision)
                    .Append(CreateRevisionMark(revision))
                    .ToList()
            });
            UpdateRevisionRange(revision, context.InlineIndex, start, end);
        }

        AddTextSlice(replacement, textRun, end, textRun.Text.Length);
        context.Inlines.RemoveAt(context.InlineIndex);
        context.Inlines.InsertRange(context.InlineIndex, MergeAdjacentTextRuns(replacement));
        EnsureEditableInlinesHaveText(context.Inlines);
        return true;
    }

    private bool TryApplyTrackedFormatting(DocumentEditorDocument document, WysiwygPatch patch)
    {
        if (patch.Selection is null || patch.Selection.IsCollapsed)
        {
            return false;
        }

        var block = document.Blocks.FirstOrDefault(candidate => candidate.Id == patch.Selection.AnchorBlockId);
        var inlines = GetEditableInlines(block?.Content);
        if (block is null || inlines is null || inlines.Count == 0)
        {
            return false;
        }

        var markType = ParseInlineMarkType(patch.MarkType);
        var start = Math.Min(patch.Selection.AnchorOffset, patch.Selection.FocusOffset);
        var end = Math.Max(patch.Selection.AnchorOffset, patch.Selection.FocusOffset);
        var textLength = inlines.Sum(inline => GetInlineText(inline).Length);
        start = Math.Clamp(start, 0, textLength);
        end = Math.Clamp(end, start, textLength);
        if (end <= start)
        {
            return false;
        }

        var rangeHadMark = RangeHasMark(inlines, markType, start, end);
        new WysiwygPatchApplier().ApplyPatch(document, patch);

        inlines = GetEditableInlines(block.Content);
        if (inlines is null)
        {
            return true;
        }

        var payload = new DocumentFormattingRevisionPayload
        {
            MarkType = markType,
            NewActive = !rangeHadMark
        };
        var revisionPayload = System.Text.Json.JsonSerializer.Serialize(payload, DocumentEditorJson.Options);
        var suppliedRevisionId = string.IsNullOrWhiteSpace(patch.RevisionId) ? null : patch.RevisionId;
        var revision = document.Revisions.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(suppliedRevisionId)
            && string.Equals(candidate.Id, suppliedRevisionId, StringComparison.Ordinal))
            ?? CreateRevision(
                DocumentRevisionType.Formatting,
                block.Id,
                revisionPayload,
                suppliedRevisionId);

        revision.Type = DocumentRevisionType.Formatting;
        revision.Action = DocumentRevisionAction.Pending;
        revision.PayloadJson = revisionPayload;
        revision.Range.BlockId = block.Id;
        UpdateRevisionRange(revision, 0, start, end);
        if (!document.Revisions.Contains(revision))
        {
            document.Revisions.Add(revision);
        }

        AddRevisionMarkToRange(inlines, revision, start, end);
        return true;
    }

    private DocumentRevision CreateRevision(DocumentRevisionType type, string? blockId, string payload, string? revisionId = null)
        => new()
        {
            Id = string.IsNullOrWhiteSpace(revisionId) ? Guid.NewGuid().ToString("N") : revisionId,
            Type = type,
            Range = new DocumentRevisionRange { BlockId = blockId },
            Author = new DocumentRevisionAuthor
            {
                Id = Author?.Id ?? string.Empty,
                DisplayName = Author?.DisplayName ?? Loc["TmDocumentEditor_UnknownAuthor"]
            },
            CreatedAt = DateTimeOffset.UtcNow,
            Action = DocumentRevisionAction.Pending,
            PayloadJson = payload
        };

    private static void UpdateRevisionRange(DocumentRevision revision, int inlineIndex, int startOffset, int endOffset)
    {
        revision.Range.StartInlineIndex = inlineIndex;
        revision.Range.EndInlineIndex = inlineIndex;
        revision.Range.StartOffset = startOffset;
        revision.Range.EndOffset = endOffset;
    }

    private static InlineMark CreateRevisionMark(DocumentRevision revision)
        => new()
        {
            Type = InlineMarkType.Revision,
            RevisionId = revision.Id,
            Value = revision.Type.ToString()
        };

    private static InlineMarkType ParseInlineMarkType(string? value)
    {
        return Enum.TryParse<InlineMarkType>(value, ignoreCase: true, out var markType)
            ? markType
            : InlineMarkType.Bold;
    }

    private static bool RangeHasMark(List<InlineContent> inlines, InlineMarkType markType, int startOffset, int endOffset)
    {
        var current = 0;
        foreach (var inline in inlines)
        {
            var text = GetInlineText(inline);
            var inlineStart = current;
            var inlineEnd = current + text.Length;
            current = inlineEnd;
            if (inlineEnd <= startOffset || inlineStart >= endOffset)
            {
                continue;
            }

            if (inline.Marks.Any(mark => mark.Type == markType))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddRevisionMarkToRange(List<InlineContent> inlines, DocumentRevision revision, int startOffset, int endOffset)
    {
        var current = 0;
        var replacement = new List<InlineContent>();
        foreach (var inline in inlines)
        {
            var text = GetInlineText(inline);
            var inlineStart = current;
            var inlineEnd = current + text.Length;
            current = inlineEnd;

            if (inlineEnd <= startOffset || inlineStart >= endOffset || inline is not TextRun)
            {
                replacement.Add(CloneInline(inline));
                continue;
            }

            var rangeStart = Math.Max(startOffset, inlineStart) - inlineStart;
            var rangeEnd = Math.Min(endOffset, inlineEnd) - inlineStart;
            if (rangeStart > 0)
            {
                replacement.Add(SplitInline(inline, 0, rangeStart));
            }

            var marked = SplitInline(inline, rangeStart, rangeEnd);
            marked.Marks.RemoveAll(mark => mark.Type == InlineMarkType.Revision);
            marked.Marks.Add(CreateRevisionMark(revision));
            replacement.Add(marked);

            if (rangeEnd < text.Length)
            {
                replacement.Add(SplitInline(inline, rangeEnd, text.Length));
            }
        }

        inlines.Clear();
        inlines.AddRange(MergeAdjacentTextRuns(replacement));
        EnsureEditableInlinesHaveText(inlines);
    }

    private static string GetRevisionPayload(string? current, string appended)
        => string.IsNullOrEmpty(current) ? appended : current + appended;

    private static (DocumentBlock? Block, List<InlineContent>? Inlines, InlineContent? Inline, int InlineIndex) ResolveEditableInlineContext(
        DocumentEditorDocument document,
        WysiwygSelectionSnapshot? selection)
    {
        var block = document.Blocks.FirstOrDefault(candidate => candidate.Id == selection?.AnchorBlockId);
        var inlines = GetEditableInlines(block?.Content);
        if (inlines is null || inlines.Count == 0)
        {
            return (block, inlines, null, -1);
        }

        var inlineIndex = string.IsNullOrWhiteSpace(selection?.AnchorInlineId)
            ? -1
            : inlines.FindIndex(inline => inline.Id == selection.AnchorInlineId);
        if (inlineIndex < 0)
        {
            inlineIndex = 0;
        }

        return (block, inlines, inlines[inlineIndex], inlineIndex);
    }

    private static string? GetPendingRevisionMark(InlineContent inline, DocumentRevisionType type)
    {
        return inline.Marks.FirstOrDefault(mark =>
            mark.Type == InlineMarkType.Revision
            && string.Equals(mark.Value, type.ToString(), StringComparison.Ordinal))?.RevisionId;
    }

    private static bool TryFindRevisionTextRun(
        DocumentEditorDocument document,
        string revisionId,
        DocumentRevisionType type,
        out (TextRun TextRun, int InlineIndex) result)
    {
        foreach (var inlines in EnumerateEditableInlineLists(document))
        {
            for (var index = 0; index < inlines.Count; index++)
            {
                if (inlines[index] is not TextRun textRun)
                {
                    continue;
                }

                var candidateRevisionId = GetPendingRevisionMark(textRun, type);
                if (string.Equals(candidateRevisionId, revisionId, StringComparison.Ordinal))
                {
                    result = (textRun, index);
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private static void AddTextSlice(List<InlineContent> target, TextRun source, int start, int end)
    {
        if (end <= start)
        {
            return;
        }

        target.Add(new TextRun
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = source.Text[start..end],
            Marks = CopyMarks(source.Marks)
        });
    }

    private static List<InlineMark> CopyMarks(IEnumerable<InlineMark> marks)
        => marks.Select(mark => new InlineMark
        {
            Type = mark.Type,
            Link = mark.Link is null ? null : new LinkMarkData { Href = mark.Link.Href, Title = mark.Link.Title },
            CommentAnchor = mark.CommentAnchor is null ? null : new CommentAnchorMarkData
            {
                CommentId = mark.CommentAnchor.CommentId,
                AnchorId = mark.CommentAnchor.AnchorId
            },
            RevisionId = mark.RevisionId,
            Value = mark.Value
        }).ToList();

    private static InlineContent CloneInline(InlineContent inline)
    {
        return inline switch
        {
            TextRun textRun => new TextRun
            {
                Id = textRun.Id,
                Text = textRun.Text,
                Marks = CopyMarks(textRun.Marks)
            },
            TokenRun token => new TokenRun
            {
                Id = token.Id,
                Key = token.Key,
                DisplayName = token.DisplayName,
                TokenType = token.TokenType,
                TypeLabel = token.TypeLabel,
                ColorClass = token.ColorClass,
                Description = token.Description,
                FallbackText = token.FallbackText,
                Marks = CopyMarks(token.Marks)
            },
            DocumentFieldRun field => new DocumentFieldRun
            {
                Id = field.Id,
                FieldType = field.FieldType,
                Format = field.Format,
                FallbackText = field.FallbackText,
                DisplayText = field.DisplayText,
                Marks = CopyMarks(field.Marks)
            },
            DocumentNoteReferenceRun note => new DocumentNoteReferenceRun
            {
                Id = note.Id,
                NoteId = note.NoteId,
                NoteType = note.NoteType,
                DisplayMarker = note.DisplayMarker,
                Marks = CopyMarks(note.Marks)
            },
            _ => new TextRun { Id = Guid.NewGuid().ToString("N"), Text = GetInlineText(inline) }
        };
    }

    private static InlineContent SplitInline(InlineContent inline, int start, int end)
    {
        var text = GetInlineText(inline);
        var length = Math.Min(end, text.Length) - start;
        length = Math.Max(length, 0);
        var slice = length > 0 ? text.Substring(start, length) : string.Empty;
        var cloned = CloneInline(inline);
        switch (cloned)
        {
            case TextRun textRun:
                textRun.Text = slice;
                break;
            case TokenRun token:
                token.DisplayName = slice;
                break;
            case DocumentFieldRun field:
                field.DisplayText = slice;
                break;
            case DocumentNoteReferenceRun note:
                note.DisplayMarker = slice;
                break;
        }

        return cloned;
    }

    private static void ApplyFormattingRevisionDecision(
        DocumentEditorDocument document,
        DocumentRevision revision,
        DocumentRevisionAction action)
    {
        var payload = string.IsNullOrWhiteSpace(revision.PayloadJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<DocumentFormattingRevisionPayload>(
                revision.PayloadJson,
                DocumentEditorJson.Options);
        var markType = payload?.MarkType ?? InlineMarkType.Bold;

        foreach (var inlines in EnumerateEditableInlineLists(document))
        {
            foreach (var inline in inlines.Where(inline => HasRevisionMark(inline, revision.Id)))
            {
                inline.Marks.RemoveAll(mark => mark.Type == InlineMarkType.Revision && mark.RevisionId == revision.Id);
                if (action == DocumentRevisionAction.Rejected)
                {
                    if (payload?.NewActive == true)
                    {
                        inline.Marks.RemoveAll(mark => mark.Type == markType);
                    }
                    else if (!inline.Marks.Any(mark => mark.Type == markType))
                    {
                        inline.Marks.Add(new InlineMark { Type = markType });
                    }
                }
            }
        }
    }

    private static void RemoveRevisionContent(DocumentEditorDocument document, string revisionId)
    {
        foreach (var inlines in EnumerateEditableInlineLists(document))
        {
            inlines.RemoveAll(inline => HasRevisionMark(inline, revisionId));
            EnsureEditableInlinesHaveText(inlines);
        }
    }

    private static void RemoveRevisionMarks(DocumentEditorDocument document, string revisionId)
    {
        foreach (var inlines in EnumerateEditableInlineLists(document))
        {
            foreach (var inline in inlines)
            {
                inline.Marks.RemoveAll(mark =>
                    mark.Type == InlineMarkType.Revision
                    && string.Equals(mark.RevisionId, revisionId, StringComparison.Ordinal));
            }
        }
    }

    private static IEnumerable<List<InlineContent>> EnumerateEditableInlineLists(DocumentEditorDocument document)
    {
        foreach (var block in document.Blocks)
        {
            var inlines = GetEditableInlines(block.Content);
            if (inlines is not null)
            {
                yield return inlines;
            }

            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            foreach (var cell in table.Rows.SelectMany(row => row.Cells))
            {
                foreach (var nestedBlock in cell.Blocks)
                {
                    var nestedInlines = GetEditableInlines(nestedBlock.Content);
                    if (nestedInlines is not null)
                    {
                        yield return nestedInlines;
                    }
                }
            }
        }
    }

    private static bool HasRevisionMark(InlineContent inline, string revisionId)
        => inline.Marks.Any(mark =>
            mark.Type == InlineMarkType.Revision
            && string.Equals(mark.RevisionId, revisionId, StringComparison.Ordinal));

    private static void EnsureEditableInlinesHaveText(List<InlineContent> inlines)
    {
        if (inlines.Count == 0)
        {
            inlines.Add(new TextRun { Id = Guid.NewGuid().ToString("N"), Text = string.Empty });
        }
    }

    private static List<InlineContent> MergeAdjacentTextRuns(List<InlineContent> inlines)
    {
        var merged = new List<InlineContent>();
        foreach (var inline in inlines)
        {
            if (inline is TextRun text && text.Text.Length == 0)
            {
                continue;
            }

            if (merged.LastOrDefault() is TextRun previousText
                && inline is TextRun currentText
                && MarksEqual(previousText.Marks, currentText.Marks))
            {
                previousText.Text += currentText.Text;
                continue;
            }

            merged.Add(inline);
        }

        return merged;
    }

    private static bool MarksEqual(IReadOnlyList<InlineMark> left, IReadOnlyList<InlineMark> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return left.Zip(right).All(pair =>
            pair.First.Type == pair.Second.Type
            && pair.First.Value == pair.Second.Value
            && pair.First.RevisionId == pair.Second.RevisionId
            && pair.First.Link?.Href == pair.Second.Link?.Href
            && pair.First.Link?.Title == pair.Second.Link?.Title
            && pair.First.CommentAnchor?.CommentId == pair.Second.CommentAnchor?.CommentId);
    }

    private void ConfigureAutoSaveTimer()
    {
        if (_configuredAutoSaveInterval == AutoSaveInterval)
        {
            return;
        }

        _configuredAutoSaveInterval = AutoSaveInterval;
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
        ScheduleAutoSave();
    }

    private void ScheduleAutoSave()
    {
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;

        if (_disposed
            || !_isDirty
            || _isSaving
            || AutoSaveInterval is not { } interval
            || interval <= TimeSpan.Zero)
        {
            return;
        }

        _autoSaveTimer = new Timer(
            _ => _ = InvokeAsync(RunDebouncedAutoSaveAsync),
            null,
            interval,
            Timeout.InfiniteTimeSpan);
    }

    private async Task RunDebouncedAutoSaveAsync()
    {
        if (_disposed || !_isDirty || _isSaving)
        {
            return;
        }

        _autosave.DebounceElapsed();
        SyncAutosavePendingAction();
        await SaveAsync(DocumentEditorSaveTrigger.AutoSave);
    }

    private void SyncAutosavePendingAction()
    {
        var state = _autosave.State;
        if (state.Status == DocumentAutosaveStatus.Waiting && _isDirty && AutoSaveInterval is not null && AutoSaveInterval > TimeSpan.Zero)
        {
            _pendingActions.Add(PendingActionId.AutosaveWaiting, Loc["TmDocumentEditor_AutosaveWaiting"]);
            return;
        }

        _pendingActions.Remove(PendingActionId.AutosaveWaiting);
    }

    private async Task<bool> TryCreateSuggestionAsync(DocumentEditorDocument? before, DocumentEditorDocument after)
    {
        if (!_suggestionsEnabled || !CanCreateSuggestions || before is null || _document is null)
        {
            return false;
        }

        var suggestion = CreateSuggestionFromDiff(before, after);
        if (suggestion is null)
        {
            _document = Clone(before);
            _currentDocument = _document;
            _suggestionSnapshot = Clone(before);
            return true;
        }

        if (!CanCreateSuggestionWithinRestrictedRegions(before, suggestion))
        {
            _suggestionMessage = Loc["TmDocumentEditor_SuggestionConflict"];
            _document = Clone(before);
            _currentDocument = _document;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _suggestionSnapshot = Clone(before);
            return true;
        }

        try
        {
            var created = await SuggestionProvider.CreateSuggestionAsync(suggestion);
            _suggestions = _suggestions
                .Where(item => item.Id != created.Id)
                .Append(created)
                .OrderByDescending(item => item.CreatedAt)
                .ToList();
            _suggestionMessage = Loc["TmDocumentEditor_SuggestionCreated"];
        }
        catch
        {
            _suggestionMessage = Loc["TmDocumentEditor_SuggestionCreateFailed"];
        }
        finally
        {
            _document = Clone(before);
            _currentDocument = _document;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _suggestionSnapshot = Clone(before);
        }

        return true;
    }

    private DocumentSuggestion? CreateSuggestionFromDiff(DocumentEditorDocument before, DocumentEditorDocument after)
    {
        foreach (var afterBlock in after.Blocks.OrderBy(block => block.Order))
        {
            var beforeBlock = before.Blocks.FirstOrDefault(block => block.Id == afterBlock.Id);
            if (beforeBlock is null)
            {
                return new DocumentSuggestion
                {
                    DocumentId = after.DocumentId,
                    Type = DocumentSuggestionType.InsertText,
                    Range = new DocumentRevisionRange { BlockId = afterBlock.Id },
                    SuggestedText = GetBlockText(afterBlock),
                    Author = Author ?? new DocumentEditorAuthor(),
                    BaseSnapshotHash = ComputeSnapshotHash(before),
                    Operations =
                    [
                        new DocumentOperation
                        {
                            Type = DocumentOperationType.InsertBlock,
                            Target = new DocumentOperationTarget { BlockId = afterBlock.Id, Order = afterBlock.Order },
                            Block = Clone(afterBlock),
                            Metadata = CreateSuggestionOperationMetadata()
                        }
                    ]
                };
            }

            var originalText = GetBlockText(beforeBlock);
            var suggestedText = GetBlockText(afterBlock);
            if (!string.Equals(originalText, suggestedText, StringComparison.Ordinal))
            {
                return new DocumentSuggestion
                {
                    DocumentId = after.DocumentId,
                    Type = suggestedText.Length == 0
                        ? DocumentSuggestionType.DeleteText
                        : originalText.Length == 0
                            ? DocumentSuggestionType.InsertText
                            : DocumentSuggestionType.ReplaceText,
                    Range = new DocumentRevisionRange
                    {
                        BlockId = afterBlock.Id,
                        StartInlineIndex = 0,
                        StartOffset = 0,
                        EndInlineIndex = 0,
                        EndOffset = originalText.Length
                    },
                    OriginalText = originalText,
                    SuggestedText = suggestedText,
                    Author = Author ?? new DocumentEditorAuthor(),
                    BaseSnapshotHash = ComputeSnapshotHash(before),
                    Operations =
                    [
                        new DocumentOperation
                        {
                            Type = DocumentOperationType.SetBlockAttribute,
                            Target = new DocumentOperationTarget { BlockId = afterBlock.Id },
                            AttributeName = "text",
                            AttributeValueJson = System.Text.Json.JsonSerializer.Serialize(suggestedText, DocumentEditorJson.Options),
                            Metadata = CreateSuggestionOperationMetadata()
                        }
                    ]
                };
            }

            var markOperation = CreateFormattingOperation(beforeBlock, afterBlock);
            if (markOperation is not null)
            {
                return new DocumentSuggestion
                {
                    DocumentId = after.DocumentId,
                    Type = DocumentSuggestionType.Formatting,
                    Range = new DocumentRevisionRange { BlockId = afterBlock.Id, StartInlineIndex = markOperation.Target.InlineIndex },
                    OriginalText = originalText,
                    SuggestedText = suggestedText,
                    Author = Author ?? new DocumentEditorAuthor(),
                    BaseSnapshotHash = ComputeSnapshotHash(before),
                    Operations = [markOperation]
                };
            }
        }

        var removedBlock = before.Blocks.FirstOrDefault(block => after.Blocks.All(item => item.Id != block.Id));
        if (removedBlock is not null)
        {
            return new DocumentSuggestion
            {
                DocumentId = after.DocumentId,
                Type = DocumentSuggestionType.DeleteText,
                Range = new DocumentRevisionRange { BlockId = removedBlock.Id },
                OriginalText = GetBlockText(removedBlock),
                Author = Author ?? new DocumentEditorAuthor(),
                BaseSnapshotHash = ComputeSnapshotHash(before),
                Operations =
                [
                    new DocumentOperation
                    {
                        Type = DocumentOperationType.DeleteBlock,
                        Target = new DocumentOperationTarget { BlockId = removedBlock.Id },
                        Metadata = CreateSuggestionOperationMetadata()
                    }
                ]
            };
        }

        return null;
    }

    private static bool CanCreateSuggestionWithinRestrictedRegions(DocumentEditorDocument document, DocumentSuggestion suggestion)
    {
        if (!document.IsProtected)
        {
            return true;
        }

        if (document.RestrictedMarkers.Count == 0)
        {
            return false;
        }

        var ranges = GetSuggestionBoundaryRanges(document, suggestion).ToList();
        return ranges.Count > 0 && ranges.All(range =>
            document.RestrictedMarkers.Any(marker => ContainsRestrictedRange(marker, range)));
    }

    private static IEnumerable<(string BlockId, int StartOffset, int EndOffset)> GetSuggestionBoundaryRanges(
        DocumentEditorDocument document,
        DocumentSuggestion suggestion)
    {
        var rangeBlockId = suggestion.Range.BlockId;
        if (!string.IsNullOrWhiteSpace(rangeBlockId))
        {
            if (suggestion.OriginalText is not null && suggestion.SuggestedText is not null)
            {
                yield return CreateTextDiffBoundary(rangeBlockId, suggestion.OriginalText, suggestion.SuggestedText);
            }
            else
            {
                var start = Math.Max(0, suggestion.Range.StartOffset.GetValueOrDefault());
                var end = suggestion.Range.EndOffset ?? Math.Max(start, GetDocumentBlockTextLength(document, rangeBlockId));
                yield return (rangeBlockId, start, Math.Max(start, end));
            }
        }

        foreach (var operation in suggestion.Operations)
        {
            var blockId = operation.Target.BlockId;
            if (string.IsNullOrWhiteSpace(blockId))
            {
                continue;
            }

            var start = Math.Max(0, operation.Target.Offset.GetValueOrDefault(suggestion.Range.StartOffset.GetValueOrDefault()));
            var length = operation.Target.Length ?? Math.Max(0, suggestion.Range.EndOffset.GetValueOrDefault(start) - start);
            var end = length > 0 ? start + length : Math.Max(start, suggestion.Range.EndOffset.GetValueOrDefault(start));
            if (operation.Type is DocumentOperationType.SetBlockAttribute && suggestion.OriginalText is not null && suggestion.SuggestedText is not null)
            {
                yield return CreateTextDiffBoundary(blockId, suggestion.OriginalText, suggestion.SuggestedText);
                continue;
            }

            yield return (blockId, start, end);
        }
    }

    private static (string BlockId, int StartOffset, int EndOffset) CreateTextDiffBoundary(
        string blockId,
        string originalText,
        string suggestedText)
    {
        var prefix = 0;
        var maxPrefix = Math.Min(originalText.Length, suggestedText.Length);
        while (prefix < maxPrefix && originalText[prefix] == suggestedText[prefix])
        {
            prefix++;
        }

        var originalSuffix = originalText.Length;
        var suggestedSuffix = suggestedText.Length;
        while (originalSuffix > prefix
            && suggestedSuffix > prefix
            && originalText[originalSuffix - 1] == suggestedText[suggestedSuffix - 1])
        {
            originalSuffix--;
            suggestedSuffix--;
        }

        return (blockId, prefix, Math.Max(prefix, originalSuffix));
    }

    private static bool ContainsRestrictedRange(
        DocumentRestrictedMarker marker,
        (string BlockId, int StartOffset, int EndOffset) range)
    {
        if (!string.Equals(marker.StartBlockId, marker.EndBlockId, StringComparison.Ordinal)
            || !string.Equals(marker.StartBlockId, range.BlockId, StringComparison.Ordinal))
        {
            return false;
        }

        return range.StartOffset >= marker.StartOffset && range.EndOffset <= marker.EndOffset;
    }

    private static int GetDocumentBlockTextLength(DocumentEditorDocument document, string blockId)
        => document.Blocks.FirstOrDefault(block => block.Id == blockId) is { } block
            ? GetBlockText(block).Length
            : 0;

    private DocumentOperation? CreateFormattingOperation(DocumentBlock beforeBlock, DocumentBlock afterBlock)
    {
        var beforeInlines = GetEditableInlines(beforeBlock.Content);
        var afterInlines = GetEditableInlines(afterBlock.Content);
        if (beforeInlines is null || afterInlines is null)
        {
            return null;
        }

        var count = Math.Min(beforeInlines.Count, afterInlines.Count);
        for (var index = 0; index < count; index++)
        {
            var addedMark = afterInlines[index].Marks.FirstOrDefault(afterMark =>
                beforeInlines[index].Marks.All(beforeMark => !SameMark(beforeMark, afterMark)));
            if (addedMark is not null)
            {
                return new DocumentOperation
                {
                    Type = DocumentOperationType.AddMark,
                    Target = new DocumentOperationTarget { BlockId = afterBlock.Id, InlineIndex = index },
                    Mark = Clone(addedMark),
                    Metadata = CreateSuggestionOperationMetadata()
                };
            }

            var removedMark = beforeInlines[index].Marks.FirstOrDefault(beforeMark =>
                afterInlines[index].Marks.All(afterMark => !SameMark(beforeMark, afterMark)));
            if (removedMark is not null)
            {
                return new DocumentOperation
                {
                    Type = DocumentOperationType.RemoveMark,
                    Target = new DocumentOperationTarget { BlockId = afterBlock.Id, InlineIndex = index },
                    Mark = Clone(removedMark),
                    Metadata = CreateSuggestionOperationMetadata()
                };
            }
        }

        return null;
    }

    private DocumentOperationMetadata CreateSuggestionOperationMetadata()
        => new()
        {
            AuthorId = Author?.Id ?? string.Empty,
            ClientId = CollaborationClientId,
            LogicalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static bool SameMark(InlineMark left, InlineMark right)
        => left.Type == right.Type
            && left.Value == right.Value
            && left.Link?.Href == right.Link?.Href
            && left.Link?.Title == right.Link?.Title
            && left.CommentAnchor?.CommentId == right.CommentAnchor?.CommentId;

    private static string ComputeSnapshotHash(DocumentEditorDocument document)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(document, DocumentEditorJson.Options);
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task EnsureCollaborationStartedAsync()
    {
        if (_document is null || CollaborationProvider is null || IsVersionPreview)
        {
            await StopCollaborationAsync();
            return;
        }

        var clientId = string.IsNullOrWhiteSpace(CollaborationClientId)
            ? _generatedCollaborationClientId
            : CollaborationClientId!;

        if (_collaborationSync is not null
            && ReferenceEquals(_loadedCollaborationProvider, CollaborationProvider)
            && string.Equals(_collaborationSync.Session?.DocumentId, _document.DocumentId, StringComparison.Ordinal)
            && string.Equals(_activeCollaborationClientId, clientId, StringComparison.Ordinal))
        {
            ConfigureCollaborationTimer();
            return;
        }

        await StopCollaborationAsync();

        try
        {
            _collaborationSync = new DocumentCollaborationSync(CollaborationProvider);
            await _collaborationSync.JoinAsync(
                _document,
                clientId,
                Author ?? new DocumentEditorAuthor { Id = clientId, DisplayName = clientId });
            _loadedCollaborationProvider = CollaborationProvider;
            _activeCollaborationClientId = clientId;
            _collaborationSnapshot = Clone(_document);
            _remoteCursors = [];
            SubscribeRealtimeCollaborationProvider();
            ConfigureCollaborationTimer();
        }
        catch
        {
            _collaborationSync = null;
            _loadedCollaborationProvider = null;
            _activeCollaborationClientId = null;
            _collaborationSnapshot = _document is null ? null : Clone(_document);
            _remoteCursors = [];
            _saveMessage = Loc["TmDocumentEditor_CollaborationUnavailable"];
            _collaborationTimer?.Dispose();
            _collaborationTimer = null;
            _configuredCollaborationInterval = null;
        }
    }

    private async Task StopCollaborationAsync()
    {
        _collaborationTimer?.Dispose();
        _collaborationTimer = null;
        _configuredCollaborationInterval = null;
        _remoteCursors = [];
        _collaborationSnapshot = null;
        _loadedCollaborationProvider = null;
        _activeCollaborationClientId = null;
        UnsubscribeRealtimeCollaborationProvider();

        if (_collaborationSync is not null)
        {
            try
            {
                await _collaborationSync.LeaveAsync();
            }
            catch
            {
                // Collaboration is best-effort and must not block editor disposal or document reload.
            }
            finally
            {
                _collaborationSync = null;
            }
        }
    }

    private void ConfigureCollaborationTimer()
    {
        if (_collaborationSync is null || CollaborationProvider is null)
        {
            _collaborationTimer?.Dispose();
            _collaborationTimer = null;
            _configuredCollaborationInterval = null;
            return;
        }

        if (CollaborationProvider is IDocumentCollaborationRealtimeProvider)
        {
            _collaborationTimer?.Dispose();
            _collaborationTimer = null;
            _configuredCollaborationInterval = null;
            return;
        }

        if (_configuredCollaborationInterval == CollaborationSyncInterval && _collaborationTimer is not null)
        {
            return;
        }

        _configuredCollaborationInterval = CollaborationSyncInterval;
        _collaborationTimer?.Dispose();
        _collaborationTimer = null;

        if (CollaborationSyncInterval <= TimeSpan.Zero)
        {
            return;
        }

        _collaborationTimer = new Timer(
            _ => _ = InvokeAsync(RefreshCollaborationAsync),
            null,
            CollaborationSyncInterval,
            CollaborationSyncInterval);
    }

    private async Task BroadcastLocalCollaborationChangeAsync(
        DocumentEditorDocument before,
        DocumentEditorDocument after,
        WysiwygPatch? patch = null)
    {
        if (!_suppressCollaborationBroadcast
            && _collaborationSync is null
            && CollaborationProvider is not null
            && _document is not null
            && !IsVersionPreview)
        {
            await EnsureCollaborationStartedAsync();
        }

        if (_suppressCollaborationBroadcast || _collaborationSync is null || !CanEditDocument)
        {
            _collaborationSnapshot = Clone(after);
            return;
        }

        var batch = patch is null
            ? _collaborationSync.CreateLocalEditBatch(before, after)
            : _collaborationSync.CreateLocalPatchBatch(before, patch);
        if (batch.Operations.Count == 0 && patch is not null)
        {
            batch = _collaborationSync.CreateLocalEditBatch(before, after);
        }

        if (batch.Operations.Count == 0)
        {
            _collaborationSnapshot = Clone(after);
            return;
        }

        try
        {
            _pendingActions.Add(PendingActionId.CollaborationSync, Loc["TmDocumentEditor_CollaborationSyncing"]);
            var result = await _collaborationSync.SubmitLocalBatchAsync(batch);
            if (result.IsValid)
            {
                _collaborationSnapshot = Clone(after);
            }
        }
        catch
        {
            // Collaboration transport failures should not prevent local editing.
        }
        finally
        {
            _pendingActions.Remove(PendingActionId.CollaborationSync);
        }
    }

    private void SubscribeRealtimeCollaborationProvider()
    {
        if (CollaborationProvider is not IDocumentCollaborationRealtimeProvider realtime)
        {
            return;
        }

        _realtimeCollaborationProvider = realtime;
        realtime.RemoteOperationBatchReceived += HandleRealtimeRemoteOperationBatchAsync;
        realtime.RemoteCursorReceived += HandleRealtimeRemoteCursorAsync;
    }

    private void UnsubscribeRealtimeCollaborationProvider()
    {
        if (_realtimeCollaborationProvider is null)
        {
            return;
        }

        _realtimeCollaborationProvider.RemoteOperationBatchReceived -= HandleRealtimeRemoteOperationBatchAsync;
        _realtimeCollaborationProvider.RemoteCursorReceived -= HandleRealtimeRemoteCursorAsync;
        _realtimeCollaborationProvider = null;
    }

    private async Task HandleRealtimeRemoteOperationBatchAsync(
        DocumentCollaborationOperationBatch batch,
        CancellationToken cancellationToken)
    {
        if (_collaborationSync is null || _document is null || _disposed)
        {
            return;
        }

        await InvokeAsync(async () =>
        {
            var result = _collaborationSync.ApplyRemoteBatch(batch);
            if (!result.IsValid || DocumentsEqual(_collaborationSnapshot, _collaborationSync.Document))
            {
                return;
            }

            _suppressCollaborationBroadcast = true;
            try
            {
                var updated = Clone(_collaborationSync.Document);
                new DocumentEditorPostFixer().Fix(updated);
                var remoteWysiwygOperations = batch.Batch.Operations
                    .Where(CanApplyRemoteOperationInWysiwyg)
                    .ToList();
                _document = updated;
                _currentDocument = updated;
                _templatePreviewDocument = null;
                _templatePreviewEnabled = false;
                _templatePreviewMessage = null;
                _collaborationSnapshot = Clone(updated);
                _suggestionSnapshot = Clone(updated);
                if (updated.Revisions.Any(revision => revision.Action == DocumentRevisionAction.Pending))
                {
                    OpenSidePanel(DocumentSidePanelTab.Revisions);
                }

                await RefreshSuggestionsAsync();
                if (remoteWysiwygOperations.Count > 0 && _wysiwygHost is not null)
                {
                    var applyResult = await _wysiwygHost.ApplyRemoteOperationBatchAsync(remoteWysiwygOperations, updated);
                    if (!applyResult.Success)
                    {
                        _saveMessage = BuildRemoteApplyRecoveryMessage(applyResult);
                        await _wysiwygHost.RefreshSnapshotAsync(updated);
                    }
                }

                if (UsingCanvasEngine && _canvasHost is not null)
                {
                    var applyResult = await _canvasHost.ApplyRemoteOperationBatchAsync(batch);
                    if (!applyResult.Success)
                    {
                        await _canvasHost.ReplaceDocumentAsync(updated);
                    }
                }

                StateHasChanged();
            }
            finally
            {
                _suppressCollaborationBroadcast = false;
            }
        });
    }

    private Task HandleRealtimeRemoteCursorAsync(DocumentCollaborationCursor cursor, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        return InvokeAsync(async () =>
        {
            var sessionId = _collaborationSync?.Session?.Id;
            if (!string.IsNullOrWhiteSpace(sessionId)
                && string.Equals(cursor.SessionId, sessionId, StringComparison.Ordinal))
            {
                return;
            }

            var renderedByJs = _wysiwygHost is not null
                && await _wysiwygHost.ApplyRemoteCursorAsync(cursor);
            if (!renderedByJs && UsingCanvasEngine && _canvasHost is not null)
            {
                var presence = await _canvasHost.ApplyRemoteCursorAsync(cursor);
                renderedByJs = presence.CursorCount > 0;
            }

            if (renderedByJs)
            {
                return;
            }

            var cursors = _remoteCursors.ToList();
            var index = cursors.FindIndex(item => string.Equals(item.SessionId, cursor.SessionId, StringComparison.Ordinal));
            var shouldRemove = cursor.Offset is < 0 || string.IsNullOrWhiteSpace(cursor.DisplayName);
            if (shouldRemove)
            {
                if (index >= 0)
                {
                    cursors.RemoveAt(index);
                }
            }
            else if (index >= 0)
            {
                cursors[index] = cursor;
            }
            else
            {
                cursors.Add(cursor);
            }

            _remoteCursors = cursors;
            StateHasChanged();
        });
    }

    private async Task RefreshCollaborationAsync()
    {
        if (_collaborationSync is null || CollaborationProvider is null || _isRefreshingCollaboration || _document is null)
        {
            return;
        }

        _isRefreshingCollaboration = true;
        var shouldRender = false;
        try
        {
            var result = await _collaborationSync.ReconnectAsync();
            if (result.IsValid && !DocumentsEqual(_collaborationSnapshot, _collaborationSync.Document))
            {
                _suppressCollaborationBroadcast = true;
                try
                {
                    var updated = Clone(_collaborationSync.Document);
                    new DocumentEditorPostFixer().Fix(updated);
                    var remoteWysiwygOperations = _collaborationSync.LastAppliedRemoteOperations
                        .Where(CanApplyRemoteOperationInWysiwyg)
                        .ToList();
                    _document = updated;
                    _currentDocument = updated;
                    _templatePreviewDocument = null;
                    _templatePreviewEnabled = false;
                    _templatePreviewMessage = null;
                    _collaborationSnapshot = Clone(updated);
                    _suggestionSnapshot = Clone(updated);
                    if (updated.Revisions.Any(revision => revision.Action == DocumentRevisionAction.Pending))
                    {
                        OpenSidePanel(DocumentSidePanelTab.Revisions);
                    }

                    await RefreshSuggestionsAsync();
                    shouldRender = true;
                    if (remoteWysiwygOperations.Count > 0 && _wysiwygHost is not null)
                    {
                        var applyResult = await _wysiwygHost.ApplyRemoteOperationBatchAsync(remoteWysiwygOperations, updated);
                        if (!applyResult.Success)
                        {
                            _saveMessage = BuildRemoteApplyRecoveryMessage(applyResult);
                            await _wysiwygHost.RefreshSnapshotAsync(updated);
                            shouldRender = true;
                        }
                    }

                    if (UsingCanvasEngine && _canvasHost is not null && _collaborationSync.LastAppliedRemoteOperations.Count > 0)
                    {
                        await _canvasHost.ReplaceDocumentAsync(updated);
                        shouldRender = true;
                    }
                }
                finally
                {
                    _suppressCollaborationBroadcast = false;
                }
            }

            var sessionId = _collaborationSync.Session?.Id;
            var remoteCursors = (await CollaborationProvider.GetCursorsAsync(_collaborationSync.Document.DocumentId))
                .Where(cursor => !string.Equals(cursor.SessionId, sessionId, StringComparison.Ordinal))
                .ToList();
            if (!CursorsEqual(_remoteCursors, remoteCursors))
            {
                _remoteCursors = remoteCursors;
                if (UsingCanvasEngine && _canvasHost is not null)
                {
                    await _canvasHost.ApplyRemoteCursorsAsync(_remoteCursors);
                }

                shouldRender = true;
            }
        }
        catch
        {
            // Keep the editor usable while the collaboration transport is unavailable.
            var unavailable = Loc["TmDocumentEditor_CollaborationUnavailable"];
            if (_lastSavedAt is null && !string.Equals(_saveMessage, unavailable, StringComparison.Ordinal))
            {
                _saveMessage = unavailable;
                shouldRender = true;
            }
        }
        finally
        {
            _isRefreshingCollaboration = false;
        }

        if (shouldRender && !_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private static bool CursorsEqual(
        IReadOnlyList<DocumentCollaborationCursor> left,
        IReadOnlyList<DocumentCollaborationCursor> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var orderedLeft = left.OrderBy(cursor => cursor.SessionId, StringComparer.Ordinal).ToList();
        var orderedRight = right.OrderBy(cursor => cursor.SessionId, StringComparer.Ordinal).ToList();
        for (var i = 0; i < orderedLeft.Count; i++)
        {
            if (!CursorEqual(orderedLeft[i], orderedRight[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CursorEqual(DocumentCollaborationCursor left, DocumentCollaborationCursor right)
        => string.Equals(left.SessionId, right.SessionId, StringComparison.Ordinal)
            && string.Equals(left.ClientId, right.ClientId, StringComparison.Ordinal)
            && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
            && string.Equals(left.BlockId, right.BlockId, StringComparison.Ordinal)
            && left.InlineIndex == right.InlineIndex
            && left.Offset == right.Offset
            && string.Equals(left.Color, right.Color, StringComparison.Ordinal)
            && left.UpdatedAt == right.UpdatedAt;

    private static bool CanApplyRemoteOperationInWysiwyg(DocumentOperation operation)
    {
        if (operation.Type is DocumentOperationType.InsertText or DocumentOperationType.DeleteText)
        {
            return operation.Target.Offset is not null
                && operation.Target.Length is > 0
                && !string.IsNullOrWhiteSpace(operation.Target.BlockId);
        }

        if (operation.Type is DocumentOperationType.AddInlineMark or DocumentOperationType.RemoveInlineMark)
        {
            return operation.Target.Offset is not null
                && operation.Target.Length is > 0
                && operation.Mark is not null;
        }

        if (operation.Type is DocumentOperationType.InsertBlock or DocumentOperationType.UpdateBlock)
        {
            return operation.Block is not null;
        }

        if (operation.Type is DocumentOperationType.DeleteBlock)
        {
            return !string.IsNullOrWhiteSpace(operation.Target.BlockId);
        }

        if (operation.Type is DocumentOperationType.MoveBlock)
        {
            return !string.IsNullOrWhiteSpace(operation.Target.BlockId)
                && operation.Target.Order is not null;
        }

        if (operation.Type is DocumentOperationType.MoveDrawingObject)
        {
            return operation.NewLayout is not null
                && (!string.IsNullOrWhiteSpace(operation.Target.ObjectId)
                    || !string.IsNullOrWhiteSpace(operation.Target.InlineId)
                    || (!string.IsNullOrWhiteSpace(operation.Target.BlockId) && operation.Target.InlineIndex is not null));
        }

        if (operation.Type is DocumentOperationType.SetBlockAttribute)
        {
            return string.Equals(operation.AttributeName, "headingLevel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.AttributeName, "table.cell.text", StringComparison.OrdinalIgnoreCase);
        }

        if (operation.Type is DocumentOperationType.CreateRevision)
        {
            return operation.Revision is not null
                && operation.Revision.Type is DocumentRevisionType.Insertion or DocumentRevisionType.Deletion
                && !string.IsNullOrWhiteSpace(operation.Target.BlockId);
        }

        if (operation.Type is DocumentOperationType.AcceptRevision or DocumentOperationType.RejectRevision)
        {
            return operation.Revision is not null || !string.IsNullOrWhiteSpace(operation.Metadata.RevisionId);
        }

        return false;
    }

    private string BuildRemoteApplyRecoveryMessage(WysiwygRemoteOperationBatchApplyResult applyResult)
    {
        var failed = applyResult.FailedOperationIds.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", applyResult.FailedOperationIds.Take(3))})";
        return $"{Loc["TmDocumentEditor_CollaborationRecovery"]}{failed}";
    }

    private async Task BroadcastCollaborationCursorAsync()
    {
        if (_collaborationSync is null || _document is null)
        {
            return;
        }

        try
        {
            var blockId = _selection.ActiveBlockId;
            int? inlineIndex = _selection.FocusedInlineRange?.StartInlineIndex;
            int? offset = _selection.FocusedInlineRange?.StartOffset;
            if (UsingCanvasEngine && _canvasHost is not null)
            {
                var canvasSelection = await _canvasHost.GetSelectionStateAsync();
                blockId = string.IsNullOrWhiteSpace(canvasSelection.FocusBlockId)
                    ? canvasSelection.AnchorBlockId
                    : canvasSelection.FocusBlockId;
                inlineIndex = null;
                offset = canvasSelection.FocusOffset > 0
                    ? canvasSelection.FocusOffset
                    : canvasSelection.AnchorOffset;
            }

            await _collaborationSync.UpdateCursorAsync(new DocumentCollaborationCursor
            {
                DisplayName = string.IsNullOrWhiteSpace(Author?.DisplayName)
                    ? _activeCollaborationClientId ?? _generatedCollaborationClientId
                    : Author!.DisplayName,
                BlockId = blockId,
                InlineIndex = inlineIndex,
                Offset = offset,
                Color = null
            });
            if (CollaborationProvider is not IDocumentCollaborationRealtimeProvider)
            {
                _remoteCursors = _collaborationSync.RemoteCursors;
            }
        }
        catch
        {
            // Cursor updates are transient; failures should not affect editing.
        }
    }

    private static bool DocumentsEqual(DocumentEditorDocument? left, DocumentEditorDocument? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return string.Equals(
            System.Text.Json.JsonSerializer.Serialize(left, DocumentEditorJson.Options),
            System.Text.Json.JsonSerializer.Serialize(right, DocumentEditorJson.Options),
            StringComparison.Ordinal);
    }

    private void OpenVersionDialog()
    {
        if (CanCreateVersion)
        {
            OpenSidePanel(DocumentSidePanelTab.Versions);
            _versionDialogOpen = true;
            _versionMessage = null;
            _floatingLayerStack.Push(new DocumentFloatingLayerState
            {
                LayerId = FloatingLayerId.VersionDialog,
                Kind = DocumentFloatingLayerKind.VersionDialog,
                ZIndex = 50,
                CloseAsync = () => { _versionDialogOpen = false; return Task.CompletedTask; }
            });
        }
    }

    private void CloseVersionDialog()
    {
        _versionDialogOpen = false;
        _floatingLayerStack.Remove(FloatingLayerId.VersionDialog);
    }

    private void CloseVersionPanel()
    {
        CloseSidePanel();
    }

    private async Task CreateVersionAsync(DocumentVersionDialogResult result)
    {
        if (Provider is null || _currentDocument is null || !CanCreateVersion || _isCreatingVersion)
        {
            return;
        }

        _isCreatingVersion = true;
        _versionMessage = null;

        try
        {
            if (_isDirty)
            {
                await SaveAsync(DocumentEditorSaveTrigger.Explicit);
                if (_isDirty)
                {
                    _versionMessage = Loc["TmDocumentEditor_VersionCreateSaveRequired"];
                    await RecordVersionAuditAsync(null, DocumentEditorAuditResult.Failure, _versionMessage);
                    return;
                }
            }

            _currentDocument = await GetCurrentDocumentForProviderExportAsync();
            _document = _currentDocument;

            var version = await Provider.CreateVersionAsync(new DocumentVersionCreateRequest
            {
                DocumentId = _currentDocument.DocumentId,
                Kind = result.Kind,
                Label = result.Label,
                Description = result.Description,
                Author = Author ?? new DocumentEditorAuthor()
            });

            _versionDialogOpen = false;
            _versionMessage = Loc["TmDocumentEditor_VersionCreated"];
            await RecordVersionAuditAsync(version, DocumentEditorAuditResult.Success, null);
            await OnVersionCreated.InvokeAsync(version);
            await RefreshVersionsAsync();
        }
        catch (Exception ex)
        {
            _versionMessage = Loc["TmDocumentEditor_VersionCreateFailed"];
            await RecordVersionAuditAsync(null, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isCreatingVersion = false;
            if (!_disposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task RefreshVersionsAsync()
    {
        if (Provider is null || string.IsNullOrWhiteSpace(DocumentId))
        {
            _versions = [];
            return;
        }

        _isLoadingVersions = true;
        try
        {
            _versions = await Provider.GetVersionsAsync(DocumentId);
        }
        catch
        {
            _versions = [];
            _versionMessage = Loc["TmDocumentEditor_VersionLoadFailed"];
        }
        finally
        {
            _isLoadingVersions = false;
        }
    }

    private async Task RefreshCommentsAsync()
    {
        if (Provider is null || string.IsNullOrWhiteSpace(DocumentId))
        {
            _comments = [];
            return;
        }

        _isLoadingComments = true;
        try
        {
            _comments = await Provider.GetCommentsAsync(DocumentId);
            if (_document is not null)
            {
                _document.Comments = _comments.Select(CloneForEditor).ToList();
                ApplyCommentMarksFromComments(_document);
            }
        }
        catch
        {
            _comments = [];
            _commentMessage = Loc["TmDocumentEditor_CommentLoadFailed"];
        }
        finally
        {
            _isLoadingComments = false;
        }
    }

    private async Task RefreshSuggestionsAsync()
    {
        if (SuggestionProvider is null || string.IsNullOrWhiteSpace(DocumentId))
        {
            _suggestions = [];
            _suggestionMessage = null;
            _isLoadingSuggestions = false;
            return;
        }

        _isLoadingSuggestions = true;
        try
        {
            _suggestions = await SuggestionProvider.GetSuggestionsAsync(new DocumentSuggestionQuery
            {
                DocumentId = DocumentId,
                Status = DocumentSuggestionStatus.Pending
            });
        }
        catch
        {
            _suggestions = [];
            _suggestionMessage = Loc["TmDocumentEditor_SuggestionsLoadFailed"];
        }
        finally
        {
            _isLoadingSuggestions = false;
        }
    }

    private async Task PreviewVersionAsync(DocumentVersion version)
    {
        try
        {
            _templatePreviewEnabled = false;
            _templatePreviewDocument = null;
            _templatePreviewMessage = null;
            OpenSidePanel(DocumentSidePanelTab.Versions);
            _currentDocument ??= _document;
            _previewVersion = version;
            _document = DocumentEditorJson.Deserialize(version.Snapshot.Json);
            _selection = new DocumentEditorSelectionState();
            _versionMessage = Loc["TmDocumentEditor_PreviewingVersion", GetVersionTitle(version)];
            await _commandStack.ClearAsync();
        }
        catch
        {
            _previewVersion = null;
            _document = _currentDocument;
            _versionMessage = Loc["TmDocumentEditor_VersionPreviewFailed"];
        }
    }

    private async Task ReturnToCurrentVersionAsync()
    {
        _previewVersion = null;
        _document = _currentDocument;
        _versionMessage = null;
        _selection = new DocumentEditorSelectionState();
        await _commandStack.ClearAsync();
    }

    private async Task RestoreVersionAsync(DocumentVersion version)
    {
        try
        {
            var restored = DocumentEditorJson.Deserialize(version.Snapshot.Json);
            restored.DocumentId = DocumentId;
            _currentDocument = restored;
            _document = restored;
            _previewVersion = null;
            _templatePreviewEnabled = false;
            _templatePreviewDocument = null;
            _templatePreviewMessage = null;
            _selection = new DocumentEditorSelectionState();
            _isDirty = true;
            _versionMessage = Loc["TmDocumentEditor_VersionRestored"];
            await _commandStack.ClearAsync();
            await RecordRestoreAuditAsync(version, DocumentEditorAuditResult.Success, null);
        }
        catch (Exception ex)
        {
            _versionMessage = Loc["TmDocumentEditor_VersionRestoreFailed"];
            await RecordRestoreAuditAsync(version, DocumentEditorAuditResult.Failure, ex.Message);
        }
    }

    private void UpsertComment(DocumentComment comment)
    {
        var comments = _comments.ToList();
        var index = comments.FindIndex(item => item.Id == comment.Id);
        if (index >= 0)
        {
            comments[index] = comment;
        }
        else
        {
            comments.Add(comment);
        }

        _comments = comments;

        if (_document is not null)
        {
            _document.Comments.RemoveAll(item => item.Id == comment.Id);
            _document.Comments.Add(CloneForEditor(comment));
            _document.BumpVersion();  // Phase C1
        }

        if (_currentDocument is not null && !ReferenceEquals(_currentDocument, _document))
        {
            _currentDocument.Comments.RemoveAll(item => item.Id == comment.Id);
            _currentDocument.Comments.Add(CloneForEditor(comment));
            _currentDocument.BumpVersion();  // Phase C1
        }
    }

    private void RemoveComment(string commentId)
    {
        _comments = _comments.Where(item => item.Id != commentId).ToList();
        if (_selectedCommentId == commentId)
        {
            _selectedCommentId = null;
        }

        if (_document is not null)
        {
            _document.Comments.RemoveAll(item => item.Id == commentId);
            _document.BumpVersion();  // Phase C1
            ApplyCommentMarksFromComments(_document);
        }

        if (_currentDocument is not null && !ReferenceEquals(_currentDocument, _document))
        {
            _currentDocument.Comments.RemoveAll(item => item.Id == commentId);
            ApplyCommentMarksFromComments(_currentDocument);
        }
    }

    private bool CanDeleteComment(DocumentComment? comment)
    {
        if (comment is null || string.IsNullOrWhiteSpace(Author?.Id))
        {
            return false;
        }

        var authorId = comment.Entries.OrderBy(item => item.CreatedAt).FirstOrDefault()?.Author.Id;
        return string.Equals(authorId, Author.Id, StringComparison.Ordinal);
    }

    private void ApplyCommentAnchorMark(DocumentComment comment)
    {
        if (_document is null || comment.Anchor.Type != DocumentCommentAnchorType.TextRange)
        {
            return;
        }

        if (ApplyCommentAnchorMark(_document, comment))
        {
            _currentDocument = _document;
        }
    }

    private static void ApplyCommentMarksFromComments(DocumentEditorDocument document)
    {
        foreach (var block in document.Blocks)
        {
            var inlines = GetEditableInlines(block.Content);
            if (inlines is null)
            {
                continue;
            }

            foreach (var inline in inlines)
            {
                inline.Marks.RemoveAll(mark => mark.Type == InlineMarkType.CommentAnchor);
            }
        }

        foreach (var comment in document.Comments.Where(comment => comment.Anchor.Type == DocumentCommentAnchorType.TextRange))
        {
            ApplyCommentAnchorMark(document, comment);
        }
    }

    private static bool ApplyCommentAnchorMark(DocumentEditorDocument document, DocumentComment comment)
    {
        var anchor = comment.Anchor;
        if (string.IsNullOrWhiteSpace(anchor.BlockId) || anchor.StartOffset is null || anchor.EndOffset is null)
        {
            return false;
        }

        var block = document.Blocks.FirstOrDefault(item => item.Id == anchor.BlockId);
        var inlines = GetEditableInlines(block?.Content);
        if (inlines is null)
        {
            return false;
        }

        var text = string.Concat(inlines.Select(GetInlineText));
        var (start, end) = ResolveCommentAnchorOffsets(inlines, anchor, text.Length);
        if (start >= end)
        {
            return false;
        }

        var replacement = new List<InlineContent>();
        var currentOffset = 0;
        foreach (var inline in inlines)
        {
            var inlineText = GetInlineText(inline);
            var inlineLength = inlineText.Length;
            if (inlineLength == 0)
            {
                replacement.Add(CloneInline(inline));
                continue;
            }

            var inlineStart = currentOffset;
            var inlineEnd = currentOffset + inlineLength;
            var overlapStart = Math.Max(start, inlineStart);
            var overlapEnd = Math.Min(end, inlineEnd);

            if (inline is not TextRun textRun || overlapStart >= overlapEnd)
            {
                replacement.Add(CloneInline(inline));
                currentOffset = inlineEnd;
                continue;
            }

            var localStart = overlapStart - inlineStart;
            var localEnd = overlapEnd - inlineStart;
            AddTextSlice(replacement, textRun, 0, localStart);
            AddCommentMarkedTextSlice(replacement, textRun, localStart, localEnd, comment.Id);
            AddTextSlice(replacement, textRun, localEnd, inlineLength);
            currentOffset = inlineEnd;
        }

        inlines.Clear();
        inlines.AddRange(replacement);
        return true;
    }

    private static (int Start, int End) ResolveCommentAnchorOffsets(
        IReadOnlyList<InlineContent> inlines,
        DocumentCommentAnchor anchor,
        int textLength)
    {
        var start = anchor.StartInlineIndex is int startInlineIndex
            ? GetAbsoluteInlineOffset(inlines, startInlineIndex, anchor.StartOffset ?? 0)
            : anchor.StartOffset.GetValueOrDefault();
        var end = anchor.EndInlineIndex is int endInlineIndex
            ? GetAbsoluteInlineOffset(inlines, endInlineIndex, anchor.EndOffset ?? textLength)
            : anchor.EndOffset.GetValueOrDefault(textLength);

        return (
            Math.Clamp(start, 0, textLength),
            Math.Clamp(end, 0, textLength));
    }

    private static int GetAbsoluteInlineOffset(
        IReadOnlyList<InlineContent> inlines,
        int inlineIndex,
        int inlineOffset)
    {
        var clampedIndex = Math.Clamp(inlineIndex, 0, inlines.Count);
        var total = 0;
        for (var index = 0; index < clampedIndex; index++)
        {
            total += GetInlineText(inlines[index]).Length;
        }

        return total + Math.Max(0, inlineOffset);
    }

    private static void AddCommentMarkedTextSlice(
        List<InlineContent> target,
        TextRun source,
        int start,
        int end,
        string commentId)
    {
        if (end <= start)
        {
            return;
        }

        var marks = CopyMarks(source.Marks);
        marks.RemoveAll(mark => mark.Type == InlineMarkType.CommentAnchor);
        marks.Add(new InlineMark
        {
            Type = InlineMarkType.CommentAnchor,
            CommentAnchor = new CommentAnchorMarkData { CommentId = commentId }
        });

        target.Add(new TextRun
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = source.Text[start..end],
            Marks = marks
        });
    }

    private static List<InlineContent>? GetEditableInlines(DocumentBlockContent? content)
    {
        return content switch
        {
            ParagraphBlockContent paragraph => paragraph.Inlines,
            HeadingBlockContent heading => heading.Inlines,
            ListBlockContent list => list.Inlines,
            QuoteBlockContent quote => quote.Inlines,
            _ => null
        };
    }

    private DocumentBlock CreateDefaultNoteBlock(DocumentNoteType noteType)
    {
        return new DocumentBlock
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = DocumentBlockType.Paragraph,
            Content = new ParagraphBlockContent
            {
                Inlines =
                [
                    new TextRun
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Text = noteType == DocumentNoteType.Endnote
                            ? Loc["TmDocumentEditor_EndnotePlaceholder"]
                            : Loc["TmDocumentEditor_FootnotePlaceholder"]
                    }
                ]
            }
        };
    }

    private static string CreateNoteMarker(DocumentEditorDocument document, DocumentNoteType noteType)
    {
        var startAt = Math.Max(1, document.Sections.FirstOrDefault()?.Properties.NoteNumbering.StartAt ?? 1);
        var count = document.Notes.Count(note => note.Type == noteType);
        return (startAt + count).ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveNoteInsertionOffset(WysiwygSelectionSnapshot selection, List<InlineContent> inlines)
    {
        var textLength = inlines.Sum(inline => GetInlineText(inline).Length);
        var anchor = selection.AnchorBlockOffset != 0 ? selection.AnchorBlockOffset : selection.AnchorOffset;
        var focus = selection.FocusBlockOffset != 0 ? selection.FocusBlockOffset : selection.FocusOffset;
        var offset = selection.IsCollapsed ? anchor : Math.Max(anchor, focus);
        return Math.Clamp(offset, 0, textLength);
    }

    private static WysiwygSelectionSnapshot? CreateFirstBodySelectionSnapshot(DocumentEditorDocument document)
    {
        var block = document.Blocks.FirstOrDefault(candidate => GetEditableInlines(candidate.Content) is { Count: > 0 });
        if (block is null)
        {
            return null;
        }

        var inline = GetEditableInlines(block.Content)?.FirstOrDefault();
        return new WysiwygSelectionSnapshot
        {
            Region = "Body",
            AnchorBlockId = block.Id,
            FocusBlockId = block.Id,
            AnchorInlineId = inline?.Id,
            FocusInlineId = inline?.Id,
            AnchorOffset = 0,
            FocusOffset = 0,
            AnchorBlockOffset = 0,
            FocusBlockOffset = 0,
            IsCollapsed = true
        };
    }

    private static void NormalizeBlockOrder(IList<DocumentBlock> blocks)
    {
        for (var index = 0; index < blocks.Count; index++)
        {
            blocks[index].Order = index;
        }
    }

    private static void InsertInlineAtBlockOffset(List<InlineContent> inlines, InlineContent insertedInline, int offset)
    {
        if (inlines.Count == 0)
        {
            inlines.Add(insertedInline);
            return;
        }

        var total = inlines.Sum(inline => GetInlineText(inline).Length);
        offset = Math.Clamp(offset, 0, total);
        var current = 0;
        for (var i = 0; i < inlines.Count; i++)
        {
            var inline = inlines[i];
            var length = GetInlineText(inline).Length;
            var inlineStart = current;
            var inlineEnd = current + length;
            if (offset < inlineStart || offset > inlineEnd)
            {
                current = inlineEnd;
                continue;
            }

            if (offset == inlineStart)
            {
                inlines.Insert(i, insertedInline);
                return;
            }

            if (offset == inlineEnd)
            {
                inlines.Insert(i + 1, insertedInline);
                return;
            }

            if (inline is TextRun textRun)
            {
                var localOffset = offset - inlineStart;
                var before = new TextRun
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = textRun.Text[..localOffset],
                    Marks = CopyMarks(textRun.Marks)
                };
                var after = new TextRun
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Text = textRun.Text[localOffset..],
                    Marks = CopyMarks(textRun.Marks)
                };
                inlines.RemoveAt(i);
                inlines.InsertRange(i, [before, insertedInline, after]);
                return;
            }

            inlines.Insert(i + 1, insertedInline);
            return;
        }

        inlines.Add(insertedInline);
    }

    private static string GetInlineText(InlineContent inline)
    {
        return inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentFieldRun field => ResolveFieldDisplayText(field),
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker!,
            _ => string.Empty
        };
    }

    private static string ResolveFieldDisplayText(DocumentFieldRun field)
    {
        if (!string.IsNullOrWhiteSpace(field.DisplayText))
        {
            return field.DisplayText;
        }

        if (!string.IsNullOrWhiteSpace(field.FallbackText))
        {
            return field.FallbackText;
        }

        return field.FieldType switch
        {
            DocumentFieldType.PageNumber => "1",
            DocumentFieldType.PageCount => "1",
            DocumentFieldType.PageXOfY => "1 / 1",
            DocumentFieldType.Date => DateTime.Today.ToShortDateString(),
            DocumentFieldType.DocumentTitle => "Document title",
            DocumentFieldType.Author => "Author",
            DocumentFieldType.LastSaved => DateTime.Today.ToShortDateString(),
            _ => string.Empty
        };
    }

    private static string GetBlockText(DocumentBlock block)
    {
        var inlines = GetEditableInlines(block.Content);
        return inlines is null ? string.Empty : string.Concat(inlines.Select(GetInlineText));
    }

    private sealed class DocumentEditorSelectionContext
    {
        public static DocumentEditorSelectionContext Empty { get; } = new()
        {
            Selection = new WysiwygSelectionSnapshot()
        };

        public required WysiwygSelectionSnapshot Selection { get; init; }

        public string ActiveRegion { get; init; } = "Body";

        public DocumentEditorInlineRange? ActiveTextRange { get; init; }

        public string? ActiveImageId { get; init; }

        public string? ActiveTableId { get; init; }

        public string? ActiveTableCellId { get; init; }

        public string? ActiveCommentId { get; init; }

        public string? ActiveRevisionId { get; init; }

        public WysiwygFormattingState FormattingState { get; init; } = new();

        public IReadOnlyDictionary<string, object?> ObjectProperties { get; init; } = new Dictionary<string, object?>();

        public static DocumentEditorSelectionContext FromSnapshot(
            WysiwygSelectionSnapshot snapshot,
            WysiwygFormattingState formattingState,
            IReadOnlyDictionary<string, object?> objectProperties)
        {
            var textSelection = snapshot.TextSelection;
            return new DocumentEditorSelectionContext
            {
                Selection = snapshot,
                ActiveRegion = string.IsNullOrWhiteSpace(snapshot.Region) ? "Body" : snapshot.Region,
                ActiveTextRange = textSelection is not null && !string.IsNullOrWhiteSpace(textSelection.AnchorBlockId)
                    ? new DocumentEditorInlineRange
                    {
                        BlockId = textSelection.AnchorBlockId,
                        StartOffset = textSelection.AnchorOffset,
                        EndOffset = textSelection.IsCollapsed ? textSelection.AnchorOffset : textSelection.FocusOffset
                    }
                    : string.IsNullOrWhiteSpace(snapshot.AnchorBlockId)
                    ? null
                    : new DocumentEditorInlineRange
                    {
                        BlockId = snapshot.AnchorBlockId,
                        StartOffset = snapshot.AnchorOffset,
                        EndOffset = snapshot.IsCollapsed ? snapshot.AnchorOffset : snapshot.FocusOffset
                    },
                ActiveImageId = !string.IsNullOrWhiteSpace(snapshot.ObjectSelection?.ObjectId)
                    ? snapshot.ObjectSelection.ObjectId
                    : !string.IsNullOrWhiteSpace(snapshot.ActiveObjectId)
                        ? snapshot.ActiveObjectId
                        : null,
                ActiveTableId = snapshot.ActiveTableId,
                ActiveTableCellId = snapshot.ActiveTableCellId,
                ActiveCommentId = snapshot.ActiveCommentId,
                ActiveRevisionId = snapshot.ActiveRevisionId,
                FormattingState = formattingState,
                ObjectProperties = objectProperties
            };
        }
    }

    private static T CloneForEditor<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, DocumentEditorJson.Options);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DocumentEditorJson.Options)
            ?? throw new System.Text.Json.JsonException("Could not clone document editor value.");
    }

    private Task RecordOpenAuditAsync(string? documentId, DocumentEditorAuditResult result, string? details)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.Open,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "document", Id = documentId },
            Details = details
        });
    }

    private Task RecordCommentAuditAsync(string? commentId, DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.Comment,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "comment", Id = commentId },
            Details = details
        });
    }

    private Task RecordSaveAuditAsync(DocumentEditorSaveTrigger trigger, DocumentEditorAuditResult result, string? details)
    {
        if (_document is null)
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = _document.DocumentId,
            Action = DocumentEditorAuditAction.Save,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "document", Id = _document.DocumentId },
            Details = string.IsNullOrWhiteSpace(details)
                ? trigger.ToString()
                : $"{trigger}: {details}"
        });
    }

    private Task RecordVersionAuditAsync(DocumentVersion? version, DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.CreateVersion,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "version", Id = version?.Id },
            Details = string.IsNullOrWhiteSpace(details)
                ? version?.Kind.ToString()
                : details
        });
    }

    private Task RecordRestoreAuditAsync(DocumentVersion version, DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.RestoreVersion,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "version", Id = version.Id },
            Details = string.IsNullOrWhiteSpace(details)
                ? GetVersionTitle(version)
                : details
        });
    }

    private Task RecordExportAuditAsync(DocumentPdfExportResult? exportResult, DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.Export,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "document", Id = documentId },
            Details = string.IsNullOrWhiteSpace(details)
                ? exportResult?.FileName
                : details
        });
    }

    private Task RecordFormatImportAuditAsync(DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.Import,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "document-format", Id = DocumentFormatProviderKind.Docx.ToString() },
            Details = details
        });
    }

    private Task RecordFormatExportAuditAsync(DocumentFormatExportProviderResult? exportResult, DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.Export,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "document-format", Id = DocumentFormatProviderKind.Docx.ToString() },
            Details = string.IsNullOrWhiteSpace(details)
                ? exportResult?.FileName
                : details
        });
    }

    private Task RecordCompareAuditAsync(DocumentCompareResult? compareResult, DocumentEditorAuditResult result, string? details)
    {
        var documentId = _currentDocument?.DocumentId ?? _document?.DocumentId;
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Task.CompletedTask;
        }

        return DispatchAuditAsync(new DocumentEditorAuditEvent
        {
            DocumentId = documentId,
            Action = DocumentEditorAuditAction.Compare,
            Result = result,
            Actor = Author,
            Target = new DocumentEditorAuditTarget { Type = "document-compare", Id = documentId },
            Details = string.IsNullOrWhiteSpace(details)
                ? $"{compareResult?.Summary.AddedBlocks ?? 0}/{compareResult?.Summary.RemovedBlocks ?? 0}/{compareResult?.Summary.ChangedBlocks ?? 0}"
                : details
        });
    }

    private async Task DispatchAuditAsync(DocumentEditorAuditEvent auditEvent)
    {
        var auditSink = AuditSink ?? Provider as IDocumentAuditSink;
        if (auditSink is null)
        {
            return;
        }

        try
        {
            await auditSink.RecordAsync(auditEvent);
        }
        catch when (AuditFailureMode == DocumentEditorAuditFailureMode.NonBlocking)
        {
            // Host applications may prefer the editor workflow to continue even if audit persistence is unavailable.
        }
    }

    private string GetVersionTitle(DocumentVersion version)
    {
        if (!string.IsNullOrWhiteSpace(version.Label))
        {
            return version.Label;
        }

        return version.Kind switch
        {
            DocumentVersionKind.Major => Loc["TmDocumentEditor_VersionKindMajor"],
            DocumentVersionKind.Autosave => Loc["TmDocumentEditor_VersionKindAutosave"],
            DocumentVersionKind.Restore => Loc["TmDocumentEditor_VersionKindRestore"],
            _ => Loc["TmDocumentEditor_VersionKindMinor"]
        };
    }

    private static int CountPages(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            return 0;
        }

        return Math.Max(1, 1 + document.Blocks.Count(block => block.Content is PageBreakBlockContent));
    }

    private static int CountWords(DocumentEditorDocument? document)
    {
        if (document is null)
        {
            return 0;
        }

        var count = 0;
        foreach (var text in EnumerateDocumentText(document))
        {
            count += CountWords(text);
        }

        return count;
    }

    private static IEnumerable<string> EnumerateDocumentText(DocumentEditorDocument document)
    {
        foreach (var block in EnumerateTextBlocks(document.Blocks))
        {
            yield return block;
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            foreach (var block in EnumerateTextBlocks(headerFooter.Blocks))
            {
                yield return block;
            }
        }
    }

    private static IEnumerable<string> EnumerateTextBlocks(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            foreach (var text in EnumerateInlineText(GetEditableInlines(block.Content) ?? []))
            {
                yield return text;
            }

            if (block.Content is TableBlockContent table)
            {
                foreach (var nested in table.Rows
                    .SelectMany(row => row.Cells)
                    .SelectMany(cell => EnumerateTextBlocks(cell.Blocks)))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateInlineText(IEnumerable<InlineContent> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextRun textRun when !string.IsNullOrWhiteSpace(textRun.Text):
                    yield return textRun.Text;
                    break;
                case TokenRun tokenRun when !string.IsNullOrWhiteSpace(tokenRun.DisplayName):
                    yield return tokenRun.DisplayName;
                    break;
                case DocumentFieldRun fieldRun:
                    var fieldText = ResolveFieldDisplayText(fieldRun);
                    if (!string.IsNullOrWhiteSpace(fieldText))
                    {
                        yield return fieldText;
                    }
                    break;
            }
        }
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else
            {
                inWord = false;
            }
        }

        return count;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!BeginDispose(out var collaborationSync))
        {
            return;
        }

        _ = DisableBeforeUnloadGuardAsync();
        if (collaborationSync is not null)
        {
            _ = collaborationSync.LeaveAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!BeginDispose(out var collaborationSync))
        {
            return;
        }

        await DisableBeforeUnloadGuardAsync();
        if (collaborationSync is not null)
        {
            await collaborationSync.LeaveAsync();
        }
    }

    private bool BeginDispose(out DocumentCollaborationSync? collaborationSync)
    {
        collaborationSync = null;
        if (_disposed)
        {
            return false;
        }

        _disposed = true;
        _commandStack.OnStackChanged -= HandleCommandStackChanged;
        _autoSaveTimer?.Dispose();
        _collaborationTimer?.Dispose();
        collaborationSync = _collaborationSync;
        _collaborationSync = null;
        return true;
    }

    private async Task DisableBeforeUnloadGuardAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("tmDocumentEditor.disableBeforeUnloadGuard");
        }
        catch
        {
            // JS interop may already be unavailable during disposal.
        }
    }
}
