using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public sealed class DocumentAutosaveStateMachineTests
{
    [Fact]
    public void NewState_IsSynchronized()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Synchronized);
    }

    [Fact]
    public void LocalChange_MovesSynchronizedToWaiting()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Waiting);
    }

    [Fact]
    public void DebounceElapsed_MovesWaitingToSaving()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Saving);
        machine.State.Attempt.Should().Be(1);
    }

    [Fact]
    public void SaveSucceeded_MovesSavingToSynchronized()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();
        machine.SaveSucceeded();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Synchronized);
        machine.State.Attempt.Should().Be(0);
    }

    [Fact]
    public void SaveFailed_MovesSavingToError()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();
        machine.SaveFailed("provider failed", recoverable: true);

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Error);
        machine.State.ErrorMessage.Should().Be("provider failed");
        machine.State.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void Retry_MovesRecoverableErrorToSaving()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();
        machine.SaveFailed("provider failed", recoverable: true);
        machine.Retry();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Saving);
        machine.State.Attempt.Should().Be(2);
    }

    [Fact]
    public void Retry_LeavesNonRecoverableErrorUnchanged()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();
        machine.SaveFailed("provider failed", recoverable: false);
        machine.Retry();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Error);
        machine.State.CanRetry.Should().BeFalse();
        machine.State.Attempt.Should().Be(1);
    }

    [Fact]
    public void LocalChangeDuringSaving_RequestsImmediateSaveAfterCurrent()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();
        machine.RegisterLocalChange();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Saving);
        machine.State.HasPendingImmediateSave.Should().BeTrue();
    }

    [Fact]
    public void SaveSucceededWithPendingImmediateSave_ReturnsToWaiting()
    {
        var machine = new DocumentAutosaveStateMachine();

        machine.RegisterLocalChange();
        machine.DebounceElapsed();
        machine.RegisterLocalChange();
        machine.SaveSucceeded();

        machine.State.Status.Should().Be(DocumentAutosaveStatus.Waiting);
        machine.State.HasPendingImmediateSave.Should().BeFalse();
    }
}
