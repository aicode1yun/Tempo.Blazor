using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.Mcp.Tests.Fixtures;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public class NotionToolsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    [Fact]
    public void ToolTypes_ExposeExpectedContract()
    {
        var names = TempoNotionMcp.ToolTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()!.Name)
            .OrderBy(n => n)
            .ToList();

        names.Should().BeEquivalentTo(new[]
        {
            "notion_apply_block_operations",
            "notion_create_page",
            "notion_delete_page",
            "notion_duplicate_page",
            "notion_get_authoring_guide",
            "notion_get_block_schema",
            "notion_get_block_tree",
            "notion_get_operation_catalog",
            "notion_get_page",
            "notion_list_block_types",
            "notion_list_pages",
            "notion_move_page",
            "notion_restore_page",
            "notion_update_page"
        });

        foreach (var type in TempoNotionMcp.ToolTypes)
        {
            type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .Should().OnlyContain(m => m.GetCustomAttribute<DescriptionAttribute>() != null);
        }
    }

    [Fact]
    public void AddTempoNotionMcpTools_RegistersSharedIdempotencyRuntime()
    {
        var services = new ServiceCollection();
        services.AddTempoNotionMcpTools();

        using var provider = services.BuildServiceProvider();
        provider.GetService<InMemoryNotionIdempotencyReceiptStore>().Should().NotBeNull();
    }

    [Fact]
    public async Task ListAndGetPage_ReturnsStoredPagesAndBlocks()
    {
        var backend = new FakeNotionBackend();
        var pageId = backend.AddPage("Project notes");
        backend.AddBlock(pageId, BlockType.Paragraph, new TextBlockContent { Html = "Hello" });

        var listRoot = Parse(await NotionPageTools.ListPages(backend, search: "project"));
        listRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        listRoot.GetProperty("totalCount").GetInt32().Should().Be(1);
        listRoot.GetProperty("items")[0].GetProperty("title").GetString().Should().Be("Project notes");

        var getRoot = Parse(await NotionPageTools.GetPage(backend, pageId.ToString()));
        getRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        getRoot.GetProperty("page").GetProperty("title").GetString().Should().Be("Project notes");
        getRoot.TryGetProperty("blocks", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CreatePage_ForwardsScopeAppId_ToProvider()
    {
        var backend = new FakeNotionBackend();
        var appId = Guid.NewGuid().ToString("D");

        var root = Parse(await NotionPageTools.CreatePage(backend, parentId: null, title: "Scoped root", scopeAppId: appId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        backend.LastScopeAppId.Should().Be(appId);
    }

    [Fact]
    public async Task ListPages_Favorites_ForwardsScopeAppId_ToProvider()
    {
        var backend = new FakeNotionBackend();
        var appId = Guid.NewGuid().ToString("D");

        var root = Parse(await NotionPageTools.ListPages(backend, favorites: true, scopeAppId: appId));

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        backend.LastScopeAppId.Should().Be(appId);
    }

    [Fact]
    public async Task PageLifecycleTools_UpdateDeleteRestoreMoveAndDuplicate()
    {
        var backend = new FakeNotionBackend();
        var parentId = backend.AddPage("Parent");

        var createRoot = Parse(await NotionPageTools.CreatePage(backend, parentId.ToString(), "Child"));
        var pageId = createRoot.GetProperty("page").GetProperty("id").GetGuid();

        var page = new NotionPage
        {
            Id = pageId,
            ParentId = parentId,
            Title = "Renamed",
            CreatedAt = DateTime.UtcNow,
            LastEditedAt = DateTime.UtcNow
        };
        var updateRoot = Parse(await NotionPageTools.UpdatePage(backend, Json(page)));
        updateRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        (await backend.GetPageAsync(pageId.ToString())).Title.Should().Be("Renamed");

        var deleteRoot = Parse(await NotionPageTools.DeletePage(backend, pageId.ToString()));
        deleteRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        (await backend.GetTrashAsync()).Should().Contain(p => p.Id == pageId);

        var restoreRoot = Parse(await NotionPageTools.RestorePage(backend, pageId.ToString()));
        restoreRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        (await backend.GetTrashAsync()).Should().NotContain(p => p.Id == pageId);

        var moveRoot = Parse(await NotionPageTools.MovePage(backend, pageId.ToString(), newParentId: null));
        moveRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        (await backend.GetPageAsync(pageId.ToString())).ParentId.Should().BeNull();

        var duplicateRoot = Parse(await NotionPageTools.DuplicatePage(backend, pageId.ToString()));
        duplicateRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        duplicateRoot.GetProperty("page").GetProperty("id").GetGuid().Should().NotBe(pageId);
    }

    [Fact]
    public async Task PageWriteTools_StaleLastEditedAt_ReturnConflict()
    {
        var backend = new FakeNotionBackend();
        var pageId = backend.AddPage("Concurrent");
        var stale = (await backend.GetPageAsync(pageId.ToString())).LastEditedAt.AddMinutes(-1);

        await backend.ToggleFavoriteAsync(pageId.ToString(), true);
        var current = (NotionPage)await backend.GetPageAsync(pageId.ToString());
        current.Title = "Changed";

        var root = Parse(await NotionPageTools.UpdatePage(
            backend,
            Json(current),
            expectedLastEditedAt: stale));

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("error").GetString().Should().Be("conflict");
    }

    [Fact]
    public void SchemaTools_ReturnBlockCatalogEntries()
    {
        var listRoot = Parse(NotionSchemaAndValidationTools.ListBlockTypes("diagram"));
        listRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        listRoot.GetProperty("items").EnumerateArray()
            .Should().Contain(i => i.GetProperty("type").GetString() == "diagram");

        var schemaRoot = Parse(NotionSchemaAndValidationTools.GetBlockSchema("Paragraph"));
        schemaRoot.GetProperty("success").GetBoolean().Should().BeTrue();
        schemaRoot.GetProperty("blockType").GetProperty("fields").EnumerateArray()
            .Should().Contain(field => field.GetProperty("name").GetString() == "html");
    }
}
