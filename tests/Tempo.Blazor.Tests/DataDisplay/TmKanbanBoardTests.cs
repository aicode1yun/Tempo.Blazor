using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.DataDisplay;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.DataDisplay;

/// <summary>TDD tests for TmKanbanBoard.</summary>
public class TmKanbanBoardTests : LocalizationTestBase
{
    private record KanbanTask(int Id, string Title, string Status);

    private static readonly IReadOnlyList<KanbanColumn> Columns =
    [
        new("todo", "To Do", "#3b82f6"),
        new("doing", "In Progress", "#f59e0b"),
        new("done", "Done", "#10b981"),
    ];

    private static readonly IReadOnlyList<KanbanTask> Tasks =
    [
        new(1, "Task A", "todo"),
        new(2, "Task B", "todo"),
        new(3, "Task C", "doing"),
        new(4, "Task D", "done"),
    ];

    [Fact]
    public void Kanban_Renders_Columns()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b =>
            {
                b.AddContent(0, t.Title);
            })));

        cut.Find(".tm-kanban").Should().NotBeNull();
        cut.FindAll(".tm-kanban__column").Count.Should().Be(3);
    }

    [Fact]
    public void Kanban_ColumnHeaders_ShowTitles()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        cut.Markup.Should().Contain("To Do");
        cut.Markup.Should().Contain("In Progress");
        cut.Markup.Should().Contain("Done");
    }

    [Fact]
    public void Kanban_CardsDistributed_PerColumn()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        // todo=2, doing=1, done=1
        var cards = cut.FindAll(".tm-kanban__card");
        cards.Count.Should().Be(4);
    }

    [Fact]
    public void Kanban_CardTemplate_Rendered()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b =>
            {
                b.OpenElement(0, "strong");
                b.AddContent(1, t.Title);
                b.CloseElement();
            })));

        cut.Markup.Should().Contain("<strong>Task A</strong>");
    }

    [Fact]
    public void Kanban_CardClick_FiresCallback()
    {
        KanbanTask? clicked = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnCardClick, t => clicked = t));

        cut.FindAll(".tm-kanban__card")[0].Click();

        clicked.Should().NotBeNull();
        clicked!.Title.Should().Be("Task A");
    }

    [Fact]
    public void Kanban_EmptyColumn_ShowsEmptyMessage()
    {
        var items = new List<KanbanTask>
        {
            new(1, "Task A", "todo"),
        };

        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, items)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        // "doing" and "done" columns should show empty state
        cut.FindAll(".tm-kanban__empty").Count.Should().Be(2);
    }

    [Fact]
    public void Kanban_WipLimit_ShowsWarning()
    {
        var columnsWithLimit = new KanbanColumn[]
        {
            new("todo", "To Do", "#3b82f6", MaxItems: 1),
            new("doing", "In Progress"),
            new("done", "Done"),
        };

        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, columnsWithLimit)
            .Add(x => x.Items, Tasks) // 2 items in "todo" but limit is 1
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        // The "todo" column should have a WIP warning class
        cut.FindAll(".tm-kanban__column--over-limit").Count.Should().Be(1);
    }

    [Fact]
    public void Kanban_ColumnCount_ShowsItemCount()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        // Should show count badges
        cut.FindAll(".tm-kanban__count").Count.Should().Be(3);
    }

    [Fact]
    public void Kanban_DraggableAttribute_OnCards()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        var cards = cut.FindAll(".tm-kanban__card");
        foreach (var card in cards)
        {
            card.GetAttribute("draggable").Should().Be("true");
        }
    }

    [Fact]
    public void Kanban_CustomClass()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.Class, "my-kanban"));

        cut.Find(".tm-kanban").ClassList.Should().Contain("my-kanban");
    }

    [Fact]
    public void Kanban_ColumnColor_AppliedAsStyle()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        var firstHeader = cut.FindAll(".tm-kanban__header-color")[0];
        var style = firstHeader.GetAttribute("style") ?? "";
        style.Should().Contain("#3b82f6");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 1: in-column reorder / drop index
    // ══════════════════════════════════════════════════════════════════════

    private const string DragStart = "ondragstart";
    private const string DragOver = "ondragover";
    private const string Drop = "ondrop";

    // ── KanbanMoveEvent construction (old + new signature) ──

    [Fact]
    public void KanbanMoveEvent_LegacySignature_HasNullIndexAndDefaultBeforeItem()
    {
        var e = new KanbanMoveEvent<KanbanTask>(new KanbanTask(1, "A", "todo"), "todo", "doing");

        e.TargetIndex.Should().BeNull();
        e.TargetBeforeItem.Should().BeNull();
    }

    [Fact]
    public void KanbanMoveEvent_NewSignature_CarriesIndexAndBeforeItem()
    {
        var before = new KanbanTask(2, "B", "doing");
        var e = new KanbanMoveEvent<KanbanTask>(new KanbanTask(1, "A", "todo"), "todo", "doing", 3, before);

        e.TargetIndex.Should().Be(3);
        e.TargetBeforeItem.Should().Be(before);
    }

    // ── In-column reorder ──

    [Fact]
    public void Kanban_Reorder_SameColumn_DropBelowCard_FiresOnItemReordered_WithEndIndex()
    {
        KanbanMoveEvent<KanbanTask>? move = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemReordered, e => move = e));

        // Drag Task A (todo idx 0), hover Task B (todo idx 1) → dragging downward drops AFTER B (end)
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[1].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        move.Should().NotBeNull();
        move!.FromColumn.Should().Be("todo");
        move.ToColumn.Should().Be("todo");
        move.TargetIndex.Should().Be(2);
        move.TargetBeforeItem.Should().BeNull();
    }

    [Fact]
    public void Kanban_Reorder_SameColumn_DropAboveCard_FiresOnItemReordered_WithCardIndex()
    {
        KanbanMoveEvent<KanbanTask>? move = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemReordered, e => move = e));

        // Drag Task B (todo idx 1), hover Task A (todo idx 0) → dragging upward drops BEFORE A (index 0)
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[1].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        move.Should().NotBeNull();
        move!.TargetIndex.Should().Be(0);
        move.TargetBeforeItem.Should().NotBeNull();
        move.TargetBeforeItem!.Id.Should().Be(1); // Task A
    }

    [Fact]
    public void Kanban_Reorder_SameColumn_DropOnColumnBackground_TargetIsEnd()
    {
        KanbanMoveEvent<KanbanTask>? move = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemReordered, e => move = e));

        // Drag Task A (todo idx 0), hover empty background of the todo column → end (index == count == 2)
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        move.Should().NotBeNull();
        move!.TargetIndex.Should().Be(2);
        move.TargetBeforeItem.Should().BeNull();
    }

    // ── Cross-column with index ──

    [Fact]
    public void Kanban_CrossColumn_DropOnCard_FiresOnItemMoved_WithTargetIndex()
    {
        KanbanMoveEvent<KanbanTask>? move = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemMoved, e => move = e));

        // Drag Task C (doing idx 0), hover Task A (todo idx 0) → insert BEFORE A (index 0)
        cut.Find("[data-testid='board-column-doing'] .tm-kanban__card").TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        move.Should().NotBeNull();
        move!.FromColumn.Should().Be("doing");
        move.ToColumn.Should().Be("todo");
        move.TargetIndex.Should().Be(0);
        move.TargetBeforeItem!.Id.Should().Be(1); // Task A
    }

    [Fact]
    public void Kanban_CrossColumn_DropOnColumnBackground_TargetIsEnd()
    {
        KanbanMoveEvent<KanbanTask>? move = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemMoved, e => move = e));

        // Drag Task C (doing), drop on todo column background → end (todo has 2 items)
        cut.Find("[data-testid='board-column-doing'] .tm-kanban__card").TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        move.Should().NotBeNull();
        move!.TargetIndex.Should().Be(2);
        move.TargetBeforeItem.Should().BeNull();
    }

    [Fact]
    public void Kanban_CrossColumn_DropOnEmptyColumn_TargetIndexZero()
    {
        var items = new List<KanbanTask>
        {
            new(1, "Task A", "todo"),
            new(2, "Task B", "todo"),
            new(3, "Task C", "doing"),
            // "done" is empty
        };

        KanbanMoveEvent<KanbanTask>? move = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, items)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemMoved, e => move = e));

        // Drag Task A (todo), drop into the empty "done" column
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='board-column-done']").TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-done']").TriggerEvent(Drop, new DragEventArgs());

        move.Should().NotBeNull();
        move!.ToColumn.Should().Be("done");
        move.TargetIndex.Should().Be(0);
        move.TargetBeforeItem.Should().BeNull();
    }

    // ── Visual drop indicator ──

    [Fact]
    public void Kanban_DropIndicator_Shown_WhileDraggingOverColumn()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        cut.FindAll(".tm-kanban__drop-indicator").Should().BeEmpty();

        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[1].TriggerEvent(DragOver, new DragEventArgs());

        cut.FindAll(".tm-kanban__drop-indicator").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Kanban_DropIndicator_Cleared_AfterDrop()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemReordered, _ => { }));

        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[1].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        cut.FindAll(".tm-kanban__drop-indicator").Should().BeEmpty();
    }

    // ── Disabled blocks reorder + move ──

    [Fact]
    public void Kanban_Disabled_Blocks_Reorder_And_Move()
    {
        KanbanMoveEvent<KanbanTask>? moved = null;
        KanbanMoveEvent<KanbanTask>? reordered = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.Disabled, true)
            .Add(x => x.OnItemMoved, e => moved = e)
            .Add(x => x.OnItemReordered, e => reordered = e));

        // same-column reorder attempt
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[1].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());
        // cross-column move attempt
        cut.Find("[data-testid='board-column-doing'] .tm-kanban__card").TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        reordered.Should().BeNull();
        moved.Should().BeNull();
        cut.FindAll(".tm-kanban__drop-indicator").Should().BeEmpty();
    }

    // ── Backward compatibility: consumer without OnItemReordered ──

    [Fact]
    public void Kanban_SameColumnDrop_WithoutOnItemReordered_IsNoOp()
    {
        KanbanMoveEvent<KanbanTask>? moved = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemMoved, e => moved = e)); // no OnItemReordered subscribed

        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='board-column-todo'] .tm-kanban__card")[1].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        // Same-column drop with no reorder handler must not raise OnItemMoved (unchanged legacy behaviour)
        moved.Should().BeNull();
    }

    [Fact]
    public void Kanban_CrossColumn_LegacyConsumer_StillFiresOnItemMoved()
    {
        // A 2.0.x consumer that ignores the new positional fields keeps working.
        KanbanMoveEvent<KanbanTask>? moved = null;
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.OnItemMoved, e => moved = e));

        cut.Find("[data-testid='board-column-doing'] .tm-kanban__card").TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='board-column-todo']").TriggerEvent(Drop, new DragEventArgs());

        moved.Should().NotBeNull();
        moved!.FromColumn.Should().Be("doing");
        moved.ToColumn.Should().Be("todo");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Phase 2: swimlanes + template parameter wiring
    // ══════════════════════════════════════════════════════════════════════

    private record LaneTask(int Id, string Title, string Status, string? Assignee);

    private static readonly IReadOnlyList<LaneTask> LaneTasks =
    [
        new(1, "A", "todo", "Alice"),
        new(2, "B", "doing", "Alice"),
        new(3, "C", "todo", "Bob"),
        new(4, "D", "done", "Bob"),
        new(5, "E", "todo", null), // no assignee → no-value lane
    ];

    private static readonly RenderFragment<LaneTask> LaneCard = t => (RenderFragment)(b => b.AddContent(0, t.Title));

    private IRenderedComponent<TmKanbanBoard<LaneTask>> RenderSwimlaneBoard(
        Action<ComponentParameterCollectionBuilder<TmKanbanBoard<LaneTask>>>? extra = null,
        IReadOnlyList<LaneTask>? items = null,
        IReadOnlyList<KanbanColumn>? columns = null)
        => Render<TmKanbanBoard<LaneTask>>(p =>
        {
            p.Add(x => x.Columns, columns ?? Columns)
             .Add(x => x.Items, items ?? LaneTasks)
             .Add(x => x.ColumnSelector, t => t.Status)
             .Add(x => x.CardTemplate, LaneCard)
             .Add(x => x.SwimlaneSelector, t => t.Assignee);
            extra?.Invoke(p);
        });

    [Fact]
    public void Kanban_Swimlanes_RendersLanePerDistinctValue()
    {
        var cut = RenderSwimlaneBoard();
        // Alice, Bob + a "no value" lane for the null-assignee card
        cut.FindAll(".tm-kanban__swimlane").Count.Should().Be(3);
    }

    [Fact]
    public void Kanban_Swimlanes_DerivedInAppearanceOrder()
    {
        var cut = RenderSwimlaneBoard();
        var titles = cut.FindAll(".tm-kanban__swimlane-title").Select(e => e.TextContent.Trim()).ToList();
        titles.Should().ContainInOrder("Alice", "Bob");
        titles.Last().Should().Be("No value"); // localized no-value lane last
    }

    [Fact]
    public void Kanban_Swimlanes_ExplicitList_UsedInOrder()
    {
        var lanes = new List<KanbanSwimlane> { new("Bob", "Bob B."), new("Alice", "Alice A.") };
        var cut = RenderSwimlaneBoard(p => p.Add(x => x.Swimlanes, lanes));
        var titles = cut.FindAll(".tm-kanban__swimlane-title").Select(e => e.TextContent.Trim()).ToList();
        titles[0].Should().Be("Bob B.");
        titles[1].Should().Be("Alice A.");
        titles.Last().Should().Be("No value"); // null-assignee still forces a trailing no-value lane
    }

    [Fact]
    public void Kanban_Swimlanes_NullValue_LaneLast_Localized_And_HoldsNullItems()
    {
        var cut = RenderSwimlaneBoard();
        var lastLane = cut.FindAll(".tm-kanban__swimlane").Last();
        lastLane.QuerySelector(".tm-kanban__swimlane-title")!.TextContent.Trim().Should().Be("No value");
        cut.Find("[data-testid='cell-todo-none'] .tm-kanban__card").TextContent.Should().Contain("E");
    }

    [Fact]
    public void Kanban_Swimlanes_ColumnHeaderCount_IsBoardLevel()
    {
        var cut = RenderSwimlaneBoard();
        // todo across all lanes: A (Alice) + C (Bob) + E (none) = 3
        var todoHeader = cut.Find(".tm-kanban__column-headers [data-testid='colhead-todo']");
        todoHeader.QuerySelector(".tm-kanban__count")!.TextContent.Trim().Should().Be("3");
    }

    [Fact]
    public void Kanban_Swimlanes_LaneHeaderCount_IsPerLane()
    {
        var cut = RenderSwimlaneBoard();
        var alice = cut.Find("[data-testid='swimlane-Alice']");
        alice.QuerySelector(".tm-kanban__swimlane-count")!.TextContent.Trim().Should().Be("2"); // A, B
    }

    [Fact]
    public void Kanban_Swimlanes_WipLimit_IsBoardLevel()
    {
        var cols = new KanbanColumn[]
        {
            new("todo", "To Do", null, MaxItems: 2),
            new("doing", "In Progress"),
            new("done", "Done"),
        };
        var cut = RenderSwimlaneBoard(columns: cols);
        // board-level todo total = 3 > limit 2 → over-limit in the shared header row
        cut.FindAll(".tm-kanban__column-headers .tm-kanban__column--over-limit").Count.Should().Be(1);
    }

    [Fact]
    public void Kanban_Swimlanes_Collapse_HidesLaneBody()
    {
        var cut = RenderSwimlaneBoard();
        cut.FindAll("[data-testid='swimlane-Alice'] .tm-kanban__swimlane-body").Should().HaveCount(1);
        cut.Find("[data-testid='swimlane-Alice'] .tm-kanban__swimlane-toggle").Click();
        cut.FindAll("[data-testid='swimlane-Alice'] .tm-kanban__swimlane-body").Should().BeEmpty();
    }

    [Fact]
    public void Kanban_Swimlanes_CollapsibleFalse_NoToggle()
    {
        var cut = RenderSwimlaneBoard(p => p.Add(x => x.SwimlanesCollapsible, false));
        cut.FindAll(".tm-kanban__swimlane-toggle").Should().BeEmpty();
    }

    [Fact]
    public void Kanban_Swimlanes_DragAcrossLanes_FiresMoved_WithSwimlanes()
    {
        KanbanMoveEvent<LaneTask>? moved = null;
        var cut = RenderSwimlaneBoard(p => p.Add(x => x.OnItemMoved, e => moved = e));

        // Drag A (todo, Alice) into the (todo, Bob) cell
        cut.Find("[data-testid='cell-todo-Alice'] .tm-kanban__card").TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='cell-todo-Bob']").TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='cell-todo-Bob']").TriggerEvent(Drop, new DragEventArgs());

        moved.Should().NotBeNull();
        moved!.FromColumn.Should().Be("todo");
        moved.ToColumn.Should().Be("todo");
        moved.FromSwimlane.Should().Be("Alice");
        moved.ToSwimlane.Should().Be("Bob");
    }

    [Fact]
    public void Kanban_Swimlanes_ReorderWithinLane_FiresReordered()
    {
        var items = new List<LaneTask>
        {
            new(1, "A", "todo", "Alice"),
            new(2, "A2", "todo", "Alice"),
            new(3, "C", "todo", "Bob"),
        };
        KanbanMoveEvent<LaneTask>? reordered = null;
        var cut = RenderSwimlaneBoard(p => p.Add(x => x.OnItemReordered, e => reordered = e), items: items);

        cut.FindAll("[data-testid='cell-todo-Alice'] .tm-kanban__card")[0].TriggerEvent(DragStart, new DragEventArgs());
        cut.FindAll("[data-testid='cell-todo-Alice'] .tm-kanban__card")[1].TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='cell-todo-Alice']").TriggerEvent(Drop, new DragEventArgs());

        reordered.Should().NotBeNull();
        reordered!.FromColumn.Should().Be("todo");
        reordered.ToColumn.Should().Be("todo");
        reordered.FromSwimlane.Should().Be("Alice");
        reordered.ToSwimlane.Should().Be("Alice");
    }

    [Fact]
    public void Kanban_Swimlanes_DropToNoValueLane_ToSwimlaneNull()
    {
        KanbanMoveEvent<LaneTask>? moved = null;
        var cut = RenderSwimlaneBoard(p => p.Add(x => x.OnItemMoved, e => moved = e));

        // Drag A (todo, Alice) into the no-value lane's todo cell
        cut.Find("[data-testid='cell-todo-Alice'] .tm-kanban__card").TriggerEvent(DragStart, new DragEventArgs());
        cut.Find("[data-testid='cell-todo-none']").TriggerEvent(DragOver, new DragEventArgs());
        cut.Find("[data-testid='cell-todo-none']").TriggerEvent(Drop, new DragEventArgs());

        moved.Should().NotBeNull();
        moved!.FromSwimlane.Should().Be("Alice");
        moved.ToSwimlane.Should().BeNull(); // the "no value" lane maps to null, not the internal sentinel
    }

    [Fact]
    public void Kanban_ColumnHeaderTemplate_Rendered_WhenProvided()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.ColumnHeaderTemplate, col => (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddAttribute(1, "class", "custom-col-header");
                b.AddContent(2, col.Title);
                b.CloseElement();
            })));

        cut.FindAll(".custom-col-header").Count.Should().Be(3);
    }

    [Fact]
    public void Kanban_EmptyColumnTemplate_Rendered_WhenProvided()
    {
        var items = new List<KanbanTask> { new(1, "A", "todo") }; // doing + done empty
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, items)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.EmptyColumnTemplate, (RenderFragment)(b =>
            {
                b.OpenElement(0, "div");
                b.AddAttribute(1, "class", "custom-empty");
                b.AddContent(2, "Nothing here");
                b.CloseElement();
            })));

        cut.FindAll(".custom-empty").Count.Should().Be(2);
        cut.FindAll(".tm-kanban__empty").Should().BeEmpty(); // fallback not used when template supplied
    }

    [Fact]
    public void Kanban_NoSwimlaneSelector_NoSwimlaneMarkup()
    {
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        cut.FindAll(".tm-kanban__swimlane").Should().BeEmpty();
        cut.FindAll(".tm-kanban__column-headers").Should().BeEmpty();
        cut.FindAll(".tm-kanban__column").Count.Should().Be(3); // unchanged classic layout
    }

    [Fact]
    public void KanbanMoveEvent_SwimlaneFields_DefaultNull_AndSettable()
    {
        var legacy = new KanbanMoveEvent<int>(1, "a", "b");
        legacy.FromSwimlane.Should().BeNull();
        legacy.ToSwimlane.Should().BeNull();

        var full = new KanbanMoveEvent<int>(1, "a", "b", 0, 0, "laneA", "laneB");
        full.FromSwimlane.Should().Be("laneA");
        full.ToSwimlane.Should().Be("laneB");
    }

    // ══════════════════════════════════════════════════════════════════════
    // K10: opt-in vertical CARD virtualization within columns
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Kanban_VirtualizeCards_RendersVirtualizedColumnBody_WithCards()
    {
        // A column holding many cards. With VirtualizeCards on, its body becomes a dedicated
        // scroll container that renders the visible window through <Virtualize>.
        var many = Enumerable.Range(1, 60).Select(i => new KanbanTask(i, $"Task {i}", "todo")).ToList();

        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, many)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.VirtualizeCards, true));

        // The opt-in virtualized scroll container exists for the (non-swimlane) column body.
        var body = cut.FindAll("[data-testid='cards-virtual-todo']");
        body.Should().HaveCount(1);
        body[0].ClassList.Should().Contain("tm-kanban__cards--virtual");

        // Cards still render through the SAME card template, inside the virtualized body.
        // (bUnit has no viewport, so <Virtualize> renders the items — assert presence, not a window size.)
        cut.FindAll("[data-testid='cards-virtual-todo'] .tm-kanban__card").Count.Should().BeGreaterThan(0);
        cut.Markup.Should().Contain("Task 1");
    }

    [Fact]
    public void Kanban_VirtualizeCards_DefaultFalse_UsesStandardColumnBody()
    {
        // Default (opt-out) keeps the exact existing @foreach card rendering — no virtual container.
        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, Tasks)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title))));

        cut.FindAll(".tm-kanban__cards--virtual").Should().BeEmpty();
        cut.FindAll("[data-testid='cards-virtual-todo']").Should().BeEmpty();
        cut.FindAll(".tm-kanban__card").Count.Should().Be(4);
    }

    [Fact]
    public void Kanban_VirtualizeCards_CardsRemainDraggable_AndClickable()
    {
        KanbanTask? clicked = null;
        var many = Enumerable.Range(1, 30).Select(i => new KanbanTask(i, $"Task {i}", "todo")).ToList();

        var cut = Render<TmKanbanBoard<KanbanTask>>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Items, many)
            .Add(x => x.ColumnSelector, t => t.Status)
            .Add(x => x.CardTemplate, t => (RenderFragment)(b => b.AddContent(0, t.Title)))
            .Add(x => x.VirtualizeCards, true)
            .Add(x => x.OnCardClick, t => clicked = t));

        var cards = cut.FindAll("[data-testid='cards-virtual-todo'] .tm-kanban__card");
        cards[0].GetAttribute("draggable").Should().Be("true");

        cards[0].Click();
        clicked.Should().NotBeNull();
        clicked!.Id.Should().Be(1);
    }
}
