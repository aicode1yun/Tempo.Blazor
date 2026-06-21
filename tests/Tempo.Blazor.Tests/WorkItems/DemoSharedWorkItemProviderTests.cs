using FluentAssertions;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Demo.Services;

namespace Tempo.Blazor.Tests.WorkItems;

/// <summary>
/// Verifies the demo's single shared provider behaves as one store, so a write made by one
/// component (e.g. the Gantt) is observed by another (e.g. Notion "My Tasks") reading the same instance.
/// </summary>
public sealed class DemoSharedWorkItemProviderTests
{
    [Fact]
    public async Task Seeded_Items_AreReturned()
    {
        var provider = new DemoSharedWorkItemProvider();

        var result = await provider.SearchAsync(new TmWorkItemQuery { IncludeCompleted = true, Take = 100 });

        result.Items.Should().Contain(i => i.Title == "Design sign-off");
    }

    [Fact]
    public async Task Create_ThroughProvider_IsVisibleToSubsequentSearch()
    {
        var provider = new DemoSharedWorkItemProvider();

        // Simulates the Gantt creating a task through WorkItemSource…
        await provider.CreateAsync(new TmWorkItem { Title = "Cross-component task", Start = DateTime.Today, End = DateTime.Today.AddDays(1) });

        // …and the Notion "My Tasks" panel reading the same shared instance.
        var seen = await provider.SearchAsync(new TmWorkItemQuery { IncludeCompleted = true, Take = 100 });
        seen.Items.Should().Contain(i => i.Title == "Cross-component task");
    }

    [Fact]
    public async Task SetCompleted_IsReflectedAcrossReads()
    {
        var provider = new DemoSharedWorkItemProvider();

        await provider.SetCompletedAsync("p4", true);

        var open = await provider.SearchAsync(new TmWorkItemQuery { IncludeCompleted = false, Take = 100 });
        open.Items.Should().NotContain(i => i.Id == "p4");

        var item = await provider.GetByIdAsync("p4");
        item!.IsCompleted.Should().BeTrue();
        item.Status.Should().Be(TmWorkItemStatus.Done);
    }

    [Fact]
    public async Task Dependencies_AreExposed()
    {
        var provider = new DemoSharedWorkItemProvider();

        var deps = await provider.GetDependenciesAsync([]);

        deps.Should().Contain(d => d.FromId == "p2" && d.ToId == "p3");
    }
}
