using Bunit;
using NSubstitute;
using Tempo.Blazor.Components.NotionEditor.Blocks.TempoBlocks;
using Tempo.Blazor.Components.NotionEditor.Services;
using Tempo.Blazor.Components.NotionEditor.UI;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// ABT-01..05: AllowedBlockTypes feature — slash menu filtering,
/// context helper, and existing block rendering.
/// </summary>
public class AllowedBlockTypesTests : LocalizationTestBase
{
    private NotionEditorContext BuildContext(IReadOnlySet<BlockType>? allowed = null)
        => new()
        {
            DataProvider  = Substitute.For<INotionDataProvider>(),
            BlockService = Substitute.For<INotionEditorBlockService>(),
            AllowedBlockTypes = allowed
        };

    // ── ABT-01: GetGrouped with AllowedBlockTypes={Paragraph} returns only Paragraph ───

    [Fact]
    public void GetGrouped_WithAllowedBlockTypes_FiltersToAllowedOnly()
    {
        var allowed   = new HashSet<BlockType> { BlockType.Paragraph };
        var groups    = SlashMenuRegistry.GetGrouped(
            query:        string.Empty,
            recentlyUsed: [],
            resolveName:  _ => string.Empty,
            resolveDescription: _ => string.Empty,
            allowedTypes: allowed);

        var allItems = groups.SelectMany(g => g.Items).ToList();
        allItems.Should().NotBeEmpty();
        allItems.Should().AllSatisfy(i => i.Type.Should().Be(BlockType.Paragraph));
    }

    // ── ABT-02: GetGrouped with null AllowedBlockTypes returns all items ─────

    [Fact]
    public void GetGrouped_WithNullAllowedBlockTypes_ReturnsAll()
    {
        var noFilter   = SlashMenuRegistry.GetGrouped(
            query: string.Empty, recentlyUsed: [],
            resolveName: _ => string.Empty, resolveDescription: _ => string.Empty,
            allowedTypes: null);

        var filtered   = SlashMenuRegistry.GetGrouped(
            query: string.Empty, recentlyUsed: [],
            resolveName: _ => string.Empty, resolveDescription: _ => string.Empty,
            allowedTypes: new HashSet<BlockType> { BlockType.Paragraph });

        noFilter.SelectMany(g => g.Items).Count()
            .Should().BeGreaterThan(filtered.SelectMany(g => g.Items).Count());
    }

    [Fact]
    public void SlashMenuRegistry_ContainsPanelVariantsAndInlineStatusAction()
    {
        var items = SlashMenuRegistry.All;

        items.Where(i => i.CalloutVariant is not null)
            .Select(i => i.CalloutVariant)
            .Should().BeEquivalentTo(new[]
            {
                CalloutVariant.Info,
                CalloutVariant.Note,
                CalloutVariant.Warning,
                CalloutVariant.Error,
                CalloutVariant.Success
            });

        var status = items.Single(i => i.Action == SlashMenuAction.InsertStatus);
        status.Type.Should().Be(BlockType.Paragraph);
        status.Name.Should().Be("TmNotionSlashMenu_ItemName_Status");
    }

    // ── ABT-03: IsBlockTypeAllowed returns false when type not in AllowedBlockTypes ─

    [Fact]
    public void Context_IsBlockTypeAllowed_ReturnsFalse_WhenTypeNotInSet()
    {
        var ctx = BuildContext(allowed: new HashSet<BlockType> { BlockType.Paragraph });

        ctx.IsBlockTypeAllowed(BlockType.Spreadsheet).Should().BeFalse();
    }

    // ── ABT-04: IsBlockTypeAllowed returns true when AllowedBlockTypes is null ─

    [Fact]
    public void Context_IsBlockTypeAllowed_ReturnsTrue_WhenAllowedTypesIsNull()
    {
        var ctx = BuildContext(allowed: null);

        ctx.IsBlockTypeAllowed(BlockType.Spreadsheet).Should().BeTrue();
    }

    // ── ABT-05: Existing Spreadsheet block renders even when type is disallowed ─

    [Fact]
    public void SpreadsheetBlock_RendersNormally_EvenWhenTypeNotInAllowedBlockTypes()
    {
        var allowed = new HashSet<BlockType> { BlockType.Paragraph };
        var ctx     = BuildContext(allowed: allowed);
        var content = new SpreadsheetBlockContent { SpreadsheetDocumentId = Guid.NewGuid() };

        // Even though Spreadsheet is not allowed, the block should still render
        // the embed wrap (just no "Create" button in the slash menu).
        // We can verify it renders without throwing.
        var cut = Render<TmNotionSpreadsheetBlock>(p => p
            .AddCascadingValue(ctx)
            .Add(x => x.Content, (ISpreadsheetBlockContent)content)
            .Add(x => x.ReadOnly, false));

        // Embed wrap is rendered (block still shows, not hidden)
        cut.Find(".tm-notion-spreadsheet-block").Should().NotBeNull();
    }
}
