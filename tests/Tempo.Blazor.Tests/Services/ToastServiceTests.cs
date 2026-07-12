using FluentAssertions;
using Tempo.Blazor.Components.Feedback;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Tests.Services;

/// <summary>TDD tests for ToastService.</summary>
public class ToastServiceTests
{
    [Fact]
    public void ShowSuccess_AddsSuccessToast()
    {
        var svc = new ToastService();
        svc.ShowSuccess("Done!");

        svc.Toasts.Should().HaveCount(1);
        svc.Toasts[0].Severity.Should().Be(ToastSeverity.Success);
        svc.Toasts[0].Message.Should().Be("Done!");
    }

    [Fact]
    public void ShowError_AddsErrorToast()
    {
        var svc = new ToastService();
        svc.ShowError("Failed!", "Error Title");

        svc.Toasts.Should().HaveCount(1);
        svc.Toasts[0].Severity.Should().Be(ToastSeverity.Error);
        svc.Toasts[0].Title.Should().Be("Error Title");
    }

    [Fact]
    public void ShowWarning_AddsWarningToast()
    {
        var svc = new ToastService();
        svc.ShowWarning("Watch out");

        svc.Toasts[0].Severity.Should().Be(ToastSeverity.Warning);
    }

    [Fact]
    public void ShowInfo_AddsInfoToast()
    {
        var svc = new ToastService();
        svc.ShowInfo("FYI", duration: 3000);

        svc.Toasts[0].Severity.Should().Be(ToastSeverity.Info);
        svc.Toasts[0].Duration.Should().Be(3000);
    }

    [Fact]
    public void OnChange_FiresWhenToastAdded()
    {
        var svc = new ToastService();
        int callCount = 0;
        svc.OnChange += () => callCount++;

        svc.ShowSuccess("Test");

        callCount.Should().Be(1);
    }

    [Fact]
    public void Remove_RemovesToastById()
    {
        var svc = new ToastService();
        svc.ShowSuccess("One");
        svc.ShowError("Two");
        var idToRemove = svc.Toasts[0].Id;

        svc.Remove(idToRemove);

        svc.Toasts.Should().HaveCount(1);
        svc.Toasts[0].Message.Should().Be("Two");
    }

    [Fact]
    public void Clear_RemovesAllToasts()
    {
        var svc = new ToastService();
        svc.ShowSuccess("One");
        svc.ShowError("Two");
        svc.ShowWarning("Three");

        svc.Clear();

        svc.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void OnChange_FiresOnRemove()
    {
        var svc = new ToastService();
        svc.ShowSuccess("Test");
        int callCount = 0;
        svc.OnChange += () => callCount++;

        svc.Remove(svc.Toasts[0].Id);

        callCount.Should().Be(1);
    }

    // ── Auto-dismiss ──

    [Fact]
    public void AutoDismiss_RemovesToastAfterDuration()
    {
        var svc = new ToastService();
        svc.ShowInfo("Auto", duration: 50);

        svc.Toasts.Should().HaveCount(1);

        WaitUntil(() => svc.Toasts.Count == 0, timeoutMs: 3000)
            .Should().BeTrue("the toast should be auto-removed shortly after its 50ms duration elapses");
    }

    [Fact]
    public void AutoDismiss_FiresOnChange_WhenToastAutoRemoved()
    {
        var svc = new ToastService();
        svc.ShowInfo("Auto", duration: 50);
        int callCount = 0;
        svc.OnChange += () => Interlocked.Increment(ref callCount);

        WaitUntil(() => Volatile.Read(ref callCount) > 0, timeoutMs: 3000)
            .Should().BeTrue("OnChange should fire when the toast auto-removes itself");
    }

    [Fact]
    public void AutoDismiss_DurationZeroOrLess_NeverAutoRemoves()
    {
        var svc = new ToastService();
        svc.ShowInfo("Sticky", duration: 0);
        svc.ShowError("AlsoSticky", duration: -1);

        // give plenty of time for a (wrongly) scheduled auto-dismiss to fire
        Thread.Sleep(300);

        svc.Toasts.Should().HaveCount(2, "duration <= 0 means the toast is sticky and must never auto-dismiss");
    }

    [Fact]
    public void Remove_BeforeTimerFires_CancelsPendingAutoDismiss_NoDoubleRemoveOrException()
    {
        var svc = new ToastService();
        svc.ShowInfo("CancelMe", duration: 150);
        var id = svc.Toasts[0].Id;

        int changeCount = 0;
        svc.OnChange += () => Interlocked.Increment(ref changeCount);

        var act = () => svc.Remove(id);
        act.Should().NotThrow();
        changeCount.Should().Be(1, "the manual Remove should raise OnChange exactly once");

        // Wait well past the original duration: if the pending timer wasn't
        // cancelled it will fire and call Remove again, raising OnChange a
        // second time (Remove currently always invokes OnChange).
        Thread.Sleep(400);

        changeCount.Should().Be(1, "the pending auto-dismiss timer must be cancelled by the manual Remove");
        svc.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void Clear_CancelsAllPendingAutoDismissTimers_NoExceptionsLater()
    {
        var svc = new ToastService();
        svc.ShowInfo("One", duration: 100);
        svc.ShowError("Two", duration: 120);

        int changeCount = 0;
        svc.OnChange += () => Interlocked.Increment(ref changeCount);

        svc.Clear();
        changeCount.Should().Be(1);

        Thread.Sleep(400);

        changeCount.Should().Be(1, "Clear() must cancel any pending auto-dismiss timers");
        svc.Toasts.Should().BeEmpty();
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs, int pollMs = 10)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            Thread.Sleep(pollMs);
        }

        return condition();
    }
}
