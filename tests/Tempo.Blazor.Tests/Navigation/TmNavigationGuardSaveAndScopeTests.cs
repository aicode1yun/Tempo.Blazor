using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.Navigation;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Navigation;

/// <summary>
/// Tests for the phase-2 additions to TmNavigationGuard: a <c>ShouldGuard</c> predicate that scopes
/// which destinations are guarded (needed for same-document URL tabs / sub-pages) and an optional
/// third "save and leave" action.
/// </summary>
public class TmNavigationGuardSaveAndScopeTests : LocalizationTestBase
{
    private BunitNavigationManager Nav => Services.GetRequiredService<NavigationManager>() as BunitNavigationManager
        ?? throw new InvalidOperationException("BunitNavigationManager not registered.");

    [Fact]
    public void ShouldGuard_ReturnsFalse_AllowsDirtyNavigationWithoutPrompt()
    {
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.ShouldGuard, uri => !uri.Contains("/loss/", StringComparison.Ordinal)));

        Nav.NavigateTo("/oprisk/123-2024/loss/new");
        cut.WaitForState(() => Nav.History.Count > 0);

        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    [Fact]
    public void ShouldGuard_ReturnsTrue_StillGuardsDirtyNavigation()
    {
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.ShouldGuard, _ => true));

        Nav.NavigateTo("/somewhere-else");
        cut.WaitForState(() => Nav.History.Count > 0);

        Nav.History.First().State.Should().Be(NavigationState.Prevented);
        cut.Find(".tm-dialog").Should().NotBeNull();
    }

    [Fact]
    public void SaveAndLeave_ThirdButton_NotRendered_WhenCallbackUnset()
    {
        var cut = Render<TmNavigationGuard>(p => p.Add(x => x.IsDirty, true));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        // Classic two-choice confirm dialog.
        cut.FindAll(".tm-dialog-footer button").Should().HaveCount(2);
    }

    [Fact]
    public void SaveAndLeave_ThirdButton_Rendered_WhenCallbackSet()
    {
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnSaveAndLeave, () => Task.FromResult(true))
            .Add(x => x.SaveAndLeaveText, "Save and leave"));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        var buttons = cut.FindAll(".tm-dialog-footer button");
        buttons.Should().HaveCount(3);
        buttons.Should().Contain(b => b.TextContent.Contains("Save and leave"));
    }

    [Fact]
    public async Task SaveAndLeave_Click_InvokesCallbackThenReissuesNavigation()
    {
        var saved = false;
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnSaveAndLeave, () => { saved = true; return Task.FromResult(true); })
            .Add(x => x.SaveAndLeaveText, "Save and leave"));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);
        Nav.History.First().State.Should().Be(NavigationState.Prevented);

        var saveButton = cut.FindAll(".tm-dialog-footer button")
            .First(b => b.TextContent.Contains("Save and leave"));
        await cut.InvokeAsync(() => saveButton.Click());
        cut.WaitForState(() => Nav.History.Count > 1);

        saved.Should().BeTrue();
        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLeave_Variant_Escape_Stays_WithoutReissuingNavigation()
    {
        var saved = false;
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnSaveAndLeave, () => { saved = true; return Task.FromResult(true); }));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);
        cut.Find(".tm-dialog").Should().NotBeNull();

        // Escape in the three-choice variant maps to "stay" (like the two-choice variant), not save/leave.
        await cut.InvokeAsync(() => cut.Find(".tm-dialog").KeyUp(new KeyboardEventArgs { Key = "Escape" }));

        saved.Should().BeFalse();
        Nav.History.Should().HaveCount(1, "Escape must stay on the page and not re-issue navigation");
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    /// <summary>
    /// A FAILED save must not leave. Until 2.8.16 the guard navigated as soon as the callback completed,
    /// no matter what it did, so an HTTP error or a rejected validation discarded the very changes the
    /// guard exists to protect — through the button that promises to save them.
    /// </summary>
    /// <remarks>
    /// The dialog stays open on purpose: the user keeps both the work and the three-way choice, so a retry
    /// or a deliberate "leave" are still one click away. This is the mutation that fails against the old
    /// behaviour — the previous implementation passes every other test in this file unchanged.
    /// </remarks>
    [Fact]
    public async Task SaveAndLeave_FailedSave_StaysPut_AndKeepsTheDialogOpen()
    {
        var attempts = 0;
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnSaveAndLeave, () => { attempts++; return Task.FromResult(false); })
            .Add(x => x.SaveAndLeaveText, "Save and leave"));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);
        Nav.History.First().State.Should().Be(NavigationState.Prevented);

        var saveButton = cut.FindAll(".tm-dialog-footer button")
            .First(b => b.TextContent.Contains("Save and leave"));
        await cut.InvokeAsync(() => saveButton.Click());

        attempts.Should().Be(1);
        Nav.History.Should().HaveCount(1, "a save that failed must not re-issue the blocked navigation");
        Nav.History.First().State.Should().Be(NavigationState.Prevented);
        cut.Find(".tm-dialog").Should().NotBeNull("the choice must still be on screen after a failed save");
    }

    /// <summary>
    /// The blocked destination survives a failed save: after a retry that succeeds, the guard leaves for
    /// the SAME place the user was originally going. Forgetting it would turn one failed save into a
    /// silent dead end.
    /// </summary>
    [Fact]
    public async Task SaveAndLeave_RetryAfterFailure_LeavesForTheOriginalDestination()
    {
        var succeed = false;
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnSaveAndLeave, () => Task.FromResult(succeed))
            .Add(x => x.SaveAndLeaveText, "Save and leave"));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        var saveButton = cut.FindAll(".tm-dialog-footer button")
            .First(b => b.TextContent.Contains("Save and leave"));
        await cut.InvokeAsync(() => saveButton.Click());
        Nav.History.Should().HaveCount(1);

        succeed = true;
        saveButton = cut.FindAll(".tm-dialog-footer button")
            .First(b => b.TextContent.Contains("Save and leave"));
        await cut.InvokeAsync(() => saveButton.Click());
        cut.WaitForState(() => Nav.History.Count > 1);

        Nav.History.Last().Uri.Should().EndWith("/next-page");
        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
        cut.FindAll(".tm-dialog").Should().BeEmpty();
    }

    /// <summary>
    /// A second click while the first save is still in flight must not start a second save. The dialog
    /// stays up during the save, so the button stays there to be clicked.
    /// </summary>
    [Fact]
    public async Task SaveAndLeave_SecondClickDuringSave_DoesNotStartASecondSave()
    {
        var gate = new TaskCompletionSource<bool>();
        var attempts = 0;
        var cut = Render<TmNavigationGuard>(p => p
            .Add(x => x.IsDirty, true)
            .Add(x => x.OnSaveAndLeave, () => { attempts++; return gate.Task; })
            .Add(x => x.SaveAndLeaveText, "Save and leave"));

        Nav.NavigateTo("/next-page");
        cut.WaitForState(() => Nav.History.Count > 0);

        var saveButton = cut.FindAll(".tm-dialog-footer button")
            .First(b => b.TextContent.Contains("Save and leave"));
        var first = cut.InvokeAsync(() => saveButton.Click());
        await cut.InvokeAsync(() => cut.FindAll(".tm-dialog-footer button")
            .First(b => b.TextContent.Contains("Save and leave")).Click());

        attempts.Should().Be(1, "the in-flight save must swallow the second click, not duplicate the write");

        gate.SetResult(true);
        await first;
        cut.WaitForState(() => Nav.History.Count > 1);
        Nav.History.First().State.Should().Be(NavigationState.Succeeded);
    }
}
