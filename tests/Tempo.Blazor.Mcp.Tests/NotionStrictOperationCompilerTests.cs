using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Tempo.Blazor.Mcp.Notion;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Mcp.Tests;

public sealed class NotionStrictOperationCompilerTests
{
    private static readonly Guid PageId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData("""[{"type":"createBlock","pageId":"11111111-1111-1111-1111-111111111111"}]""", "$.operations[0].type")]
    [InlineData("""[{"op":"createBlock","pageId":"11111111-1111-1111-1111-111111111111","parentId":"x","block":{"type":"paragraph","content":{}}}]""", "$.operations[0].parentId")]
    [InlineData("""[{"op":"createBlock","pageId":"11111111-1111-1111-1111-111111111111","block":{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","type":"paragraph","content":{}}}]""", "$.operations[0].block.id")]
    [InlineData("""[{"op":"createBlock","pageId":"11111111-1111-1111-1111-111111111111","block":{"type":"paragraph","content":{},"legacyChildren":[]}}]""", "$.operations[0].block.legacyChildren")]
    public async Task CompileAsync_LegacyAliasesAndUnknownFields_AreRejectedAtExactPath(
        string json,
        string expectedPath)
    {
        var result = await Compile(json);

        result.Success.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "unknown_field" &&
            issue.Path == expectedPath);
    }

    [Fact]
    public async Task CompileAsync_CreateBlockWithRecursiveChildren_AssignsDeterministicIdsAndParents()
    {
        const string json =
            """
            [{
              "op": "createBlock",
              "clientRef": "outline",
              "pageId": "11111111-1111-1111-1111-111111111111",
              "order": 2,
              "block": {
                "type": "toggle",
                "content": { "html": "Parent" },
                "children": [{
                  "type": "paragraph",
                  "content": { "html": "Child" },
                  "children": [{
                    "type": "paragraph",
                    "content": { "html": "Grandchild" }
                  }]
                }]
              }
            }]
            """;

        var firstWorkingSet = WorkingSet();
        var secondWorkingSet = WorkingSet();
        var first = await Compile(json, firstWorkingSet);
        var second = await Compile(json, secondWorkingSet);

        first.Success.Should().BeTrue();
        Apply(first, firstWorkingSet).Should().OnlyContain(result => result.Success);
        Apply(second, secondWorkingSet).Should().OnlyContain(result => result.Success);

        var firstBlocks = firstWorkingSet.Pages[PageId].Blocks;
        var secondBlocks = secondWorkingSet.Pages[PageId].Blocks;
        firstBlocks.Select(block => block.Id).Should().Equal(secondBlocks.Select(block => block.Id));
        firstBlocks.Should().HaveCount(3);
        firstBlocks[0].ParentBlockId.Should().BeNull();
        firstBlocks[0].Order.Should().Be(2);
        firstBlocks[1].ParentBlockId.Should().Be(firstBlocks[0].Id);
        firstBlocks[1].Order.Should().Be(0);
        firstBlocks[2].ParentBlockId.Should().Be(firstBlocks[1].Id);

        var created = Apply(first, WorkingSet()).SelectMany(result => result.Created).ToList();
        created.Should().HaveCount(3);
        created.Should().OnlyContain(change =>
            change.OperationIndex == 0 &&
            change.ClientRef == "outline" &&
            change.PageId == PageId);
    }

    [Fact]
    public async Task CompileAsync_CreateBlocks_PreservesArrayAndChildOrder()
    {
        var workingSet = WorkingSet();
        var result = await Compile(
            """
            [{
              "op":"createBlocks",
              "clientRef":"batch",
              "pageId":"11111111-1111-1111-1111-111111111111",
              "blocks":[
                {"type":"paragraph","content":{"html":"First"}},
                {"type":"toggle","content":{"html":"Second"},"children":[
                  {"type":"paragraph","content":{"html":"Nested"}}
                ]}
              ]
            }]
            """,
            workingSet);

        result.Success.Should().BeTrue();
        Apply(result, workingSet).Should().OnlyContain(item => item.Success);
        NotionAggregateNormalizer.Normalize(workingSet);

        var blocks = workingSet.Pages[PageId].Blocks;
        blocks.Select(block => block.Content.GetProperty("html").GetString())
            .Should().Equal("First", "Second", "Nested");
        blocks[0].Order.Should().Be(0);
        blocks[1].Order.Should().Be(1);
        blocks[2].ParentBlockId.Should().Be(blocks[1].Id);
    }

    [Fact]
    public async Task CompileAsync_CreateTable_ProducesRichLogicalTableAndRows()
    {
        var workingSet = WorkingSet();
        var result = await Compile(
            """
            [{
              "op":"createTable",
              "clientRef":"risk-table",
              "pageId":"11111111-1111-1111-1111-111111111111",
              "columnCount":2,
              "hasHeaderRow":true,
              "hasHeaderColumn":true,
              "columnAlignments":["left","right"],
              "columnWidths":[180,120],
              "rows":[{
                "cells":[{
                  "html":"<strong>Impact</strong>",
                  "inlines":[{"text":"Impact","bold":true,"textColor":"#ffffff"}],
                  "backgroundColor":"#1f4e78",
                  "textColor":"#ffffff",
                  "horizontalAlignment":"center",
                  "verticalAlignment":"middle",
                  "rowSpan":1,
                  "columnSpan":1,
                  "width":180,
                  "borders":{"bottom":{"style":"double","color":"#000000","width":2}}
                },{
                  "html":"High",
                  "rowSpan":2,
                  "columnSpan":1
                }]
              },{
                "cells":[{"html":"Service outage"}]
              }]
            }]
            """,
            workingSet);

        result.Success.Should().BeTrue();
        Apply(result, workingSet).Should().OnlyContain(item => item.Success);
        NotionAggregateNormalizer.Normalize(workingSet);

        var blocks = workingSet.Pages[PageId].Blocks;
        blocks.Should().HaveCount(3);
        blocks[0].Type.Should().Be(BlockType.Table);
        var table = blocks[0].Content.Deserialize<NotionAuthoringTable>(NotionAggregateJson.Options)!;
        table.ColumnCount.Should().Be(2);
        table.HasHeaderRow.Should().BeTrue();
        table.HasHeaderColumn.Should().BeTrue();
        table.ColumnAlignments.Should().Equal(
            NotionTableHorizontalAlignment.Left,
            NotionTableHorizontalAlignment.Right);
        table.ColumnWidths.Should().Equal(180, 120);

        blocks.Skip(1).Should().OnlyContain(block =>
            block.Type == BlockType.TableRow &&
            block.ParentBlockId == blocks[0].Id);
        var firstRow = blocks[1].Content.Deserialize<NotionAuthoringTableRow>(NotionAggregateJson.Options)!;
        firstRow.Cells[0].Inlines.Should().ContainSingle(inline =>
            inline.Text == "Impact" && inline.Bold);
        firstRow.Cells[0].Borders.Bottom!.Style.Should().Be(NotionTableBorderStyle.Double);
        firstRow.Cells[1].RowSpan.Should().Be(2);
    }

    [Fact]
    public async Task CompileAsync_CreateTableWithOverflowingSpan_ReturnsCellPath()
    {
        var result = await Compile(
            """
            [{
              "op":"createTable",
              "pageId":"11111111-1111-1111-1111-111111111111",
              "columnCount":2,
              "rows":[{"cells":[{"html":"Too wide","columnSpan":3}]}]
            }]
            """);

        result.Success.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "table_span_out_of_range" &&
            issue.Path == "$.operations[0].rows[0].cells[0].columnSpan");
    }

    [Fact]
    public async Task CompileAsync_CreateTableWithInvalidBorderWidth_ReturnsBorderPath()
    {
        var result = await Compile(
            """
            [{
              "op":"createTable",
              "pageId":"11111111-1111-1111-1111-111111111111",
              "columnCount":1,
              "rows":[{"cells":[{
                "html":"Invalid",
                "borders":{"top":{"style":"solid","width":0}}
              }]}]
            }]
            """);

        result.Success.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "table_border_width_out_of_range" &&
            issue.Path == "$.operations[0].rows[0].cells[0].borders.top.width");
    }

    [Fact]
    public async Task CompileAsync_PatchBlockContent_MergesContentAndPreservesStructuralMetadata()
    {
        var blockId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var block = Block(
            blockId,
            BlockType.Paragraph,
            """{"html":"Old","textColor":"red","nested":{"a":1,"b":2}}""");
        block.ParentBlockId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        block.Order = 7;
        block.CreatedAt = createdAt;
        var workingSet = WorkingSet(block);

        var result = await Compile(
            """
            [{
              "op":"patchBlockContent",
              "clientRef":"patch",
              "blockId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "patch":{"html":"New","textColor":null,"nested":{"b":3}}
            }]
            """,
            workingSet);

        result.Success.Should().BeTrue();
        Apply(result, workingSet).Should().OnlyContain(item => item.Success);

        var updated = workingSet.Pages[PageId].Blocks.Single(block => block.Id == blockId);
        updated.PageId.Should().Be(PageId);
        updated.Type.Should().Be(BlockType.Paragraph);
        updated.ParentBlockId.Should().Be(block.ParentBlockId);
        updated.Order.Should().Be(7);
        updated.CreatedAt.Should().Be(createdAt);
        updated.Content.GetProperty("html").GetString().Should().Be("New");
        updated.Content.TryGetProperty("textColor", out _).Should().BeFalse();
        updated.Content.GetProperty("nested").GetProperty("a").GetInt32().Should().Be(1);
        updated.Content.GetProperty("nested").GetProperty("b").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task CompileAsync_ExplicitReorderMoveConvertAndDelete_ApplyInOrder()
    {
        var secondPageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var firstId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var secondId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        var thirdId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
        var targetParentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var targetChildId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var targetChild = Block(targetChildId);
        targetChild.ParentBlockId = targetParentId;
        var workingSet = WorkingSet(
            new Dictionary<Guid, NotionPageSnapshot>
            {
                [PageId] = Page(PageId, Block(firstId), Block(secondId), Block(thirdId)),
                [secondPageId] = Page(secondPageId, new NotionBlockSnapshot
                {
                    Id = targetParentId,
                    PageId = secondPageId,
                    Type = BlockType.Toggle,
                    Content = JsonSerializer.Deserialize<JsonElement>("""{"html":"Target"}""")
                },
                targetChild)
            });
        var result = await Compile(
            $$"""
            [
              {
                "op":"reorderBlocks",
                "clientRef":"reorder",
                "pageId":"{{PageId}}",
                "orderedBlockIds":["{{thirdId}}","{{firstId}}","{{secondId}}"]
              },
              {
                "op":"convertBlockType",
                "clientRef":"convert",
                "blockId":"{{firstId}}",
                "newType":"quote",
                "content":{"html":"Converted"}
              },
              {
                "op":"moveBlock",
                "clientRef":"move",
                "blockId":"{{secondId}}",
                "targetPageId":"{{secondPageId}}",
                "targetParentBlockId":"{{targetParentId}}",
                "targetOrder":0
              },
              {
                "op":"deleteBlock",
                "clientRef":"delete",
                "blockId":"{{thirdId}}"
              }
            ]
            """,
            workingSet);

        result.Success.Should().BeTrue();
        var applied = Apply(result, workingSet);
        applied.Should().OnlyContain(item => item.Success);
        NotionAggregateNormalizer.Normalize(workingSet);

        var sourceBlocks = workingSet.Pages[PageId].Blocks;
        sourceBlocks.Should().ContainSingle(block =>
            block.Id == firstId &&
            block.Type == BlockType.Quote &&
            block.Content.GetProperty("html").GetString() == "Converted");
        var targetBlocks = workingSet.Pages[secondPageId].Blocks;
        targetBlocks.Should().Contain(block =>
            block.Id == secondId &&
            block.ParentBlockId == targetParentId &&
            block.PageId == secondPageId &&
            block.Order == 0);
        targetBlocks.Should().Contain(block =>
            block.Id == targetChildId &&
            block.ParentBlockId == targetParentId &&
            block.Order == 1);
        applied.SelectMany(item => item.Updated).Should().Contain(change =>
            change.Id == firstId && change.ClientRef == "convert");
        applied.SelectMany(item => item.Deleted).Should().Contain(change =>
            change.Id == thirdId && change.ClientRef == "delete");
    }

    [Fact]
    public async Task CompileAsync_ReplaceBlocks_DeletesOldSubtreesAndCreatesRecursiveReplacement()
    {
        var oldRootId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var oldChild = Block(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        oldChild.ParentBlockId = oldRootId;
        var workingSet = WorkingSet(Block(oldRootId), oldChild);
        var result = await Compile(
            """
            [{
              "op":"replaceBlocks",
              "clientRef":"replace",
              "pageId":"11111111-1111-1111-1111-111111111111",
              "blocks":[{
                "type":"heading1",
                "content":{"html":"Replacement"},
                "children":[{"type":"paragraph","content":{"html":"Body"}}]
              }]
            }]
            """,
            workingSet);

        result.Success.Should().BeTrue();
        var applied = Apply(result, workingSet);
        applied.Should().OnlyContain(item => item.Success);
        NotionAggregateNormalizer.Normalize(workingSet);

        var blocks = workingSet.Pages[PageId].Blocks;
        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(BlockType.Heading1);
        blocks[1].ParentBlockId.Should().Be(blocks[0].Id);
        applied.SelectMany(item => item.Deleted).Select(change => change.Id)
            .Should().BeEquivalentTo(new[] { oldRootId, oldChild.Id });
        applied.SelectMany(item => item.Created).Should().HaveCount(2);
    }

    [Fact]
    public async Task CompileAsync_LegacyUpdateBlockContent_IsNotAccepted()
    {
        var result = await Compile(
            """[{"op":"updateBlockContent","blockId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","content":{}}]""");

        result.Success.Should().BeFalse();
        result.Issues.Should().ContainSingle(issue =>
            issue.Code == "unknown_operation" &&
            issue.Path == "$.operations[0].op");
    }

    [Fact]
    public async Task ApplyBlockOperations_CreateTable_DiscoversTargetAndSavesOnce()
    {
        var provider = new SinglePageAggregateProvider(Page(PageId));
        using var services = new ServiceCollection()
            .AddTempoNotionMcpTools()
            .BuildServiceProvider();
        var key = $"strict-tool-{Guid.NewGuid():N}";

        const string operationsJson =
            """
            [{
              "op":"createTable",
              "clientRef":"table",
              "pageId":"11111111-1111-1111-1111-111111111111",
              "columnCount":2,
              "hasHeaderRow":true,
              "rows":[{"cells":[{"html":"A"},{"html":"B"}]}]
            }]
            """;
        const string expectedVersionsJson =
            """[{"pageId":"11111111-1111-1111-1111-111111111111","concurrencyToken":"token-11111111111111111111111111111111"}]""";
        var json = await NotionBlockTools.ApplyBlockOperations(
            services,
            provider,
            key,
            operationsJson,
            expectedVersionsJson);
        var replayJson = await NotionBlockTools.ApplyBlockOperations(
            services,
            provider,
            key,
            operationsJson,
            expectedVersionsJson);

        var root = JsonNode.Parse(json)!.AsObject();
        root["success"]!.GetValue<bool>().Should().BeTrue();
        root["atomic"]!.GetValue<bool>().Should().BeTrue();
        root["applied"]!.GetValue<int>().Should().Be(1);
        root["created"]!.AsArray().Should().HaveCount(2);
        root["created"]!.AsArray().Should().OnlyContain(item =>
            item!["operationIndex"]!.GetValue<int>() == 0 &&
            item["clientRef"]!.GetValue<string>() == "table");
        provider.SaveCount.Should().Be(1);
        provider.Stored.Blocks.Should().HaveCount(2);
        JsonNode.Parse(replayJson)!["replayed"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task CompileAsync_OperationsDependingOnEarlierOperations_UseSequentialPreview()
    {
        var workingSet = WorkingSet(Block(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            contentJson: """{"html":"Existing"}"""));
        var result = await Compile(
            """
            [
              {
                "op":"createBlock",
                "pageId":"11111111-1111-1111-1111-111111111111",
                "block":{"type":"paragraph","content":{"html":"Transient"}}
              },
              {
                "op":"replaceBlocks",
                "pageId":"11111111-1111-1111-1111-111111111111",
                "blocks":[{"type":"paragraph","content":{"html":"Replacement"}}]
              },
              {
                "op":"createBlock",
                "pageId":"11111111-1111-1111-1111-111111111111",
                "block":{"type":"paragraph","content":{"html":"Appended"}}
              }
            ]
            """,
            workingSet);

        result.Success.Should().BeTrue();
        Apply(result, workingSet).Should().OnlyContain(item => item.Success);
        NotionAggregateNormalizer.Normalize(workingSet);

        workingSet.Pages[PageId].Blocks
            .Select(block => block.Content.GetProperty("html").GetString())
            .Should().Equal("Replacement", "Appended");
        workingSet.Pages[PageId].Blocks.Select(block => block.Order).Should().Equal(0, 1);
    }

    private static async Task<NotionOperationCompilationResult> Compile(
        string json,
        NotionAggregateWorkingSet? workingSet = null)
    {
        var source = JsonNode.Parse(json)!.AsArray();
        var compiler = new NotionStrictOperationCompiler();
        return await compiler.CompileAsync(
            source,
            workingSet ?? WorkingSet(),
            new NotionOperationCompileContext("sha256:test", "strict-test-key"),
            CancellationToken.None);
    }

    private static IReadOnlyList<NotionCanonicalApplyResult> Apply(
        NotionOperationCompilationResult compilation,
        NotionAggregateWorkingSet workingSet)
        => compilation.Operations.Select(operation => operation.Apply(workingSet)).ToList();

    private static NotionAggregateWorkingSet WorkingSet(params NotionBlockSnapshot[] blocks)
        => WorkingSet(new Dictionary<Guid, NotionPageSnapshot>
        {
            [PageId] = Page(PageId, blocks)
        });

    private static NotionAggregateWorkingSet WorkingSet(
        IReadOnlyDictionary<Guid, NotionPageSnapshot> pages)
        => new(pages);

    private static NotionPageSnapshot Page(
        Guid pageId,
        params NotionBlockSnapshot[] blocks)
        => new()
        {
            Page = new NotionPageState { Id = pageId, Title = "Page" },
            Blocks = blocks.Select(block =>
            {
                block.PageId = pageId;
                return block;
            }).ToList(),
            ConcurrencyToken = $"token-{pageId:N}",
            Digest = $"sha256:{pageId:N}"
        };

    private static NotionBlockSnapshot Block(
        Guid id,
        BlockType type = BlockType.Paragraph,
        string contentJson = """{"html":"Text"}""")
        => new()
        {
            Id = id,
            PageId = PageId,
            Type = type,
            Content = JsonSerializer.Deserialize<JsonElement>(contentJson)
        };

    private sealed class SinglePageAggregateProvider(NotionPageSnapshot page)
        : INotionAggregateProvider
    {
        public NotionPageSnapshot Stored { get; private set; } = Clone(page);
        public int SaveCount { get; private set; }

        public Task<NotionAggregateLoadResult> LoadPageAsync(
            Guid pageId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(pageId == Stored.Page.Id
                ? new NotionAggregateLoadResult
                {
                    Found = true,
                    Snapshot = Clone(Stored)
                }
                : new NotionAggregateLoadResult());
        }

        public Task<NotionAggregateLoadResult> LoadBlockAsync(
            Guid blockId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Stored.Blocks.Any(block => block.Id == blockId)
                ? new NotionAggregateLoadResult
                {
                    Found = true,
                    Snapshot = Clone(Stored),
                    MatchedBlockId = blockId
                }
                : new NotionAggregateLoadResult());
        }

        public Task<NotionAggregateSaveResult> SaveAsync(
            NotionAggregateSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCount++;
            var save = request.Pages.Should().ContainSingle().Subject;
            save.BaseConcurrencyToken.Should().Be(Stored.ConcurrencyToken);
            Stored = Clone(save.Snapshot);
            Stored.ConcurrencyToken = "saved-token";
            return Task.FromResult(new NotionAggregateSaveResult
            {
                Success = true,
                Pages =
                [
                    new NotionSavedPage
                    {
                        PageId = Stored.Page.Id,
                        ConcurrencyToken = Stored.ConcurrencyToken,
                        Digest = Stored.Digest,
                        SchemaVersion = Stored.SchemaVersion
                    }
                ]
            });
        }

        private static NotionPageSnapshot Clone(NotionPageSnapshot value)
            => JsonSerializer.Deserialize<NotionPageSnapshot>(
                JsonSerializer.Serialize(value, NotionAggregateJson.Options),
                NotionAggregateJson.Options)!;
    }
}
