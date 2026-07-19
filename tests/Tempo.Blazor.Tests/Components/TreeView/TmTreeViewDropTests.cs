using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Components.TreeView;
using Tempo.Blazor.Interfaces;
using Tempo.Blazor.Models;
using Tempo.Blazor.Services;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.TreeView;

public class TmTreeViewDropTests : LocalizationTestBase
{
    private record DropNode(
        string Id,
        string Label,
        string? Icon,
        bool IsLeaf,
        bool IsLoading,
        IReadOnlyList<ITreeNode<string>> Children) : ITreeNode<string>;

    private static List<ITreeNode<string>> TwoNodes() =>
    [
        new DropNode("s1", "Suite A", null, true, false, []),
        new DropNode("s2", "Suite B", null, true, false, []),
    ];

    // DragDropService is required by the component
    public TmTreeViewDropTests()
    {
        Services.AddScoped<DragDropService>();
    }

    // ── AllowDrop=false (default) ─────────────────────────────────

    [Fact]
    public void AllowDrop_False_Nodes_HaveNo_Drop_Attribute()
    {
        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, false));

        // When AllowDrop=false the component does not attach ondrop to nodes
        var firstNode = cut.FindAll(".tm-tree-node").First();
        firstNode.HasAttribute("ondrop").Should().BeFalse();
    }

    [Fact]
    public void AllowDrop_False_Nodes_HaveNo_DragOver_Attribute()
    {
        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, false));

        var firstNode = cut.FindAll(".tm-tree-node").First();
        firstNode.HasAttribute("ondragover").Should().BeFalse();
    }

    // ── AllowDrop=true ────────────────────────────────────────────

    [Fact]
    public void AllowDrop_True_Drop_Fires_OnItemsDrop_WithTargetNode()
    {
        TreeDropEventArgs<string>? received = null;

        // Seed DragDropService with some IDs before the drop
        var dragDrop = Services.GetRequiredService<DragDropService>();
        dragDrop.StartDrag(["tc-001", "tc-002"]);

        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true)
            .Add(c => c.OnItemsDrop,
                EventCallback.Factory.Create<TreeDropEventArgs<string>>(this, a => received = a)));

        cut.FindAll(".tm-tree-node").First().Drop();

        received.Should().NotBeNull();
        received!.TargetNode.Id.Should().Be("s1");
    }

    [Fact]
    public void AllowDrop_True_Drop_Passes_DraggedIds_From_Service()
    {
        TreeDropEventArgs<string>? received = null;

        var dragDrop = Services.GetRequiredService<DragDropService>();
        dragDrop.StartDrag(["tc-001", "tc-002"]);

        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true)
            .Add(c => c.OnItemsDrop,
                EventCallback.Factory.Create<TreeDropEventArgs<string>>(this, a => received = a)));

        cut.FindAll(".tm-tree-node").First().Drop();

        received!.DraggedIds.Should().BeEquivalentTo(["tc-001", "tc-002"]);
    }

    [Fact]
    public void AllowDrop_True_Drop_WithEmpty_DraggedIds_DoesNotFire_OnItemsDrop()
    {
        TreeDropEventArgs<string>? received = null;

        // DragDropService has no active drag
        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true)
            .Add(c => c.OnItemsDrop,
                EventCallback.Factory.Create<TreeDropEventArgs<string>>(this, a => received = a)));

        cut.FindAll(".tm-tree-node").First().Drop();

        received.Should().BeNull();
    }

    [Fact]
    public void AllowDrop_True_DragOver_AddsDropTargetClass()
    {
        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true));

        cut.FindAll(".tm-tree-node").First().TriggerEvent("ondragover",
            new Microsoft.AspNetCore.Components.Web.DragEventArgs());

        cut.FindAll(".tm-tree-node--drop-target").Should().HaveCount(1);
    }

    [Fact]
    public void AllowDrop_True_DragLeave_RemovesDropTargetClass()
    {
        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true));

        // Trigger dragover — re-find after each state change to avoid stale refs
        cut.FindAll(".tm-tree-node").First().TriggerEvent("ondragover",
            new Microsoft.AspNetCore.Components.Web.DragEventArgs());
        cut.FindAll(".tm-tree-node--drop-target").Should().HaveCount(1);

        cut.FindAll(".tm-tree-node--drop-target").First().TriggerEvent("ondragleave",
            new Microsoft.AspNetCore.Components.Web.DragEventArgs());
        cut.FindAll(".tm-tree-node--drop-target").Should().BeEmpty();
    }

    [Fact]
    public void AllowDrop_True_Drop_On_SecondNode_Fires_WithCorrectTarget()
    {
        TreeDropEventArgs<string>? received = null;

        var dragDrop = Services.GetRequiredService<DragDropService>();
        dragDrop.StartDrag(["tc-010"]);

        var cut = Render<TmTreeView<string>>(p => p
            .Add(c => c.Nodes, TwoNodes())
            .Add(c => c.AllowDrop, true)
            .Add(c => c.OnItemsDrop,
                EventCallback.Factory.Create<TreeDropEventArgs<string>>(this, a => received = a)));

        cut.FindAll(".tm-tree-node")[1].Drop();

        received!.TargetNode.Id.Should().Be("s2");
    }
}
