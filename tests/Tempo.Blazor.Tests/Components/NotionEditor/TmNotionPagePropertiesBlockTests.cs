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

public sealed class TmNotionPagePropertiesBlockTests : LocalizationTestBase
{
    public TmNotionPagePropertiesBlockTests()
    {
        UseCustomLocalization(new Dictionary<string, string>
        {
            ["Tm_Loading"] = "Loading",
            ["Tm_Delete"] = "Delete",
            ["Notion_PageProps_Title"] = "Page properties",
            ["Notion_PageProps_AddRow"] = "Add row",
            ["Notion_PageProps_Key"] = "Key",
            ["Notion_PageProps_Value"] = "Value",
            ["Notion_PageProps_Empty"] = "No properties yet",
            ["Notion_PropsReport_Title"] = "Properties report",
            ["Notion_PropsReport_Configure"] = "Configure report",
            ["Notion_PropsReport_Labels"] = "Labels",
            ["Notion_PropsReport_Columns"] = "Columns",
            ["Notion_PropsReport_Empty"] = "No pages match this report.",
            ["Notion_PropsReport_MissingValue"] = "Not set",
            ["Notion_PropsReport_NoProvider"] = "The properties report provider is not available."
        });
    }

    [Fact]
    public void PagePropertiesBlock_EditsRowsAndPersistsContent()
    {
        PagePropertiesBlockContent? changed = null;
        var content = new PagePropertiesBlockContent
        {
            Rows = [new PagePropertyRow { Key = "Status", ValueHtml = "Green" }]
        };

        var cut = RenderComponent<TmNotionPagePropertiesBlock>(parameters => parameters
            .Add(component => component.Block, MakeBlock(BlockType.PageProperties, content))
            .Add(component => component.Content, content)
            .Add(component => component.OnContentChanged, EventCallback.Factory.Create<PagePropertiesBlockContent>(
                this,
                value => changed = value)));

        cut.Find(".tm-page-props__add").Click();
        cut.FindAll(".tm-page-props__key-input")[1].Change("Owner");
        cut.FindAll(".tm-page-props__value-input")[1].Change("Platform team");

        changed.Should().NotBeNull();
        changed!.Rows.Should().HaveCount(2);
        changed.Rows[1].Key.Should().Be("Owner");
        changed.Rows[1].ValueHtml.Should().Be("Platform team");
    }

    [Fact]
    public void PagePropertiesBlock_RendersEmptyStateAndAddsFirstRow()
    {
        PagePropertiesBlockContent? changed = null;
        var content = new PagePropertiesBlockContent();

        var cut = RenderComponent<TmNotionPagePropertiesBlock>(parameters => parameters
            .Add(component => component.Block, MakeBlock(BlockType.PageProperties, content))
            .Add(component => component.Content, content)
            .Add(component => component.OnContentChanged, EventCallback.Factory.Create<PagePropertiesBlockContent>(
                this,
                value => changed = value)));

        cut.Find(".tm-page-props__empty").TextContent.Should().Be("No properties yet");
        cut.Find(".tm-page-props__add").Click();

        changed.Should().NotBeNull();
        changed!.Rows.Should().ContainSingle();
        cut.FindAll(".tm-page-props__row").Should().ContainSingle();
    }

