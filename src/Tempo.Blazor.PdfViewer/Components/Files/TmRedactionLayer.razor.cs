using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Files;

/// <summary>
/// Redaction layer for PDF documents (over <see cref="TmPdfViewer"/>) and image
/// previews: drag rectangles over sensitive content, categorize them by PII type,
/// preview before/after, persist through <see cref="IRedactionProvider"/>, and export
/// DESTRUCTIVELY — the JS pipeline rasterizes the pages, burns the black rectangles in,
/// and rebuilds a brand-new file from the pixels only, so the original text/image
/// content is not extractable from the export (this is not a visual overlay).
/// </summary>
public partial class TmRedactionLayer : TmComponentBase
{
    private const double MinRectSize = 0.005;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── Parameters ───────────────────────────────────────────────────────────

    /// <summary>URL of the PDF document to redact. Mutually exclusive with <see cref="ImageUrl"/> (PDF wins).</summary>
    [Parameter] public string? Url { get; set; }

    /// <summary>URL of the image to redact, when no PDF is set.</summary>
    [Parameter] public string? ImageUrl { get; set; }

    /// <summary>Document identifier used with <see cref="Provider"/>.</summary>
    [Parameter] public string? DocumentId { get; set; }

    /// <summary>Optional persistence of the redaction definitions.</summary>
    [Parameter] public IRedactionProvider? Provider { get; set; }

    /// <summary>Category assigned to newly drawn areas. Default is Other.</summary>
    [Parameter] public RedactionCategory DefaultCategory { get; set; } = RedactionCategory.Other;

    /// <summary>Whether the area panel is shown. Default is true.</summary>
    [Parameter] public bool ShowPanel { get; set; } = true;

    /// <summary>Viewer height for the PDF mode.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Export file name. Defaults to "redacted.pdf" / "redacted.png" by mode.</summary>
    [Parameter] public string? ExportFileName { get; set; }

    /// <summary>Callback invoked whenever the set of areas changes, with a snapshot.</summary>
    [Parameter] public EventCallback<IReadOnlyList<RedactionArea>> OnAreasChanged { get; set; }

