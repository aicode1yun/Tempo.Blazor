using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Displays a normalized signing document page and optional overlays.</summary>
public partial class TmDocumentPageViewer
{
    private static readonly double[] ZoomSteps = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];
    private double _scale = 1.0;
    private DocumentPageZoomMode _zoomMode = DocumentPageZoomMode.Custom;
    private DocumentCommentAnchor? _draftAnchor;
    private IReadOnlyList<DocumentCommentMention> _draftMentions = [];
    private bool _isCommentPointerDown;
    private double _commentStartX;
    private double _commentStartY;
    private ElementReference _pageElement;

    /// <summary>Optional id assigned to the rendered page element.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Document page to render. When null, an empty state is shown.</summary>
    [Parameter] public SigningDocumentPage? Page { get; set; }

    /// <summary>Whether to show the loading skeleton instead of the page.</summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>Error message shown instead of the page.</summary>
    [Parameter] public string? Error { get; set; }

    /// <summary>Optional alt text for the page image. Defaults to page label or localized page number.</summary>
    [Parameter] public string? Alt { get; set; }

    /// <summary>Whether overlay content should receive pointer events. Defaults to true.</summary>
    [Parameter] public bool IsOverlayInteractive { get; set; } = true;

    /// <summary>Whether to prevent the browser context menu on the page element. Defaults to true.</summary>
    [Parameter] public bool PreventDefaultContextMenu { get; set; } = true;

    /// <summary>Current visual scale. Values are clamped between <see cref="MinScale"/> and <see cref="MaxScale"/>.</summary>
    [Parameter] public double Scale { get; set; } = 1.0;

    /// <summary>Callback invoked when the visual scale changes.</summary>
    [Parameter] public EventCallback<double> ScaleChanged { get; set; }

    /// <summary>Smallest allowed visual scale. Defaults to 0.5.</summary>
    [Parameter] public double MinScale { get; set; } = 0.5;

    /// <summary>Largest allowed visual scale. Defaults to 2.0.</summary>
    [Parameter] public double MaxScale { get; set; } = 2.0;

    /// <summary>Current zoom behavior. Defaults to <see cref="DocumentPageZoomMode.Custom"/>.</summary>
    [Parameter] public DocumentPageZoomMode ZoomMode { get; set; } = DocumentPageZoomMode.Custom;

    /// <summary>Callback invoked when the zoom behavior changes.</summary>
    [Parameter] public EventCallback<DocumentPageZoomMode> ZoomModeChanged { get; set; }

    /// <summary>Current page view mode for parent tools. Defaults to <see cref="DocumentPageViewMode.SinglePage"/>.</summary>
    [Parameter] public DocumentPageViewMode ViewMode { get; set; } = DocumentPageViewMode.SinglePage;

    /// <summary>Callback invoked when the page view mode changes.</summary>
    [Parameter] public EventCallback<DocumentPageViewMode> ViewModeChanged { get; set; }

    /// <summary>Whether to render the viewer toolbar. Defaults to false for layout compatibility.</summary>
    [Parameter] public bool ShowToolbar { get; set; }

    /// <summary>Whether zoom controls are visible when the toolbar is rendered. Defaults to true.</summary>
    [Parameter] public bool ShowZoomControls { get; set; } = true;

    /// <summary>Whether page navigation controls are visible when the toolbar is rendered. Defaults to false.</summary>
    [Parameter] public bool ShowPaginationControls { get; set; }

    /// <summary>Displayed current page number for pagination controls. Defaults to the page index plus one.</summary>
    [Parameter] public int? PageNumber { get; set; }

    /// <summary>Total displayed page count for pagination controls.</summary>
    [Parameter] public int? TotalPages { get; set; }

    /// <summary>Callback invoked when the previous page button is clicked.</summary>
    [Parameter] public EventCallback PreviousPageRequested { get; set; }

    /// <summary>Callback invoked when the next page button is clicked.</summary>
    [Parameter] public EventCallback NextPageRequested { get; set; }

    /// <summary>Optional template for replacing the rendered page image.</summary>
    [Parameter] public RenderFragment<SigningDocumentPage>? PageTemplate { get; set; }

    /// <summary>Overlay content rendered over the document page.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Overlay template rendered over the document page with page context.</summary>
    [Parameter] public RenderFragment<SigningDocumentPage>? OverlayTemplate { get; set; }

    /// <summary>Callback invoked when the page is clicked.</summary>
    [Parameter] public EventCallback<TmDocumentPageViewerPointerEventArgs> OnPageClick { get; set; }

    /// <summary>Callback invoked when the page context menu is requested.</summary>
    [Parameter] public EventCallback<TmDocumentPageViewerPointerEventArgs> OnPageContextMenu { get; set; }

    /// <summary>Whether document comments are enabled for this viewer.</summary>
    [Parameter] public bool CommentsEnabled { get; set; }

    /// <summary>Current document comment mode. Defaults to browse mode.</summary>
    [Parameter] public DocumentCommentMode CommentMode { get; set; } = DocumentCommentMode.Browse;

    /// <summary>Callback invoked when the comment mode changes.</summary>
    [Parameter] public EventCallback<DocumentCommentMode> CommentModeChanged { get; set; }

    /// <summary>Comment threads rendered over the document page.</summary>
    [Parameter] public IReadOnlyList<DocumentCommentThread> CommentThreads { get; set; } = [];

    /// <summary>Selected comment thread id.</summary>
    [Parameter] public string? SelectedCommentThreadId { get; set; }

    /// <summary>Callback invoked when the selected comment thread changes.</summary>
    [Parameter] public EventCallback<string?> SelectedCommentThreadIdChanged { get; set; }

    /// <summary>Whether resolved comment threads are visible. Defaults to false.</summary>
    [Parameter] public bool ShowResolvedComments { get; set; }

    /// <summary>Whether comment interactions are disabled. Defaults to false.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Whether a plain page click clears the selected comment thread. Defaults to true.</summary>
    [Parameter] public bool ClearCommentSelectionOnPageClick { get; set; } = true;

    /// <summary>Current user id used for mention and reaction indicators.</summary>
    [Parameter] public string? CurrentUserId { get; set; }

    /// <summary>Users available for comment mentions.</summary>
    [Parameter] public IReadOnlyList<DocumentCommentUser> MentionUsers { get; set; } = [];

    /// <summary>Whether replies are allowed on resolved comment threads.</summary>
    [Parameter] public bool AllowReplyToResolved { get; set; }

    /// <summary>Optional custom marker template for comment anchors.</summary>
    [Parameter] public RenderFragment<DocumentCommentThread>? CommentMarkerTemplate { get; set; }

    /// <summary>Optional custom panel for rendering comment threads.</summary>
    [Parameter] public RenderFragment<IReadOnlyList<DocumentCommentThread>>? CommentThreadPanelTemplate { get; set; }

    /// <summary>Callback invoked when creating a new comment thread is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentThreadCreateRequest> OnCommentThreadCreateRequested { get; set; }

    /// <summary>Callback invoked when replying to a comment thread is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentReplyRequest> OnCommentReplyRequested { get; set; }

    /// <summary>Callback invoked when resolving a comment thread is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentThreadStatusRequest> OnCommentResolveRequested { get; set; }

    /// <summary>Callback invoked when reopening a comment thread is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentThreadStatusRequest> OnCommentReopenRequested { get; set; }

    /// <summary>Callback invoked when editing a document comment is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentEditRequest> OnCommentEditRequested { get; set; }

    /// <summary>Callback invoked when deleting a document comment is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentDeleteRequest> OnCommentDeleteRequested { get; set; }

    /// <summary>Callback invoked when toggling a document comment reaction is requested.</summary>
    [Parameter] public EventCallback<DocumentCommentReactionToggleRequest> OnCommentReactionToggled { get; set; }

    /// <summary>Callback invoked when the comment panel requests navigation to a thread page.</summary>
    [Parameter] public EventCallback<DocumentCommentThreadNavigateRequest> OnCommentThreadNavigateRequested { get; set; }

    /// <summary>Callback invoked when draft mentioned users change.</summary>
    [Parameter] public EventCallback<IReadOnlyList<DocumentCommentMention>> OnCommentMentionedUsersChanged { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnParametersSet()
    {
        _scale = ClampScale(Scale);
        _zoomMode = ZoomMode;
    }

    private string RootClass => string.Join(" ", new[]
    {
        "tm-document-page-viewer",
        ShowToolbar ? "tm-document-page-viewer--with-toolbar" : null,
        CommentsEnabled ? "tm-document-page-viewer--with-comments" : null,
        CommentsEnabled && CommentMode == DocumentCommentMode.Comment ? "tm-document-page-viewer--comment-mode" : null,
        $"tm-document-page-viewer--zoom-{_zoomMode.ToString().ToLowerInvariant()}",
        Class
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private string OverlayClass => IsOverlayInteractive
        ? "tm-document-page-viewer__overlay"
        : "tm-document-page-viewer__overlay tm-document-page-viewer__overlay--readonly";

    private string PageElementId => !string.IsNullOrWhiteSpace(Id)
        ? Id
        : $"tm-document-page-{Page?.PageIndex ?? 0}";

    private string PageStyle
    {
        get
        {
            if (Page is null || Page.Width <= 0 || Page.Height <= 0)
            {
                return string.Empty;
            }

            return string.Create(CultureInfo.InvariantCulture, $"aspect-ratio: {Page.Width} / {Page.Height}; --tm-document-page-scale: {_scale};");
        }
    }

    private string ZoomLabel => string.Create(CultureInfo.InvariantCulture, $"{(int)Math.Round(_scale * 100)}%");

    private int CurrentPageNumber => PageNumber ?? (Page?.PageIndex ?? 0) + 1;

    private int CurrentTotalPages => Math.Max(1, TotalPages ?? CurrentPageNumber);

    private bool CanGoPrevious => CurrentPageNumber > 1 && PreviousPageRequested.HasDelegate;

    private bool CanGoNext => CurrentPageNumber < CurrentTotalPages && NextPageRequested.HasDelegate;

    private bool CanZoomOut => _scale > MinScale + 0.001;

    private bool CanZoomIn => _scale < MaxScale - 0.001;

    private int OpenCommentCount => DocumentCommentHelper.CountOpenThreads(CommentThreads);

    private int MentionCommentCount => DocumentCommentHelper.CountMentionedThreads(CommentThreads, CurrentUserId);

    private string CommentModeButtonClass => CommentMode == DocumentCommentMode.Comment
        ? "tm-document-page-viewer__toolbar-button tm-document-page-viewer__comment-toggle tm-document-page-viewer__comment-toggle--active"
        : "tm-document-page-viewer__toolbar-button tm-document-page-viewer__comment-toggle";

    private bool ShouldPreventCommentPointerDefault => CommentsEnabled && CommentMode == DocumentCommentMode.Comment;

    private string ImageAlt => !string.IsNullOrWhiteSpace(Alt)
        ? Alt
        : Page?.Label ?? Loc["TmDocumentPageViewer_PageAlt", (Page?.PageIndex ?? 0) + 1];

    private string PageAriaLabel => Page?.Label ?? Loc["TmDocumentPageViewer_PageAriaLabel", (Page?.PageIndex ?? 0) + 1];

    private async Task HandlePageClickAsync(MouseEventArgs args)
    {
        if (CommentsEnabled && CommentMode == DocumentCommentMode.Comment)
        {
            return;
        }

        if (CommentsEnabled
            && ClearCommentSelectionOnPageClick
            && !string.IsNullOrWhiteSpace(SelectedCommentThreadId))
        {
            await SelectCommentThreadAsync(null);
        }

        if (Page is not null && OnPageClick.HasDelegate)
        {
            await OnPageClick.InvokeAsync(new TmDocumentPageViewerPointerEventArgs(Page, args));
        }
    }

    private Task HandlePageContextMenuAsync(MouseEventArgs args)
    {
        return Page is null || !OnPageContextMenu.HasDelegate
            ? Task.CompletedTask
            : OnPageContextMenu.InvokeAsync(new TmDocumentPageViewerPointerEventArgs(Page, args));
    }

    private Task GoToPreviousPageAsync()
    {
        return CanGoPrevious ? PreviousPageRequested.InvokeAsync() : Task.CompletedTask;
    }

    private Task GoToNextPageAsync()
    {
        return CanGoNext ? NextPageRequested.InvokeAsync() : Task.CompletedTask;
    }

    private Task ZoomOutAsync()
    {
        var next = ZoomSteps.LastOrDefault(value => value < _scale - 0.001);
        return SetScaleAsync(next <= 0 ? MinScale : next, DocumentPageZoomMode.Custom);
    }

    private Task ZoomInAsync()
    {
        var next = ZoomSteps.FirstOrDefault(value => value > _scale + 0.001);
        return SetScaleAsync(next <= 0 ? MaxScale : next, DocumentPageZoomMode.Custom);
    }

    private Task FitWidthAsync()
    {
        return SetScaleAsync(1.0, DocumentPageZoomMode.FitWidth);
    }

    private Task FitPageAsync()
    {
        return SetScaleAsync(0.85, DocumentPageZoomMode.FitPage);
    }

    private async Task ToggleCommentModeAsync()
    {
        if (Disabled)
        {
            return;
        }

        var next = CommentMode == DocumentCommentMode.Comment
            ? DocumentCommentMode.Browse
            : DocumentCommentMode.Comment;
        CommentMode = next;
        if (next == DocumentCommentMode.Browse)
        {
            _draftAnchor = null;
            _draftMentions = [];
        }

        await CommentModeChanged.InvokeAsync(next);
    }

    private async Task SelectCommentThreadAsync(string? threadId)
    {
        SelectedCommentThreadId = threadId;
        _draftAnchor = null;
        await SelectedCommentThreadIdChanged.InvokeAsync(threadId);
    }

    private Task HandleDraftMentionsChangedAsync(IReadOnlyList<DocumentCommentMention> mentions)
    {
        _draftMentions = mentions;
        return OnCommentMentionedUsersChanged.InvokeAsync(mentions);
    }

    private async Task CreateThreadAsync(DocumentCommentComposerSubmitEventArgs args)
    {
        if (Disabled || _draftAnchor is null || string.IsNullOrWhiteSpace(args.Body))
        {
            return;
        }

        await OnCommentThreadCreateRequested.InvokeAsync(new DocumentCommentThreadCreateRequest
        {
            Anchor = _draftAnchor,
            Body = args.Body,
            Mentions = args.Mentions.Count > 0 ? args.Mentions.ToList() : _draftMentions.ToList()
        });
        _draftAnchor = null;
        _draftMentions = [];
    }

    private async Task CancelDraftAsync()
    {
        _draftAnchor = null;
        _draftMentions = [];
        await _pageElement.FocusAsync();
    }

    private Task HandlePageKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape" && _draftAnchor is not null)
        {
            return CancelDraftAsync();
        }

        return Task.CompletedTask;
    }

    private Task HandleCommentPointerDownAsync(MouseEventArgs args)
    {
        if (!CanCreateCommentDraft(args))
        {
            return Task.CompletedTask;
        }

        _isCommentPointerDown = true;
        _commentStartX = NormalizeOffsetX(args);
        _commentStartY = NormalizeOffsetY(args);
        _draftAnchor = DocumentCommentAnchor.Point(CurrentPageNumber, _commentStartX, _commentStartY);
        return Task.CompletedTask;
    }

    private Task HandleCommentPointerMoveAsync(MouseEventArgs args)
    {
        if (!_isCommentPointerDown || !CanCreateCommentDraft(args))
        {
            return Task.CompletedTask;
        }

        var currentX = NormalizeOffsetX(args);
        var currentY = NormalizeOffsetY(args);
        var width = Math.Abs(currentX - _commentStartX);
        var height = Math.Abs(currentY - _commentStartY);
        if (width > 0.01 || height > 0.01)
        {
            _draftAnchor = DocumentCommentAnchor.Area(
                CurrentPageNumber,
                Math.Min(_commentStartX, currentX),
                Math.Min(_commentStartY, currentY),
                width,
                height);
        }

        return Task.CompletedTask;
    }

    private Task HandleCommentPointerUpAsync(MouseEventArgs args)
    {
        if (!_isCommentPointerDown || !CanCreateCommentDraft(args))
        {
            _isCommentPointerDown = false;
            return Task.CompletedTask;
        }

        var currentX = NormalizeOffsetX(args);
        var currentY = NormalizeOffsetY(args);
        var width = Math.Abs(currentX - _commentStartX);
        var height = Math.Abs(currentY - _commentStartY);
        _draftAnchor = width > 0.01 || height > 0.01
            ? DocumentCommentAnchor.Area(
                CurrentPageNumber,
                Math.Min(_commentStartX, currentX),
                Math.Min(_commentStartY, currentY),
                width,
                height)
            : DocumentCommentAnchor.Point(CurrentPageNumber, currentX, currentY);

        _isCommentPointerDown = false;
        return Task.CompletedTask;
    }

    private bool CanCreateCommentDraft(MouseEventArgs args)
    {
        return CommentsEnabled
            && !Disabled
            && CommentMode == DocumentCommentMode.Comment
            && Page is not null
            && (args.Buttons is 0 or 1);
    }

    private double NormalizeOffsetX(MouseEventArgs args)
    {
        var width = Page?.Width > 0 ? Page.Width : 1;
        return Clamp01(args.OffsetX / width);
    }

    private double NormalizeOffsetY(MouseEventArgs args)
    {
        var height = Page?.Height > 0 ? Page.Height : 1;
        return Clamp01(args.OffsetY / height);
    }

    private static double Clamp01(double value)
    {
        return double.IsFinite(value) ? Math.Min(Math.Max(value, 0), 1) : 0;
    }

    private async Task SetScaleAsync(double scale, DocumentPageZoomMode zoomMode)
    {
        _scale = ClampScale(scale);
        _zoomMode = zoomMode;
        await ScaleChanged.InvokeAsync(_scale);
        await ZoomModeChanged.InvokeAsync(_zoomMode);
    }

    private double ClampScale(double scale)
    {
        var min = Math.Max(0.1, MinScale);
        var max = Math.Max(min, MaxScale);
        return double.IsFinite(scale)
            ? Math.Min(Math.Max(scale, min), max)
            : 1.0;
    }
}
