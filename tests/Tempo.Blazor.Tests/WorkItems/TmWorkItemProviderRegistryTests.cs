using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tempo.Blazor.Abstractions.WorkItems;
using Tempo.Blazor.Models;

namespace Tempo.Blazor.Tests.WorkItems;

public sealed class TmWorkItemProviderRegistryTests
{
    [Fact]
    public void Registry_SeparatesProvidersBySourceKey()
    {
        var demo = new InMemoryWorkItemProvider("demo", "Demo source", [
            new TmWorkItem { Id = "T1", SourceKey = "demo", Title = "Demo item" }
        ]);
        var ops = new InMemoryWorkItemProvider("ops", "Ops source", [
            new TmWorkItem { Id = "T2", SourceKey = "ops", Title = "Ops item" }
        ]);

        var registry = new TmWorkItemProviderRegistry([demo, ops], NullLogger<TmWorkItemProviderRegistry>.Instance);

        registry.Count.Should().Be(2);
        registry.GetProvider("demo").Should().BeSameAs(demo);
        registry.GetProvider("ops").Should().BeSameAs(ops);
        registry.GetProvider("missing").Should().BeNull();
        registry.GetAll().Select(p => p.SourceKey).Should().Equal("demo", "ops");
    }

    [Fact]
    public void GetDefault_ReturnsSingleProvider_OrNullWhenAmbiguous()
    {
        var single = new TmWorkItemProviderRegistry(
            [new InMemoryWorkItemProvider("only", "Only", [])], null);
        single.GetDefault().Should().NotBeNull();
        single.GetDefault()!.SourceKey.Should().Be("only");

        var many = new TmWorkItemProviderRegistry(
            [new InMemoryWorkItemProvider("a", "A", []), new InMemoryWorkItemProvider("b", "B", [])], null);
        many.GetDefault().Should().BeNull();
    }

    [Fact]
    public void Registry_IgnoresDuplicateSourceKeys_KeepingFirst()
    {
        var first = new InMemoryWorkItemProvider("dup", "First", []);
        var second = new InMemoryWorkItemProvider("dup", "Second", []);

        var registry = new TmWorkItemProviderRegistry([first, second], NullLogger<TmWorkItemProviderRegistry>.Instance);

        registry.Count.Should().Be(1);
        registry.GetProvider("dup").Should().BeSameAs(first);
    }

    [Fact]
    public async Task ProviderBase_GetById_FallsBackToSearch()
    {
        var provider = new InMemoryWorkItemProvider("demo", "Demo", [
            new TmWorkItem { Id = "T1", SourceKey = "demo", Title = "One" },
            new TmWorkItem { Id = "T2", SourceKey = "demo", Title = "Two" }
        ]);

        var item = await provider.GetByIdAsync("T2");

        item.Should().NotBeNull();
        item!.Title.Should().Be("Two");
    }

    [Fact]
    public async Task ProviderBase_SetCompleted_UpdatesStatusAndProgress()
    {
        var provider = new InMemoryWorkItemProvider("demo", "Demo", [
            new TmWorkItem { Id = "T1", SourceKey = "demo", Title = "One", Status = TmWorkItemStatus.Open }
        ]);

        await provider.SetCompletedAsync("T1", true);

        var item = await provider.GetByIdAsync("T1");
        item!.IsCompleted.Should().BeTrue();
        item.Status.Should().Be(TmWorkItemStatus.Done);
        item.PercentComplete.Should().Be(100);
    }

    [Fact]
    public async Task ProviderBase_UnsupportedMutations_Throw()
    {
        var provider = new ReadOnlyWorkItemProvider();

        provider.Capabilities.Should().Be(TmWorkItemCapabilities.Read);
        await FluentActions.Awaiting(() => provider.CreateAsync(new TmWorkItem()))
            .Should().ThrowAsync<NotSupportedException>();
        await FluentActions.Awaiting(() => provider.DeleteAsync("x"))
            .Should().ThrowAsync<NotSupportedException>();
        await FluentActions.Awaiting(() => provider.AddDependencyAsync(new TmWorkItemDependency()))
            .Should().ThrowAsync<NotSupportedException>();
    }

    private sealed class ReadOnlyWorkItemProvider : TmWorkItemProviderBase
    {
        public override string SourceKey => "ro";
        public override string DisplayName => "Read only";

        public override Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<TmWorkItem> { Items = [], TotalCount = 0, Page = 1, PageSize = query.Take });
    }

    private sealed class InMemoryWorkItemProvider : TmWorkItemProviderBase
    {
        private readonly List<TmWorkItem> _items;

        public InMemoryWorkItemProvider(string sourceKey, string displayName, IEnumerable<TmWorkItem> items)
        {
            SourceKey = sourceKey;
            DisplayName = displayName;
            _items = items.ToList();
        }

        public override string SourceKey { get; }
        public override string DisplayName { get; }
        public override TmWorkItemCapabilities Capabilities => TmWorkItemCapabilities.All;

        public override Task<PagedResult<TmWorkItem>> SearchAsync(TmWorkItemQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<TmWorkItem> q = _items;

            if (query.Ids.Count > 0)
                q = q.Where(i => query.Ids.Contains(i.Id));
            if (!string.IsNullOrWhiteSpace(query.FreeText))
                q = q.Where(i => i.Title.Contains(query.FreeText, StringComparison.OrdinalIgnoreCase));
            if (!query.IncludeCompleted)
                q = q.Where(i => !i.IsCompleted);

            var matches = q.ToArray();
            return Task.FromResult(new PagedResult<TmWorkItem>
            {
                Items = matches,
                TotalCount = matches.Length,
                Page = 1,
                PageSize = Math.Max(matches.Length, 1)
            });
        }

        public override Task<TmWorkItem> UpdateAsync(TmWorkItem item, CancellationToken cancellationToken = default)
        {
            var idx = _items.FindIndex(i => i.Id == item.Id);
            if (idx >= 0) _items[idx] = item;
            return Task.FromResult(item);
        }
    }
}
