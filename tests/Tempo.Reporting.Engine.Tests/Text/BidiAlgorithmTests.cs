using Tempo.Reporting.Engine.Text;

namespace Tempo.Reporting.Engine.Tests.Text;

/// <summary>
/// Readable, headline-case tests for <see cref="BidiAlgorithm"/> that document the intended
/// visual behaviour of mixed LTR/RTL text. The exhaustive Unicode conformance suite lives in
/// <see cref="BidiConformanceTests"/>.
/// </summary>
public sealed class BidiAlgorithmTests
{
    private const char Alef = 'א';  // Hebrew letter alef (R)
    private const char Bet = 'ב';   // Hebrew letter bet (R)
    private const char Gimel = 'ג'; // Hebrew letter gimel (R)
    private const char ArAlef = 'ا'; // Arabic letter alef (AL)
    private const char ArBa = 'ب';   // Arabic letter beh (AL)

    // Reconstruct the source text in left-to-right visual order from the result.
    private static string ToVisualString(string source, BidiResult result)
    {
        // VisualOrder maps logical index -> visual position; invert it.
        char[] visual = new char[source.Length];
        for (int logical = 0; logical < source.Length; logical++)
        {
            visual[result.VisualOrder[logical]] = source[logical];
        }

        return new string(visual);
    }

    [Fact]
    public void PureLatin_KeepsLogicalOrder_AtLevelZero()
    {
        BidiResult result = BidiAlgorithm.Resolve("abc");

        result.ParagraphLevel.Should().Be(0);
        result.Levels.Should().AllBeEquivalentTo<byte>(0);
        ToVisualString("abc", result).Should().Be("abc");
    }

    [Fact]
    public void PureHebrew_AutoDetectsRtl_AndReversesVisualOrder()
    {
        string text = new([Alef, Bet, Gimel]);

        BidiResult result = BidiAlgorithm.Resolve(text);

        result.ParagraphLevel.Should().Be(1, "the first strong character is right-to-left");
        result.Levels.Should().AllBeEquivalentTo<byte>(1);
        // Hebrew letters render right-to-left: gimel, bet, alef from the left.
        ToVisualString(text, result).Should().Be(new string([Gimel, Bet, Alef]));
    }

    [Fact]
    public void HebrewSentenceWithLatinBrand_PlacesBrandLeftToRightInsideRtl()
    {
        // Logical: <alef><bet> SP a b SP <gimel>  (RTL paragraph containing a Latin word).
        string text = new([Alef, Bet, ' ', 'a', 'b', ' ', Gimel]);

        BidiResult result = BidiAlgorithm.Resolve(text);

        result.ParagraphLevel.Should().Be(1);
        // The Latin run "ab" stays left-to-right, while the Hebrew reverses around it.
        // Visual (left to right): gimel SP a b SP bet alef
        ToVisualString(text, result).Should().Be(new string([Gimel, ' ', 'a', 'b', ' ', Bet, Alef]));
        // "ab" keeps an even (LTR) level inside the RTL paragraph.
        result.Levels[3].Should().Be(2);
        result.Levels[4].Should().Be(2);
    }

    [Fact]
    public void ArabicWordWithLatinBrandAndNumber_OrdersEachRunCorrectly()
    {
        // Logical: <ar-alef><ar-ba> SP T M SP 4 2  (Arabic + Latin brand + European number).
        string text = new([ArAlef, ArBa, ' ', 'T', 'M', ' ', '4', '2']);

        BidiResult result = BidiAlgorithm.Resolve(text);

        result.ParagraphLevel.Should().Be(1, "the first strong character is Arabic (AL)");
        // European digits keep their own left-to-right order ("42", not "24"),
        // the Latin brand stays "TM", and the Arabic word reverses to the right.
        // Visual (left to right): T M SP 4 2 SP <ar-ba> <ar-alef>
        ToVisualString(text, result).Should().Be(new string(['T', 'M', ' ', '4', '2', ' ', ArBa, ArAlef]));
    }

    [Fact]
    public void ExplicitLtrParagraphLevel_OverridesAutoDetection()
    {
        string text = new([Alef, Bet, Gimel]);

        BidiResult ltr = BidiAlgorithm.Resolve(text, paragraphLevel: 0);

        ltr.ParagraphLevel.Should().Be(0, "the paragraph level was supplied explicitly");
        // Still an RTL run, but embedded in an LTR paragraph (odd level 1).
        ltr.Levels.Should().AllBeEquivalentTo<byte>(1);
    }

    [Fact]
    public void EnglishNumberAfterLatin_StaysLeftToRight()
    {
        BidiResult result = BidiAlgorithm.Resolve("a1");

        result.ParagraphLevel.Should().Be(0);
        ToVisualString("a1", result).Should().Be("a1");
    }

    [Fact]
    public void BracketPairInRtl_ResolvesViaRuleN0()
    {
        // Logical: <alef> ( a ) <bet>  — a Latin letter parenthesised inside Hebrew.
        string text = new([Alef, '(', 'a', ')', Bet]);

        BidiResult result = BidiAlgorithm.Resolve(text);

        result.ParagraphLevel.Should().Be(1);
        // N0 resolves the bracket pair to the embedding (R) direction; the Latin "a"
        // is an isolated LTR island, so it stays upright while brackets mirror.
        // Visual (left to right): bet ) a ( alef
        ToVisualString(text, result).Should().Be(new string([Bet, ')', 'a', '(', Alef]));
    }

    [Fact]
    public void MirroredBracket_IsFlaggedForMirroringInRtl()
    {
        string text = new([Alef, '(', Bet]);

        BidiResult result = BidiAlgorithm.Resolve(text);

        // '(' resolves to an odd (RTL) level and has Bidi_Mirrored=Yes, so it must be mirrored.
        result.Mirrored[1].Should().BeTrue();
        BidiAlgorithm.GetMirrorGlyph('(').Should().Be(')');
    }

    [Fact]
    public void SupplementaryRtlCharacter_IsHandledAsOneScalar()
    {
        // U+10800 CYPRIOT SYLLABLE A has Bidi_Class R and is a surrogate pair in UTF-16.
        string text = "a" + char.ConvertFromUtf32(0x10800) + "b";

        BidiResult result = BidiAlgorithm.Resolve(text);

        result.ParagraphLevel.Should().Be(0);
        result.Levels.Should().HaveCount(4, "the supplementary character occupies two UTF-16 code units");
        // Both halves of the surrogate pair share the resolved RTL level.
        result.Levels[1].Should().Be(1);
        result.Levels[2].Should().Be(1);
        // The Latin letters remain at the paragraph level.
        result.Levels[0].Should().Be(0);
        result.Levels[3].Should().Be(0);
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyResult()
    {
        BidiResult result = BidiAlgorithm.Resolve(ReadOnlySpan<char>.Empty);

        result.ParagraphLevel.Should().Be(0);
        result.Levels.Should().BeEmpty();
        result.VisualOrder.Should().BeEmpty();
        result.Mirrored.Should().BeEmpty();
    }

    [Fact]
    public void InvalidParagraphLevel_Throws()
    {
        Action act = () => BidiAlgorithm.Resolve("a", paragraphLevel: 2);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
