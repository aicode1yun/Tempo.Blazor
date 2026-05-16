using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Components.DocumentEditor.Commands;
using Tempo.Blazor.DocumentEditor.Interfaces;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;
using Tempo.Blazor.Interfaces;

namespace Tempo.Blazor.Components.DocumentEditor;

/// <summary>Word-like document editor shell backed by the document editor JSON provider contracts.</summary>
public partial class TmDocumentEditor : ComponentBase, IDisposable
{
    private readonly DocumentEditorCommandStack _commandStack = new();
    private readonly DocumentEditorKeyboardManager _keyboardManager = new();
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

    /// <summary>Whether the comments rail is displayed.</summary>
    [Parameter] public bool ShowComments { get; set; } = true;

    /// <summary>Whether the version history panel is displayed.</summary>
    [Parameter] public bool ShowVersionHistory { get; set; } = true;

    /// <summary>Optional resolver for provider-managed document image assets.</summary>
    [Parameter] public IDocumentImageUrlResolver? ImageUrlResolver { get; set; }

    /// <summary>Optional image provider used by upload and clipboard image flows.</summary>
    [Parameter] public IDocumentImageProvider? ImageProvider { get; set; }

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

    /// <summary>Optional provider used to synchronize realtime collaborative edits.</summary>
    [Parameter] public IDocumentCollaborationProvider? CollaborationProvider { get; set; }

    /// <summary>Stable collaboration client id for the current editor instance.</summary>
    [Parameter] public string? CollaborationClientId { get; set; }

    /// <summary>Interval used to poll provider-backed collaboration updates.</summary>
    [Parameter] public TimeSpan CollaborationSyncInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Offline draft behavior for the editor. Defaults to disabled.</summary>
    [Parameter] public DocumentEditorOfflineMode OfflineMode { get; set; } = DocumentEditorOfflineMode.Disabled;

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
    private DocumentEditorSelectionState _selection = new();
    private string? _errorMessage;
    private string? _saveMessage;
    private string? _versionMessage;
    private string? _commentMessage;
    private string? _suggestionMessage;
    private string? _revisionMessage;
    private string? _templatePreviewMessage;
    private WysiwygFormattingState _formattingState = new();
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
    private bool _commentComposerOpen;
    private string? _selectedCommentId;
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
    private WysiwygSelectionSnapshot? _lastBodySelectionSnapshot;
    private WysiwygSelectionSnapshot? _lastBodyRangeSelectionSnapshot;
    private WysiwygSelectionSnapshot? _pendingLinkSelectionSnapshot;
    private bool _showRuler = true;
    private int _zoomPercent = 100;
    private bool _zoomPageWidth = true;
    private bool _ribbonKeyboardMode;
    private WysiwygTextContextMenuRequest? _textContextMenu;
    private WysiwygTableContextMenuRequest? _tableContextMenu;
    private WysiwygMiniToolbarRequest? _miniToolbar;
    private WysiwygUndoState _wysiwygUndoState = new();
    private WysiwygDirtyState _wysiwygDirtyState = new();
    private string? _runtimeDraftStateJson;
    private int _blazorRenderCount;
    private bool _suppressNextWysiwygStateRender;
    private string? _lastCollapsedSelectionRenderKey;

