namespace Tempo.Reporting.Engine.Text;

/// <summary>
/// Unicode character property lookups backing the Bidirectional Algorithm (UAX#9).
/// The raw tables are generated from the Unicode Character Database 16.0.0
/// (see <c>BidiCharacterData.g.cs</c>).
/// </summary>
internal static partial class BidiCharacterData
{
    /// <summary>Returns the Bidi_Class of a Unicode scalar value.</summary>
    public static BidiClass GetBidiClass(int codePoint)
    {
        // BidiClassRanges is sorted ascending by packed start. Each entry packs
        // (start << 5) | class; the range extends to the code point before the
        // next entry's start. Binary search for the greatest start <= codePoint.
        uint[] ranges = BidiClassRanges;
        int lo = 0;
        int hi = ranges.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if ((int)(ranges[mid] >> 5) <= codePoint)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return (BidiClass)(byte)(ranges[lo] & 0x1F);
    }

    /// <summary>
    /// Paired-bracket type per BD14/BD15: 0 = none, 1 = opening, 2 = closing.
    /// </summary>
    public const byte PairedBracketNone = 0;

    /// <summary>Opening paired bracket.</summary>
    public const byte PairedBracketOpen = 1;

    /// <summary>Closing paired bracket.</summary>
    public const byte PairedBracketClose = 2;

    /// <summary>
    /// Looks up the paired-bracket type and canonical pair value for a code point.
    /// The pair value is a stable identifier shared by both members of a pair
    /// (folding canonical-equivalent brackets such as U+2329/U+3008), as required
    /// by BD16. Returns false for non-bracket characters.
    /// </summary>
    public static bool TryGetBracket(int codePoint, out byte pairedType, out int pairValue)
    {
        int[] data = BracketData; // triplets: codePoint, type, pairValue
        int lo = 0;
        int hi = (data.Length / 3) - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int cp = data[mid * 3];
            if (cp == codePoint)
            {
                pairedType = (byte)data[mid * 3 + 1];
                pairValue = data[mid * 3 + 2];
                return true;
            }

            if (cp < codePoint)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        pairedType = PairedBracketNone;
        pairValue = 0;
        return false;
    }

    /// <summary>
    /// Returns the Bidi_Mirroring_Glyph for a code point, or the code point itself
    /// when no explicit mirror glyph exists. Used by rule L4 in the renderer (pass 3b).
    /// </summary>
    public static int GetMirrorGlyph(int codePoint)
    {
        int[] data = MirrorGlyphData; // pairs: codePoint, mirrorGlyph
        int lo = 0;
        int hi = (data.Length / 2) - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int cp = data[mid * 2];
            if (cp == codePoint)
            {
                return data[mid * 2 + 1];
            }

            if (cp < codePoint)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return codePoint;
    }

    /// <summary>Returns whether a code point has the Bidi_Mirrored=Yes property.</summary>
    public static bool IsMirrored(int codePoint)
    {
        int[] ranges = MirroredRanges; // pairs: start, end (inclusive)
        int lo = 0;
        int hi = (ranges.Length / 2) - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int start = ranges[mid * 2];
            int end = ranges[mid * 2 + 1];
            if (codePoint < start)
            {
                hi = mid - 1;
            }
            else if (codePoint > end)
            {
                lo = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
