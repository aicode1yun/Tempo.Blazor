using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Services;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentPendingActionServiceTests
{
    // ── Initial state ───────────────────────────────────────────────────────

    [Fact]
    public void New_HasAny_IsFalse()
    {
        var svc = new DocumentPendingActionService();
        svc.HasAny.Should().BeFalse();
    }

    [Fact]
    public void New_Count_IsZero()
    {
        var svc = new DocumentPendingActionService();
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void New_FirstMessage_IsNull()
    {
        var svc = new DocumentPendingActionService();
        svc.FirstMessage.Should().BeNull();
    }

    [Fact]
    public void New_Messages_IsEmpty()
    {
        var svc = new DocumentPendingActionService();
        svc.Messages.Should().BeEmpty();
    }

    // ── Add ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_SingleAction_HasAnyIsTrue()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.HasAny.Should().BeTrue();
    }

    [Fact]
    public void Add_SingleAction_CountIsOne()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Count.Should().Be(1);
    }

    [Fact]
    public void Add_SingleAction_FirstMessageReturnsMessage()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.FirstMessage.Should().Be("Saving...");
    }

    [Fact]
    public void Add_MultipleDistinctIds_CountMatchesRegistrations()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export", "Exporting PDF...");
        svc.Add("upload", "Uploading image...");
        svc.Count.Should().Be(3);
    }

    [Fact]
    public void Add_MultipleDistinctIds_MessagesContainsAll()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export", "Exporting PDF...");
        svc.Messages.Should().Contain("Saving...").And.Contain("Exporting PDF...");
    }

    [Fact]
    public void Add_DuplicateId_ReplacesMessage()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("save", "Saving (retry)...");
        svc.Count.Should().Be(1);
        svc.FirstMessage.Should().Be("Saving (retry)...");
    }

    [Fact]
    public void Add_DuplicateId_DoesNotIncreaseCount()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("save", "Saving...");
        svc.Count.Should().Be(1);
    }

    [Fact]
    public void Add_NullId_ThrowsArgumentException()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Add(null!, "msg");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_EmptyId_ThrowsArgumentException()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Add(string.Empty, "msg");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_EmptyMessage_ThrowsArgumentException()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Add("save", string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    // ── Remove ──────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ExistingId_RemovesAction()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Remove("save");
        svc.HasAny.Should().BeFalse();
    }

    [Fact]
    public void Remove_ExistingId_DecreasesCount()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export", "Exporting...");
        svc.Remove("save");
        svc.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_UnknownId_DoesNotThrow()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Remove("unknown");
        act.Should().NotThrow();
    }

    [Fact]
    public void Remove_UnknownId_CountStaysZero()
    {
        var svc = new DocumentPendingActionService();
        svc.Remove("unknown");
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_LastAction_FirstMessageBecomesNull()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Remove("save");
        svc.FirstMessage.Should().BeNull();
    }

    [Fact]
    public void Remove_NullId_ThrowsArgumentException()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Remove(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Remove_EmptyId_ThrowsArgumentException()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Remove(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    // ── Clear ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllActions()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export", "Exporting...");
        svc.Clear();
        svc.HasAny.Should().BeFalse();
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void Clear_EmptyService_DoesNotThrow()
    {
        var svc = new DocumentPendingActionService();
        var act = () => svc.Clear();
        act.Should().NotThrow();
    }

    [Fact]
    public void Clear_AfterClear_FirstMessageIsNull()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Clear();
        svc.FirstMessage.Should().BeNull();
    }

    // ── FirstMessage ordering ───────────────────────────────────────────────

    [Fact]
    public void FirstMessage_ReturnsFirstRegisteredAction()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export", "Exporting...");
        svc.FirstMessage.Should().Be("Saving...");
    }

    [Fact]
    public void FirstMessage_AfterRemovingFirst_ReturnsSecond()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export", "Exporting...");
        svc.Remove("save");
        svc.FirstMessage.Should().Be("Exporting...");
    }

    // ── Lifecycle scenario ──────────────────────────────────────────────────

    [Fact]
    public void TypicalSaveLifecycle_AddThenRemove_ReturnsToCleanState()
    {
        var svc = new DocumentPendingActionService();

        svc.Add("save", "Saving...");
        svc.HasAny.Should().BeTrue();
        svc.Count.Should().Be(1);

        svc.Remove("save");
        svc.HasAny.Should().BeFalse();
        svc.Count.Should().Be(0);
        svc.FirstMessage.Should().BeNull();
    }

    [Fact]
    public void SimultaneousSaveAndExport_BothVisible_RemoveOneAtATime()
    {
        var svc = new DocumentPendingActionService();
        svc.Add("save", "Saving...");
        svc.Add("export-pdf", "Exporting PDF...");
        svc.Count.Should().Be(2);
        svc.HasAny.Should().BeTrue();

        svc.Remove("save");
        svc.Count.Should().Be(1);
        svc.FirstMessage.Should().Be("Exporting PDF...");

        svc.Remove("export-pdf");
        svc.HasAny.Should().BeFalse();
    }
}