    /// <summary>Callback invoked after a successful export, with the file name.</summary>
    [Parameter] public EventCallback<string> OnExported { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private readonly List<RedactionArea> _areas = [];
    private bool _applied;
    private bool _busy;
    private DateTimeOffset? _savedAt;
    private string? _errorKey;
    private int _currentPage = 1;
    private bool _drawing;
    private double _startX;
    private double _startY;
    private double[]? _surfaceSize;
    private RedactionArea? _activeRect;
    private Microsoft.AspNetCore.Components.ElementReference _surfaceRef;
    private IRedactionProvider? _loadedProvider;
    private string? _loadedDocumentId;

    private bool IsPdfMode => !string.IsNullOrEmpty(Url);

    private IEnumerable<RedactionArea> VisibleAreas
        => IsPdfMode ? _areas.Where(a => a.PageNumber == _currentPage) : _areas;

    private string EffectiveExportFileName
        => ExportFileName ?? (IsPdfMode ? "redacted.pdf" : "redacted.png");

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Provider, _loadedProvider) && string.Equals(DocumentId, _loadedDocumentId, StringComparison.Ordinal))
        {
            return;
        }

        _loadedProvider = Provider;
        _loadedDocumentId = DocumentId;
        _areas.Clear();
        _savedAt = null;
        _errorKey = null;

        if (Provider is not null && !string.IsNullOrEmpty(DocumentId))
        {
            try
            {
                _areas.AddRange((await Provider.LoadAsync(DocumentId)).Select(a => a.Clone()));
            }
            catch
            {
                _errorKey = "TmRedactionLayer_LoadFailed";
            }
        }
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    private async Task HandlePointerDownAsync(PointerEventArgs e)
    {
        try
        {
            _surfaceSize = await JS.InvokeAsync<double[]>("tmRedaction.measure", _surfaceRef);
        }
        catch
        {
            _surfaceSize = null;
        }

        if (_surfaceSize is not [> 0, > 0])
        {
            return;
        }

        _drawing = true;
        _startX = Math.Clamp(e.OffsetX / _surfaceSize[0], 0d, 1d);
        _startY = Math.Clamp(e.OffsetY / _surfaceSize[1], 0d, 1d);
        _activeRect = null;
    }

    private void HandlePointerMove(PointerEventArgs e)
    {
        if (!_drawing || _surfaceSize is not [> 0, > 0])
        {
            return;
        }

        _activeRect = BuildRect(e);
    }

    private async Task HandlePointerUpAsync(PointerEventArgs e)
    {
        if (!_drawing || _surfaceSize is not [> 0, > 0])
        {
            return;
        }

        _drawing = false;
        var rect = BuildRect(e);
        _activeRect = null;
        if (rect.Width < MinRectSize || rect.Height < MinRectSize)
        {
            return;
        }

        rect.Category = DefaultCategory;
        _areas.Add(rect);
        _savedAt = null;
        await OnAreasChanged.InvokeAsync(Snapshot());
    }

    private void HandlePointerLeave(PointerEventArgs e)
    {
        _drawing = false;
        _activeRect = null;
    }

    private RedactionArea BuildRect(PointerEventArgs e)
    {
        var endX = Math.Clamp(e.OffsetX / _surfaceSize![0], 0d, 1d);
        var endY = Math.Clamp(e.OffsetY / _surfaceSize[1], 0d, 1d);
        return new RedactionArea
        {
            PageNumber = IsPdfMode ? _currentPage : 1,
            X = Math.Min(_startX, endX),
            Y = Math.Min(_startY, endY),
            Width = Math.Abs(endX - _startX),
            Height = Math.Abs(endY - _startY)
        };
    }

    // ── Panel actions ────────────────────────────────────────────────────────

    private async Task ChangeCategoryAsync(RedactionArea area, ChangeEventArgs e)
    {
        if (Enum.TryParse<RedactionCategory>(e.Value?.ToString(), out var category))
        {
            area.Category = category;
            await OnAreasChanged.InvokeAsync(Snapshot());
        }
    }

    private async Task ChangeNoteAsync(RedactionArea area, ChangeEventArgs e)
    {
        area.Note = string.IsNullOrEmpty(e.Value?.ToString()) ? null : e.Value!.ToString();
        await OnAreasChanged.InvokeAsync(Snapshot());
    }

    private async Task RemoveAreaAsync(RedactionArea area)
    {
        _areas.Remove(area);
        _savedAt = null;
        await OnAreasChanged.InvokeAsync(Snapshot());
    }

    private void HandlePageChanged(int page) => _currentPage = page;

    // ── Persistence + export ─────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        if (Provider is null || string.IsNullOrEmpty(DocumentId))
        {
            return;
        }

        _busy = true;
        _errorKey = null;
        try
        {
            await Provider.SaveAsync(DocumentId, Snapshot());
            _savedAt = DateTimeOffset.UtcNow;
        }
        catch
        {
            _errorKey = "TmRedactionLayer_SaveFailed";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (_areas.Count == 0 || _busy)
        {
            return;
        }

        _busy = true;
        _errorKey = null;
        await InvokeAsync(StateHasChanged);
        try
        {
            var payload = RedactionExportPayloadBuilder.Build(_areas);
            if (IsPdfMode)
            {
                await JS.InvokeVoidAsync("tmRedaction.exportRedactedPdf", Url, payload, EffectiveExportFileName);
            }
            else
            {
                await JS.InvokeVoidAsync("tmRedaction.exportRedactedImage", ImageUrl, payload, EffectiveExportFileName);
            }

            await OnExported.InvokeAsync(EffectiveExportFileName);
        }
        catch
        {
            _errorKey = "TmRedactionLayer_ExportFailed";
        }
        finally
        {
            _busy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private IReadOnlyList<RedactionArea> Snapshot() => _areas.Select(a => a.Clone()).ToList();

    // ── Display helpers ──────────────────────────────────────────────────────

    private static string RectStyle(RedactionArea area)
        => string.Create(CultureInfo.InvariantCulture,
            $"left:{area.X * 100:0.###}%;top:{area.Y * 100:0.###}%;width:{area.Width * 100:0.###}%;height:{area.Height * 100:0.###}%;");

    private string CategoryLabel(RedactionCategory category)
        => category switch
        {
            RedactionCategory.PersonalId => Loc["TmRedactionLayer_Category_PersonalId"],
            RedactionCategory.Name => Loc["TmRedactionLayer_Category_Name"],
            RedactionCategory.Address => Loc["TmRedactionLayer_Category_Address"],
            RedactionCategory.Contact => Loc["TmRedactionLayer_Category_Contact"],
            RedactionCategory.BankAccount => Loc["TmRedactionLayer_Category_BankAccount"],
            RedactionCategory.Date => Loc["TmRedactionLayer_Category_Date"],
            _ => Loc["TmRedactionLayer_Category_Other"]
        };
}
