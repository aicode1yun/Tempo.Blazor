using Tempo.Blazor.Components.Spreadsheet.Commands;
using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SetDataValidationCommandTests
{
    private static SpreadsheetDataValidation MakeRule(int startRow, int endRow, SpreadsheetValidationType type = SpreadsheetValidationType.List)
        => new()
        {
            Range = new SpreadsheetRange(startRow, 0, endRow, 0),
            Type = type,
            Formula1 = "Yes,No",
            AllowBlank = true,
            ShowDropDown = true
        };

    // ── SetDataValidationCommand ─────────────────────────────────────────────

    [Fact]
    public void Execute_AddsValidationToSheet()
    {
        var sheet = new SpreadsheetSheet();
        var rule = MakeRule(0, 4);

        new SetDataValidationCommand(sheet, rule).Execute();

        sheet.DataValidations.Should().HaveCount(1);
        sheet.DataValidations[0].Type.Should().Be(SpreadsheetValidationType.List);
    }

    [Fact]
    public void Execute_SetsCellValidationRefs()
    {
        var sheet = new SpreadsheetSheet();
        var rule = MakeRule(0, 2);

        new SetDataValidationCommand(sheet, rule).Execute();

        for (var r = 0; r <= 2; r++)
        {
            var cell = sheet.GetOrCreateCell($"A{r + 1}");
            cell.Validation.Should().NotBeNull();
            cell.Validation!.Type.Should().Be(SpreadsheetValidationType.List);
        }
    }

    [Fact]
    public void Execute_ReplacesExistingRuleForSameRange()
    {
        var sheet = new SpreadsheetSheet();
        // Both rules use the same range (0,0)-(4,0)
        new SetDataValidationCommand(sheet, MakeRule(0, 4, SpreadsheetValidationType.Whole)).Execute();
        new SetDataValidationCommand(sheet, MakeRule(0, 4, SpreadsheetValidationType.List)).Execute();

        sheet.DataValidations.Should().HaveCount(1);
        sheet.DataValidations[0].Type.Should().Be(SpreadsheetValidationType.List);
    }

    [Fact]
    public void Undo_RemovesAddedRuleAndRestoresPrevious()
    {
        var sheet = new SpreadsheetSheet();
        var first = MakeRule(0, 4, SpreadsheetValidationType.Whole);
        var firstCmd = new SetDataValidationCommand(sheet, first);
        firstCmd.Execute();

        var second = MakeRule(0, 4, SpreadsheetValidationType.List);
        var secondCmd = new SetDataValidationCommand(sheet, second);
        secondCmd.Execute();

        sheet.DataValidations[0].Type.Should().Be(SpreadsheetValidationType.List);

        secondCmd.Undo();

        sheet.DataValidations.Should().HaveCount(1);
        sheet.DataValidations[0].Type.Should().Be(SpreadsheetValidationType.Whole);
    }

    [Fact]
    public void Undo_ClearsCellRefsWhenNoPreviousRule()
    {
        var sheet = new SpreadsheetSheet();
        var cmd = new SetDataValidationCommand(sheet, MakeRule(0, 1));
        cmd.Execute();

        cmd.Undo();

        sheet.DataValidations.Should().BeEmpty();
        var cell = sheet.Cells.GetValueOrDefault("A1");
        cell?.Validation.Should().BeNull();
    }

    // ── ClearDataValidationCommand ───────────────────────────────────────────

    [Fact]
    public void Clear_Execute_RemovesOverlappingRules()
    {
        var sheet = new SpreadsheetSheet();
        new SetDataValidationCommand(sheet, MakeRule(0, 4)).Execute();
        new SetDataValidationCommand(sheet, MakeRule(10, 14)).Execute();

        var clearCmd = new ClearDataValidationCommand(sheet, new SpreadsheetRange(0, 0, 4, 0));
        clearCmd.Execute();

        sheet.DataValidations.Should().HaveCount(1);
        sheet.DataValidations[0].Range.StartRow.Should().Be(10);
    }

    [Fact]
    public void Clear_Undo_RestoresRemovedRules()
    {
        var sheet = new SpreadsheetSheet();
        new SetDataValidationCommand(sheet, MakeRule(0, 4)).Execute();

        var clearCmd = new ClearDataValidationCommand(sheet, new SpreadsheetRange(0, 0, 4, 0));
        clearCmd.Execute();
        clearCmd.Undo();

        sheet.DataValidations.Should().HaveCount(1);
        sheet.DataValidations[0].Type.Should().Be(SpreadsheetValidationType.List);

        var cell = sheet.GetOrCreateCell("A1");
        cell.Validation.Should().NotBeNull();
    }

    [Fact]
    public void Clear_ClearsCellValidationRefsForRange()
    {
        var sheet = new SpreadsheetSheet();
        new SetDataValidationCommand(sheet, MakeRule(0, 2)).Execute();

        new ClearDataValidationCommand(sheet, new SpreadsheetRange(0, 0, 2, 0)).Execute();

        sheet.GetOrCreateCell("A1").Validation.Should().BeNull();
        sheet.GetOrCreateCell("A3").Validation.Should().BeNull();
    }
}
