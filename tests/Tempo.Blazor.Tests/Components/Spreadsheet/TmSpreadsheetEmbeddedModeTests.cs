using Bunit;
using Tempo.Blazor.Components.Spreadsheet;
using Tempo.Blazor.Components.Spreadsheet.Enums;
using Tempo.Blazor.Components.Spreadsheet.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

/// <summary>
/// SS-EMB-01..05: TmSpreadsheet Embedded mód a TmSpreadsheetCanvasGrid IsReadonly parametr.
/// </summary>
public class TmSpreadsheetEmbeddedModeTests : LocalizationTestBase
{
    // ── SS-EMB-01: Embedded mód skryje toolbar ─────────────────────────────

    [Fact]
    public void EmbeddedMode_HidesToolbar()
    {
        var cut = RenderComponent<TmSpreadsheet>(p => p
            .Add(x => x.Mode, SpreadsheetMode.Embedded));

        cut.FindAll(".tm-spreadsheet-toolbar").Should().BeEmpty();
    }

    // ── SS-EMB-02: Embedded mód skryje formula bar ─────────────────────────

    [Fact]
    public void EmbeddedMode_HidesFormulaBar()
    {
        var cut = RenderComponent<TmSpreadsheet>(p => p
            .Add(x => x.Mode, SpreadsheetMode.Embedded));

        cut.FindAll(".tm-spreadsheet-formula-bar").Should().BeEmpty();
    }

    // ── SS-EMB-03: Full mód zobrazuje toolbar ──────────────────────────────

    [Fact]
    public void FullMode_ShowsToolbar()
    {
        var cut = RenderComponent<TmSpreadsheet>(p => p
            .Add(x => x.Mode, SpreadsheetMode.Full));

        cut.Find(".tm-spreadsheet-toolbar").Should().NotBeNull();
    }

    // ── SS-EMB-04: Full mód zobrazuje formula bar ─────────────────────────

    [Fact]
    public void FullMode_ShowsFormulaBar()
    {
        var cut = RenderComponent<TmSpreadsheet>(p => p
            .Add(x => x.Mode, SpreadsheetMode.Full));

        cut.Find(".tm-spreadsheet-formula-bar").Should().NotBeNull();
    }

    // ── SS-EMB-05: Embedded mód předá IsReadonly=true do canvas gridu ─────

    [Fact]
    public void EmbeddedMode_CanvasGrid_HasReadonlyAttribute()
    {
        var cut = RenderComponent<TmSpreadsheet>(p => p
            .Add(x => x.Mode, SpreadsheetMode.Embedded));

        var grid = cut.Find(".tm-spreadsheet-canvas-grid");
        grid.GetAttribute("data-readonly").Should().Be("true");
    }

    // ── SS-EMB-06: CanvasGrid IsReadonly=true nastaví data-readonly ────────

    [Fact]
    public void CanvasGrid_IsReadonly_SetsDataAttribute()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };

        var cut = RenderComponent<TmSpreadsheetCanvasGrid>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.IsReadonly, true));

        var root = cut.Find(".tm-spreadsheet-canvas-grid");
        root.GetAttribute("data-readonly").Should().Be("true");
    }

    // ── SS-EMB-07: CanvasGrid IsReadonly=false nemá data-readonly ──────────

    [Fact]
    public void CanvasGrid_NotReadonly_NoDataAttribute()
    {
        var sheet = new SpreadsheetSheet { Name = "Sheet1" };

        var cut = RenderComponent<TmSpreadsheetCanvasGrid>(p => p
            .Add(x => x.Sheet, sheet)
            .Add(x => x.IsReadonly, false));

        var root = cut.Find(".tm-spreadsheet-canvas-grid");
        root.GetAttribute("data-readonly").Should().BeNullOrEmpty();
    }
}
