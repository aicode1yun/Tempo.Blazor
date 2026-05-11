using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
    private Timer? _autoSaveTimer;
    private TimeSpan? _configuredAutoSaveInterval;

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

    /// <summary>Additional HTML attributes for the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private DocumentEditorDocument? _document;
    private TmDocumentSurface? _surface;
    private DocumentEditorSelectionState _selection = new();
    private string? _errorMessage;
    private string? _saveMessage;
    private string? _versionMessage;
    private string? _commentMessage;
    private string? _templatePreviewMessage;
    private string? _concurrencyToken;
    private DateTimeOffset? _lastSavedAt;
    private DocumentEditorDocument? _currentDocument;
    private DocumentEditorDocument? _templatePreviewDocument;
    private IReadOnlyList<DocumentVersion> _versions = [];
    private IReadOnlyList<DocumentComment> _comments = [];
    private DocumentVersion? _previewVersion;
    private DocumentCommentAnchor? _draftCommentAnchor;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isCreatingVersion;
    private bool _isExportingPdf;
    private bool _isLoadingVersions;
    private bool _isLoadingComments;
    private bool _isSubmittingComment;
    private bool _isDirty;
    private bool _trackChangesEnabled;
    private bool _templatePreviewEnabled;
    private bool _versionDialogOpen;
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
    private bool _disposed;

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

    private bool OfflineEnabled => OfflineMode == DocumentEditorOfflineMode.Enabled && OfflineStore is not null;

    private bool ShowOfflineBanner => OfflineEnabled
        && (_offlineDraft is not null || _offlineConflict is not null || _offlineStatus != DocumentSyncStatus.Online);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        _trackChangesEnabled = TrackChangesEnabled || _trackChangesEnabled;
        ConfigureAutoSaveTimer();

        if (Provider is null)
        {
            _document = null;
            _errorMessage = null;
            _loadedDocumentId = null;
            _loadedProvider = null;
            _currentDocument = null;
            _previewVersion = null;
            _versions = [];
            _comments = [];
            _draftCommentAnchor = null;
            _isLoading = false;
            _isDirty = false;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _commentComposerOpen = false;
            _selectedCommentId = null;
            _lastSavedAt = null;
            _offlineDraft = null;
            _offlineConflict = null;
            _offlineStatus = DocumentSyncStatus.Online;
            _offlineMessage = null;
            return;
        }

        if (!CanReadDocument)
        {
            _document = null;
            _errorMessage = Loc["TmDocumentEditor_ReadDenied"];
            _loadedDocumentId = null;
            _loadedProvider = null;
            _currentDocument = null;
            _previewVersion = null;
            _versions = [];
            _comments = [];
            _draftCommentAnchor = null;
            _isLoading = false;
            _isDirty = false;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _templatePreviewMessage = null;
            _commentComposerOpen = false;
            _selectedCommentId = null;
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
            return;
        }

        await LoadDocumentAsync();
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
            _templatePreviewMessage = null;
            _lastSavedAt = null;
            _previewVersion = null;
            _templatePreviewDocument = null;
            _templatePreviewEnabled = false;
            _comments = [];
            _draftCommentAnchor = null;
            _versionDialogOpen = false;
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
            await RecordOpenAuditAsync(_document.DocumentId, DocumentEditorAuditResult.Success, null);
            await OnDocumentLoaded.InvokeAsync(_document);
        }
        catch (Exception ex)
        {
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
        var request = new DocumentEditorSaveRequest
        {
            DocumentId = (_currentDocument ?? _document).DocumentId,
            Document = _currentDocument ?? _document,
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
                if (!result.Conflict)
                {
                    await SaveOfflineDraftAsync();
                }

                await RecordSaveAuditAsync(trigger, DocumentEditorAuditResult.Failure, _saveMessage);
            }
        }
        catch (Exception ex)
        {
            _saveMessage = Loc["TmDocumentEditor_SaveFailed"];
            await SaveOfflineDraftAsync();
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
        if (_surface is not null)
        {
            await _surface.ToggleInsertPanelAsync();
        }
    }

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
            var result = await PdfExportProvider.ExportPdfAsync(new DocumentPdfExportRequest
            {
                DocumentId = _document.DocumentId,
                Document = _currentDocument ?? _document,
                FileName = string.IsNullOrWhiteSpace(_document.Metadata.Title) ? _document.DocumentId : _document.Metadata.Title,
                Author = Author
            });

            _saveMessage = Loc["TmDocumentEditor_ExportPdfComplete"];
            await RecordExportAuditAsync(result, DocumentEditorAuditResult.Success, null);
            await OnPdfExported.InvokeAsync(result);
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
    }

    private async Task OpenImageDialogAsync()
    {
        if (_surface is not null)
        {
            await _surface.OpenImageDialogAsync();
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

    private Task HandleDocumentChangedAsync(DocumentEditorDocument document)
    {
        _document = document;
        _currentDocument = document;
        _templatePreviewDocument = null;
        _templatePreviewEnabled = false;
        _templatePreviewMessage = null;
        _isDirty = true;
        return Task.CompletedTask;
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

    private async Task SaveOfflineDraftAsync()
    {
        if (!OfflineEnabled || OfflineStore is null || _currentDocument is null)
        {
            return;
        }

        var pendingAssets = CollectPendingAssets(_currentDocument);
        var draft = new DocumentOfflineDraft
        {
            Id = _offlineDraft?.Id ?? Guid.NewGuid().ToString("N"),
            DocumentId = _currentDocument.DocumentId,
            BaseVersionId = _concurrencyToken,
            JsonSnapshot = DocumentEditorJson.Serialize(_currentDocument),
            State = DocumentOfflineDraftState.PendingSync,
            SyncStatus = DocumentSyncStatus.Offline,
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
        _offlineStatus = DocumentSyncStatus.Offline;
        _offlineMessage = Loc["TmDocumentEditor_OfflineDraftSaved"];
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

    private Task HandleSelectionChangedAsync(DocumentEditorSelectionState selection)
    {
        _selection = selection;
        return Task.CompletedTask;
    }

    private async Task BeginCommentFromToolbarAsync()
    {
        if (!CanUseComments || _document is null)
        {
            return;
        }

        var selectionAnchor = _surface is null
            ? null
            : await _surface.CaptureTextSelectionAnchorAsync();

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

    private void ToggleTrackChanges()
    {
        _trackChangesEnabled = !_trackChangesEnabled;
    }

    private async Task UndoAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        await _commandStack.UndoAsync();
        MarkDirtyAfterCommand();
    }

    private async Task RedoAsync()
    {
        if (EffectiveReadOnly)
        {
            return;
        }

        await _commandStack.RedoAsync();
        MarkDirtyAfterCommand();
    }

    private async Task ToggleInlineMarkAsync(InlineMarkType markType)
    {
        if (_surface is not null && !EffectiveReadOnly)
        {
            await _surface.ToggleInlineMarkAsync(markType);
        }
    }

    private async Task ClearInlineFormattingAsync()
    {
        if (_surface is not null && !EffectiveReadOnly)
        {
            await _surface.ClearInlineFormattingAsync();
        }
    }

    private async Task ApplyLinkAsync(string href)
    {
        if (_surface is not null && !EffectiveReadOnly)
        {
            await _surface.ApplyLinkAsync(href);
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
                if (_surface is not null)
                {
                    await _surface.ClosePanelsAsync();
                }
                break;
        }
    }

    private void MarkDirtyAfterCommand()
    {
        if (_document is not null)
        {
            _isDirty = true;
        }

        StateHasChanged();
    }

    private void HandleCommandStackChanged()
    {
        _ = InvokeAsync(StateHasChanged);
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
    }
}
