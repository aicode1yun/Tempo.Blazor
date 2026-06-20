using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;
using ZXing;
using ZXing.Common;

namespace Tempo.Blazor.Components.DataDisplay;

/// <summary>Renders a barcode as an SVG element.</summary>
public partial class TmBarcode : ComponentBase
{
    private string? _svgContent;
    private string? _errorMessage;

    /// <summary>The value to encode.</summary>
    [Parameter] public string Value { get; set; } = string.Empty;

    /// <summary>The barcode format. Default is Code128.</summary>
    [Parameter] public BarcodeFormat Format { get; set; } = BarcodeFormat.Code128;

    /// <summary>The width in pixels. Default is 200.</summary>
    [Parameter] public int Width { get; set; } = 200;

    /// <summary>The height in pixels. Default is 100.</summary>
    [Parameter] public int Height { get; set; } = 100;

    /// <summary>Whether to display the value as text below the barcode.</summary>
    [Parameter] public bool DisplayValue { get; set; }

    /// <summary>Optional CSS class.</summary>
    [Parameter] public string? CssClass { get; set; }

    protected override void OnParametersSet()
    {
        GenerateBarcode();
        base.OnParametersSet();
    }

    private void GenerateBarcode()
    {
        _svgContent = null;
        _errorMessage = null;

        if (string.IsNullOrEmpty(Value))
        {
            return;
        }

        try
        {
            var zxingFormat = Format switch
            {
                BarcodeFormat.Code128 => ZXing.BarcodeFormat.CODE_128,
                BarcodeFormat.Code39 => ZXing.BarcodeFormat.CODE_39,
                BarcodeFormat.EAN13 => ZXing.BarcodeFormat.EAN_13,
                BarcodeFormat.UPC => ZXing.BarcodeFormat.UPC_A,
                BarcodeFormat.QR => ZXing.BarcodeFormat.QR_CODE,
                _ => ZXing.BarcodeFormat.CODE_128
            };

            var writer = new BarcodeWriterSvg
            {
                Format = zxingFormat,
                Options = new EncodingOptions
                {
                    Width = Width,
                    Height = Height,
                    Margin = 10
                }
            };

            var svgImage = writer.Write(Value);
            var svg = svgImage.ToString();

            // ZXing always renders the value as a <text> element; remove it so DisplayValue controls visibility
            svg = Regex.Replace(svg, @"<text[^>]*>.*?</text>", string.Empty);

            // Add width/height attributes if missing on root svg (ZXing uses viewBox only)
            if (!Regex.IsMatch(svg, @"<svg\b[^>]*\bwidth="""))
            {
                svg = Regex.Replace(svg, @"<svg\b", $"<svg width=\"{Width}\" height=\"{Height}\"");
            }
            else
            {
                svg = new Regex(@"<svg\b[^>]*\bwidth=""[^""]*""").Replace(svg, $"width=\"{Width}\"", 1);
                svg = new Regex(@"<svg\b[^>]*\bheight=""[^""]*""").Replace(svg, $"height=\"{Height}\"", 1);
            }

            _svgContent = svg;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Invalid value for {Format}: {ex.Message}";
        }
    }
}
