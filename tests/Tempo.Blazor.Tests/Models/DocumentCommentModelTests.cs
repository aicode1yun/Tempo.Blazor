using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Tests.Models;

public class DocumentCommentModelTests
{
    [Fact]
    public void Anchor_Point_ClampsNormalizedCoordinates()
    {
        var anchor = DocumentCommentAnchor.Point(2, 1.2, -0.2);

        anchor.PageNumber.Should().Be(2);
        anchor.Kind.Should().Be(DocumentCommentAnchorKind.Point);
        anchor.X.Should().Be(1);
        anchor.Y.Should().Be(0);
        DocumentCommentHelper.IsValidAnchor(anchor).Should().BeTrue();
    }

    [Fact]
    public void Anchor_Area_ClampsSizeInsidePage()
    {
        var anchor = DocumentCommentAnchor.Area(1, 0.8, 0.7, 0.5, 0.5);

        anchor.Kind.Should().Be(DocumentCommentAnchorKind.Area);
        anchor.Width.Should().BeApproximately(0.2, 0.001);
        anchor.Height.Should().BeApproximately(0.3, 0.001);
        DocumentCommentHelper.IsValidAnchor(anchor).Should().BeTrue();
    }

    [Fact]
    public void Anchor_Page_IsValidWithoutCoordinates()
    {
        var anchor = DocumentCommentAnchor.Page(3);

        anchor.PageNumber.Should().Be(3);
        anchor.Kind.Should().Be(DocumentCommentAnchorKind.Page);
        DocumentCommentHelper.IsValidAnchor(anchor).Should().BeTrue();
    }

    [Fact]
    public void Anchor_InvalidPageNumber_IsRejected()
    {
        var anchor = DocumentCommentAnchor.Page(0);

        DocumentCommentHelper.IsValidAnchor(anchor).Should().BeFalse();
    }

    [Fact]
    public void Helper_CountsOpenThreads()
    {
        var threads = new[]
        {
            new DocumentCommentThread { Status = DocumentCommentThreadStatus.Open },
            new DocumentCommentThread { Status = DocumentCommentThreadStatus.Resolved },
            new DocumentCommentThread { Status = DocumentCommentThreadStatus.Open }
        };

        DocumentCommentHelper.CountOpenThreads(threads).Should().Be(2);
    }

    [Fact]
    public void Helper_CountsMentionedThreadsForCurrentUser()
    {
        var threads = new[]
        {
            new DocumentCommentThread
            {
                Comments =
                [
                    new DocumentComment
                    {
                        Mentions = [new DocumentCommentMention { UserId = "u1", DisplayName = "Alice" }]
                    }
                ]
            },
            new DocumentCommentThread
            {
                Comments =
                [
                    new DocumentComment
                    {
                        Mentions = [new DocumentCommentMention { UserId = "u2", DisplayName = "Bob" }]
                    }
                ]
            }
        };

        DocumentCommentHelper.CountMentionedThreads(threads, "u1").Should().Be(1);
        DocumentCommentHelper.MentionsUser(threads[0], "u1").Should().BeTrue();
    }

    [Fact]
    public void PermissionHelper_AllowsExplicitPermissionOrAuthor()
    {
        var comment = new DocumentComment
        {
            AuthorId = "u1",
            CanEdit = false,
            CanDelete = true
        };

        DocumentCommentPermissionHelper.CanEdit(comment, "u1").Should().BeTrue();
        DocumentCommentPermissionHelper.CanDelete(comment, "u2").Should().BeTrue();
        DocumentCommentPermissionHelper.CanEdit(comment, "u2").Should().BeFalse();
    }

    [Fact]
    public void GeometryHelper_FormatsAnchorStylesUsingInvariantPercentages()
    {
        var point = DocumentCommentAnchor.Point(1, 0.25, 0.4);
        var area = DocumentCommentAnchor.Area(1, 0.125, 0.4, 0.2, 0.1);

        DocumentCommentGeometryHelper.ToPointStyle(point).Should().Be("left: 25%; top: 40%;");
        DocumentCommentGeometryHelper.ToAreaStyle(area).Should().Be("left: 12.5%; top: 40%; width: 20%; height: 10%;");
    }
}
