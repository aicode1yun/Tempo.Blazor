using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tempo.ReportServer.Web.Services;

namespace Tempo.ReportServer.Web.Tests;

/// <summary>
/// Round-trip specification for <see cref="DistributedCacheReportServerTokenStore"/>, the scale-out
/// (shared cache) backing of the server-side token store.
/// </summary>
public sealed class DistributedCacheReportServerTokenStoreTests
{
    private static IDistributedCache NewCache()
        => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public void SetThenGet_ReturnsStoredTokens()
    {
        var store = new DistributedCacheReportServerTokenStore(NewCache());
        var tokens = new ReportServerTokenSet("access-1", "refresh-1", DateTimeOffset.UtcNow.AddMinutes(5));

        store.Set("subject-1", tokens);
        var round = store.Get("subject-1");

        round.Should().NotBeNull();
        round!.AccessToken.Should().Be("access-1");
        round.RefreshToken.Should().Be("refresh-1");
        round.ExpiresUtc.Should().BeCloseTo(tokens.ExpiresUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Get_UnknownSubject_ReturnsNull()
    {
        var store = new DistributedCacheReportServerTokenStore(NewCache());

        store.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Remove_DeletesTokens()
    {
        var cache = NewCache();
        var store = new DistributedCacheReportServerTokenStore(cache);
        store.Set("subject-1", new ReportServerTokenSet("a", "r", DateTimeOffset.UtcNow.AddMinutes(5)));

        store.Remove("subject-1");

        store.Get("subject-1").Should().BeNull();
    }

    [Fact]
    public void TwoStoresOverSharedCache_SeeSameTokens()
    {
        // Simulates two host instances sharing one distributed cache.
        var cache = NewCache();
        var instanceA = new DistributedCacheReportServerTokenStore(cache);
        var instanceB = new DistributedCacheReportServerTokenStore(cache);

        instanceA.Set("subject-1", new ReportServerTokenSet("access-shared", "refresh-shared", DateTimeOffset.UtcNow.AddMinutes(5)));

        instanceB.Get("subject-1")!.AccessToken.Should().Be("access-shared");
    }
}
