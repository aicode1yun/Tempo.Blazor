using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Page;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionPageReactionsTests : LocalizationTestBase
{
    private static readonly Guid PageId = Guid.Parse("cf170000-0000-0000-0000-000000000001");

    public TmNotionPageReactionsTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Notion_Reactions_Like"] = "Like",
            ["Notion_Reactions_AddReaction"] = "Add reaction",
            ["Notion_Reactions_Count"] = "{0} reactions",
            ["Notion_Reactions_PickerLabel"] = "Choose page reaction"
        });
    }

    [Fact]
    public void Component_IsHiddenWithoutProvider()
    {
        var cut = RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(p => p.Value, new NotionEditorContext())
            .AddChildContent<TmNotionPageReactions>(child => child
                .Add(p => p.PageId, PageId)));

        cut.FindAll(".tm-page-reactions").Should().BeEmpty();
    }

    [Fact]
    public void LikeToggle_UpdatesCountAndActiveState()
    {
        var provider = new FakeReactionProvider();
        var cut = RenderReactions(provider);

        cut.WaitForAssertion(() =>
            cut.Find(".tm-page-reactions__like-count").TextContent.Trim().Should().Be("0"));

        cut.Find(".tm-page-reactions__like").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".tm-page-reactions__like-count").TextContent.Trim().Should().Be("1");
            cut.Find(".tm-page-reactions__like").ClassList.Should().Contain("tm-page-reactions__like--active");
        });
    }

    [Fact]
    public void EmojiPicker_TogglesReaction()
    {
        var provider = new FakeReactionProvider();
        var cut = RenderReactions(provider);

        cut.Find(".tm-page-reactions__add").Click();
        cut.FindAll(".tm-page-reactions__choice").First(button => button.TextContent.Trim() == "🎉").Click();

        cut.WaitForAssertion(() =>
        {
            var reaction = cut.Find(".tm-page-reactions__pill[data-reaction='🎉']");
            reaction.TextContent.Should().Contain("🎉");
            reaction.TextContent.Should().Contain("1");
            reaction.ClassList.Should().Contain("tm-page-reactions__pill--active");
        });
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderReactions(FakeReactionProvider provider)
        => RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(p => p.Value, new NotionEditorContext
            {
                CurrentUserId = "alice",
                ReactionProvider = provider
            })
            .AddChildContent<TmNotionPageReactions>(child => child
                .Add(p => p.PageId, PageId)));

    private sealed class FakeReactionProvider : INotionReactionProvider
    {
        private readonly Dictionary<string, HashSet<string>> _reactions = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<PageReactionDto>> GetReactionsAsync(Guid pageId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PageReactionDto>>(Snapshot());

        public Task<IReadOnlyList<PageReactionDto>> ToggleLikeAsync(Guid pageId, string userId, CancellationToken cancellationToken = default)
            => ToggleReactionAsync(pageId, TmNotionPageReactions.LikeReaction, userId, cancellationToken);

        public Task<IReadOnlyList<PageReactionDto>> ToggleReactionAsync(Guid pageId, string reaction, string userId, CancellationToken cancellationToken = default)
        {
            if (!_reactions.TryGetValue(reaction, out var users))
            {
                users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _reactions[reaction] = users;
            }

            if (!users.Add(userId))
                users.Remove(userId);

            return Task.FromResult<IReadOnlyList<PageReactionDto>>(Snapshot());
        }

        private IReadOnlyList<PageReactionDto> Snapshot()
            => _reactions
                .Where(pair => pair.Value.Count > 0)
                .Select(pair => new PageReactionDto
                {
                    Reaction = pair.Key,
                    UserIds = pair.Value.Order(StringComparer.OrdinalIgnoreCase).ToArray()
                })
                .ToArray();
    }
}
