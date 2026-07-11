using FluentAssertions;
using Tempo.Blazor.Components.NotionEditor.Services;

namespace Tempo.Blazor.Tests.Components.NotionEditor;

/// <summary>
/// A column list seeds its default columns exactly once. Seeding on every load — the old behaviour —
/// meant a column list the user had emptied resurrected two blank columns and could never be removed.
/// </summary>
public sealed class NotionColumnListSeedingTests
{
    [Fact]
    public void AFreshColumnList_Seeds() =>
        NotionColumnListSeeding.Decide(declaredColumnCount: 0, storedColumnCount: 0)
            .Should().Be(ColumnListAction.Seed);

    [Fact]
    public void AColumnListThatDeclaresColumnsButHasNone_Collapses() =>
        NotionColumnListSeeding.Decide(declaredColumnCount: 2, storedColumnCount: 0)
            .Should().Be(ColumnListAction.Collapse);

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    public void AColumnListWithStoredColumns_IsLeftAlone(int declared, int stored) =>
        NotionColumnListSeeding.Decide(declared, stored).Should().Be(ColumnListAction.Keep);

    [Fact]
    public void ANegativeDeclaredCount_IsTreatedAsFresh() =>
        NotionColumnListSeeding.Decide(declaredColumnCount: -1, storedColumnCount: 0)
            .Should().Be(ColumnListAction.Seed);
}
