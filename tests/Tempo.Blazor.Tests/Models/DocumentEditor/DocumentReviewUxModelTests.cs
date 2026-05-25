using FluentAssertions;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.Models.DocumentEditor;

public class DocumentReviewUxModelTests
{
    [Fact]
    public void CommentComparer_FiltersOpenResolvedMineAndAll()
    {
        var comments = new[]
        {
            Comment("open-mine", "b", 10, DocumentCommentStatus.Open, "me"),
            Comment("resolved-mine", "b", 20, DocumentCommentStatus.Resolved, "me"),
            Comment("open-other", "b", 30, DocumentCommentStatus.Open, "other")
        };

        DocumentCommentComparer.Apply(comments, DocumentCommentFilter.Open, DocumentCommentSortMode.Position, "me")
            .Select(comment => comment.Id)
            .Should()
            .Equal("open-mine", "open-other");
        DocumentCommentComparer.Apply(comments, DocumentCommentFilter.Resolved, DocumentCommentSortMode.Position, "me")
            .Select(comment => comment.Id)
            .Should()
            .Equal("resolved-mine");
        DocumentCommentComparer.Apply(comments, DocumentCommentFilter.Mine, DocumentCommentSortMode.Position, "me")
            .Select(comment => comment.Id)
            .Should()
            .Equal("open-mine", "resolved-mine");
        DocumentCommentComparer.Apply(comments, DocumentCommentFilter.All, DocumentCommentSortMode.Position, "me")
            .Should()
            .HaveCount(3);
    }

    [Fact]
    public void CommentComparer_SortsByPositionThenTime()
    {
        var older = DateTimeOffset.Parse("2026-05-17T08:00:00Z");
        var newer = DateTimeOffset.Parse("2026-05-17T09:00:00Z");
        var comments = new[]
        {
            Comment("second", "b", 20, DocumentCommentStatus.Open, "a", older),
            Comment("first", "a", 30, DocumentCommentStatus.Open, "a", older),
            Comment("latest", "c", 10, DocumentCommentStatus.Open, "a", newer)
        };

        DocumentCommentComparer.Apply(comments, DocumentCommentFilter.All, DocumentCommentSortMode.Position)
            .Select(comment => comment.Id)
            .Should()
            .Equal("first", "second", "latest");
        DocumentCommentComparer.Apply(comments, DocumentCommentFilter.All, DocumentCommentSortMode.Time)
            .Select(comment => comment.Id)
            .Should()
            .StartWith("latest");
    }

    [Fact]
    public void RevisionFilter_MatchesAuthorAndType()
    {
        var filter = new DocumentRevisionFilter
        {
            AuthorId = "author-1",
            Type = DocumentRevisionType.Deletion
        };

        filter.Matches(Revision("author-1", DocumentRevisionType.Deletion)).Should().BeTrue();
        filter.Matches(Revision("author-2", DocumentRevisionType.Deletion)).Should().BeFalse();
        filter.Matches(Revision("author-1", DocumentRevisionType.Insertion)).Should().BeFalse();
    }

    [Fact]
    public void RevisionGrouper_GroupsAdjacentPendingTypingByAuthorTypeAndBlock()
    {
        var revisions = new[]
        {
            Revision("author-1", DocumentRevisionType.Insertion, "p1", 0, 1, "r1"),
            Revision("author-1", DocumentRevisionType.Insertion, "p1", 1, 2, "r2"),
            Revision("author-1", DocumentRevisionType.Insertion, "p1", 2, 3, "r3")
        };

        var groups = DocumentRevisionGrouper.GroupAdjacentPending(revisions);

        groups.Should().ContainSingle();
        groups[0].GroupId.Should().Be("r1");
        groups[0].Revisions.Select(revision => revision.Id).Should().Equal("r1", "r2", "r3");
        groups[0].Range.StartOffset.Should().Be(0);
        groups[0].Range.EndOffset.Should().Be(3);
    }

    [Fact]
    public void RevisionGrouper_DoesNotGroupAcrossAuthorTypeBlockOrReviewedStatus()
    {
        var revisions = new[]
        {
            Revision("author-1", DocumentRevisionType.Insertion, "p1", 0, 1, "r1"),
            Revision("author-2", DocumentRevisionType.Insertion, "p1", 1, 2, "r2"),
            Revision("author-2", DocumentRevisionType.Deletion, "p1", 2, 3, "r3"),
            Revision("author-2", DocumentRevisionType.Deletion, "p2", 3, 4, "r4"),
            Revision("author-2", DocumentRevisionType.Deletion, "p2", 4, 5, "r5", DocumentRevisionAction.Accepted)
        };

        var groups = DocumentRevisionGrouper.GroupAdjacentPending(revisions);

        groups.Should().HaveCount(5);
        groups.Should().OnlyContain(group => group.Revisions.Count == 1);
    }

    private static DocumentComment Comment(
        string id,
        string blockId,
        int offset,
        DocumentCommentStatus status,
        string authorId,
        DateTimeOffset? createdAt = null)
        => new()
        {
            Id = id,
            Status = status,
            Anchor = new DocumentCommentAnchor
            {
                BlockId = blockId,
                StartOffset = offset
            },
            Entries =
            [
                new DocumentCommentEntry
                {
                    Author = new DocumentEditorAuthor { Id = authorId, DisplayName = authorId },
                    Text = id,
                    CreatedAt = createdAt ?? DateTimeOffset.Parse("2026-05-17T08:00:00Z")
                }
            ]
        };

    private static DocumentRevision Revision(string authorId, DocumentRevisionType type)
        => new()
        {
            Type = type,
            Author = new DocumentRevisionAuthor { Id = authorId }
        };

    private static DocumentRevision Revision(
        string authorId,
        DocumentRevisionType type,
        string blockId,
        int start,
        int end,
        string id,
        DocumentRevisionAction action = DocumentRevisionAction.Pending)
        => new()
        {
            Id = id,
            Type = type,
            Author = new DocumentRevisionAuthor { Id = authorId },
            Action = action,
            Range = new DocumentRevisionRange
            {
                BlockId = blockId,
                StartInlineIndex = 0,
                EndInlineIndex = 0,
                StartOffset = start,
                EndOffset = end
            }
        };
}
