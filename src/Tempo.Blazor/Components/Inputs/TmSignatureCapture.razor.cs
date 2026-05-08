using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Tempo.Blazor.Components.Inputs;

/// <summary>Captures signatures by drawing, typing, or uploading an image.</summary>
public partial class TmSignatureCapture
{
    private readonly List<Stroke> _strokes = [];
    private Stroke? _currentStroke;
    private bool _isDrawing;
    private string? _typedText;
    private string? _reason;
    private bool _rememberSignature;
    private ElementReference _canvasRef;

    /// <summary>JavaScript runtime used for optional PNG export.</summary>
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>Captured signature value.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Callback invoked when the captured signature value changes.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Callback invoked when the signature value or signing metadata changes.</summary>
    [Parameter] public EventCallback<TmSignatureCaptureChangedEventArgs> Changed { get; set; }

    /// <summary>Current capture mode. Defaults to draw mode.</summary>
    [Parameter] public TmSignatureCaptureMode Mode { get; set; } = TmSignatureCaptureMode.Draw;

    /// <summary>Capture modes shown in the mode selector.</summary>
    [Parameter] public IReadOnlyList<TmSignatureCaptureMode>? Modes { get; set; }

    /// <summary>Export format used when drawing signatures. Defaults to SVG.</summary>
    [Parameter] public TmSignatureCaptureExportFormat ExportFormat { get; set; } = TmSignatureCaptureExportFormat.Svg;

    /// <summary>Whether the signature is required.</summary>
    [Parameter] public bool Required { get; set; }

    /// <summary>Whether the component is disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Whether typed mode should collect initials instead of a full signature.</summary>
    [Parameter] public bool Initials { get; set; }

    /// <summary>Typed signature font key. Supported values are script, serif, and sans.</summary>
    [Parameter] public string TypedFont { get; set; } = "script";

    /// <summary>Stroke color used for draw mode. Defaults to the current text color.</summary>
    [Parameter] public string StrokeColor { get; set; } = "currentColor";

    /// <summary>Stroke width used for draw mode.</summary>
    [Parameter] public double StrokeWidth { get; set; } = 2.4;

    /// <summary>Canvas width used for SVG export.</summary>
    [Parameter] public int Width { get; set; } = 520;

    /// <summary>Canvas height used for SVG export.</summary>
    [Parameter] public int Height { get; set; } = 180;

    /// <summary>Optional SVG background color.</summary>
    [Parameter] public string? BackgroundColor { get; set; }

    /// <summary>Whether a reason input should be shown.</summary>
    [Parameter] public bool RequireReason { get; set; }

    /// <summary>Whether to show the remember signature checkbox.</summary>
    [Parameter] public bool ShowRememberSignature { get; set; }

    /// <summary>Whether to show a QR/mobile signing button.</summary>
    [Parameter] public bool ShowQrSigningButton { get; set; }

    /// <summary>Previously saved signature value that can be reused.</summary>
    [Parameter] public string? PreviousValue { get; set; }

    /// <summary>Callback invoked when the upload flow should be opened.</summary>
    [Parameter] public EventCallback OnUploadRequested { get; set; }

    /// <summary>Callback invoked when QR/mobile signing is requested.</summary>
    [Parameter] public EventCallback OnQrSigningRequested { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Whether the component currently has no signature value or strokes.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value)
        && string.IsNullOrWhiteSpace(_typedText)
        && _strokes.Count == 0;

    private IReadOnlyList<TmSignatureCaptureMode> AvailableModes => Modes is { Count: > 0 }
        ? Modes
        : [TmSignatureCaptureMode.Draw, TmSignatureCaptureMode.Typed, TmSignatureCaptureMode.Upload];

