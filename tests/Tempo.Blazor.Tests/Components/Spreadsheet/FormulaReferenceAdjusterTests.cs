using Tempo.Blazor.Components.Spreadsheet.Formula;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

public class FormulaReferenceAdjusterTests
{
    // ── AdjustFormula – relative references ─────────────────────────────────

    [Fact]
    public void AdjustFormula_RelativeRef_ShiftsRowAndCol()
    {
        var result = FormulaReferenceAdjuster.AdjustFormula("=A1+B2", 1, 1);
        result.Should().Be("=B2+C3");
    }

    [Fact]
    public void AdjustFormula_ZeroOffset_ReturnsOriginal()
    {
        const string formula = "=A1+B2";
        FormulaReferenceAdjuster.AdjustFormula(formula, 0, 0).Should().Be(formula);
    }

    [Fact]
    public void AdjustFormula_RowOnly()
    {
        FormulaReferenceAdjuster.AdjustFormula("=A1", 3, 0).Should().Be("=A4");
    }

    [Fact]
    public void AdjustFormula_ColOnly()
    {
        FormulaReferenceAdjuster.AdjustFormula("=A1", 0, 2).Should().Be("=C1");
    }

    // ── AdjustFormula – absolute references ─────────────────────────────────

    [Fact]
    public void AdjustFormula_AbsoluteCol_ShiftsOnlyRow()
    {
        FormulaReferenceAdjuster.AdjustFormula("=$A1", 1, 2).Should().Be("=$A2");
    }

    [Fact]
    public void AdjustFormula_AbsoluteRow_ShiftsOnlyCol()
    {
        FormulaReferenceAdjuster.AdjustFormula("=A$1", 2, 1).Should().Be("=B$1");
    }

    [Fact]
    public void AdjustFormula_FullyAbsolute_NoChange()
    {
        FormulaReferenceAdjuster.AdjustFormula("=$A$1", 5, 5).Should().Be("=$A$1");
    }

    // ── AdjustFormula – range references ────────────────────────────────────

    [Fact]
    public void AdjustFormula_RangeRef_BothPartsShifted()
    {
        FormulaReferenceAdjuster.AdjustFormula("=SUM(A1:B3)", 1, 0).Should().Be("=SUM(A2:B4)");
    }

    [Fact]
    public void AdjustFormula_RangeRef_MixedAbsolute()
    {
        FormulaReferenceAdjuster.AdjustFormula("=SUM($A$1:B3)", 2, 1).Should().Be("=SUM($A$1:C5)");
    }

    [Fact]
    public void AdjustFormula_MultipleRefs_AllShifted()
    {
        FormulaReferenceAdjuster.AdjustFormula("=A1*C3+D5", 1, 1).Should().Be("=B2*D4+E6");
    }

    // ── AdjustFormula – edge cases ───────────────────────────────────────────

    [Fact]
    public void AdjustFormula_OutOfBoundsCol_ReturnsRefError()
    {
        // A is column 0; shifting by -1 gives column -1 → #REF!
        var result = FormulaReferenceAdjuster.AdjustFormula("=A1", 0, -1);
        result.Should().Contain("#REF!");
    }

    [Fact]
    public void AdjustFormula_OutOfBoundsRow_ReturnsRefError()
    {
        // Row 1 shifted by -1 gives row 0 → #REF!
        var result = FormulaReferenceAdjuster.AdjustFormula("=A1", -1, 0);
        result.Should().Contain("#REF!");
    }

    [Fact]
    public void AdjustFormula_NonFormulaString_ReturnsOriginal()
    {
        const string text = "hello";
        FormulaReferenceAdjuster.AdjustFormula(text, 1, 1).Should().Be(text);
    }

    [Fact]
    public void AdjustFormula_PreservesOperatorsAndFunctions()
    {
        var result = FormulaReferenceAdjuster.AdjustFormula("=IF(A1>0,B1,C1)", 1, 0);
        result.Should().Be("=IF(A2>0,B2,C2)");
    }

    [Fact]
    public void AdjustFormula_PreservesStringLiterals()
    {
        var result = FormulaReferenceAdjuster.AdjustFormula("=CONCATENATE(A1,\"text\")", 1, 1);
        result.Should().Be("=CONCATENATE(B2,\"text\")");
    }

    // ── AdjustCellRef directly ───────────────────────────────────────────────

    [Fact]
    public void AdjustCellRef_Relative_Shifts()
    {
        FormulaReferenceAdjuster.AdjustCellRef("A1", 2, 3).Should().Be("D3");
    }

    [Fact]
    public void AdjustCellRef_AbsoluteColRow_Unchanged()
    {
        FormulaReferenceAdjuster.AdjustCellRef("$Z$99", 10, 10).Should().Be("$Z$99");
    }

    [Fact]
    public void AdjustCellRef_AbsoluteCol_OnlyRowMoves()
    {
        FormulaReferenceAdjuster.AdjustCellRef("$B5", 2, 3).Should().Be("$B7");
    }

