using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>TDD tests for TmScheduler main container.</summary>
public class TmSchedulerTests : LocalizationTestBase
{
    [Fact]
    public void Renders_Container()
    {
        var cut = Render<TmScheduler>();

        cut.Find(".tm-scheduler").Should().NotBeNull();
    }

    [Fact]
    public void Renders_Toolbar()
    {
        var cut = Render<TmScheduler>();

        cut.Find(".tm-scheduler-toolbar").Should().NotBeNull();
    }

    [Fact]
    public void Toolbar_Shows_Navigation_Buttons()
    {
        var cut = Render<TmScheduler>();

        cut.Find("[data-testid='scheduler-prev']").Should().NotBeNull();
        cut.Find("[data-testid='scheduler-today']").Should().NotBeNull();
        cut.Find("[data-testid='scheduler-next']").Should().NotBeNull();
    }

    [Fact]
    public void Toolbar_Shows_View_Switcher()
    {
        var cut = Render<TmScheduler>();

        cut.FindAll(".tm-scheduler-toolbar-view-btn").Count.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void View_Switcher_Changes_View()
    {
        TmScheduleViewType view = TmScheduleViewType.Month;
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.ViewChanged, EventCallback.Factory.Create<TmScheduleViewType>(this, v => view = v)));

        // Click "Agenda" button
        var agendaBtn = cut.FindAll(".tm-scheduler-toolbar-view-btn")
            .First(b => b.GetAttribute("data-view") == "Agenda");
        agendaBtn.Click();

        view.Should().Be(TmScheduleViewType.Agenda);
    }

    [Fact]
    public void Active_View_Button_Has_Active_Class()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month));

        var monthBtn = cut.FindAll(".tm-scheduler-toolbar-view-btn")
            .First(b => b.GetAttribute("data-view") == "Month");
        monthBtn.ClassList.Should().Contain("tm-scheduler-toolbar-view-btn--active");
    }

    [Fact]
    public void Next_Navigation_Changes_Date()
    {
        DateTime date = new DateTime(2025, 6, 15);
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, date)
            .Add(c => c.CurrentDateChanged, EventCallback.Factory.Create<DateTime>(this, d => date = d)));

        cut.Find("[data-testid='scheduler-next']").Click();

        date.Month.Should().Be(7);
    }

    [Fact]
    public void Prev_Navigation_Changes_Date()
    {
        DateTime date = new DateTime(2025, 6, 15);
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, date)
            .Add(c => c.CurrentDateChanged, EventCallback.Factory.Create<DateTime>(this, d => date = d)));

        cut.Find("[data-testid='scheduler-prev']").Click();

        date.Month.Should().Be(5);
    }

    [Fact]
    public void Today_Navigation_Returns_To_Today()
    {
        DateTime date = new DateTime(2025, 1, 1);
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, date)
            .Add(c => c.CurrentDateChanged, EventCallback.Factory.Create<DateTime>(this, d => date = d)));

        cut.Find("[data-testid='scheduler-today']").Click();

        date.Date.Should().Be(DateTime.Today);
    }

    [Fact]
    public void Applies_Custom_Class()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.Class, "my-scheduler"));

        cut.Find(".tm-scheduler").ClassList.Should().Contain("my-scheduler");
    }

    [Fact]
    public void Default_View_Is_Month()
    {
        var cut = Render<TmScheduler>();

        cut.FindAll(".tm-scheduler-month").Count.Should().Be(1);
    }

    [Fact]
    public void Displays_Month_View_When_View_Is_Month()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month));

        cut.FindAll(".tm-scheduler-month").Count.Should().Be(1);
        cut.FindAll(".tm-scheduler-agenda").Count.Should().Be(0);
    }

    [Fact]
    public void Displays_Agenda_View_When_View_Is_Agenda()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Agenda));

        cut.FindAll(".tm-scheduler-agenda").Count.Should().Be(1);
        cut.FindAll(".tm-scheduler-month").Count.Should().Be(0);
    }

    [Fact]
    public void Displays_Day_View_When_View_Is_Day()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Day));

        cut.FindAll(".tm-scheduler-day").Count.Should().Be(1);
        cut.FindAll(".tm-scheduler-month").Count.Should().Be(0);
    }

    [Fact]
    public void Displays_Week_View_When_View_Is_Week()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Week));

        cut.FindAll(".tm-scheduler-week").Count.Should().Be(1);
        cut.FindAll(".tm-scheduler-month").Count.Should().Be(0);
    }

    [Fact]
    public void Day_Navigation_Changes_By_One_Day()
    {
        DateTime date = new DateTime(2025, 6, 15);
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Day)
            .Add(c => c.CurrentDate, date)
            .Add(c => c.CurrentDateChanged, EventCallback.Factory.Create<DateTime>(this, d => date = d)));

        cut.Find("[data-testid='scheduler-next']").Click();

        date.Day.Should().Be(16);
    }

    [Fact]
    public void Week_Navigation_Changes_By_Seven_Days()
    {
        DateTime date = new DateTime(2025, 6, 15);
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Week)
            .Add(c => c.CurrentDate, date)
            .Add(c => c.CurrentDateChanged, EventCallback.Factory.Create<DateTime>(this, d => date = d)));

        cut.Find("[data-testid='scheduler-next']").Click();

        date.Day.Should().Be(22);
    }

    [Fact]
    public void Displays_Timeline_View_When_View_Is_Timeline()
    {
        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Timeline));

        cut.FindAll(".tm-scheduler-timeline").Count.Should().Be(1);
        cut.FindAll(".tm-scheduler-month").Count.Should().Be(0);
    }

    [Fact]
    public void Month_View_Uses_EventTemplate_When_Provided()
    {
        var events = new List<TmScheduleEvent>
        {
            new()
            {
                Title = "Custom Month",
                Start = new DateTime(2025, 6, 10, 9, 0, 0),
                End = new DateTime(2025, 6, 10, 10, 0, 0),
                Color = "#16a34a"
            }
        };

        var cut = Render<TmScheduler>(p => p
            .Add(c => c.View, TmScheduleViewType.Month)
            .Add(c => c.CurrentDate, new DateTime(2025, 6, 15))
            .Add(c => c.Events, events)
            .Add(c => c.EventTemplate, (RenderFragment<TmScheduleEvent>)(e => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "class", "scheduler-month-template");
                builder.AddContent(2, e.Title);
                builder.CloseElement();
            })));

        var eventEl = cut.Find(".tm-scheduler-month-event");
        eventEl.GetAttribute("style").Should().Contain("--event-color: #16a34a");
        cut.Find(".scheduler-month-template").TextContent.Should().Contain("Custom Month");
    }
}
