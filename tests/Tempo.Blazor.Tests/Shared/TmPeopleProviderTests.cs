using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Abstractions.Shared;

namespace Tempo.Blazor.Tests.Shared;

public class TmPeopleProviderTests
{
    [Fact]
    public async Task BaseProvider_GetByIdAsync_ResolvesThroughBatchSearch()
    {
        var provider = new MemoryPeopleProvider(
        [
            new TmUser { Id = "u1", DisplayName = "Ada Lovelace" },
            new TmUser { Id = "u2", DisplayName = "Grace Hopper" }
        ]);

        var user = await provider.GetByIdAsync("u2");

        user!.DisplayName.Should().Be("Grace Hopper");
        provider.LastQuery!.Ids.Should().Equal("u2");
    }

    [Fact]
    public async Task BaseProvider_GetByIdsAsync_DeduplicatesAndPreservesRequestedOrder()
    {
        var provider = new MemoryPeopleProvider(
        [
            new TmUser { Id = "u1", DisplayName = "Ada Lovelace" },
            new TmUser { Id = "u2", DisplayName = "Grace Hopper" },
            new TmUser { Id = "u3", DisplayName = "Katherine Johnson" }
        ]);

        var users = await provider.GetByIdsAsync(["u3", "u1", "u3", "missing"]);

        users.Select(user => user.Id).Should().Equal("u3", "u1");
    }

    [Fact]
    public async Task SearchAsync_HonorsSearchTextVirtualFilterAndLimit()
    {
        var provider = new MemoryPeopleProvider(
        [
            new TmUser { Id = "u1", DisplayName = "Ada Lovelace", UserName = "ada" },
            new TmUser { Id = "u2", DisplayName = "Ada Virtual Team", UserName = "ada-team", IsVirtual = true },
            new TmUser { Id = "u3", DisplayName = "Grace Hopper", UserName = "grace" }
        ]);

        var users = await provider.SearchAsync(new TmPeopleQuery
        {
            SearchText = "ada",
            IncludeVirtual = false,
            Take = 1
        });

        users.Should().ContainSingle(user => user.Id == "u1");
    }

    [Fact]
    public void ServiceCollectionExtensions_RegisterPeopleProviderAndCurrentUser()
    {
        var provider = new MemoryPeopleProvider([]);
        var services = new ServiceCollection()
            .AddTmPeopleProvider(provider)
            .AddTmCurrentUser<StaticCurrentUser>();

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ITmPeopleProvider>().Should().BeSameAs(provider);
        serviceProvider.GetRequiredService<ITmCurrentUser>().Should().BeOfType<StaticCurrentUser>();
    }

    private sealed class MemoryPeopleProvider(IReadOnlyList<TmUser> users) : TmPeopleProviderBase
    {
        public TmPeopleQuery? LastQuery { get; private set; }

        public override Task<IReadOnlyList<TmUser>> SearchAsync(
            TmPeopleQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            IEnumerable<TmUser> results = users;

            if (query.Ids.Count > 0)
            {
                var requested = query.Ids.ToHashSet(StringComparer.Ordinal);
                results = results.Where(user => requested.Contains(user.Id));
            }
            else if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                results = results.Where(user =>
                    user.DisplayName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (user.UserName?.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.Email?.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (!query.IncludeVirtual)
            {
                results = results.Where(user => !user.IsVirtual);
            }

            var take = query.Take <= 0 ? users.Count : query.Take;
            return Task.FromResult<IReadOnlyList<TmUser>>(results.Take(take).ToArray());
        }
    }

    private sealed class StaticCurrentUser : ITmCurrentUser
    {
        public ValueTask<TmCurrentUserState> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TmCurrentUserState.FromUser(new TmUserRef
            {
                Id = "u1",
                DisplayName = "Ada Lovelace"
            }));
    }
}