    [Fact]
    public void AdjustCellRef_AbsoluteRow_OnlyColMoves()
    {
        FormulaReferenceAdjuster.AdjustCellRef("C$10", 5, 2).Should().Be("E$10");
    }

    // ── CycleLastAbsoluteRef ─────────────────────────────────────────────────

    [Fact]
    public void CycleLastAbsoluteRef_NoneToFull()
    {
        FormulaReferenceAdjuster.CycleLastAbsoluteRef("=A1").Should().Be("=$A$1");
    }

    [Fact]
    public void CycleLastAbsoluteRef_FullToRowAbsolute()
    {
        FormulaReferenceAdjuster.CycleLastAbsoluteRef("=$A$1").Should().Be("=A$1");
    }

    [Fact]
    public void CycleLastAbsoluteRef_RowAbsoluteToColAbsolute()
    {
        FormulaReferenceAdjuster.CycleLastAbsoluteRef("=A$1").Should().Be("=$A1");
    }

    [Fact]
    public void CycleLastAbsoluteRef_ColAbsoluteToNone()
    {
        FormulaReferenceAdjuster.CycleLastAbsoluteRef("=$A1").Should().Be("=A1");
    }

    [Fact]
    public void CycleLastAbsoluteRef_MultiRef_OnlyCyclesLast()
    {
        // Only the last ref (B2) should cycle; A1 stays unchanged
        FormulaReferenceAdjuster.CycleLastAbsoluteRef("=A1+B2").Should().Be("=A1+$B$2");
    }

    [Fact]
    public void CycleLastAbsoluteRef_NoRef_ReturnsOriginal()
    {
        const string formula = "=1+2";
        FormulaReferenceAdjuster.CycleLastAbsoluteRef(formula).Should().Be(formula);
    }

    // ── ParseFormulaReferences ───────────────────────────────────────────────

    [Fact]
    public void ParseFormulaReferences_SingleCellRef()
    {
        var refs = FormulaReferenceAdjuster.ParseFormulaReferences("=A1");
        refs.Should().ContainSingle().Which.Should().Be("A1");
    }

    [Fact]
    public void ParseFormulaReferences_MultipleRefs_InOrder()
    {
        var refs = FormulaReferenceAdjuster.ParseFormulaReferences("=A1+B2+C3");
        refs.Should().Equal("A1", "B2", "C3");
    }

    [Fact]
    public void ParseFormulaReferences_DeduplicatesRefs()
    {
        var refs = FormulaReferenceAdjuster.ParseFormulaReferences("=A1+A1+B2");
        refs.Should().Equal("A1", "B2");
    }

    [Fact]
    public void ParseFormulaReferences_IncludesRangeRef()
    {
        var refs = FormulaReferenceAdjuster.ParseFormulaReferences("=SUM(A1:B3)");
        refs.Should().ContainSingle().Which.Should().Be("A1:B3");
    }

    [Fact]
    public void ParseFormulaReferences_NoRefs_ReturnsEmpty()
    {
        FormulaReferenceAdjuster.ParseFormulaReferences("=1+2").Should().BeEmpty();
    }

    // ── InsertOrReplaceLastRef ───────────────────────────────────────────────

    [Fact]
    public void InsertOrReplaceLastRef_AppendsAfterOperator()
    {
        var result = FormulaReferenceAdjuster.InsertOrReplaceLastRef("=A1+", "B2");
        result.Should().Be("=A1+B2");
    }

    [Fact]
    public void InsertOrReplaceLastRef_ReplacesTrailingCellRef()
    {
        var result = FormulaReferenceAdjuster.InsertOrReplaceLastRef("=A1", "B2");
        result.Should().Be("=B2");
    }

    [Fact]
    public void InsertOrReplaceLastRef_ReplacesTrailingRangeRef()
    {
        var result = FormulaReferenceAdjuster.InsertOrReplaceLastRef("=SUM(A1:B3", "C1:D5");
        result.Should().Be("=SUM(C1:D5");
    }

    [Fact]
    public void InsertOrReplaceLastRef_AppendsAfterEqualSign()
    {
        var result = FormulaReferenceAdjuster.InsertOrReplaceLastRef("=", "A1");
        result.Should().Be("=A1");
    }

    [Fact]
    public void InsertOrReplaceLastRef_AppendsAfterOpenParen()
    {
        var result = FormulaReferenceAdjuster.InsertOrReplaceLastRef("=SUM(", "A1");
        result.Should().Be("=SUM(A1");
    }

    [Fact]
    public void InsertOrReplaceLastRef_MidFormula_ReplacesLastRef()
    {
        // =A1+B2 → clicking C3 replaces B2 (the last ref)
        var result = FormulaReferenceAdjuster.InsertOrReplaceLastRef("=A1+B2", "C3");
        result.Should().Be("=A1+C3");
    }
}
