using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Navigation;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Navigation;

/// <summary>
/// TDD tests for TmNavigationGuard.
/// </summary>
/// <remarks>
/// bUnit 1.38.5's <see cref="FakeNavigationManager"/> updates <c>Uri</c> synchronously for every
/// <c>NavigateTo</c> call regardless of whether a registered LocationChanging handler later calls
/// <c>PreventNavigation()</c> (the property write happens before the handler pipeline even runs).
/// The reliable signal for "was this navigation actually blocked" is
/// <see cref="FakeNavigationManager.History"/>, whose <c>State</c> is computed from the real
/// <c>NotifyLocationChangingAsync</c> pipeline — so these tests assert on history state rather than Uri.
/// </remarks>
public class TmNavigationGuardTests : LocalizationTestBase
{
    private FakeNavigationManager Nav => Services.GetRequiredService<NavigationManager>() as FakeNavigationManager
        ?? throw new InvalidOperationException("FakeNavigationManager not registered.");

    [Fact]
    public void Clean_InternalNavigation_IsNotBlocked()
    {
        var cut = RenderComponent<TmNavigationGuard>(p => p.Add(x => x.IsDirty, false));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    [Fact]
    public void Dirty_InternalNavigation_PreventsNavigationAndShowsConfirmDialog()
    {
        var cut = RenderComponent<TmNavigationGuard>(p => p.Add(x => x.IsDirty, true));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        Nav.History.First().State.Should().Be(NavigationState.Prevented);
        cut.Find(".tm-dialog").Should().NotBeNull();
    }

    [Fact]
    public async Task Dirty_ConfirmLeave_ReissuesNavigationAndInvokesCallback()
    {
        var confirmed = false;
        var cut = RenderComponent<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnConfirmLeave, () => confirmed = true));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);
        cut.Find(".tm-dialog").Should().NotBeNull();

        var okButton = cut.Find(".tm-dialog-btn-ok");
        await cut.InvokeAsync(() => okButton.Click());
        cut.WaitForState(() => Nav.History.Count > 1);

        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
        confirmed.Should().BeTrue();
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    [Fact]
    public async Task Dirty_CancelLeave_StaysAndInvokesCallback()
    {
        var cancelled = false;
        var cut = RenderComponent<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnCancel, () => cancelled = true));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);
        cut.Find(".tm-dialog").Should().NotBeNull();

        var cancelButton = cut.Find(".tm-dialog-btn-cancel");
        await cut.InvokeAsync(() => cancelButton.Click());

        cancelled.Should().BeTrue();
        cut.FindAll(".tm-dialog").Should().BeEmpty();
        Nav.History.Should().HaveCount(1, "cancelling must not re-issue a second navigation");
    }

    [Fact]
    public void Disabled_DirtyInternalNavigation_IsNotBlocked()
    {
        var cut = RenderComponent<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.Enabled, false));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    [Fact]
    public void IsDirtyCallback_TakesPrecedenceForGating()
    {
        var cut = RenderComponent<TmNavigationGuard>(p => p
            .Add(x => x.IsDirtyCallback, () => true));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        Nav.History.First().State.Should().Be(NavigationState.Prevented);
        cut.Find(".tm-dialog").Should().NotBeNull();
    }

    [Fact]
    public void Suppress_BypassesGuardForNextNavigationOnly()
    {
        var cut = RenderComponent<TmNavigationGuard>(p => p.Add(x => x.IsDirty, true));

        cut.Instance.Suppress();
        Nav.NavigateTo("/first-target");
        cut.WaitForState(() => Nav.History.Count > 0);
        Nav.History.First().State.Should().Be(NavigationState.Succeeded, "Suppress() bypasses the very next navigation");
        cut.FindAll(".tm-dialog").Should().BeEmpty();

        // Suppress only bypasses the immediately-following navigation; the guard re-arms afterwards.
        Nav.NavigateTo("/second-target");
        cut.WaitForState(() => Nav.History.Count > 1);
        Nav.History.First().State.Should().Be(NavigationState.Prevented, "the guard must re-arm after consuming the suppression");
        cut.Find(".tm-dialog").Should().NotBeNull();
    }

    [Fact]
    public void CustomConfirmText_RendersInDialog()
    {
        var cut = RenderComponent<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.ConfirmTitle, "Custom title")
            .Add(x => x.ConfirmMessage, "Custom message")
            .Add(x => x.ConfirmLeaveText, "Go now")
            .Add(x => x.CancelText, "Stay here"));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        cut.Find(".tm-dialog-title").TextContent.Should().Be("Custom title");
        cut.Find(".tm-dialog-message").TextContent.Should().Be("Custom message");
        cut.Find(".tm-dialog-btn-ok").TextContent.Should().Be("Go now");
        cut.Find(".tm-dialog-btn-cancel").TextContent.Should().Be("Stay here");
    }
}
