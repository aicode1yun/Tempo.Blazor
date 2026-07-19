using Bunit;
using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Pickers;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Pickers;

public class TmRecurrenceEditorTests : LocalizationTestBase
{
    [Fact]
    public void TmRecurrenceEditor_Renders_Container()
    {
        var cut = Render<TmRecurrenceEditor>();
        cut.Find(".tm-recurrence-editor").Should().NotBeNull();
    }

    [Fact]
    public void TmRecurrenceEditor_Default_Pattern_Is_Daily()
    {
        var cut = Render<TmRecurrenceEditor>();
        var summary = cut.Find(".tm-recurrence-editor__summary-text").TextContent;
        summary.Should().Contain("FREQ=DAILY");
    }

    [Fact]
    public void TmRecurrenceEditor_Select_Weekly_Shows_Day_Checkboxes()
    {
        var cut = Render<TmRecurrenceEditor>();

        var patternSelect = cut.Find("select");
        patternSelect.Change("Weekly");

        cut.FindAll(".tm-recurrence-editor__day").Count.Should().Be(7);
    }

    [Fact]
    public void TmRecurrenceEditor_Select_Monthly_Shows_Monthly_Options()
    {
        var cut = Render<TmRecurrenceEditor>();

        var patternSelect = cut.Find("select");
        patternSelect.Change("Monthly");

        cut.FindAll("input[type='radio']").Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void TmRecurrenceEditor_Select_Yearly_Shows_Month_Select()
    {
        var cut = Render<TmRecurrenceEditor>();

        var patternSelect = cut.Find("select");
        patternSelect.Change("Yearly");

        var monthSelects = cut.FindAll("select");
        monthSelects.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void TmRecurrenceEditor_Change_Interval_Updates_RRule()
    {
        string? result = null;
        var cut = Render<TmRecurrenceEditor>(p => p
            .Add(c => c.ValueChanged, v => result = v));

        var numberInputs = cut.FindAll("input[type='number']");
        numberInputs[0].Change("3");

        result.Should().Contain("INTERVAL=3");
    }

    [Fact]
    public void TmRecurrenceEditor_Toggle_Day_Updates_RRule()
    {
        string? result = null;
        var cut = Render<TmRecurrenceEditor>(p => p
            .Add(c => c.Value, "FREQ=WEEKLY")
            .Add(c => c.ValueChanged, v => result = v));

        var days = cut.FindAll(".tm-recurrence-editor__day input");
        days[1].Change(true); // Monday

        result.Should().Contain("BYDAY=MO");
    }

    [Fact]
    public void TmRecurrenceEditor_End_After_Count_Updates_RRule()
    {
        string? result = null;
        var cut = Render<TmRecurrenceEditor>(p => p
            .Add(c => c.ValueChanged, v => result = v));

        var labels = cut.FindAll(".tm-recurrence-editor__radio-label");
        foreach (var label in labels)
        {
            if (label.TextContent.Contains("After"))
            {
                var radio = label.QuerySelector("input[type='radio']");
                radio?.Change(true);
                break;
            }
        }

        result.Should().Contain("COUNT=");
    }

    [Fact]
    public void TmRecurrenceEditor_ShowSummary_False_Hides_Summary()
    {
        var cut = Render<TmRecurrenceEditor>(p => p
            .Add(c => c.ShowSummary, false));

        cut.FindAll(".tm-recurrence-editor__summary").Should().BeEmpty();
    }

    [Fact]
    public void TmRecurrenceEditor_Custom_Class_Applied()
    {
        var cut = Render<TmRecurrenceEditor>(p => p
            .Add(c => c.Class, "my-recurrence"));

        cut.Find(".tm-recurrence-editor").ClassList.Should().Contain("my-recurrence");
    }

    [Fact]
    public void TmRecurrenceEditor_Parse_Existing_RRule()
    {
        var cut = Render<TmRecurrenceEditor>(p => p
            .Add(c => c.Value, "FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=10"));

        var summary = cut.Find(".tm-recurrence-editor__summary-text").TextContent;
        summary.Should().Contain("FREQ=WEEKLY");
        summary.Should().Contain("BYDAY=MO,WE,FR");
        summary.Should().Contain("COUNT=10");
    }
}
