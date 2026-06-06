using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.Spreadsheet.Data;

namespace Tempo.Blazor.Components.Spreadsheet;

/// <summary>
/// The spreadsheet status bar: selection aggregations (Sum/Average/Count/Numerical count/Min/Max)
/// and a zoom control (slider, +/- buttons and a percentage reset button).
/// </summary>
public partial class TmSpreadsheetStatusBar
{
    /// <summary>Identifies the kinds of aggregation that can be shown or hidden.</summary>
    private enum AggregationKind { Average, Count, CountNumbers, Min, Max, Sum }

    private static readonly AggregationKind[] AllKinds =
    [
        AggregationKind.Average, AggregationKind.Count, AggregationKind.CountNumbers,
        AggregationKind.Min, AggregationKind.Max, AggregationKind.Sum
    ];

    private const int MinPercent = 50;
    private const int MaxPercent = 200;

    private readonly Dictionary<AggregationKind, bool> _visible = new()
    {
        [AggregationKind.Average] = true,
        [AggregationKind.Count] = true,
        [AggregationKind.CountNumbers] = true,
        [AggregationKind.Min] = false,
        [AggregationKind.Max] = false,
        [AggregationKind.Sum] = true,
    };

    private bool _aggregationMenuVisible;
    private double _menuX;
    private double _menuY;

    /// <summary>The aggregation result for the current selection.</summary>
    [Parameter] public SpreadsheetAggregationResult Aggregation { get; set; }

    /// <summary>The current zoom factor (1.0 = 100%).</summary>
    [Parameter] public double Zoom { get; set; } = 1.0;

    /// <summary>Called when the zoom factor changes.</summary>
    [Parameter] public EventCallback<double> OnZoomChanged { get; set; }

    private int ZoomPercent => (int)Math.Round(Math.Clamp(Zoom, MinPercent / 100.0, MaxPercent / 100.0) * 100);

    /// <summary>Aggregations are shown only when more than one non-empty cell is selected.</summary>
    private bool ShowAggregations => Aggregation.Count >= 2;

    private static string FormatNumber(double? value)
        => value is null ? string.Empty : value.Value.ToString("0.######", CultureInfo.CurrentCulture);

    private Task EmitZoom(int percent)
    {
        var clamped = Math.Clamp(percent, MinPercent, MaxPercent);
        return OnZoomChanged.InvokeAsync(clamped / 100.0);
    }

    private Task ZoomIn() => EmitZoom(ZoomPercent + 10);

    private Task ZoomOut() => EmitZoom(ZoomPercent - 10);

    private Task ResetZoom() => EmitZoom(100);

    private Task OnSliderInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
            return EmitZoom(percent);
        return Task.CompletedTask;
    }

    private void OpenAggregationMenu(MouseEventArgs e)
    {
        _menuX = e.OffsetX;
        _menuY = -110;
        _aggregationMenuVisible = true;
    }

    private void ToggleAggregation(AggregationKind kind, ChangeEventArgs e)
    {
        _visible[kind] = e.Value is bool b ? b : !_visible[kind];
    }

    private static string LabelKey(AggregationKind kind) => kind switch
    {
        AggregationKind.Average => "TmSpreadsheet_Status_Average",
        AggregationKind.Count => "TmSpreadsheet_Status_Count",
        AggregationKind.CountNumbers => "TmSpreadsheet_Status_CountNumbers",
        AggregationKind.Min => "TmSpreadsheet_Status_Min",
        AggregationKind.Max => "TmSpreadsheet_Status_Max",
        AggregationKind.Sum => "TmSpreadsheet_Status_Sum",
        _ => string.Empty
    };
}
