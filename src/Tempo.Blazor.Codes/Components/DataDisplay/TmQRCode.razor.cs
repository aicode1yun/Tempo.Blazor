using Microsoft.AspNetCore.Components;
using QRCoder;
using System.Text.RegularExpressions;

namespace Tempo.Blazor.Components.DataDisplay;

/// <summary>Renders a QR code as an SVG element.</summary>
public partial class TmQRCode : ComponentBase
{
    private string? _svgContent;

    /// <summary>The text or URL to encode.</summary>
    [Parameter] public string Value { get; set; } = string.Empty;

    /// <summary>The size in pixels. Default is 200.</summary>
    [Parameter] public int Size { get; set; } = 200;

    /// <summary>Error correction level. Default is M.</summary>
    [Parameter] public QRErrorCorrectionLevel ErrorCorrectionLevel { get; set; } = QRErrorCorrectionLevel.M;

    /// <summary>Foreground color in hex format. Default is #000000.</summary>
    [Parameter] public string ForegroundColor { get; set; } = "#000000";

    /// <summary>Background color in hex format. Default is #ffffff.</summary>
    [Parameter] public string BackgroundColor { get; set; } = "#ffffff";

    /// <summary>Optional CSS class.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        GenerateQrCode();
        base.OnParametersSet();
    }

    private void GenerateQrCode()
    {
        if (string.IsNullOrEmpty(Value))
        {
            _svgContent = null;
            return;
        }

        var eccLevel = ErrorCorrectionLevel switch
        {
            QRErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
            QRErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
            QRErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
            QRErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(Value, eccLevel);
        using var qr = new SvgQRCode(data);
        var svg = qr.GetGraphic(10, ForegroundColor, BackgroundColor);

        // Extract original dimensions before replacement so we can set a proper viewBox
        var origWidthMatch = Regex.Match(svg, @"width=""([^""]*)""");
        var origHeightMatch = Regex.Match(svg, @"height=""([^""]*)""");
        var origWidth = origWidthMatch.Success ? origWidthMatch.Groups[1].Value : Size.ToString();
        var origHeight = origHeightMatch.Success ? origHeightMatch.Groups[1].Value : Size.ToString();

        // Add viewBox so the graphic scales correctly to the requested size
        if (!svg.Contains("viewBox"))
        {
            svg = Regex.Replace(svg, @"<svg\b", $"<svg viewBox=\"0 0 {origWidth} {origHeight}\"");
        }

        // Override width/height to match requested Size (only first occurrence = svg element)
        svg = new Regex(@"width=""[^""]*""").Replace(svg, $"width=\"{Size}\"", 1);
        svg = new Regex(@"height=""[^""]*""").Replace(svg, $"height=\"{Size}\"", 1);

        // Add background color as style on the svg element if not already present
        if (!svg.Contains("style="))
        {
            svg = Regex.Replace(svg, @"<svg\b", $"<svg style=\"background-color: {BackgroundColor};\"");
        }

        _svgContent = svg;
    }
}
