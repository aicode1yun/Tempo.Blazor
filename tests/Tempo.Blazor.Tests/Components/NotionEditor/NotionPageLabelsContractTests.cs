using System.Text.Json;
using FluentAssertions;
using Tempo.Blazor.NotionEditor.Interfaces;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

public sealed class NotionPageLabelsContractTests
{
    [Fact]
    public void NotionPage_Labels_DefaultToEmptyAndRoundtripThroughJson()
    {
        var page = new NotionPage
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "Release Plan",
            Labels = ["release", "český štítek", "customer success"]
        };

        var json = JsonSerializer.Serialize(page, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<NotionPage>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        restored.Should().NotBeNull();
        restored!.Labels.Should().Equal("release", "český štítek", "customer success");
        new NotionPage().Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task INotionDataProvider_LabelMethods_FilterAndPersistLabels()
    {
        INotionDataProvider provider = new InMemoryLabelProvider([
            new NotionPage
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Release Plan",
                Labels = ["release", "roadmap"]
            },
            new NotionPage
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Title = "Customer Notes",
                Labels = ["customer success"]
            }
        ]);

        var labels = await provider.GetAllLabelsAsync();
        labels.Should().Equal("customer success", "release", "roadmap");

        var releasePages = await provider.GetPagesByLabelAsync("RELEASE");
        releasePages.Should().ContainSingle(page => page.Title == "Release Plan");

        await provider.SetPageLabelsAsync(Guid.Parse("22222222-2222-2222-2222-222222222222"), [" release ", "Release", "český štítek"]);
        var updated = await provider.GetPageAsync("22222222-2222-2222-2222-222222222222");
        updated.Labels.Should().Equal("release", "český štítek");
    }

    private sealed class InMemoryLabelProvider : INotionDataProvider
    {
        private readonly Dictionary<Guid, NotionPage> _pages;

        public InMemoryLabelProvider(IEnumerable<NotionPage> pages)
            => _pages = pages.ToDictionary(page => page.Id);

        public Task<INotionPage> GetPageAsync(string pageId)
            => Task.FromResult<INotionPage>(_pages[Guid.Parse(pageId)]);

        public Task<IEnumerable<INotionPage>> GetChildPagesAsync(string? parentId)
            => Task.FromResult(_pages.Values.Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetFavoritesAsync()
            => Task.FromResult(_pages.Values.Where(page => page.IsFavorite).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetRecentPagesAsync(int count)
            => Task.FromResult(_pages.Values.Take(count).Cast<INotionPage>());

        public Task<IEnumerable<INotionPage>> GetTrashAsync()
            => Task.FromResult(_pages.Values.Where(page => page.IsDeleted).Cast<INotionPage>());

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
            var pages = _pages.Values
                .Where(page => page.Labels.Any(existing => string.Equals(existing, label.Trim(), StringComparison.OrdinalIgnoreCase)))
                .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
                .Cast<INotionPage>()
                .ToArray();

            return Task.FromResult<IReadOnlyList<INotionPage>>(pages);
        }

        public Task<IReadOnlyList<string>> GetAllLabelsAsync(CancellationToken cancellationToken = default)
        {
            var labels = _pages.Values
                .SelectMany(page => page.Labels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult<IReadOnlyList<string>>(labels);
        }

        public Task SetPageLabelsAsync(Guid pageId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
        {
            _pages[pageId].Labels = Normalize(labels);
            return Task.CompletedTask;
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string> labels)
        {
            var result = new List<string>();
            foreach (var label in labels)
            {
                var trimmed = label.Trim();
                if (trimmed.Length == 0 || result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    continue;

                result.Add(trimmed);
            }

            return result;
        }
    }
}
