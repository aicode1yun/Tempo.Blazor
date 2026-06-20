using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Services;

namespace Tempo.Blazor.EmailTemplates.Tests.Services;

public class EditorHistoryServiceTests
{
    private static EmailTemplateDocument DocWithSubject(string subject) => new() { Subject = subject };

    [Fact]
    public void New_HasNoUndoOrRedo()
    {
        var history = new EditorHistoryService();
        history.Initialize(DocWithSubject("a"));

        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Push_ThenUndo_ReturnsPreviousState()
    {
        var history = new EditorHistoryService();
        history.Initialize(DocWithSubject("v1"));
        history.Push(DocWithSubject("v2"));

        history.CanUndo.Should().BeTrue();
        var undone = history.Undo();
        undone!.Subject.Should().Be("v1");
        history.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_ReappliesUndoneState()
    {
        var history = new EditorHistoryService();
        history.Initialize(DocWithSubject("v1"));
        history.Push(DocWithSubject("v2"));
        history.Undo();

        var redone = history.Redo();
        redone!.Subject.Should().Be("v2");
    }

    [Fact]
    public void Push_ClearsRedoStack()
    {
        var history = new EditorHistoryService();
        history.Initialize(DocWithSubject("v1"));
        history.Push(DocWithSubject("v2"));
        history.Undo();
        history.Push(DocWithSubject("v3"));

        history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Snapshots_AreIndependentCopies()
    {
        var history = new EditorHistoryService();
        var doc = DocWithSubject("v1");
        history.Initialize(doc);
        history.Push(DocWithSubject("v2"));

        var undone = history.Undo()!;
        undone.Subject = "mutated";

        history.Redo()!.Subject.Should().Be("v2"); // original snapshot untouched
    }

    [Fact]
    public void RapidSameKeyEdits_CoalesceIntoOneUndoStep()
    {
        var time = new FakeTimeProvider();
        var history = new EditorHistoryService(time);
        history.Initialize(DocWithSubject(""));

        history.Push(DocWithSubject("H"), coalesceKey: "text");
        time.Advance(TimeSpan.FromMilliseconds(100));
        history.Push(DocWithSubject("He"), coalesceKey: "text");
        time.Advance(TimeSpan.FromMilliseconds(100));
        history.Push(DocWithSubject("Hel"), coalesceKey: "text");

        // One coalesced step → single undo returns the pre-typing state.
        history.Undo()!.Subject.Should().Be("");
        history.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void EditsAfterCoalesceWindow_AreSeparateSteps()
    {
        var time = new FakeTimeProvider();
        var history = new EditorHistoryService(time);
        history.Initialize(DocWithSubject(""));

        history.Push(DocWithSubject("a"), coalesceKey: "text");
        time.Advance(TimeSpan.FromSeconds(2));
        history.Push(DocWithSubject("ab"), coalesceKey: "text");

        history.Undo()!.Subject.Should().Be("a");
        history.Undo()!.Subject.Should().Be("");
    }

    [Fact]
    public void DepthLimit_DropsOldestStates()
    {
        var history = new EditorHistoryService(maxDepth: 3);
        history.Initialize(DocWithSubject("0"));
        for (var i = 1; i <= 10; i++) history.Push(DocWithSubject(i.ToString(CultureInfo.InvariantCulture)));

        var count = 0;
        while (history.CanUndo) { history.Undo(); count++; }
        count.Should().Be(3);
    }
}
