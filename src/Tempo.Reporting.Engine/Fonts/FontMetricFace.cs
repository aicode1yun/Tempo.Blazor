namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Precomputed metrics for one font family/style face.</summary>
public sealed class FontMetricFace
{
    private readonly IReadOnlyDictionary<int, ushort> _advanceWidths;
    private readonly IReadOnlyDictionary<FontKerningPair, short> _kerningPairs;
    private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, ushort>> _hintedAdvanceWidths;
    private readonly IReadOnlyDictionary<int, ushort> _missingGlyphHintedAdvanceWidths;

    /// <summary>Initializes font metrics for one face.</summary>
    public FontMetricFace(
        string familyName,
        FontStyleKey styleKey,
        int unitsPerEm,
        short ascent,
        short descent,
        short lineGap,
        ushort missingGlyphAdvanceWidth,
        IReadOnlyDictionary<int, ushort> advanceWidths,
        IReadOnlyDictionary<FontKerningPair, short> kerningPairs,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, ushort>>? hintedAdvanceWidths = null,
        IReadOnlyDictionary<int, ushort>? missingGlyphHintedAdvanceWidths = null)
    {
        FamilyName = string.IsNullOrWhiteSpace(familyName) ? "Tempo Fallback" : familyName;
        StyleKey = styleKey;
        UnitsPerEm = unitsPerEm > 0 ? unitsPerEm : 1000;
        Ascent = ascent;
        Descent = descent;
        LineGap = lineGap;
        MissingGlyphAdvanceWidth = missingGlyphAdvanceWidth;
        _advanceWidths = new Dictionary<int, ushort>(advanceWidths, EqualityComparer<int>.Default);
        _kerningPairs = new Dictionary<FontKerningPair, short>(kerningPairs);
        _hintedAdvanceWidths = hintedAdvanceWidths?
            .ToDictionary(
                static item => item.Key,
                static item => (IReadOnlyDictionary<int, ushort>)new Dictionary<int, ushort>(item.Value, EqualityComparer<int>.Default))
            ?? new Dictionary<int, IReadOnlyDictionary<int, ushort>>();
        _missingGlyphHintedAdvanceWidths = missingGlyphHintedAdvanceWidths is null
            ? new Dictionary<int, ushort>()
            : new Dictionary<int, ushort>(missingGlyphHintedAdvanceWidths, EqualityComparer<int>.Default);
    }

    /// <summary>Font family name.</summary>
    public string FamilyName { get; }

    /// <summary>Font style key.</summary>
    public FontStyleKey StyleKey { get; }

    /// <summary>Units per em from the TrueType head table.</summary>
    public int UnitsPerEm { get; }

    /// <summary>Ascender in font units.</summary>
    public short Ascent { get; }

    /// <summary>Descender in font units.</summary>
    public short Descent { get; }

    /// <summary>Line gap in font units.</summary>
    public short LineGap { get; }

    /// <summary>Advance width used when no glyph is available.</summary>
    public ushort MissingGlyphAdvanceWidth { get; }

    /// <summary>Unicode advance width table.</summary>
    public IReadOnlyDictionary<int, ushort> AdvanceWidths => _advanceWidths;

    /// <summary>Unicode kerning table.</summary>
    public IReadOnlyDictionary<FontKerningPair, short> KerningPairs => _kerningPairs;

    /// <summary>Hinted Unicode advance widths keyed by CSS pixels per em.</summary>
    public IReadOnlyDictionary<int, IReadOnlyDictionary<int, ushort>> HintedAdvanceWidths => _hintedAdvanceWidths;

    /// <summary>Hinted missing-glyph advance widths keyed by CSS pixels per em.</summary>
    public IReadOnlyDictionary<int, ushort> MissingGlyphHintedAdvanceWidths => _missingGlyphHintedAdvanceWidths;

    /// <summary>Returns true when the face contains a code point.</summary>
    public bool ContainsCodePoint(int codePoint)
        => _advanceWidths.ContainsKey(codePoint);

    /// <summary>Gets an advance width for a Unicode code point.</summary>
    public ushort GetAdvanceWidth(int codePoint)
        => _advanceWidths.TryGetValue(codePoint, out var width) ? width : MissingGlyphAdvanceWidth;

    /// <summary>Attempts to get a hinted CSS pixel advance width for a Unicode code point.</summary>
    public bool TryGetHintedAdvanceWidth(int codePoint, int pixelsPerEm, out ushort width)
    {
        if (_hintedAdvanceWidths.TryGetValue(pixelsPerEm, out var widths) && widths.TryGetValue(codePoint, out width))
        {
            return true;
        }

        width = 0;
        return false;
    }

    /// <summary>Attempts to get a hinted CSS pixel advance width for a missing glyph.</summary>
    public bool TryGetHintedMissingGlyphAdvanceWidth(int pixelsPerEm, out ushort width)
        => _missingGlyphHintedAdvanceWidths.TryGetValue(pixelsPerEm, out width);

    /// <summary>Gets kerning in font units for a code-point pair.</summary>
    public short GetKerning(int leftCodePoint, int rightCodePoint)
        => _kerningPairs.TryGetValue(new FontKerningPair(leftCodePoint, rightCodePoint), out var value) ? value : (short)0;
}
