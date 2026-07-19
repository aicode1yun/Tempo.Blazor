using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tempo.Blazor.Components.NotionEditor.Sidebar;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionSpaceSwitcherTests : LocalizationTestBase
{
    public TmNotionSpaceSwitcherTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading...",
            ["Tm_Retry"] = "Retry",
            ["TmNotionSidebar_Untitled"] = "Untitled",
            ["Notion_Space_Switch"] = "Switch space",
            ["Notion_Space_Overview"] = "Overview",
            ["Notion_Space_Personal"] = "Personal",
            ["Notion_Space_Team"] = "Team",
            ["Notion_Space_Public"] = "Public",
            ["Notion_Space_Default"] = "Pages",
            ["Notion_Space_MoveHere"] = "Move current page here",
            ["Notion_Space_Empty"] = "No root pages in this space.",
            ["Notion_Space_Pages"] = "Pages",
            ["Notion_Space_PageCount"] = "{0} pages"
        });
    }

    [Fact]
    public void WithoutProvider_ShowsSingleImplicitSpace()
    {
        var cut = Render<TmNotionSpaceSwitcher>(parameters => parameters
            .Add(p => p.SpaceProvider, null));

        cut.Markup.Should().Contain("Pages");
        cut.Markup.Should().Contain("data-testid=\"notion-space-current\"");
    }

    [Fact]
    public async Task SelectingSpace_RaisesChangedAndUpdatesSelectedState()
    {
        var provider = new FakeSpaceProvider();
        string? selected = null;
        var cut = Render<TmNotionSpaceSwitcher>(parameters => parameters
            .Add(p => p.SpaceProvider, provider)
            .Add(p => p.SelectedSpaceId, "team")
            .Add(p => p.SelectedSpaceIdChanged, EventCallback.Factory.Create<string?>(this, value => selected = value)));

        cut.WaitForAssertion(() => cut.Find("[data-testid='notion-space-current']").TextContent.Should().Contain("Team Space"));
        await cut.Find("[data-testid='notion-space-current']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-space-id='personal']").ClickAsync(new MouseEventArgs());

        selected.Should().Be("personal");
    }

    [Fact]
    public async Task OverviewCanMoveCurrentPageToAnotherSpace()
    {
        var provider = new FakeSpaceProvider();
        var cut = Render<TmNotionSpaceSwitcher>(parameters => parameters
            .Add(p => p.SpaceProvider, provider)
            .Add(p => p.SelectedSpaceId, "team")
            .Add(p => p.CurrentPageId, FakeSpaceProvider.PageId.ToString("D")));

        cut.WaitForAssertion(() => cut.Find("[data-testid='notion-space-overview-toggle']").Should().NotBeNull());
        await cut.Find("[data-testid='notion-space-overview-toggle']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='notion-space-move-personal']").ClickAsync(new MouseEventArgs());

        provider.Moved.Should().ContainSingle().Which.Should().Be((FakeSpaceProvider.PageId.ToString("D"), "personal"));
    }

    private sealed class FakeSpaceProvider : INotionSpaceProvider
    {
        public static readonly Guid PageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public List<(string PageId, string SpaceId)> Moved { get; } = [];

        public Task<IReadOnlyList<NotionSpaceDto>> GetSpacesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotionSpaceDto>>([
                new NotionSpaceDto { Id = "team", Key = "TEAM", Name = "Team Space", IconEmoji = "T", Type = NotionSpaceType.Team },
                new NotionSpaceDto { Id = "personal", Key = "PERSONAL", Name = "Personal Space", IconEmoji = "P", Type = NotionSpaceType.Personal }
            ]);

        public Task<NotionSpaceDto?> GetSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<NotionSpaceDto?>(new NotionSpaceDto { Id = spaceId, Key = spaceId.ToUpperInvariant(), Name = spaceId });

        public Task<NotionSpaceDto> CreateSpaceAsync(NotionSpaceDto space, CancellationToken cancellationToken = default)
            => Task.FromResult(space);

        public Task<IReadOnlyList<INotionPage>> GetPagesInSpaceAsync(string spaceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<INotionPage>>(spaceId == "team"
                ? [new NotionPage { Id = PageId, Title = "Architecture", SpaceId = "team" }]
                : []);

        public Task MovePageToSpaceAsync(string pageId, string spaceId, CancellationToken cancellationToken = default)
        {
            Moved.Add((pageId, spaceId));
            return Task.CompletedTask;
        }
    }
}
