using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tempo.Blazor.E2E.Mcp;

/// <summary>
/// Live HTTPS contract coverage for agent-friendly Notion discovery plus the corresponding
/// advanced-table browser rendering.
/// </summary>
[TestClass]
public sealed class NotionAuthoringContractE2ETests : WasmTestBase
{
    private static readonly Uri McpEndpoint = new("https://localhost:5100/mcp");
    private const string TableSeedEndpoint =
        "https://localhost:5100/api/notion/e2e/seed/seedTablePage";

    [TestMethod]
    [TestCategory("NotionUxBaseline")]
    [Description("Phase 5 verifies primitive MCP inputs, complete nested table schemas, errors, and the schema-described table UI.")]
    public async Task Phase5_NotionDiscoveryContract_MapsToAdvancedTableUi()
    {
        var client = await ConnectAsync();
        var tools = await client.ListToolsAsync();
        var notionTools = tools
            .Where(tool => tool.Name.StartsWith("notion_", StringComparison.Ordinal))
            .ToList();

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "notion_get_block_tree",
                "notion_get_block_schema",
                "notion_get_operation_catalog",
                "notion_get_authoring_guide",
                "notion_apply_block_operations"
            },
            notionTools.Select(tool => tool.Name).ToList());
        foreach (var tool in notionTools)
        {
            Assert.IsFalse(
                ContainsForbiddenSchemaKeyword(tool.InputSchema),
                $"{tool.Name} must expose primitive MCP arguments without $ref, $defs, or anyOf: {tool.InputSchema}");
        }

        var tableResult = await client.CallToolAsync(
            "notion_get_block_schema",
            new { type = "Table" });
        Assert.IsTrue(tableResult.GetProperty("success").GetBoolean(), tableResult.GetRawText());
        var tableSchema = tableResult.GetProperty("blockType");
        var alignments = FindField(tableSchema, "columnAlignments");
        CollectionAssert.Contains(
            alignments.GetProperty("items")
                .GetProperty("enumValues")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToList(),
            "center");
        Assert.AreEqual(
            1000,
            tableSchema.GetProperty("limits").GetProperty("maxRows").GetInt32());
        Assert.AreEqual(
            100,
            tableSchema.GetProperty("limits").GetProperty("maxColumns").GetInt32());

        var operationResult = await client.CallToolAsync(
            "notion_get_operation_catalog",
            new { operation = "createTable" });
        Assert.IsTrue(operationResult.GetProperty("success").GetBoolean(), operationResult.GetRawText());
        var createTable = operationResult.GetProperty("operations")[0];
        var rows = FindField(createTable, "rows");
        var cells = rows.GetProperty("items")
            .GetProperty("fields")
            .EnumerateArray()
            .Single(field => field.GetProperty("name").GetString() == "cells");
        var logicalCellFields = cells.GetProperty("items")
            .GetProperty("fields")
            .EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .ToList();
        CollectionAssert.IsSubsetOf(
            new[] { "rowSpan", "columnSpan", "backgroundColor", "textColor", "borders" },
            logicalCellFields);

        var unknown = await client.CallToolAsync(
            "notion_get_block_schema",
            new { type = "NotARealBlock" });
        Assert.IsFalse(unknown.GetProperty("success").GetBoolean());
        Assert.AreEqual("not_found", unknown.GetProperty("error").GetString());

        using var seedHttp = CreateHttpsClient();
        using var seedResponse = await seedHttp.PostAsync(TableSeedEndpoint, null);
        seedResponse.EnsureSuccessStatusCode();

        var context = await CreateContextAsync();
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1366, 768);
        await page.GotoAsync(
            $"{BaseUrl}/notion-editor",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });
        await WaitForAppReadyAsync(page);

        var advancedTable = page
            .Locator("[data-block-id='cf110000-0000-0000-0000-000000000010']")
            .First;
        await advancedTable.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 60000
        });
        await advancedTable.ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(
                advancedTable.Locator("[data-tm-row='0'][data-tm-col='0']").First)
            .ToHaveAttributeAsync("colspan", "2");
        await Assertions.Expect(
                advancedTable.Locator("[data-tm-row='1'][data-tm-col='2']").First)
            .ToHaveAttributeAsync("rowspan", "2");
        await Assertions.Expect(advancedTable.Locator("[style*='rgba']").First)
            .ToBeVisibleAsync();

        var outputDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "__baseline__",
            "notion",
            "agent-contract"));
        Directory.CreateDirectory(outputDirectory);
        var screenshotPath = Path.Combine(
            outputDirectory,
            "phase5-schema-described-advanced-table.png");
        await advancedTable.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = screenshotPath,
            Type = ScreenshotType.Png,
            OmitBackground = false
        });

        Assert.IsTrue(File.Exists(screenshotPath));
        TestContext.AddResultFile(screenshotPath);
    }

    private static async Task<McpJsonRpcClient> ConnectAsync()
    {
        var client = new McpJsonRpcClient(CreateHttpsClient(), McpEndpoint);
        await client.InitializeAsync();
        return client;
    }

    private static HttpClient CreateHttpsClient()
        => new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });

    private static bool ContainsForbiddenSchemaKeyword(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name is "$ref" or "$defs" or "anyOf")
                {
                    return true;
                }
                if (ContainsForbiddenSchemaKeyword(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(ContainsForbiddenSchemaKeyword);
        }

        return false;
    }

    private static JsonElement FindField(JsonElement schema, string name)
        => schema.GetProperty("fields")
            .EnumerateArray()
            .Single(field => field.GetProperty("name").GetString() == name);
}
