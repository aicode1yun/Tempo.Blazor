using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionReadSchemaContractTests
{
    [Fact]
    public async Task BlockTree_IsRecursiveAndReturnsLogicalTableCellsAndVersions()
    {
        var pageId = Guid.Parse("51000000-0000-0000-0000-000000000001");
        var toggleId = Guid.Parse("51000000-0000-0000-0000-000000000010");
        var firstChildId = Guid.Parse("51000000-0000-0000-0000-000000000011");
        var secondChildId = Guid.Parse("51000000-0000-0000-0000-000000000012");
        var tableId = Guid.Parse("51000000-0000-0000-0000-000000000020");
        var firstRowId = Guid.Parse("51000000-0000-0000-0000-000000000021");
        var secondRowId = Guid.Parse("51000000-0000-0000-0000-000000000022");
        var snapshot = new NotionPageSnapshot
        {
            Page = new NotionPageState { Id = pageId, Title = "Agent readback" },
            ConcurrencyToken = "opaque-read-token",
            Digest = "sha256:canonical-read-digest",
            Blocks =
            [
                Block(tableId, pageId, null, BlockType.Table, 1, new NotionAuthoringTable
                {
                    ColumnCount = 2,
                    HasHeaderRow = true,
                    ColumnAlignments =
                    [
                        NotionTableHorizontalAlignment.Left,
                        NotionTableHorizontalAlignment.Right
                    ],
                    ColumnWidths = [220, 120]
                }),
                Block(secondChildId, pageId, toggleId, BlockType.Paragraph, 1,
                    new TextBlockContent { Html = "Second nested" }),
                Block(toggleId, pageId, null, BlockType.Toggle, 0,
                    new ToggleBlockContent { Html = "Parent" }),
                Block(secondRowId, pageId, tableId, BlockType.TableRow, 1,
                    new NotionAuthoringTableRow
                    {
                        Cells =
                        [
                            new NotionAuthoringTableCell
                            {
                                Html = "Low",
                                BackgroundColor = "#dcfce7",
                                HorizontalAlignment = NotionTableHorizontalAlignment.Right
                            }
                        ]
                    }),
                Block(firstRowId, pageId, tableId, BlockType.TableRow, 0,
                    new NotionAuthoringTableRow
                    {
                        Cells =
                        [
                            new NotionAuthoringTableCell
                            {
                                Html = "<strong>Risk</strong>",
                                BackgroundColor = "#fef3c7",
                                TextColor = "#111827",
                                RowSpan = 2,
                                ColumnSpan = 1,
                                Borders = new NotionTableCellBorders
                                {
                                    Bottom = new NotionTableBorder
                                    {
                                        Style = NotionTableBorderStyle.Solid,
                                        Color = "#d97706",
                                        Width = 1
                                    }
                                }
                            },
                            new NotionAuthoringTableCell
                            {
                                Inlines =
                                [
                                    new NotionRichTextInline
                                    {
                                        Text = "Impact",
                                        Bold = true,
                                        TextColor = "#111827"
                                    }
                                ]
                            }
                        ]
                    }),
                Block(firstChildId, pageId, toggleId, BlockType.Paragraph, 0,
                    new TextBlockContent { Html = "First nested" })
            ]
        };
        var provider = new ReadbackProvider(snapshot);

        var root = Parse(await NotionBlockTools.GetBlockTree(
            provider,
            pageId.ToString("D")));

        root.GetProperty("concurrencyToken").GetString().Should().Be("opaque-read-token");
        root.GetProperty("digest").GetString().Should().Be("sha256:canonical-read-digest");
        root.GetProperty("totalCount").GetInt32().Should().Be(6);
        root.GetProperty("blocks")[0].GetProperty("id").GetGuid().Should().Be(toggleId);
        root.GetProperty("blocks")[0].GetProperty("children").EnumerateArray()
            .Select(child => child.GetProperty("id").GetGuid())
            .Should().Equal(firstChildId, secondChildId);

        var table = root.GetProperty("blocks")[1];
        table.GetProperty("id").GetGuid().Should().Be(tableId);
        table.GetProperty("children").GetArrayLength().Should().Be(0);
        var rows = table.GetProperty("content").GetProperty("rows");
        rows.GetArrayLength().Should().Be(2);
        rows[0].GetProperty("id").GetGuid().Should().Be(firstRowId);
        var mergedCell = rows[0].GetProperty("cells")[0];
        mergedCell.GetProperty("rowSpan").GetInt32().Should().Be(2);
        mergedCell.GetProperty("columnSpan").GetInt32().Should().Be(1);
        mergedCell.GetProperty("backgroundColor").GetString().Should().Be("#fef3c7");
        mergedCell.GetProperty("textColor").GetString().Should().Be("#111827");
        mergedCell.GetProperty("borders").GetProperty("bottom")
            .GetProperty("style").GetString().Should().Be("solid");
        rows[0].GetProperty("cells")[1].GetProperty("inlines")[0]
            .GetProperty("bold").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task BlockTree_RejectsProviderErrorsAndNonObjectContent()
    {
        var pageId = Guid.Parse("51000000-0000-0000-0000-000000000101");
        var snapshot = new NotionPageSnapshot
        {
            Page = new NotionPageState { Id = pageId, Title = "Invalid readback" },
            ConcurrencyToken = "opaque-invalid-token",
            Digest = "sha256:invalid-readback",
            Blocks =
            [
                new NotionBlockSnapshot
                {
                    Id = Guid.Parse("51000000-0000-0000-0000-000000000102"),
                    PageId = pageId,
                    Type = BlockType.Paragraph,
                    Order = 0,
                    Content = JsonSerializer.SerializeToElement("not-an-object")
                }
            ]
        };

        var invalidContent = Parse(await NotionBlockTools.GetBlockTree(
            new ReadbackProvider(snapshot),
            pageId.ToString("D")));
        invalidContent.GetProperty("success").GetBoolean().Should().BeFalse();
        invalidContent.GetProperty("error").GetString().Should().Be("validation_failed");
        invalidContent.GetProperty("validationErrors")[0].GetString()
            .Should().Contain("block_content_object_required");

        snapshot.Blocks = [];
        var providerError = Parse(await NotionBlockTools.GetBlockTree(
            new ReadbackProvider(
                snapshot,
                [
                    new NotionAggregateIssue
                    {
                        Code = "provider_decode_failed",
                        Severity = NotionIssueSeverity.Error,
                        Message = "Provider could not decode canonical content.",
                        Path = "$.blocks[0].content"
                    }
                ]),
            pageId.ToString("D")));
        providerError.GetProperty("success").GetBoolean().Should().BeFalse();
        providerError.GetProperty("validationErrors")[0].GetString()
            .Should().Contain("provider_decode_failed");
    }

    [Fact]
    public void SchemaAndGuideTools_ReturnCompleteJsonDataContracts()
    {
        var tableSchema = Parse(NotionSchemaAndValidationTools.GetBlockSchema("Table"))
            .GetProperty("blockType");

        tableSchema.GetProperty("fields").EnumerateArray()
            .Should().Contain(field => field.GetProperty("name").GetString() == "columnCount");
        tableSchema.GetRawText().Should().Contain("\"required\"");
        tableSchema.GetRawText().Should().Contain("\"nullable\"");
        tableSchema.GetRawText().Should().Contain("\"default\"");
        tableSchema.GetRawText().Should().Contain("\"enumValues\"");
        tableSchema.GetRawText().Should().Contain("\"fields\"");
        tableSchema.GetRawText().Should().Contain("\"example\"");

        var toolNames = TempoNotionMcp.ToolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => name is not null)
            .ToList();
        toolNames.Should().Contain("notion_get_operation_catalog");
        toolNames.Should().Contain("notion_get_authoring_guide");
        toolNames.Should().NotContain("notion_list_blocks");
    }

    [Fact]
    public void McpToolArguments_RemainPrimitive()
    {
        var toolMethods = TempoNotionMcp.ToolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        foreach (var parameter in toolMethods.SelectMany(method => method.GetParameters()))
        {
            if (parameter.ParameterType == typeof(CancellationToken) ||
                IsInjectedService(parameter.ParameterType))
            {
                continue;
            }

            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            type.Should().Match(
                candidate => candidate == typeof(string) ||
                    candidate == typeof(bool) ||
                    candidate == typeof(int) ||
                    candidate == typeof(DateTime),
                $"{parameter.Member.Name}.{parameter.Name} must stay primitive so MCP input schemas do not need $ref/$defs/anyOf");
        }
    }

    [Fact]
    public void Catalog_CoversModelsSerializerValidatorOperationsAndDocumentation()
    {
        var blockSchemas = NotionAuthoringCatalog.ListBlockTypes();
        blockSchemas.Select(schema => schema["type"]!.GetValue<string>())
            .Should().BeEquivalentTo(
                Enum.GetNames<BlockType>()
                    .Select(JsonNamingPolicy.CamelCase.ConvertName));

        AssertSchemaMatchesModel(
            NotionAuthoringCatalog.TryGetBlockSchema(BlockType.Table)!,
            typeof(NotionAuthoringTable));
        AssertSchemaMatchesModel(
            NotionAuthoringCatalog.TryGetBlockSchema(BlockType.TableRow)!,
            typeof(NotionAuthoringTableRow));

        var tableSchema = NotionAuthoringCatalog.TryGetBlockSchema(BlockType.Table)!;
        var tableExample = tableSchema["example"]!["rows"]!.AsArray();
        var rows = tableExample.Deserialize<List<NotionAuthoringTableRow>>(
            NotionAggregateJson.Options)!;
        NotionTableGridProjector.TryProject(
                rows,
                2,
                "$.rows",
                out var projection,
                out var issues)
            .Should().BeTrue(string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        projection!.Slots.Should().HaveCount(2);

        var expectedOperations = new[]
        {
            "createBlock",
            "createBlocks",
            "createTable",
            "patchBlockContent",
            "moveBlock",
            "reorderBlocks",
            "convertBlockType",
            "deleteBlock",
            "replaceBlocks"
        };
        NotionAuthoringCatalog.SupportedOperationNames.Should().Equal(expectedOperations);
        NotionAuthoringCatalog.GetOperationCatalog(operation: null)
            .Select(operation => operation["operation"]!.GetValue<string>())
            .Should().Equal(expectedOperations);
        var createTable = NotionAuthoringCatalog.GetOperationCatalog("createTable").Single();
        var rowsField = createTable["fields"]!.AsArray()
            .Single(field => field!["name"]!.GetValue<string>() == "rows")!;
        rowsField["items"]!["fields"]!.AsArray()
            .Should().Contain(field => field!["name"]!.GetValue<string>() == "cells");
        var createBlock = NotionAuthoringCatalog.GetOperationCatalog("createBlock").Single();
        createBlock["fields"]!.AsArray()
            .Single(field => field!["name"]!.GetValue<string>() == "block")!["fields"]!
            .AsArray()
            .Should().Contain(field => field!["name"]!.GetValue<string>() == "children");

        var documentation = File.ReadAllText(
            Path.Combine(RepoRoot(), "docs", "notion-mcp-authoring.md"));
        foreach (var operation in expectedOperations)
        {
            documentation.Should().Contain($"#### `{operation}`");
        }
        foreach (var tool in RegisteredNotionToolNames())
        {
            documentation.Should().Contain($"### `{tool}`");
        }
    }

    [Fact]
    public void EveryCatalogField_DeclaresAgentMetadata()
    {
        foreach (var type in Enum.GetValues<BlockType>())
        {
            var schema = NotionAuthoringCatalog.TryGetBlockSchema(type)!;
            AssertFieldMetadata(schema["fields"]!.AsArray());
        }
        foreach (var operation in NotionAuthoringCatalog.GetOperationCatalog(operation: null))
        {
            AssertFieldMetadata(operation["fields"]!.AsArray());
        }
    }

    private static void AssertSchemaMatchesModel(JsonObject schema, Type modelType)
    {
        var catalogNames = schema["fields"]!.AsArray()
            .Select(field => field!["name"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var modelNames = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property =>
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name))
            .ToHashSet(StringComparer.Ordinal);
        catalogNames.Should().BeEquivalentTo(modelNames);

        var serialized = JsonSerializer.SerializeToElement(
            Activator.CreateInstance(modelType),
            modelType,
            NotionAggregateJson.Options);
        serialized.EnumerateObject().Select(property => property.Name)
            .Should().OnlyContain(name => catalogNames.Contains(name));
    }

    private static void AssertFieldMetadata(JsonArray fields)
    {
        foreach (var fieldNode in fields)
        {
            var field = fieldNode!.AsObject();
            field.Should().ContainKeys(
                "name",
                "jsonType",
                "required",
                "optional",
                "nullable",
                "default",
                "description",
                "enumValues",
                "example");
            field["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            if (field["fields"] is JsonArray nested)
            {
                AssertFieldMetadata(nested);
            }
            if (field["items"]?["fields"] is JsonArray itemFields)
            {
                AssertFieldMetadata(itemFields);
            }
        }
    }

    private static IReadOnlyList<string> RegisteredNotionToolNames()
        => TempoNotionMcp.ToolTypes
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "TempoBlazor.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }

    private static bool IsInjectedService(Type type)
        => type == typeof(IServiceProvider) ||
           type.IsInterface;

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    private static NotionBlockSnapshot Block<T>(
        Guid id,
        Guid pageId,
        Guid? parentId,
        BlockType type,
        int order,
        T content)
        => new()
        {
            Id = id,
            PageId = pageId,
            ParentBlockId = parentId,
            Type = type,
            Order = order,
            Content = JsonSerializer.SerializeToElement(content, NotionAggregateJson.Options)
        };

    private sealed class ReadbackProvider(
        NotionPageSnapshot snapshot,
        IReadOnlyList<NotionAggregateIssue>? issues = null)
        : INotionAggregateProvider
    {
        public Task<NotionAggregateLoadResult> LoadPageAsync(
            Guid pageId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(pageId == snapshot.Page.Id
                ? new NotionAggregateLoadResult
                {
                    Found = true,
                    Snapshot = snapshot,
                    Issues = issues ?? []
                }
                : new NotionAggregateLoadResult { Found = false });

        public Task<NotionAggregateLoadResult> LoadBlockAsync(
            Guid blockId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new NotionAggregateLoadResult { Found = false });

        public Task<NotionAggregateSaveResult> SaveAsync(
            NotionAggregateSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
