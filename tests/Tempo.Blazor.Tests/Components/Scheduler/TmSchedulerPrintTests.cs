using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.Scheduler;
using Tempo.Blazor.Tests.Localization;
using Xunit;

namespace Tempo.Blazor.Tests.Components.Scheduler;

/// <summary>Print button wiring on the scheduler toolbar.</summary>
public class TmSchedulerPrintTests : LocalizationTestBase
{
    [Fact]
    public void Scheduler_ShowsPrintButton_ByDefault()
    {
        var cut = RenderComponent<TmScheduler>();
        cut.FindAll("[data-testid='scheduler-print']").Should().ContainSingle();
    }

    [Fact]
    public void Scheduler_AllowPrintFalse_HidesPrintButton()
    {
        var cut = RenderComponent<TmScheduler>(p => p.Add(c => c.AllowPrint, false));
        cut.FindAll("[data-testid='scheduler-print']").Should().BeEmpty();
    }

    [Fact]
    public void Toolbar_PrintClick_FiresOnPrint()
    {
        var clicked = false;
        var cut = RenderComponent<TmSchedulerToolbar>(p => p
            .Add(c => c.ShowPrint, true)
            .Add(c => c.OnPrint, EventCallback.Factory.Create(this, () => clicked = true)));

        cut.Find("[data-testid='scheduler-print']").Click();

        clicked.Should().BeTrue();
    }
}
