using FluentAssertions;
using System.Text.Json;
using Tempo.Blazor.Demo.Api.Services;
using Tempo.Blazor.EmailTemplates.Abstractions.Dtos;
using Tempo.Blazor.EmailTemplates.Abstractions.Rendering;
using Tempo.Blazor.EmailTemplates.Abstractions.Serialization;
using Tempo.Blazor.EmailTemplates.Abstractions.Templating;

namespace Tempo.Blazor.Demo.Api.Tests;

public class DemoEmailTemplateStoreTests
{
    private static EmailTemplateRenderer CreateRenderer() => new(
        new ScribanTemplateEngine(), new MjmlGenerator(), new MjmlNetCompiler(), new TextVersionGenerator());

    [Fact]
    public async Task Seed_ContainsThreeTemplates()
    {
        var list = await new DemoEmailTemplateStore().ListAsync();
        list.Should().HaveCount(3);
    }

    [Fact]
    public async Task NewsletterSeed_IncludesEmptyColumnForDragDropTarget()
    {
        var store = new DemoEmailTemplateStore();
        var newsletter = (await store.ListAsync()).First(t => t.Name == "Newsletter");
        var detail = (await store.GetAsync(newsletter.Id))!;
        var document = EmailTemplateSerializer.Deserialize(detail.ContentJson);

        document.Sections.Should().Contain(section => section.Columns.Any(column => column.Blocks.Count == 0));
    }

    [Fact]
    public async Task Create_AddsTemplate_WithNewId()
    {
        var store = new DemoEmailTemplateStore();
        var request = new CreateEmailTemplateRequest
        {
            Name = "Custom", Subject = "S", Language = "en",
            ContentJson = "{\"sections\":[]}",
        };

        var created = await store.CreateAsync(request);

        created.Id.Should().NotBe(Guid.Empty);
        created.Name.Should().Be("Custom");
        (await store.ListAsync()).Should().HaveCount(4);
        (await store.GetAsync(created.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Update_ExistingReturnsTrue_MissingReturnsFalse()
    {
        var store = new DemoEmailTemplateStore();
        var welcome = (await store.ListAsync()).First(t => t.Name == "Welcome email");
        var request = new UpdateEmailTemplateRequest
        {
            Name = "Welcome v2", Subject = "S", Language = "en", IsActive = true,
            ContentJson = "{\"sections\":[]}",
        };

        (await store.UpdateAsync(welcome.Id, request)).Should().BeTrue();
        (await store.UpdateAsync(Guid.NewGuid(), request)).Should().BeFalse();
        (await store.GetAsync(welcome.Id))!.Name.Should().Be("Welcome v2");
    }

    [Fact]
    public async Task Delete_RemovesTemplate()
    {
        var store = new DemoEmailTemplateStore();
        var id = (await store.ListAsync()).First().Id;

        (await store.DeleteAsync(id)).Should().BeTrue();
        (await store.GetAsync(id)).Should().BeNull();
        (await store.DeleteAsync(id)).Should().BeFalse();
    }

    [Fact]
    public async Task IsNameAvailable_RespectsExistingNames()
    {
        var store = new DemoEmailTemplateStore();
        (await store.IsNameAvailableAsync("Welcome email")).Should().BeFalse();
        (await store.IsNameAvailableAsync("Totally new name")).Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentCreates_AllSucceed_NoCorruption()
    {
        var store = new DemoEmailTemplateStore();
        var tasks = Enumerable.Range(0, 20).Select(i => store.CreateAsync(new CreateEmailTemplateRequest
        {
            Name = $"T{i}", Subject = "S", Language = "en", ContentJson = "{\"sections\":[]}",
        }));

        await Task.WhenAll(tasks);

        (await store.ListAsync()).Should().HaveCount(23); // 3 seed + 20
    }

    [Fact]
    public async Task SeededTemplates_RenderWithoutErrors()
    {
        var store = new DemoEmailTemplateStore();
        var renderer = CreateRenderer();

        foreach (var summary in await store.ListAsync())
        {
            var detail = (await store.GetAsync(summary.Id))!;
            var document = EmailTemplateSerializer.Deserialize(detail.ContentJson);
            var model = JsonSerializer.Deserialize<Dictionary<string, object?>>(detail.SampleDataJson!);

            var result = await renderer.RenderAsync(document, model);

            result.Success.Should().BeTrue($"seeded template '{detail.Name}' must render cleanly");
            result.Html.Should().Contain("<html");
        }
    }
}
