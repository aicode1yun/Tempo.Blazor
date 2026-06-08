using FluentAssertions;
using Tempo.Blazor.Demo.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.NotionEditor.Services;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionBlockDiffServiceTests
{
    [Fact]
    public void Compare_ReturnsAddedRemovedModifiedAndMovedBlockDiffs()
    {
        var sharedModifiedId = Guid.Parse("cf230000-0000-0000-0000-000000000001");
        var sharedMovedId = Guid.Parse("cf230000-0000-0000-0000-000000000002");
        var removedId = Guid.Parse("cf230000-0000-0000-0000-000000000003");
        var addedId = Guid.Parse("cf230000-0000-0000-0000-000000000004");

        var before = new[]
        {
            Block(sharedModifiedId, 0, BlockType.Paragraph, "Original paragraph"),
            Block(sharedMovedId, 1, BlockType.Heading2, "Moved heading"),
            Block(removedId, 2, BlockType.Callout, "Removed callout")
        };
        var after = new[]
        {
            Block(sharedMovedId, 0, BlockType.Heading2, "Moved heading"),
            Block(sharedModifiedId, 1, BlockType.Paragraph, "Updated paragraph"),
            Block(addedId, 2, BlockType.TodoItem, "Added task")
        };

        var diffs = NotionBlockDiffService.Compare(before, after);

        diffs.Should().ContainSingle(diff =>
            diff.BlockId == sharedModifiedId.ToString("D") &&
            diff.DiffType == BlockDiffType.Modified &&
            diff.BeforeOrder == 0 &&
            diff.AfterOrder == 1);
        diffs.Should().ContainSingle(diff =>
            diff.BlockId == sharedMovedId.ToString("D") &&
            diff.DiffType == BlockDiffType.Moved &&
            diff.BeforeOrder == 1 &&
            diff.AfterOrder == 0);
        diffs.Should().ContainSingle(diff =>
            diff.BlockId == removedId.ToString("D") &&
            diff.DiffType == BlockDiffType.Removed &&
            diff.After == null);
        diffs.Should().ContainSingle(diff =>
            diff.BlockId == addedId.ToString("D") &&
            diff.DiffType == BlockDiffType.Added &&
            diff.Before == null);
    }

    [Fact]
    public void Compare_ReturnsEmpty_WhenSnapshotsAreEquivalent()
    {
        var blockId = Guid.Parse("cf230000-0000-0000-0000-000000000010");
        var before = new[] { Block(blockId, 0, BlockType.Paragraph, "Stable text") };
        var after = new[] { Block(blockId, 0, BlockType.Paragraph, "Stable text") };

        NotionBlockDiffService.Compare(before, after).Should().BeEmpty();
    }

    [Fact]
    public void Compare_ReturnsSnapshotCopies()
    {
        var removed = Block(Guid.Parse("cf230000-0000-0000-0000-000000000020"), 0, BlockType.Paragraph, "Stable text");

        var diff = NotionBlockDiffService.Compare([removed], []).Should().ContainSingle().Subject;

        diff.Before.Should().NotBeNull();
        diff.Before.Should().NotBeSameAs(removed);
        diff.Before!.Content.Should().NotBeSameAs(removed.Content);

        ((TextBlockContent)diff.Before.Content).Html = "Mutated snapshot";

        ((TextBlockContent)removed.Content).Html.Should().Be("Stable text");
    }

    [Fact]
    public async Task MockHistoryProvider_UsesStableBlockIdsAcrossSeededVersions()
    {
        var provider = new MockNotionHistoryProvider();

        var diffs = await provider.GetDiffAsync(
            "11111111-1111-1111-1111-111111111111",
            "a0000000-0000-0000-0000-000000000002",
            "a0000000-0000-0000-0000-000000000003");

        diffs.Should().Contain(diff => diff.DiffType == BlockDiffType.Modified);
        diffs.Should().Contain(diff => diff.DiffType == BlockDiffType.Moved);
    }

    private static PageBlock Block(Guid id, int order, BlockType type, string html) => new()
    {
        Id = id,
        PageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Type = type,
        Order = order,
        Content = type switch
        {
            BlockType.Heading1 => new HeadingBlockContent { Level = 1, Html = html },
            BlockType.Heading2 => new HeadingBlockContent { Level = 2, Html = html },
            BlockType.TodoItem => new TodoBlockContent { Html = html },
            BlockType.Callout => new CalloutBlockContent { Html = html },
            _ => new TextBlockContent { Html = html }
        },
        CreatedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
        LastEditedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(order)
    };
}