    private bool IsInvalid => Required && IsEmpty;

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-signature-capture" };
            AddClass(classes, Disabled, "tm-signature-capture--disabled");
            AddClass(classes, IsInvalid, "tm-signature-capture--invalid");
            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class);
            }

            return string.Join(" ", classes);
        }
    }

    private string CanvasViewBox => string.Create(CultureInfo.InvariantCulture, $"0 0 {Width} {Height}");

    private string TypedAriaLabel => Initials
        ? Loc["TmSignatureCapture_InitialsInput"]
        : Loc["TmSignatureCapture_TypedInput"];

    private string TypedPlaceholder => Initials
        ? Loc["TmSignatureCapture_InitialsPlaceholder"]
        : Loc["TmSignatureCapture_TypedPlaceholder"];

    private string TypedPreviewText => string.IsNullOrWhiteSpace(_typedText)
        ? TypedPlaceholder
        : _typedText;

    private string TypedPreviewClass => string.Join(
        " ",
        "tm-signature-capture__typed-preview",
        $"tm-signature-capture__typed-preview--{NormalizeFont(TypedFont)}");

    private static void AddClass(List<string> classes, bool condition, string cssClass)
    {
        if (condition)
        {
            classes.Add(cssClass);
        }
    }

    private static string BoolText(bool value) => value ? "true" : "false";

    private string GetModeButtonClass(TmSignatureCaptureMode mode)
    {
        return mode == Mode
            ? "tm-signature-capture__tab tm-signature-capture__tab--active"
            : "tm-signature-capture__tab";
    }

    private string GetModeLabel(TmSignatureCaptureMode mode)
    {
        return mode switch
        {
            TmSignatureCaptureMode.Draw => Loc["TmSigning_Draw"],
            TmSignatureCaptureMode.Typed => Loc["TmSigning_Type"],
            TmSignatureCaptureMode.Upload => Loc["TmSigning_Upload"],
            _ => mode.ToString()
        };
    }

    private async Task SetModeAsync(TmSignatureCaptureMode mode)
    {
        if (Disabled || Mode == mode)
        {
            return;
        }

        Mode = mode;
        if (Mode == TmSignatureCaptureMode.Typed && string.IsNullOrWhiteSpace(_typedText))
        {
            _typedText = Value?.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) == true
                ? null
                : Value;
        }

        await Task.CompletedTask;
    }

    private void HandlePointerDown(PointerEventArgs args)
    {
        if (Disabled || Mode != TmSignatureCaptureMode.Draw)
        {
            return;
        }

        _isDrawing = true;
        _currentStroke = new Stroke(StrokeColor, StrokeWidth);
        _currentStroke.AddPoint(args.OffsetX, args.OffsetY);
        _strokes.Add(_currentStroke);
    }

    private void HandlePointerMove(PointerEventArgs args)
    {
        if (Disabled || !_isDrawing || _currentStroke is null)
        {
            return;
        }

        _currentStroke.AddPoint(args.OffsetX, args.OffsetY);
    }

    private async Task HandlePointerUpAsync(PointerEventArgs args)
    {
        if (Disabled || !_isDrawing)
        {
            return;
        }

        if (_currentStroke is { PointCount: 1 })
        {
            _currentStroke.AddPoint(args.OffsetX, args.OffsetY);
        }

        _isDrawing = false;
        _currentStroke = null;
        await CommitValueAsync(await ExportDrawValueAsync());
    }

    private async Task HandleTypedInputAsync(ChangeEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        _typedText = args.Value?.ToString();
        await CommitValueAsync(BuildTypedSvgString());
    }

    private async Task HandleFontChangedAsync(ChangeEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        TypedFont = NormalizeFont(args.Value?.ToString());
        if (!string.IsNullOrWhiteSpace(_typedText))
        {
            await CommitValueAsync(BuildTypedSvgString());
        }
    }

    private void HandleReasonChanged(ChangeEventArgs args)
    {
        _reason = args.Value?.ToString();
    }

    private void HandleRememberChanged(ChangeEventArgs args)
    {
        _rememberSignature = args.Value is bool boolValue && boolValue;
    }

    private async Task HandleUploadChangedAsync(InputFileChangeEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        var fileName = args.File.Name;
        var value = string.Create(CultureInfo.InvariantCulture, $"upload://{Uri.EscapeDataString(fileName)}");
        await CommitValueAsync(value);
    }

    private Task HandleUploadRequestedAsync()
    {
        return Disabled || !OnUploadRequested.HasDelegate
            ? Task.CompletedTask
            : OnUploadRequested.InvokeAsync();
    }

    private Task HandleQrSigningRequestedAsync()
    {
        return Disabled || !OnQrSigningRequested.HasDelegate
            ? Task.CompletedTask
            : OnQrSigningRequested.InvokeAsync();
    }

    /// <summary>Clears the captured signature and notifies value callbacks.</summary>
    public async Task ClearAsync()
    {
        if (Disabled)
        {
            return;
        }

        _strokes.Clear();
        _currentStroke = null;
        _isDrawing = false;
        _typedText = null;
        await CommitValueAsync(null);
        StateHasChanged();
    }

    private async Task ReusePreviousAsync()
    {
        if (Disabled || string.IsNullOrWhiteSpace(PreviousValue))
        {
            return;
        }

        await CommitValueAsync(PreviousValue);
    }

    private async Task CommitValueAsync(string? value)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        if (Changed.HasDelegate)
        {
            await Changed.InvokeAsync(new TmSignatureCaptureChangedEventArgs(
                value,
                Mode,
                _reason,
                _rememberSignature));
        }
    }

    private async Task<string?> ExportDrawValueAsync()
    {
        var svg = BuildDrawSvgString();
        if (ExportFormat == TmSignatureCaptureExportFormat.Svg)
        {
            return svg;
        }

        try
        {
            var png = await JSRuntime.InvokeAsync<string>("tmSignatureCapture.exportPng", _canvasRef);
            return string.IsNullOrWhiteSpace(png) ? svg : png;
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            return svg;
        }
    }

    private string BuildDrawSvgString()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {Width} {Height}\" width=\"{Width}\" height=\"{Height}\">");
        if (!string.IsNullOrWhiteSpace(BackgroundColor))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<rect width=\"100%\" height=\"100%\" fill=\"{BackgroundColor}\"/>");
        }

        foreach (var stroke in _strokes)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<polyline points=\"{stroke.Points}\" fill=\"none\" stroke=\"{stroke.Color}\" stroke-width=\"{stroke.Width.ToString("0.0", CultureInfo.InvariantCulture)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private string? BuildTypedSvgString()
    {
        if (string.IsNullOrWhiteSpace(_typedText))
        {
            return null;
        }

        var escaped = System.Net.WebUtility.HtmlEncode(_typedText);
        var fontFamily = NormalizeFont(TypedFont) switch
        {
            "serif" => "Georgia, serif",
            "sans" => "Arial, sans-serif",
            _ => "cursive"
        };

        return string.Create(
            CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {Width} {Height}\" width=\"{Width}\" height=\"{Height}\"><text x=\"24\" y=\"{Height / 2}\" dominant-baseline=\"middle\" font-family=\"{fontFamily}\" font-size=\"48\" fill=\"currentColor\">{escaped}</text></svg>");
    }

    private RenderFragment RenderPreview(string value) => builder =>
    {
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            builder.OpenElement(0, "img");
            builder.AddAttribute(1, "class", "tm-signature-capture__preview-image");
            builder.AddAttribute(2, "src", value);
            builder.AddAttribute(3, "alt", Loc["TmSignatureCapture_CurrentPreview"]);
            builder.CloseElement();
            return;
        }

        if (value.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            builder.OpenElement(4, "img");
            builder.AddAttribute(5, "class", "tm-signature-capture__preview-image tm-signature-capture__preview-svg");
            builder.AddAttribute(6, "src", "data:image/svg+xml," + Uri.EscapeDataString(value));
            builder.AddAttribute(7, "alt", Loc["TmSignatureCapture_CurrentPreview"]);
            builder.CloseElement();
            return;
        }

        builder.OpenElement(7, "span");
        builder.AddAttribute(8, "class", "tm-signature-capture__preview-text");
        builder.AddContent(9, value);
        builder.CloseElement();
    };

    private static string NormalizeFont(string? font)
    {
        return font?.Trim().ToLowerInvariant() switch
        {
            "serif" => "serif",
            "sans" => "sans",
            _ => "script"
        };
    }

    private sealed class Stroke(string color, double width)
    {
        private readonly StringBuilder _points = new();

        public string Color { get; } = color;

        public double Width { get; } = width;

        public int PointCount { get; private set; }

        public string Points => _points.ToString();

        public void AddPoint(double x, double y)
        {
            if (PointCount > 0)
            {
                _points.Append(' ');
            }

            _points.AppendFormat(CultureInfo.InvariantCulture, "{0:0.0},{1:0.0}", x, y);
            PointCount++;
        }
    }
}
