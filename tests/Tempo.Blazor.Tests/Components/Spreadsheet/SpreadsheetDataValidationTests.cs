using Tempo.Blazor.Components.Spreadsheet.Data;
using Tempo.Blazor.Components.Spreadsheet.Models;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class SpreadsheetDataValidationTests
{
    [Fact]
    public void Clone_ProducesDeepCopy()
    {
        var dv = new SpreadsheetDataValidation
        {
            Range = new SpreadsheetRange(0, 0, 5, 2),
            Type = SpreadsheetValidationType.Whole,
            Operator = SpreadsheetValidationOperator.Between,
            Formula1 = "1",
            Formula2 = "10",
            AllowBlank = false,
            ShowDropDown = false,
            InputMessage = new SpreadsheetInputMessage { Title = "T", Message = "M" },
            ErrorAlert = new SpreadsheetValidationErrorAlert { Style = SpreadsheetValidationErrorStyle.Warning, Title = "E", Message = "EM" }
        };

        var clone = dv.DeepClone();

        clone.Should().NotBeSameAs(dv);
        clone.Range.StartRow.Should().Be(0);
        clone.Range.EndRow.Should().Be(5);
        clone.Type.Should().Be(SpreadsheetValidationType.Whole);
        clone.Operator.Should().Be(SpreadsheetValidationOperator.Between);
        clone.Formula1.Should().Be("1");
        clone.Formula2.Should().Be("10");
        clone.AllowBlank.Should().BeFalse();
        clone.ShowDropDown.Should().BeFalse();
        clone.InputMessage.Should().NotBeSameAs(dv.InputMessage);
        clone.InputMessage!.Title.Should().Be("T");
        clone.ErrorAlert.Should().NotBeSameAs(dv.ErrorAlert);
        clone.ErrorAlert!.Style.Should().Be(SpreadsheetValidationErrorStyle.Warning);
    }

    [Fact]
    public void SpreadsheetSheet_DataValidations_IsEmpty_ByDefault()
    {
        var sheet = new SpreadsheetSheet();
        sheet.DataValidations.Should().NotBeNull();
        sheet.DataValidations.Should().BeEmpty();
    }

    [Fact]
    public void SpreadsheetSheet_Clone_ClonesDataValidations()
    {
        var sheet = new SpreadsheetSheet();
        sheet.DataValidations.Add(new SpreadsheetDataValidation
        {
            Range = new SpreadsheetRange(0, 0, 2, 0),
            Type = SpreadsheetValidationType.List,
            Formula1 = "Yes,No",
            ShowDropDown = true
        });

        var clone = sheet.Clone();

        clone.DataValidations.Should().HaveCount(1);
        clone.DataValidations[0].Should().NotBeSameAs(sheet.DataValidations[0]);
        clone.DataValidations[0].Formula1.Should().Be("Yes,No");
    }

    [Fact]
    public void SpreadsheetCell_Validation_IsNullByDefault()
    {
        var cell = new SpreadsheetCell();
        cell.Validation.Should().BeNull();
    }

    [Fact]
    public void SpreadsheetCell_Clone_ShallowCopiesValidation()
    {
        var dv = new SpreadsheetDataValidation { Type = SpreadsheetValidationType.Decimal };
        var cell = new SpreadsheetCell { Validation = dv };
        var clone = cell.Clone();

        clone.Validation.Should().BeSameAs(dv); // shared reference — not deep-copied
    }
}
