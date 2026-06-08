using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Components.NotionEditor.Blocks.Special;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class TmNotionContentByLabelBlockTests : LocalizationTestBase
{
    public TmNotionContentByLabelBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["Tm_Open"] = "Open",
            ["Notion_Labels_Remove"] = "Remove label",
            ["Notion_ContentByLabel_Title"] = "Content by label",
            ["Notion_ContentByLabel_Configure"] = "Configure",
            ["Notion_ContentByLabel_Empty"] = "No pages match these labels.",
            ["Notion_ContentByLabel_SelectLabels"] = "Select labels",
            ["Notion_ContentByLabel_AddLabel"] = "Add label",
            ["Notion_ContentByLabel_MaxItems"] = "Maximum pages",
            ["Notion_ContentByLabel_SortBy"] = "Sort by",
            ["Notion_ContentByLabel_Sort_LastEditedDesc"] = "Recently edited",
            ["Notion_ContentByLabel_Sort_LastEditedAsc"] = "Oldest edited",
            ["Notion_ContentByLabel_Sort_TitleAsc"] = "Title A-Z",
            ["Notion_ContentByLabel_Sort_TitleDesc"] = "Title Z-A",
            ["Notion_ContentByLabel_Sort_CreatedDesc"] = "Newest created",
            ["Notion_ContentByLabel_Sort_CreatedAsc"] = "Oldest created"
        });
    }

    [Fact]
    public void ContentByLabelBlock_RendersPagesFromProviderWithMaxItemsAndSkipsDeleted()
    {
        var provider = new ContentByLabelDataProvider();
        var cut = RenderBlock(provider, new ContentByLabelBlockContent
        {
            Labels = ["release"],
            MaxItems = 1,
            SortBy = ContentByLabelSortBy.TitleAscending
        });

        cut.WaitForAssertion(() =>
        {
            var pages = cut.FindAll(".tm-cbl__page");
            pages.Should().ContainSingle();
            pages[0].TextContent.Should().Contain("Alpha Release");
            cut.Markup.Should().NotContain("Deleted Release");
        });
    }

    [Fact]
    public void ContentByLabelBlock_ShowsEmptyStateWhenNoPagesMatch()
    {
        var provider = new ContentByLabelDataProvider();
        var cut = RenderBlock(provider, new ContentByLabelBlockContent
        {
            Labels = ["missing"],
            MaxItems = 5,
            SortBy = ContentByLabelSortBy.LastEditedDescending
        });

        cut.WaitForAssertion(() =>
            cut.Find(".tm-cbl__empty").TextContent.Should().Be("No pages match these labels."));
    }

    [Fact]
    public void ContentByLabelBlock_AddsSelectedLabelFromAutocompleteAndPersistsContent()
    {
        var provider = new ContentByLabelDataProvider();
        ContentByLabelBlockContent? changed = null;
        var cut = RenderBlock(provider, new ContentByLabelBlockContent(), content => changed = content);

        cut.Find(".tm-cbl__label-select").Change("release");
        cut.Find(".tm-cbl__add-label").Click();

        cut.WaitForAssertion(() =>
        {
            changed.Should().NotBeNull();
            changed!.Labels.Should().Equal("release");
            cut.Find(".tm-cbl__chip").TextContent.Should().Contain("release");
            cut.FindAll(".tm-cbl__page").Should().HaveCount(2);
        });
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderBlock(
        ContentByLabelDataProvider provider,
        ContentByLabelBlockContent content,
        Action<ContentByLabelBlockContent>? changed = null)
    {
        var context = new NotionEditorContext
        {
            DataProvider = provider,
            NavigateTo = _ => Task.CompletedTask
        };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionContentByLabelBlock>(child => child
                .Add(component => component.Content, content)
                .Add(component => component.OnContentChanged, EventCallback.Factory.Create<ContentByLabelBlockContent>(
                    this,
                    changed ?? (_ => { })))));
    }

    private sealed class ContentByLabelDataProvider : INotionDataProvider
    {
        private readonly IReadOnlyList<NotionPage> _pages =
        [
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Alpha Release",
                IconEmoji = "A",
                CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
                Labels = ["release"]
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "Beta Release",
                IconEmoji = "B",
                CreatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 1, 6, 10, 0, 0, DateTimeKind.Utc),
                Labels = ["release"]
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Title = "Deleted Release",
                IconEmoji = "D",
                CreatedAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc),
                LastEditedAt = new DateTime(2026, 1, 7, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = true,
                Labels = ["release"]
            }
        ];

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult<INotionPage>(_pages.Single(page => page.Id == Guid.Parse(pageId)));

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult(_pages.Where(page => !page.IsDeleted).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => Task.FromResult(_pages.Where(page => page.IsFavorite && !page.IsDeleted).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult(_pages.Where(page => !page.IsDeleted).Take(count).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => Task.FromResult(_pages.Where(page => page.IsDeleted).Cast<INotionPage>());

        public Task<INotionPage> CreatePageAsync(string? parentId, string title)
            => throw new NotSupportedException();

        public Task UpdatePageAsync(INotionPage page)
            => throw new NotSupportedException();

        public Task DeletePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task RestorePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task PermanentlyDeletePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task ToggleFavoriteAsync(string pageId, bool isFavorite)
            => throw new NotSupportedException();

        public Task MovePageAsync(string pageId, string? newParentId)
            => throw new NotSupportedException();

        public Task<INotionPage> DuplicatePageAsync(string pageId)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default)
        {
            var pages = _pages
                .Where(page => page.Labels.Any(existing => string.Equals(existing, label, StringComparison.OrdinalIgnoreCase)))
                .Cast<INotionPage>()
                .ToArray();

            return Task.FromResult<IReadOnlyList<INotionPage>>(pages);
        }

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["customer success", "release"]);

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
