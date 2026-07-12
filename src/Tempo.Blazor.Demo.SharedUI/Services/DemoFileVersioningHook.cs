using Tempo.Blazor.Abstractions.Interfaces;
using Tempo.Blazor.Abstractions.Shared;
using Tempo.Blazor.Components.Files;

namespace Tempo.Blazor.Demo.Services;

/// <summary>
/// In-memory demo versioning store. Each item is lazily seeded with a couple of text versions so
/// the history, compare (line diff) and restore actions have something to show. Uses
/// <see cref="TmTextLineDiff"/> for the diff.
/// </summary>
public sealed class DemoFileVersioningHook : IFileVersioningHook
{
    private sealed record Entry(TmFileVersion Version, string Content);

    private readonly Dictionary<string, List<Entry>> _store = new();

    private List<Entry> EnsureSeeded(string itemId)
    {
        if (_store.TryGetValue(itemId, out var existing)) return existing;

        var list = new List<Entry>
        {
            new(new TmFileVersion
            {
                VersionId = $"{itemId}-1", ItemId = itemId, VersionNumber = 1,
                FileName = "document.txt", SizeBytes = 68,
                CreatedAt = DateTimeOffset.Now.AddDays(-6),
                CreatedBy = new TmUserRef { Id = "amelia", DisplayName = "Amelia Novák" },
                Comment = "Initial draft"
            }, "# Project plan\nGoal: ship v1\nBudget: TBD\nOwner: unassigned"),
            new(new TmFileVersion
            {
                VersionId = $"{itemId}-2", ItemId = itemId, VersionNumber = 2,
                FileName = "document.txt", SizeBytes = 96,
                CreatedAt = DateTimeOffset.Now.AddDays(-2),
                CreatedBy = new TmUserRef { Id = "ben", DisplayName = "Ben Dvořák" },
                Comment = "Add budget and timeline", IsCurrent = true
            }, "# Project plan\nGoal: ship v1\nBudget: $10,000\nOwner: Ben Dvořák\nTimeline: Q3")
        };
        _store[itemId] = list;
        return list;
    }

    public Task<IReadOnlyList<TmFileVersion>> GetVersionsAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var list = EnsureSeeded(itemId);
        var ordered = list.Select(e => e.Version).OrderByDescending(v => v.VersionNumber).ToList();
        return Task.FromResult<IReadOnlyList<TmFileVersion>>(ordered);
    }

    public Task<TmFileVersion> CreateVersionAsync(FileVersionRequest request, CancellationToken cancellationToken = default)
    {
        var list = EnsureSeeded(request.ItemId);
        foreach (var e in list) e.Version.IsCurrent = false;
        var version = new TmFileVersion
        {
            ItemId = request.ItemId,
            VersionNumber = list.Count + 1,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            AssetId = request.AssetId,
            CreatedBy = request.CreatedBy,
            Comment = request.Comment,
            IsCurrent = true
        };
        list.Add(new Entry(version, string.Empty));
        return Task.FromResult(version);
    }

    public Task<TmFileVersion> RestoreVersionAsync(string itemId, string versionId, CancellationToken cancellationToken = default)
    {
        var list = EnsureSeeded(itemId);
        var source = list.First(e => e.Version.VersionId == versionId);
        foreach (var e in list) e.Version.IsCurrent = false;
        var restored = new TmFileVersion
        {
            ItemId = itemId,
            VersionNumber = list.Count + 1,
            FileName = source.Version.FileName,
            ContentType = source.Version.ContentType,
            SizeBytes = source.Version.SizeBytes,
            CreatedAt = DateTimeOffset.Now,
            CreatedBy = new TmUserRef { Id = "you", DisplayName = "You" },
            Comment = $"Restored from v{source.Version.VersionNumber}",
            IsCurrent = true
        };
        list.Add(new Entry(restored, source.Content));
        return Task.FromResult(restored);
    }

    public Task<TmFileVersionDiff> DiffAsync(string itemId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
    {
        var list = EnsureSeeded(itemId);
        var from = list.First(e => e.Version.VersionId == fromVersionId);
        var to = list.First(e => e.Version.VersionId == toVersionId);
        return Task.FromResult(new TmFileVersionDiff
        {
            ItemId = itemId,
            FromVersionId = fromVersionId,
            ToVersionId = toVersionId,
            IsTextDiff = true,
            Lines = TmTextLineDiff.Compute(from.Content, to.Content),
            SizeDelta = to.Version.SizeBytes - from.Version.SizeBytes,
            FromFileName = from.Version.FileName,
            ToFileName = to.Version.FileName
        });
    }
}