    private int NextBlazorRenderCount => ++_blazorRenderCount;

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
    protected override void OnInitialized()
    {
        _commandStack.OnStackChanged += HandleCommandStackChanged;
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

            if (!ShowToolbar)
            {
                classes.Add("tm-document-editor--no-toolbar");
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

    private bool IsTemplatePreview => _templatePreviewEnabled;

    private DocumentEditorPermissions EffectivePermissions => Permissions ?? new DocumentEditorPermissions();

    private bool CanReadDocument => EffectivePermissions.CanRead;

    private bool CanEditDocument => EffectivePermissions.CanEdit && !ReadOnly && !IsVersionPreview && !IsTemplatePreview;

    private bool EffectiveReadOnly => !CanEditDocument;

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

    private string WorkspaceClass => _sidePanelOpen
        ? "tm-document-editor__workspace tm-document-editor__workspace--side-panel-open"
        : "tm-document-editor__workspace tm-document-editor__workspace--side-panel-closed";

    private DocumentSidePanelTab ActiveSidePanelTab => NormalizeSidePanelTab(_activeSidePanelTab);

    private bool EffectiveTrackChangesEnabled => _trackChangesEnabled;

    private DocumentSection? ActiveSection => DisplayedDocument?.Sections.OrderBy(section => section.Order).FirstOrDefault();

    private bool DifferentFirstPageHeaderFooter => ActiveSection?.Properties.DifferentFirstPage == true;

    private bool DifferentOddAndEvenHeaderFooter => ActiveSection?.Properties.DifferentOddAndEvenPages == true;

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
            DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(_document);
            _currentDocument = _document;
            _concurrencyToken = result.ConcurrencyToken;
            _selection = new DocumentEditorSelectionState();
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

        var documentToSave = _currentDocument ?? _document;
        var saveUndoEpoch = _wysiwygUndoState.Epoch;

        if (_wysiwygHost is not null)
        {
            var undoState = await _wysiwygHost.RequestUndoStateAsync();
            if (undoState is not null)
            {
                _wysiwygUndoState = undoState;
                saveUndoEpoch = undoState.Epoch;
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
            BaseConcurrencyToken = _concurrencyToken,
            ConcurrencyMode = DocumentEditorConcurrencyMode.Optional,
            Author = Author,
            IsAutosave = trigger == DocumentEditorSaveTrigger.AutoSave,
            VersionKind = trigger == DocumentEditorSaveTrigger.AutoSave ? DocumentVersionKind.Autosave : null
        };

        try
        {
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

                _saveMessage = trigger == DocumentEditorSaveTrigger.AutoSave
                    ? Loc["TmDocumentEditor_AutoSaveComplete"]
                    : Loc["TmDocumentEditor_SaveComplete"];
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

                await RecordSaveAuditAsync(trigger, DocumentEditorAuditResult.Failure, _saveMessage);
            }
        }
        catch (Exception ex)
        {
            _saveMessage = Loc["TmDocumentEditor_SaveFailed"];
            await SaveOfflineDraftAsync(documentToSave);
            await RecordSaveAuditAsync(trigger, DocumentEditorAuditResult.Failure, ex.Message);
        }
        finally
        {
            _isSaving = false;
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
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

        var documentToCompare = await GetCurrentDocumentForProviderExportAsync();
        _compareDocumentSnapshot = CloneForEditor(documentToCompare);
        _compareDialogOpen = true;
    }

    private void CloseCompareDialog()
    {
        _compareDialogOpen = false;
        _compareDocumentSnapshot = null;
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
        try
        {
            var documentToExport = await GetCurrentDocumentForProviderExportAsync();
            var result = await PdfExportProvider.ExportPdfAsync(new DocumentPdfExportRequest
            {
                DocumentId = documentToExport.DocumentId,
                Document = CloneForEditor(documentToExport),
                FileName = string.IsNullOrWhiteSpace(documentToExport.Metadata.Title)
                    ? documentToExport.DocumentId
                    : documentToExport.Metadata.Title,
                Author = Author,
                Options = CreatePdfExportOptions(documentToExport)
            });

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
            _document = imported;
            _currentDocument = imported;
            _selection = new DocumentEditorSelectionState();
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

        try
        {
            var documentToExport = await GetCurrentDocumentForProviderExportAsync();
            var result = await FormatProvider.ExportAsync(new DocumentFormatExportProviderRequest
            {
                DocumentId = documentToExport.DocumentId,
                Format = DocumentFormatProviderKind.Docx,
                Document = CloneForEditor(documentToExport),
                FileName = string.IsNullOrWhiteSpace(documentToExport.Metadata.Title)
                    ? documentToExport.DocumentId
                    : documentToExport.Metadata.Title,
                Author = Author
            });

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
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<DocumentEditorDocument> GetCurrentDocumentForProviderExportAsync()
    {
        var documentToExport = _currentDocument ?? _document ?? DocumentEditorDocument.Empty();
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

        return CreateProviderBoundarySnapshot(documentToExport);
    }

    private static DocumentEditorDocument CreateProviderBoundarySnapshot(
        DocumentEditorDocument currentDocument,
        DocumentEditorDocument? wysiwygSnapshot = null)
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
        snapshot.Comments = CloneForEditor(currentDocument.Comments);
        snapshot.Notes = CloneForEditor(currentDocument.Notes);
        snapshot.Assets = CloneForEditor(currentDocument.Assets);
        snapshot.Anchors = CloneForEditor(currentDocument.Anchors);

        if (snapshot.HeadersFooters.Count == 0 && currentDocument.HeadersFooters.Count > 0)
        {
            snapshot.HeadersFooters = CloneForEditor(currentDocument.HeadersFooters);
        }

        if (snapshot.Revisions.Count == 0 && currentDocument.Revisions.Count > 0)
        {
            snapshot.Revisions = CloneForEditor(currentDocument.Revisions);
        }

        DocumentHeaderFooterResolver.EnsurePrimaryHeadersFooters(snapshot);
        RemoveTransientDisplayData(snapshot);
        return snapshot;
    }

    private static void RemoveTransientDisplayData(DocumentEditorDocument document)
    {
        foreach (var block in EnumerateProviderBoundaryBlocks(document.Blocks))
        {
            RemoveTransientDisplayData(block);
        }

        foreach (var headerFooter in document.HeadersFooters)
        {
            foreach (var block in EnumerateProviderBoundaryBlocks(headerFooter.Blocks))
            {
                RemoveTransientDisplayData(block);
            }
        }

        foreach (var note in document.Notes)
        {
            foreach (var block in EnumerateProviderBoundaryBlocks(note.Blocks))
            {
                RemoveTransientDisplayData(block);
            }
        }
    }

    private static void RemoveTransientDisplayData(DocumentBlock block)
    {
        if (block.Content is ImageBlockContent { Source: DocumentImageSource.Asset } image
            && !string.IsNullOrWhiteSpace(image.AssetId))
        {
            image.Url = null;
        }
    }

    private static IEnumerable<DocumentBlock> EnumerateProviderBoundaryBlocks(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            if (block.Content is not TableBlockContent table)
            {
                continue;
            }

            foreach (var nested in table.Rows
                         .SelectMany(row => row.Cells)
                         .SelectMany(cell => EnumerateProviderBoundaryBlocks(cell.Blocks)))
            {
                yield return nested;
            }
        }
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

    private static DocumentPdfExportOptions CreatePdfExportOptions(DocumentEditorDocument document)
    {
        var pageSettings = document.PageSettings ?? new DocumentPageSettings();
        return new DocumentPdfExportOptions
        {
            IncludeComments = true,
            IncludeSuggestions = true,
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
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.OpenImageDialogAsync();
        }
    }

    private async Task InsertTableAsync()
    {
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("insertTable");
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
            else if (!deferRenderUntilTransactionCommit)
            {
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
        if (snapshot?.IsCollapsed != false)
        {
            _miniToolbar = null;
        }

        if (snapshot is null)
        {
            _selection = new DocumentEditorSelectionState();
            _formattingState = new WysiwygFormattingState();
            _activeWysiwygRegion = "Body";
            _lastCollapsedSelectionRenderKey = null;
            await BroadcastCollaborationCursorAsync();
            return;
        }

        _activeWysiwygRegion = string.IsNullOrWhiteSpace(snapshot.Region) ? "Body" : snapshot.Region;
        var collapsedRenderKey = snapshot.IsCollapsed ? GetCollapsedSelectionRenderKey(snapshot) : null;
        if (collapsedRenderKey is not null && string.Equals(collapsedRenderKey, _lastCollapsedSelectionRenderKey, StringComparison.Ordinal))
        {
            UpdateCollapsedSelectionWithoutRender(snapshot);
            _suppressNextWysiwygStateRender = true;
            return;
        }

        _lastCollapsedSelectionRenderKey = collapsedRenderKey;
        if (string.Equals(_activeWysiwygRegion, "Body", StringComparison.OrdinalIgnoreCase))
        {
            _lastBodySelectionSnapshot = snapshot;
            if (snapshot.IsCollapsed == false)
            {
                _lastBodyRangeSelectionSnapshot = snapshot;
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
            FocusedInlineRange = range,
            ActiveTableCellId = snapshot.ActiveTableCellId,
            Region = _activeWysiwygRegion,
            HeaderFooterId = snapshot.HeaderFooterId,
            PageIndex = snapshot.PageIndex
        };
        _formattingState = await ResolveRuntimeFormattingStateAsync(snapshot);
        await BroadcastCollaborationCursorAsync();
    }

    private static string GetCollapsedSelectionRenderKey(WysiwygSelectionSnapshot snapshot)
        => string.Join('|',
            string.IsNullOrWhiteSpace(snapshot.Region) ? "Body" : snapshot.Region,
            snapshot.AnchorBlockId ?? string.Empty,
            snapshot.AnchorInlineId ?? string.Empty,
            snapshot.ActiveTableCellId ?? string.Empty,
            snapshot.ActiveImageBlockId ?? string.Empty,
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
        _selection.ActiveTableCellId = snapshot.ActiveTableCellId;
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

    private Task HandleTextContextMenuRequestedAsync(WysiwygTextContextMenuRequest request)
    {
        _textContextMenu = request;
        _tableContextMenu = null;
        _miniToolbar = null;
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleTableContextMenuRequestedAsync(WysiwygTableContextMenuRequest request)
    {
        _tableContextMenu = request;
        _textContextMenu = null;
        _miniToolbar = null;
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleMiniToolbarChangedAsync(WysiwygMiniToolbarRequest? request)
    {
        _miniToolbar = request?.IsVisible == true ? request : null;
        if (_miniToolbar is not null)
        {
            _textContextMenu = null;
            _tableContextMenu = null;
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task InsertTableRowFromContextAsync()
        => RunTableContextCommandAsync("insertTableRow");

    private Task InsertTableColumnFromContextAsync()
        => RunTableContextCommandAsync("insertTableColumn");

    private Task DeleteTableRowFromContextAsync()
        => RunTableContextCommandAsync("deleteTableRow");

    private Task DeleteTableColumnFromContextAsync()
        => RunTableContextCommandAsync("deleteTableColumn");

    private async Task RunTableContextCommandAsync(string command)
    {
        var selection = _tableContextMenu?.Selection;
        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
            await _wysiwygHost.ExecuteEditorCommandAsync(command);
        }

        CloseFloatingUi();
    }

    private async Task RunFloatingSelectionCommandAsync(WysiwygSelectionSnapshot? selection, Func<Task> command)
    {
        if (_wysiwygHost is not null && selection is not null)
        {
            await _wysiwygHost.RestoreSelectionAsync(selection);
        }

        await command();
        CloseFloatingUi();
    }

    private Task ApplyDefaultFloatingLinkAsync()
    {
        return ApplyLinkAsync("https://example.com");
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
    }

    private static string FloatingStyle(WysiwygFloatingUiPosition position)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: {position.Left:0.##}px; top: {position.Top:0.##}px;");
    }

    private WysiwygFormattingState ComputeFormattingState(WysiwygSelectionSnapshot snapshot)
    {
        var (paragraphAlignment, paragraphAlignmentMixed) = ComputeParagraphAlignmentState(snapshot);
        return new WysiwygFormattingState
        {
            Bold = ComputeMarkState(snapshot, InlineMarkType.Bold),
            Italic = ComputeMarkState(snapshot, InlineMarkType.Italic),
            Underline = ComputeMarkState(snapshot, InlineMarkType.Underline),
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


    private Task HandleWysiwygTransactionCommittedAsync()
    {
        _activeWysiwygTransactionId = null;
        return Task.CompletedTask;
    }

    private Task HandleWysiwygUndoStateChangedAsync(WysiwygUndoState state)
    {
        _wysiwygUndoState = state ?? new WysiwygUndoState();
        _suppressNextWysiwygStateRender = true;
        return Task.CompletedTask;
    }

    private Task HandleWysiwygDirtyStateChangedAsync(WysiwygDirtyState state)
    {
        _wysiwygDirtyState = state ?? new WysiwygDirtyState();
        var wasDirty = _isDirty;
        _isDirty = _wysiwygDirtyState.IsDirty;
        if (_isDirty && _document is not null)
        {
            _suggestionSnapshot = Clone(_document);
        }

        if (wasDirty == _isDirty)
        {
            _suppressNextWysiwygStateRender = true;
            return Task.CompletedTask;
        }

        return InvokeAsync(StateHasChanged);
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
            existing.Range = Clone(runtimeRevision.Range);
            existing.Author = Clone(runtimeRevision.Author);
            existing.CreatedAt = runtimeRevision.CreatedAt;
            existing.Action = runtimeRevision.Action;
            existing.PayloadJson = runtimeRevision.PayloadJson;
            changed = true;
        }

        if (!changed)
        {
            return Task.CompletedTask;
        }

        _currentDocument = _document;
        return InvokeAsync(StateHasChanged);
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
                    : 0;
            var undoEpoch = root.TryGetProperty("runtimeUndoEpoch", out var undoEpochElement)
                ? undoEpochElement.GetInt32()
                : root.TryGetProperty("dirtyState", out dirtyState)
                    && dirtyState.TryGetProperty("UndoEpoch", out var dirtyUndoEpochElement)
                        ? dirtyUndoEpochElement.GetInt32()
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
        OpenSidePanel(tab);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenSidePanelAsync()
    {
        _sidePanelOpen = true;
        _activeSidePanelTab = NormalizeSidePanelTab(_activeSidePanelTab);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenCommentsPanelAsync()
    {
        OpenSidePanel(DocumentSidePanelTab.Comments);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenRevisionsPanelAsync()
    {
        OpenSidePanel(DocumentSidePanelTab.Revisions);
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenVersionsPanelAsync()
    {
        OpenSidePanel(DocumentSidePanelTab.Versions);
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

    private Task SetRulerVisibleAsync(bool visible)
    {
        _showRuler = visible;
        return InvokeAsync(StateHasChanged);
    }

    private Task SetZoomPercentAsync(int percent)
    {
        _zoomPercent = Math.Clamp(percent, 50, 200);
        _zoomPageWidth = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task SetZoomPageWidthAsync()
    {
        _zoomPageWidth = true;
        _zoomPercent = 100;
        return InvokeAsync(StateHasChanged);
    }

    private void OpenSidePanel(DocumentSidePanelTab tab)
    {
        _activeSidePanelTab = NormalizeSidePanelTab(tab);
        _sidePanelOpen = true;
    }

    private void CloseSidePanel()
    {
        _sidePanelOpen = false;
    }

    private DocumentSidePanelTab NormalizeSidePanelTab(DocumentSidePanelTab tab)
    {
        return tab switch
        {
            DocumentSidePanelTab.Comments when ShowComments => DocumentSidePanelTab.Comments,
            DocumentSidePanelTab.Revisions => DocumentSidePanelTab.Revisions,
            DocumentSidePanelTab.Versions when ShowVersionHistory => DocumentSidePanelTab.Versions,
            DocumentSidePanelTab.Properties => DocumentSidePanelTab.Properties,
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
            if (_wysiwygHost is not null)
            {
                await _wysiwygHost.UpsertCommentAsync(created);
            }

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

    private async Task ReplyToCommentAsync(DocumentEditorCommentReplyRequest request)
    {
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

    private async Task ResolveCommentAsync(string commentId)
    {
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
        if (Provider is null || _document is null || !CanDeleteOwnComments || _isSubmittingComment)
        {
            return;
        }

        var comment = _comments.FirstOrDefault(item => item.Id == commentId);
        if (!CanDeleteComment(comment))
        {
            return;
        }

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

            _commentMessage = Loc["TmDocumentEditor_CommentDeleted"];
            await RecordCommentAuditAsync(commentId, DocumentEditorAuditResult.Success, null);
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

    private async Task SelectCommentAsync(string commentId)
    {
        _selectedCommentId = commentId;
        OpenSidePanel(DocumentSidePanelTab.Comments);
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ScrollToCommentAsync(commentId);
        }
    }

    private async Task ToggleTrackChanges()
    {
        if (EffectiveReadOnly)
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

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetReviewDisplayModeAsync(DocumentReviewDisplayMode mode)
    {
        _reviewDisplayMode = mode;
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.SetReviewDisplayModeAsync(mode);
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
    }

    private Task AcceptRevisionAsync(DocumentRevision revision)
        => ReviewRevisionAsync(revision, DocumentRevisionAction.Accepted);

    private Task RejectRevisionAsync(DocumentRevision revision)
        => ReviewRevisionAsync(revision, DocumentRevisionAction.Rejected);

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

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.UndoRuntimeAsync();
            await RefreshRuntimeUndoDirtyStateAsync();
            return;
        }

        await _commandStack.UndoAsync();
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.RefreshSnapshotAsync();
        }
    }

    private async Task RedoAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.RedoRuntimeAsync();
            await RefreshRuntimeUndoDirtyStateAsync();
            return;
        }

        await _commandStack.RedoAsync();
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.RefreshSnapshotAsync();
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

        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleInlineMarkAsync(InlineMarkType markType)
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (_wysiwygHost is not null)
        {
            var command = markType switch
            {
                InlineMarkType.Bold => "toggleBold",
                InlineMarkType.Italic => "toggleItalic",
                InlineMarkType.Underline => "toggleUnderline",
                _ => "toggleMark"
            };
            var payload = command == "toggleMark"
                ? new WysiwygMarkPayload { MarkType = markType.ToString() }
                : null;

            await _wysiwygHost.ExecuteEditorCommandAsync(command, payload);
        }
    }

    private async Task ApplyFontFamilyAsync(string cssFamily)
    {
        if (_wysiwygHost is null || EffectiveReadOnly || string.IsNullOrWhiteSpace(cssFamily))
        {
            return;
        }

        if (!_fontFamilies.Any(font => string.Equals(font.CssFamily, cssFamily, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("setFontFamily", new
        {
            Value = cssFamily
        });
    }

    private async Task ApplyFontSizeAsync(double sizePt)
    {
        if (_wysiwygHost is null || EffectiveReadOnly || sizePt is < 6 or > 96)
        {
            return;
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("setFontSize", new
        {
            Value = FormattableString.Invariant($"{sizePt:0.##}pt")
        });
    }

    private Task ApplyTextColorAsync(string color)
    {
        return ApplyColorMarkAsync(InlineMarkType.TextColor, color);
    }

    private Task ApplyHighlightColorAsync(string color)
    {
        return ApplyColorMarkAsync(InlineMarkType.Highlight, color);
    }

    private Task ApplyParagraphAlignmentAsync(DocumentTextAlignment alignment)
    {
        return ExecuteParagraphCommandAsync("setParagraphAlignment", new { Alignment = alignment });
    }

    private Task ApplyLineSpacingAsync(double lineSpacing)
    {
        if (lineSpacing is < 0.8 or > 3)
        {
            return Task.CompletedTask;
        }

        return ExecuteParagraphCommandAsync("setLineSpacing", new { LineSpacing = lineSpacing });
    }

    private Task ApplySpacingBeforeAsync(double spacingBefore)
    {
        if (spacingBefore is < 0 or > 144)
        {
            return Task.CompletedTask;
        }

        return ApplyParagraphPropertiesAsync(new DocumentParagraphPropertiesPatch { SpacingBefore = spacingBefore });
    }

    private Task ApplySpacingAfterAsync(double spacingAfter)
    {
        if (spacingAfter is < 0 or > 144)
        {
            return Task.CompletedTask;
        }

        return ApplyParagraphPropertiesAsync(new DocumentParagraphPropertiesPatch { SpacingAfter = spacingAfter });
    }

    private Task IncreaseParagraphIndentAsync()
    {
        return ExecuteParagraphCommandAsync("increaseIndent");
    }

    private Task DecreaseParagraphIndentAsync()
    {
        return ExecuteParagraphCommandAsync("decreaseIndent");
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

    private async Task ExecuteParagraphCommandAsync(string command, object? payload = null)
    {
        if (_wysiwygHost is null || EffectiveReadOnly)
        {
            return;
        }

        await _wysiwygHost.ExecuteEditorCommandAsync(command, payload);
    }

    private async Task ApplyColorMarkAsync(InlineMarkType markType, string color)
    {
        if (_wysiwygHost is null || EffectiveReadOnly || !IsSafeHexColor(color))
        {
            return;
        }

        var command = markType == InlineMarkType.TextColor
            ? "setTextColor"
            : "setHighlightColor";

        await _wysiwygHost.ExecuteEditorCommandAsync(command, new
        {
            Value = color
        });
    }

    private static bool IsSafeHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color) || color.Length != 7 || color[0] != '#')
        {
            return false;
        }

        return color.Skip(1).All(Uri.IsHexDigit);
    }

    private async Task ClearInlineFormattingAsync()
    {
        if (_wysiwygHost is not null && !EffectiveReadOnly)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("clearFormatting");
        }
    }

    private async Task ApplyLinkAsync(string href)
        => await ApplyLinkAsync(new WysiwygLinkPayload { Href = href });

    private async Task ApplyLinkAsync(WysiwygLinkPayload payload)
    {
        if (_wysiwygHost is null || EffectiveReadOnly)
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
            await _wysiwygHost.RestoreSelectionAsync(selection);
        }

        await _wysiwygHost.ExecuteEditorCommandAsync("insertLink", new
        {
            Href = href,
            Title = string.IsNullOrWhiteSpace(payload.Title) ? null : payload.Title.Trim(),
            Selection = selection
        });
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
        switch (_keyboardManager.GetCommand(args))
        {
            case DocumentEditorKeyboardCommand.Save:
                await SaveAsync();
                break;
            case DocumentEditorKeyboardCommand.Undo:
                if (_wysiwygHost is null)
                {
                    await UndoAsync();
                }
                break;
            case DocumentEditorKeyboardCommand.Redo:
                if (_wysiwygHost is null)
                {
                    await RedoAsync();
                }
                break;
            case DocumentEditorKeyboardCommand.Bold:
                await ToggleInlineMarkAsync(InlineMarkType.Bold);
                break;
            case DocumentEditorKeyboardCommand.Italic:
                await ToggleInlineMarkAsync(InlineMarkType.Italic);
                break;
            case DocumentEditorKeyboardCommand.Link:
                await ApplyLinkAsync("https://example.com");
                break;
            case DocumentEditorKeyboardCommand.OpenVersions:
                OpenSidePanel(DocumentSidePanelTab.Versions);
                await InvokeAsync(StateHasChanged);
                break;
            case DocumentEditorKeyboardCommand.ActivateRibbon:
                await ActivateRibbonKeyboardModeAsync();
                break;
            case DocumentEditorKeyboardCommand.ClosePanel:
                await CloseTopmostEditorLayerAsync();
                break;
        }
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
        var handled = false;
        if (_textContextMenu is not null || _tableContextMenu is not null || _miniToolbar is not null)
        {
            CloseFloatingUi();
            handled = true;
        }
        else if (_versionDialogOpen)
        {
            CloseVersionDialog();
            handled = true;
        }
        else if (_compareDialogOpen)
        {
            CloseCompareDialog();
            handled = true;
        }
        else if (_sidePanelOpen)
        {
            CloseSidePanel();
            handled = true;
        }

        if (handled)
        {
            await FocusDocumentAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task FocusDocumentAsync()
    {
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.FocusAsync();
        }
    }

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

        _ = InvokeAsync(StateHasChanged);
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

        if (AutoSaveInterval is not { } interval || interval <= TimeSpan.Zero)
        {
            return;
        }

        _autoSaveTimer = new Timer(
            _ => _ = InvokeAsync(() => SaveAsync(DocumentEditorSaveTrigger.AutoSave)),
            null,
            interval,
            interval);
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
            await _collaborationSync.UpdateCursorAsync(new DocumentCollaborationCursor
            {
                DisplayName = string.IsNullOrWhiteSpace(Author?.DisplayName)
                    ? _activeCollaborationClientId ?? _generatedCollaborationClientId
                    : Author!.DisplayName,
                BlockId = _selection.ActiveBlockId,
                InlineIndex = _selection.FocusedInlineRange?.StartInlineIndex,
                Offset = _selection.FocusedInlineRange?.StartOffset,
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
        }
    }

    private void CloseVersionDialog()
    {
        _versionDialogOpen = false;
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
        }

        if (_currentDocument is not null && !ReferenceEquals(_currentDocument, _document))
        {
            _currentDocument.Comments.RemoveAll(item => item.Id == comment.Id);
            _currentDocument.Comments.Add(CloneForEditor(comment));
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
        var start = Math.Clamp(anchor.StartOffset.Value, 0, text.Length);
        var end = Math.Clamp(anchor.EndOffset.Value, 0, text.Length);
        if (start >= end)
        {
            return false;
        }

        var before = text[..start];
        var selected = text[start..end];
        var after = text[end..];
        inlines.Clear();
        if (!string.IsNullOrEmpty(before))
        {
            inlines.Add(new TextRun { Text = before });
        }

        inlines.Add(new TextRun
        {
            Text = selected,
            Marks =
            [
                new InlineMark
                {
                    Type = InlineMarkType.CommentAnchor,
                    CommentAnchor = new CommentAnchorMarkData { CommentId = comment.Id }
                }
            ]
        });

        if (!string.IsNullOrEmpty(after))
        {
            inlines.Add(new TextRun { Text = after });
        }

        return true;
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

    private static string GetInlineText(InlineContent inline)
    {
        return inline switch
        {
            TextRun text => text.Text,
            TokenRun token => string.IsNullOrWhiteSpace(token.DisplayName) ? token.Key : token.DisplayName,
            DocumentNoteReferenceRun note => string.IsNullOrWhiteSpace(note.DisplayMarker) ? note.NoteId : note.DisplayMarker!,
            _ => string.Empty
        };
    }

    private static string GetBlockText(DocumentBlock block)
    {
        var inlines = GetEditableInlines(block.Content);
        return inlines is null ? string.Empty : string.Concat(inlines.Select(GetInlineText));
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
        _disposed = true;
        _commandStack.OnStackChanged -= HandleCommandStackChanged;
        _autoSaveTimer?.Dispose();
        _collaborationTimer?.Dispose();
        if (_collaborationSync is not null)
        {
            _ = _collaborationSync.LeaveAsync();
            _collaborationSync = null;
        }
    }
}
