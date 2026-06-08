using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class PageRestrictionContractTests
{
    [Fact]
    public void PageRestrictionDto_RoundtripsThroughJson()
    {
        var dto = new PageRestrictionDto
        {
            PageId = Guid.Parse("cf200000-0000-0000-0000-000000000001"),
            Mode = PageRestrictionMode.EditForSome,
            Entries =
            [
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.User,
                    SubjectId = "alice",
                    Permission = PageRestrictionPermission.Edit
                },
                new PageRestrictionEntryDto
                {
                    SubjectType = PageRestrictionSubjectType.Group,
                    SubjectId = "readers",
                    Permission = PageRestrictionPermission.View
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto);
        var roundtrip = JsonSerializer.Deserialize<PageRestrictionDto>(json);

        roundtrip.Should().BeEquivalentTo(dto);
    }
}
