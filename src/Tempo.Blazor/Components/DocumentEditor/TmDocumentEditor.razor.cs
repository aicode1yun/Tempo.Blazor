using System.Globalization;
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
    private TmDocumentWysiwygHost? _wysiwygHost;
    private DocumentEditorSelectionState _selection = new();
    private string? _errorMessage;
    private string? _saveMessage;
    private string? _versionMessage;
    private string? _commentMessage;
    private string? _suggestionMessage;
    private string? _templatePreviewMessage;
    private string? _concurrencyToken;
    private DateTimeOffset? _lastSavedAt;
    private DocumentEditorDocument? _currentDocument;
    private DocumentEditorDocument? _compareDocumentSnapshot;
    private DocumentEditorDocument? _templatePreviewDocument;
    private IReadOnlyList<DocumentVersion> _versions = [];
    private IReadOnlyList<DocumentComment> _comments = [];
    private IReadOnlyList<DocumentSuggestion> _suggestions = [];
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
    private bool _isDirty;
    private bool _trackChangesEnabled;
    private bool _suggestionsEnabled;
    private bool _templatePreviewEnabled;
    private bool _versionDialogOpen;
    private bool _compareDialogOpen;
    private bool _versionPanelOpen = true;
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
    private IDocumentSuggestionProvider? _loadedSuggestionProvider;
    private IDocumentFormatProvider? _loadedFormatProvider;
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

    private bool EffectiveTrackChangesEnabled => _suggestionsEnabled || _trackChangesEnabled;

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
            _formatMessage = null;
            _formatWarnings = [];
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _loadedSuggestionProvider = null;
            _loadedFormatProvider = FormatProvider;
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
            _formatMessage = null;
            _formatWarnings = [];
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _loadedSuggestionProvider = null;
            _loadedFormatProvider = FormatProvider;
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
            _currentDocument = result.Document;
            _concurrencyToken = result.ConcurrencyToken;
            _selection = new DocumentEditorSelectionState();
            _isDirty = false;
            _saveMessage = null;
            _versionMessage = null;
            _commentMessage = null;
            _suggestionMessage = null;
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
            _versionPanelOpen = true;
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _offlineMessage = null;
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

        if (_wysiwygHost is not null)
        {
            var jsSnapshot = await _wysiwygHost.RequestSnapshotAsync();
            if (jsSnapshot is not null)
            {
                // Preserve document-level metadata that the JS engine does not own.
                jsSnapshot.DocumentId = documentToSave.DocumentId;
                jsSnapshot.SchemaVersion = documentToSave.SchemaVersion;
                jsSnapshot.Metadata = documentToSave.Metadata;
                jsSnapshot.PageSettings = documentToSave.PageSettings;
                jsSnapshot.Sections = documentToSave.Sections;
                jsSnapshot.Comments = documentToSave.Comments;
                jsSnapshot.Notes = documentToSave.Notes;
                // Phase 12: Headers/footers are serialized from JS DOM; preserve them.
                jsSnapshot.Revisions = documentToSave.Revisions;
                jsSnapshot.Assets = documentToSave.Assets;
                jsSnapshot.Anchors = documentToSave.Anchors;
                documentToSave = jsSnapshot;
                _document = documentToSave;
                _currentDocument = documentToSave;
            }
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
                _document = result.Document ?? _document;
                _currentDocument = _document;
                _concurrencyToken = result.ConcurrencyToken;
                _isDirty = false;
                _lastSavedAt = DateTimeOffset.Now;
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
            _document = imported;
            _currentDocument = imported;
            _selection = new DocumentEditorSelectionState();
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
                jsSnapshot.DocumentId = documentToExport.DocumentId;
                jsSnapshot.SchemaVersion = documentToExport.SchemaVersion;
                jsSnapshot.Metadata = documentToExport.Metadata;
                jsSnapshot.PageSettings = documentToExport.PageSettings;
                jsSnapshot.Sections = documentToExport.Sections;
                jsSnapshot.Comments = documentToExport.Comments;
                jsSnapshot.Notes = documentToExport.Notes;
                jsSnapshot.Revisions = documentToExport.Revisions;
                jsSnapshot.Assets = documentToExport.Assets;
                jsSnapshot.Anchors = documentToExport.Anchors;
                _document = jsSnapshot;
                _currentDocument = jsSnapshot;
                documentToExport = jsSnapshot;
            }
        }

        return documentToExport;
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
            var applier = new WysiwygPatchApplier();
            applier.ApplyPatch(_document, patch);

            var after = DocumentEditorCommandCloner.Clone(_document);
            if (await TryCreateSuggestionAsync(before, after))
            {
                await InvokeAsync(StateHasChanged);
                return;
            }

            var command = new DocumentEditorSnapshotCommand(
                _document,
                before,
                after,
                GetPatchDescription(patch));

            // Transaction batching: patches sharing the same TransactionId
            // are grouped into a single undo step.
            _suppressCommandStackChangedRender = deferRenderUntilTransactionCommit;
            try
            {
                if (!string.IsNullOrWhiteSpace(patch.TransactionId))
                {
                    if (_commandStack.IsInBatch && _activeWysiwygTransactionId != patch.TransactionId)
                    {
                        _commandStack.CommitBatch();
                        _commandStack.BeginBatch(command.Description);
                        _activeWysiwygTransactionId = patch.TransactionId;
                    }
                    else if (!_commandStack.IsInBatch)
                    {
                        _commandStack.BeginBatch(command.Description);
                        _activeWysiwygTransactionId = patch.TransactionId;
                    }

                    await _commandStack.PushAsync(command);
                }
                else
                {
                    if (_commandStack.IsInBatch)
                    {
                        _commandStack.CommitBatch();
                        _activeWysiwygTransactionId = null;
                    }

                    await _commandStack.PushAsync(command);
                }
            }
            finally
            {
                _suppressCommandStackChangedRender = false;
            }

            _currentDocument = _document;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _isDirty = true;
            await BroadcastLocalCollaborationChangeAsync(before, after);
            _suggestionSnapshot = Clone(after);
            if (!deferRenderUntilTransactionCommit)
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
        if (snapshot is null)
        {
            _selection = new DocumentEditorSelectionState();
            await BroadcastCollaborationCursorAsync();
            return;
        }

        var range = new DocumentEditorInlineRange
        {
            BlockId = snapshot.AnchorBlockId,
            StartOffset = snapshot.AnchorOffset,
            EndOffset = snapshot.IsCollapsed ? snapshot.AnchorOffset : snapshot.FocusOffset
        };

        if (!string.IsNullOrWhiteSpace(snapshot.AnchorBlockId) && _document is not null)
        {
            var block = _document.Blocks.FirstOrDefault(b => b.Id == snapshot.AnchorBlockId);
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
            FocusedInlineRange = range
        };
        await BroadcastCollaborationCursorAsync();
    }

    private Task HandleWysiwygTransactionCommittedAsync()
    {
        if (_commandStack.IsInBatch)
        {
            _commandStack.CommitBatch();
            _activeWysiwygTransactionId = null;
        }

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
            "InsertParagraph" or "InsertLineBreak" => "Insert paragraph",
            "InsertBlock" => $"Insert {patch.BlockType}",
            "RemoveBlock" => "Remove block",
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

        var pendingAssets = CollectPendingAssets(document);
        var draft = new DocumentOfflineDraft
        {
            Id = _offlineDraft?.Id ?? Guid.NewGuid().ToString("N"),
            DocumentId = document.DocumentId,
            BaseVersionId = _concurrencyToken,
            JsonSnapshot = DocumentEditorJson.Serialize(document),
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
                if (result.SaveResult?.Document is not null)
                {
                    _document = result.SaveResult.Document;
                    _currentDocument = _document;
                    _concurrencyToken = result.SaveResult.ConcurrencyToken;
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
            ApplyCommentAnchorMark(created);
            _selectedCommentId = created.Id;
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

    private void SelectComment(string commentId)
    {
        _selectedCommentId = commentId;
    }

    private async Task ToggleTrackChanges()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (SuggestionProvider is not null)
        {
            if (!CanCreateSuggestions)
            {
                return;
            }

            _suggestionsEnabled = !_suggestionsEnabled;
            if (_suggestionsEnabled)
            {
                await RefreshSuggestionsAsync();
                _suggestionSnapshot = _document is null ? null : Clone(_document);
            }

            return;
        }

        _trackChangesEnabled = !_trackChangesEnabled;
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

        await _commandStack.RedoAsync();
        MarkDirtyAfterCommand();
        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.RefreshSnapshotAsync();
        }
    }

    private async Task ToggleInlineMarkAsync(InlineMarkType markType)
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        if (_wysiwygHost is not null)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("toggleMark", new WysiwygMarkPayload { MarkType = markType.ToString() });
        }
    }

    private async Task ClearInlineFormattingAsync()
    {
        if (_wysiwygHost is not null && !EffectiveReadOnly)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("clearFormatting");
        }
    }

    private async Task ApplyLinkAsync(string href)
    {
        if (_wysiwygHost is not null && !EffectiveReadOnly)
        {
            await _wysiwygHost.ExecuteEditorCommandAsync("applyLink", new { Href = href });
        }
    }

    private async Task HandleEditorKeyDownAsync(KeyboardEventArgs args)
    {
        switch (_keyboardManager.GetCommand(args))
        {
            case DocumentEditorKeyboardCommand.Save:
                await SaveAsync();
                break;
            case DocumentEditorKeyboardCommand.Undo:
                await UndoAsync();
                break;
            case DocumentEditorKeyboardCommand.Redo:
                await RedoAsync();
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
            case DocumentEditorKeyboardCommand.ClosePanel:
                await Task.CompletedTask;
                break;
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
            or "InsertLineBreak"
            or "ToggleMark";
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

    private async Task BroadcastLocalCollaborationChangeAsync(DocumentEditorDocument before, DocumentEditorDocument after)
    {
        if (_suppressCollaborationBroadcast || _collaborationSync is null || !CanEditDocument)
        {
            _collaborationSnapshot = Clone(after);
            return;
        }

        var batch = _collaborationSync.CreateLocalEditBatch(before, after);
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

    private async Task RefreshCollaborationAsync()
    {
        if (_collaborationSync is null || CollaborationProvider is null || _isRefreshingCollaboration || _document is null)
        {
            return;
        }

        _isRefreshingCollaboration = true;
        try
        {
            var result = await _collaborationSync.ReconnectAsync();
            if (result.IsValid && !DocumentsEqual(_collaborationSnapshot, _collaborationSync.Document))
            {
                _suppressCollaborationBroadcast = true;
                try
                {
                    var updated = Clone(_collaborationSync.Document);
                    _document = updated;
                    _currentDocument = updated;
                    _templatePreviewDocument = null;
                    _templatePreviewEnabled = false;
                    _templatePreviewMessage = null;
                    _collaborationSnapshot = Clone(updated);
                    _suggestionSnapshot = Clone(updated);
                    await RefreshSuggestionsAsync();
                }
                finally
                {
                    _suppressCollaborationBroadcast = false;
                }
            }

            var sessionId = _collaborationSync.Session?.Id;
            _remoteCursors = (await CollaborationProvider.GetCursorsAsync(_collaborationSync.Document.DocumentId))
                .Where(cursor => !string.Equals(cursor.SessionId, sessionId, StringComparison.Ordinal))
                .ToList();
        }
        catch
        {
            // Keep the editor usable while the collaboration transport is unavailable.
        }
        finally
        {
            _isRefreshingCollaboration = false;
        }

        if (!_disposed)
        {
            await InvokeAsync(StateHasChanged);
        }
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
            _remoteCursors = _collaborationSync.RemoteCursors;
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
        _versionPanelOpen = false;
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

            if (_wysiwygHost is not null)
            {
                var jsSnapshot = await _wysiwygHost.RequestSnapshotAsync();
                if (jsSnapshot is not null)
                {
                    jsSnapshot.DocumentId = _currentDocument.DocumentId;
                    jsSnapshot.SchemaVersion = _currentDocument.SchemaVersion;
                    jsSnapshot.Metadata = _currentDocument.Metadata;
                    jsSnapshot.PageSettings = _currentDocument.PageSettings;
                    jsSnapshot.Sections = _currentDocument.Sections;
                    jsSnapshot.Comments = _currentDocument.Comments;
                    jsSnapshot.Notes = _currentDocument.Notes;
                    // Phase 12: Headers/footers are serialized from JS DOM; preserve them.
                    jsSnapshot.Revisions = _currentDocument.Revisions;
                    jsSnapshot.Assets = _currentDocument.Assets;
                    jsSnapshot.Anchors = _currentDocument.Anchors;
                    _currentDocument = jsSnapshot;
                    _document = jsSnapshot;
                }
            }

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