    [Fact]
    public void PagePropertiesReportBlock_AggregatesPropertiesFromProvider()
    {
        var provider = new PagePropertiesProvider();
        var content = new PagePropertiesReportBlockContent
        {
            Labels = ["release"],
            Columns = ["Status", "Owner", "Risk"]
        };

        var cut = RenderReport(provider, content);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".tm-props-report__page-link").Should().HaveCount(2);
            cut.Markup.Should().Contain("Alpha Release");
            cut.Markup.Should().Contain("<strong>Green</strong>");
            cut.Markup.Should().Contain("Platform");
            cut.FindAll(".tm-props-report__missing").Should().ContainSingle();
            provider.LastQuery.Should().NotBeNull();
            provider.LastQuery!.Labels.Should().Equal("release");
            provider.LastQuery.Columns.Should().Equal("Status", "Owner", "Risk");
        });
    }

    [Fact]
    public void PagePropertiesReportBlock_ConfigurationPersistsAndReloadsReport()
    {
        var provider = new PagePropertiesProvider();
        PagePropertiesReportBlockContent? changed = null;

        var cut = RenderReport(provider, new PagePropertiesReportBlockContent(), value => changed = value);

        cut.Find(".tm-props-report__labels-input").Change("release, customer-facing");
        cut.Find(".tm-props-report__columns-input").Change("Status, Owner");

        changed.Should().NotBeNull();
        changed!.Labels.Should().Equal("release", "customer-facing");
        changed.Columns.Should().Equal("Status", "Owner");
        provider.LastQuery.Should().NotBeNull();
        provider.LastQuery!.Labels.Should().Equal("release", "customer-facing");
    }

    [Fact]
    public void PagePropertiesReportBlock_ShowsEmptyStateWhenProviderReturnsNoRows()
    {
        var provider = new PagePropertiesProvider { Rows = [] };
        var content = new PagePropertiesReportBlockContent
        {
            Labels = ["missing"],
            Columns = ["Status"]
        };

        var cut = RenderReport(provider, content);

        cut.WaitForAssertion(() =>
            cut.Find(".tm-props-report__empty").TextContent.Should().Be("No pages match this report."));
    }

    private IRenderedComponent<CascadingValue<NotionEditorContext>> RenderReport(
        PagePropertiesProvider provider,
        PagePropertiesReportBlockContent content,
        Action<PagePropertiesReportBlockContent>? changed = null)
    {
        var context = new NotionEditorContext
        {
            DataProvider = new EmptyDataProvider(),
            PagePropertiesProvider = provider,
            NavigateTo = _ => Task.CompletedTask
        };

        return RenderComponent<CascadingValue<NotionEditorContext>>(parameters => parameters
            .Add(component => component.Value, context)
            .AddChildContent<TmNotionPagePropertiesReportBlock>(child => child
                .Add(component => component.Block, MakeBlock(BlockType.PagePropertiesReport, content))
                .Add(component => component.Content, content)
                .Add(component => component.OnContentChanged, EventCallback.Factory.Create<PagePropertiesReportBlockContent>(
                    this,
                    changed ?? (_ => { })))));
    }

    private static PageBlock MakeBlock(BlockType type, IBlockContent content) => new()
    {
        Id = Guid.Parse("cf150000-0000-0000-0000-000000000010"),
        PageId = Guid.Parse("cf150000-0000-0000-0000-000000000001"),
        Type = type,
        Order = 0,
        Content = content
    };

    private sealed class PagePropertiesProvider : INotionPagePropertiesProvider
    {
        public PagePropertiesReportQuery? LastQuery { get; private set; }

        public IReadOnlyList<PagePropertiesReportRow> Rows { get; set; } =
        [
            new()
            {
                PageId = Guid.Parse("cf150000-0000-0000-0000-000000000101"),
                Title = "Alpha Release",
                IconEmoji = "A",
                Labels = ["release"],
                Properties = new Dictionary<string, string?>
                {
                    ["Status"] = "<strong>Green</strong>",
                    ["Owner"] = "Platform",
                    ["Risk"] = "Low"
                }
            },
            new()
            {
                PageId = Guid.Parse("cf150000-0000-0000-0000-000000000102"),
                Title = "Beta Release",
                IconEmoji = "B",
                Labels = ["release"],
                Properties = new Dictionary<string, string?>
                {
                    ["Status"] = "Amber",
                    ["Risk"] = "Medium"
                }
            }
        ];

        public Task<IReadOnlyList<PagePropertiesReportRow>> QueryPagePropertiesAsync(
            PagePropertiesReportQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Rows);
        }
    }

    private sealed class EmptyDataProvider : INotionDataProvider
    {
        public Task<INotionPage> GetPageAsync(string pageId) => throw new NotSupportedException();
        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId) => Task.FromResult(Enumerable.Empty<INotionPage>());
        public Task<IEnumerable<INotionPage>> GetFavoritesAsync() => Task.FromResult(Enumerable.Empty<INotionPage>());
        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count) => Task.FromResult(Enumerable.Empty<INotionPage>());
        public Task<IEnumerable<INotionPage>> GetTrashAsync() => Task.FromResult(Enumerable.Empty<INotionPage>());
        public Task<INotionPage> CreatePageAsync(string? parentId, string title) => throw new NotSupportedException();
        public Task UpdatePageAsync(INotionPage page) => throw new NotSupportedException();
        public Task DeletePageAsync(string pageId) => throw new NotSupportedException();
        public Task RestorePageAsync(string pageId) => throw new NotSupportedException();
        public Task PermanentlyDeletePageAsync(string pageId) => throw new NotSupportedException();
        public Task ToggleFavoriteAsync(string pageId, bool isFavorite) => throw new NotSupportedException();
        public Task MovePageAsync(string pageId, string? newParentId) => throw new NotSupportedException();
        public Task<INotionPage> DuplicatePageAsync(string pageId) => throw new NotSupportedException();
        public Task<IReadOnlyList<INotionPage>> GetPagesByLabelAsync(string label, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<INotionPage>>([]);
        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
