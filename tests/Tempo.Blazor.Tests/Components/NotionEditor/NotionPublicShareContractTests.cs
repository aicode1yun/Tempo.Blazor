using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionPublicShareContractTests
{
    [Fact]
    public void PublicShareDto_RoundTripsThroughJson()
    {
        var dto = new PublicShareDto
        {
            PageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Token = "cf33-token",
            IsEnabled = true,
            AllowComments = true,
            ExpiresAt = new DateTime(2026, 2, 3, 23, 59, 59, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(dto);
        var result = JsonSerializer.Deserialize<PublicShareDto>(json);

        result.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task INotionPublicShareProvider_CreatesResolvesAndRevokesShare()
    {
        var provider = new InMemoryPublicShareProvider();
        var pageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var created = await provider.CreateShareAsync(pageId, new PublicShareOptions
        {
            AllowComments = true,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        created.PageId.Should().Be(pageId);
        created.Token.Should().NotBeNullOrWhiteSpace();
        created.IsEnabled.Should().BeTrue();
        created.AllowComments.Should().BeTrue();
        (await provider.GetShareAsync(pageId)).Should().BeEquivalentTo(created);
        (await provider.ResolveByTokenAsync(created.Token)).Should().BeEquivalentTo(created);

        await provider.RevokeAsync(pageId);

        var revoked = await provider.GetShareAsync(pageId);
        revoked.Should().NotBeNull();
        revoked!.IsEnabled.Should().BeFalse();
        (await provider.ResolveByTokenAsync(created.Token)).Should().BeNull();
    }

    [Fact]
    public async Task INotionPublicShareProvider_DoesNotResolveExpiredShare()
    {
        var provider = new InMemoryPublicShareProvider();
        var created = await provider.CreateShareAsync(Guid.NewGuid(), new PublicShareOptions
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        });

        (await provider.GetShareAsync(created.PageId)).Should().NotBeNull();
        (await provider.ResolveByTokenAsync(created.Token)).Should().BeNull();
    }

    private sealed class InMemoryPublicShareProvider : INotionPublicShareProvider
    {
        private readonly Dictionary<Guid, PublicShareDto> _byPage = [];
        private readonly Dictionary<string, Guid> _byToken = new(StringComparer.Ordinal);
        private int _nextToken;

        public Task<PublicShareDto> CreateShareAsync(Guid pageId, PublicShareOptions options, CancellationToken cancellationToken = default)
        {
            var share = new PublicShareDto
            {
                PageId = pageId,
                Token = $"share-{Interlocked.Increment(ref _nextToken):000}",
                IsEnabled = true,
                AllowComments = options.AllowComments,
                ExpiresAt = options.ExpiresAt
            };

            if (_byPage.TryGetValue(pageId, out var existing))
                _byToken.Remove(existing.Token);

            _byPage[pageId] = share;
            _byToken[share.Token] = pageId;
            return Task.FromResult(Clone(share));
        }

        public Task RevokeAsync(Guid pageId, CancellationToken cancellationToken = default)
        {
            if (_byPage.TryGetValue(pageId, out var share))
            {
                share.IsEnabled = false;
                _byToken.Remove(share.Token);
            }

            return Task.CompletedTask;
        }

        public Task<PublicShareDto?> GetShareAsync(Guid pageId, CancellationToken cancellationToken = default)
            => Task.FromResult(_byPage.TryGetValue(pageId, out var share) ? Clone(share) : null);

        public Task<PublicShareDto?> ResolveByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (!_byToken.TryGetValue(token, out var pageId) || !_byPage.TryGetValue(pageId, out var share))
                return Task.FromResult<PublicShareDto?>(null);

            if (!share.IsEnabled || share.ExpiresAt is not null && share.ExpiresAt.Value.ToUniversalTime() <= DateTime.UtcNow)
                return Task.FromResult<PublicShareDto?>(null);

            return Task.FromResult<PublicShareDto?>(Clone(share));
        }

        private static PublicShareDto Clone(PublicShareDto share) => new()
        {
            PageId = share.PageId,
            Token = share.Token,
            IsEnabled = share.IsEnabled,
            AllowComments = share.AllowComments,
            ExpiresAt = share.ExpiresAt
        };
    }
}
