using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Commands;
using Tempo.Blazor.NotionEditor.Commands;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// Some edits are already performed by the provider before the editor learns their outcome — a
/// conversion, for instance, is a single provider call that returns the finished block. Those must
/// be recorded on the undo stack without being executed a second time.
/// </summary>
public sealed class NotionCommandStackRecordTests
{
    [Fact]
    public void Record_DoesNotExecuteTheCommand()
    {
        var stack = new NotionCommandStack();
        var command = new SpyCommand();

        stack.Record(command);

        command.Executions.Should().Be(0, "the provider already applied this edit");
        stack.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task Record_ThenUndo_UndoesIt()
    {
        var stack = new NotionCommandStack();
        var command = new SpyCommand();
        stack.Record(command);

        await stack.UndoAsync();

        command.Undos.Should().Be(1);
        stack.CanUndo.Should().BeFalse();
        stack.CanRedo.Should().BeTrue();
    }

    [Fact]
    public async Task Record_ThenUndo_ThenRedo_ExecutesItOnce()
    {
        var stack = new NotionCommandStack();
        var command = new SpyCommand();
        stack.Record(command);

        await stack.UndoAsync();
        await stack.RedoAsync();

        command.Executions.Should().Be(1, "redo re-applies the edit exactly once");
    }

    [Fact]
    public void Record_ClearsTheRedoStack()
    {
        var stack = new NotionCommandStack();
        stack.Record(new SpyCommand());
        stack.UndoAsync().GetAwaiter().GetResult();
        stack.CanRedo.Should().BeTrue();

        stack.Record(new SpyCommand());

        stack.CanRedo.Should().BeFalse("a new edit invalidates the redo history");
    }

    [Fact]
    public void Record_InsideABatch_JoinsTheBatch()
    {
        var stack = new NotionCommandStack();
        stack.BeginBatch("edit");

        stack.Record(new SpyCommand());
        stack.Record(new SpyCommand());
        stack.CommitBatch();

        stack.CanUndo.Should().BeTrue();
        stack.NextUndoDescription.Should().Be("edit");
    }

    private sealed class SpyCommand : INotionCommand
    {
        public int Executions { get; private set; }
        public int Undos { get; private set; }
        public string Description => "spy";

        public Task ExecuteAsync() { Executions++; return Task.CompletedTask; }
        public Task UndoAsync() { Undos++; return Task.CompletedTask; }
    }
}
