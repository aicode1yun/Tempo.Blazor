namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Reads the TrueType tables required by the reporting metric generator.</summary>
public static class TrueTypeFontMetricReader
{
    /// <summary>Builds a reporting font face from a TrueType font stream.</summary>
    public static FontMetricFace Read(Stream stream, string familyName, FontStyleKey styleKey)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();
        var tables = ReadTableDirectory(data);

        var unitsPerEm = ReadUInt16(data, RequireTable(tables, "head").Offset + 18);
        var hhea = RequireTable(tables, "hhea");
        var ascent = ReadInt16(data, hhea.Offset + 4);
        var descent = ReadInt16(data, hhea.Offset + 6);
        var lineGap = ReadInt16(data, hhea.Offset + 8);
        var numberOfHMetrics = ReadUInt16(data, hhea.Offset + 34);
        var numGlyphs = ReadUInt16(data, RequireTable(tables, "maxp").Offset + 4);
        var glyphAdvances = ReadHorizontalMetrics(data, RequireTable(tables, "hmtx"), numGlyphs, numberOfHMetrics);
        var codePointToGlyph = ReadCmap(data, RequireTable(tables, "cmap"));
        var advances = new Dictionary<int, ushort>(codePointToGlyph.Count);

        foreach (var item in codePointToGlyph.OrderBy(static item => item.Key))
        {
            if (item.Value < glyphAdvances.Length)
            {
                advances[item.Key] = glyphAdvances[item.Value];
            }
        }

        var kerning = tables.TryGetValue("kern", out var kernTable)
            ? ReadKerning(data, kernTable, codePointToGlyph)
            : new Dictionary<FontKerningPair, short>();
        var hdmx = tables.TryGetValue("hdmx", out var hdmxTable)
            ? ReadHintedAdvanceWidths(data, hdmxTable, codePointToGlyph, numGlyphs)
            : new HintedAdvanceWidthTable(
                new Dictionary<int, IReadOnlyDictionary<int, ushort>>(),
                new Dictionary<int, ushort>());

        var missingAdvance = glyphAdvances.Length > 0 ? glyphAdvances[0] : (ushort)(unitsPerEm / 2);
        return new FontMetricFace(
            familyName,
            styleKey,
            unitsPerEm,
            ascent,
            descent,
            lineGap,
            missingAdvance,
            advances,
            kerning,
            hdmx.AdvanceWidths,
            hdmx.MissingGlyphAdvanceWidths);
    }

    private static Dictionary<string, TableRecord> ReadTableDirectory(byte[] data)
    {
        if (data.Length < 12)
        {
            throw new InvalidDataException("TrueType font is too short.");
        }

        var numTables = ReadUInt16(data, 4);
        var tables = new Dictionary<string, TableRecord>(StringComparer.Ordinal);
        var offset = 12;
        for (var i = 0; i < numTables; i++)
        {
            EnsureAvailable(data, offset, 16);
            var tag = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var tableOffset = checked((int)ReadUInt32(data, offset + 8));
            var tableLength = checked((int)ReadUInt32(data, offset + 12));
            EnsureAvailable(data, tableOffset, tableLength);
            tables[tag] = new TableRecord(tableOffset, tableLength);
            offset += 16;
        }

        return tables;
    }

    private static ushort[] ReadHorizontalMetrics(byte[] data, TableRecord hmtx, int numGlyphs, int numberOfHMetrics)
    {
        var advances = new ushort[Math.Max(0, numGlyphs)];
        ushort lastAdvance = 0;
        for (var glyph = 0; glyph < advances.Length; glyph++)
        {
            if (glyph < numberOfHMetrics)
            {
                var metricOffset = hmtx.Offset + glyph * 4;
                EnsureAvailable(data, metricOffset, 2);
                lastAdvance = ReadUInt16(data, metricOffset);
                advances[glyph] = lastAdvance;
            }
            else
            {
                advances[glyph] = lastAdvance;
            }
        }

        return advances;
    }

    private static Dictionary<int, ushort> ReadCmap(byte[] data, TableRecord cmap)
    {
        EnsureAvailable(data, cmap.Offset, 4);
        var subtableCount = ReadUInt16(data, cmap.Offset + 2);
        var candidates = new List<(int Score, Dictionary<int, ushort> Map)>();
        for (var i = 0; i < subtableCount; i++)
        {
            var recordOffset = cmap.Offset + 4 + i * 8;
            EnsureAvailable(data, recordOffset, 8);
            var platformId = ReadUInt16(data, recordOffset);
            var encodingId = ReadUInt16(data, recordOffset + 2);
            var subtableOffset = cmap.Offset + checked((int)ReadUInt32(data, recordOffset + 4));
            EnsureAvailable(data, subtableOffset, 2);
            var format = ReadUInt16(data, subtableOffset);

            if (format == 12)
            {
                var score = platformId == 3 && encodingId == 10 ? 100 : 80;
                candidates.Add((score, ReadCmapFormat12(data, subtableOffset)));
            }
            else if (format == 4)
            {
                var score = platformId == 3 ? 70 : 60;
                candidates.Add((score, ReadCmapFormat4(data, subtableOffset)));
            }
        }

        var best = candidates
            .Where(static candidate => candidate.Map.Count > 0)
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.Map.Count)
            .FirstOrDefault();
        if (best.Map is null)
        {
            throw new InvalidDataException("TrueType font does not contain a supported Unicode cmap table.");
        }

        return best.Map;
    }

    private static Dictionary<int, ushort> ReadCmapFormat12(byte[] data, int offset)
    {
        EnsureAvailable(data, offset, 16);
        var length = checked((int)ReadUInt32(data, offset + 4));
        EnsureAvailable(data, offset, length);
        var groupCount = checked((int)ReadUInt32(data, offset + 12));
        var map = new Dictionary<int, ushort>();
        var groupOffset = offset + 16;
        for (var i = 0; i < groupCount; i++)
        {
            var startChar = ReadUInt32(data, groupOffset);
            var endChar = ReadUInt32(data, groupOffset + 4);
            var startGlyph = ReadUInt32(data, groupOffset + 8);
            for (var codePoint = startChar; codePoint <= endChar && codePoint <= int.MaxValue; codePoint++)
            {
                var glyph = startGlyph + (codePoint - startChar);
                if (glyph <= ushort.MaxValue)
                {
                    map[(int)codePoint] = (ushort)glyph;
                }
            }

            groupOffset += 12;
        }

        return map;
    }

    private static Dictionary<int, ushort> ReadCmapFormat4(byte[] data, int offset)
    {
        EnsureAvailable(data, offset, 16);
        var length = ReadUInt16(data, offset + 2);
        EnsureAvailable(data, offset, length);
        var segCount = ReadUInt16(data, offset + 6) / 2;
        var endCodeOffset = offset + 14;
        var startCodeOffset = endCodeOffset + segCount * 2 + 2;
        var idDeltaOffset = startCodeOffset + segCount * 2;
        var idRangeOffsetOffset = idDeltaOffset + segCount * 2;
        var map = new Dictionary<int, ushort>();

        for (var segment = 0; segment < segCount; segment++)
        {
            var endCode = ReadUInt16(data, endCodeOffset + segment * 2);
            var startCode = ReadUInt16(data, startCodeOffset + segment * 2);
            var delta = ReadInt16(data, idDeltaOffset + segment * 2);
            var rangeOffsetAddress = idRangeOffsetOffset + segment * 2;
            var rangeOffset = ReadUInt16(data, rangeOffsetAddress);
            if (startCode == 0xFFFF && endCode == 0xFFFF)
            {
                continue;
            }

            for (var codePoint = startCode; codePoint <= endCode; codePoint++)
            {
                ushort glyph;
                if (rangeOffset == 0)
                {
                    glyph = unchecked((ushort)((codePoint + delta) & 0xFFFF));
                }
                else
                {
                    var glyphOffset = rangeOffsetAddress + rangeOffset + (codePoint - startCode) * 2;
                    if (glyphOffset < offset || glyphOffset + 2 > offset + length)
                    {
                        continue;
                    }

                    glyph = ReadUInt16(data, glyphOffset);
                    if (glyph != 0)
                    {
                        glyph = unchecked((ushort)((glyph + delta) & 0xFFFF));
                    }
                }

                if (glyph != 0)
                {
                    map[codePoint] = glyph;
                }
            }
        }

        return map;
    }

    private static Dictionary<FontKerningPair, short> ReadKerning(byte[] data, TableRecord kern, IReadOnlyDictionary<int, ushort> codePointToGlyph)
    {
        var glyphToCodePoint = codePointToGlyph
            .GroupBy(static item => item.Value)
            .ToDictionary(static group => group.Key, static group => group.Min(item => item.Key));
        var result = new Dictionary<FontKerningPair, short>();
        EnsureAvailable(data, kern.Offset, 4);
        var version = ReadUInt16(data, kern.Offset);
        if (version != 0)
        {
            return result;
        }

        var subtableCount = ReadUInt16(data, kern.Offset + 2);
        var offset = kern.Offset + 4;
        for (var subtable = 0; subtable < subtableCount; subtable++)
        {
            EnsureAvailable(data, offset, 6);
            var length = ReadUInt16(data, offset + 2);
            var coverage = ReadUInt16(data, offset + 4);
            var format = coverage >> 8;
            if (format == 0)
            {
                ReadKerningFormat0(data, offset + 6, result, glyphToCodePoint);
            }

            offset += length;
        }

        return result;
    }

    private static HintedAdvanceWidthTable ReadHintedAdvanceWidths(
        byte[] data,
        TableRecord hdmx,
        IReadOnlyDictionary<int, ushort> codePointToGlyph,
        int numGlyphs)
    {
        EnsureAvailable(data, hdmx.Offset, 8);
        var version = ReadUInt16(data, hdmx.Offset);
        var recordCount = ReadInt16(data, hdmx.Offset + 2);
        var recordSize = checked((int)ReadUInt32(data, hdmx.Offset + 4));
        if (version != 0 || recordCount <= 0 || recordSize < 2 || numGlyphs <= 0)
        {
            return new HintedAdvanceWidthTable(
                new Dictionary<int, IReadOnlyDictionary<int, ushort>>(),
                new Dictionary<int, ushort>());
        }

        var widthsByPpem = new Dictionary<int, IReadOnlyDictionary<int, ushort>>();
        var missingWidthsByPpem = new Dictionary<int, ushort>();
        var recordOffset = hdmx.Offset + 8;
        for (var record = 0; record < recordCount; record++)
        {
            EnsureAvailable(data, recordOffset, recordSize);
            var pixelsPerEm = data[recordOffset];
            if (pixelsPerEm > 0 && 2 + numGlyphs <= recordSize)
            {
                var widths = new Dictionary<int, ushort>(codePointToGlyph.Count);
                missingWidthsByPpem[pixelsPerEm] = data[recordOffset + 2];
                foreach (var item in codePointToGlyph)
                {
                    if (item.Value < numGlyphs)
                    {
                        widths[item.Key] = data[recordOffset + 2 + item.Value];
                    }
                }

                widthsByPpem[pixelsPerEm] = widths;
            }

            recordOffset += recordSize;
        }

        return new HintedAdvanceWidthTable(widthsByPpem, missingWidthsByPpem);
    }

    private static void ReadKerningFormat0(
        byte[] data,
        int offset,
        IDictionary<FontKerningPair, short> result,
        IReadOnlyDictionary<ushort, int> glyphToCodePoint)
    {
        EnsureAvailable(data, offset, 8);
        var pairCount = ReadUInt16(data, offset);
        var pairOffset = offset + 8;
        for (var i = 0; i < pairCount; i++)
        {
            EnsureAvailable(data, pairOffset, 6);
            var leftGlyph = ReadUInt16(data, pairOffset);
            var rightGlyph = ReadUInt16(data, pairOffset + 2);
            var value = ReadInt16(data, pairOffset + 4);
            if (glyphToCodePoint.TryGetValue(leftGlyph, out var left) && glyphToCodePoint.TryGetValue(rightGlyph, out var right))
            {
                result[new FontKerningPair(left, right)] = value;
            }

            pairOffset += 6;
        }
    }

    private static TableRecord RequireTable(IReadOnlyDictionary<string, TableRecord> tables, string tag)
        => tables.TryGetValue(tag, out var table) ? table : throw new InvalidDataException($"TrueType font is missing required '{tag}' table.");

    private static ushort ReadUInt16(byte[] data, int offset)
    {
        EnsureAvailable(data, offset, 2);
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static short ReadInt16(byte[] data, int offset)
        => unchecked((short)ReadUInt16(data, offset));

    private static uint ReadUInt32(byte[] data, int offset)
    {
        EnsureAvailable(data, offset, 4);
        return ((uint)data[offset] << 24)
            | ((uint)data[offset + 1] << 16)
            | ((uint)data[offset + 2] << 8)
            | data[offset + 3];
    }

    private static void EnsureAvailable(byte[] data, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > data.Length)
        {
            throw new InvalidDataException("TrueType table data is truncated.");
        }
    }

    private readonly record struct TableRecord(int Offset, int Length);

    private sealed record HintedAdvanceWidthTable(
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, ushort>> AdvanceWidths,
        IReadOnlyDictionary<int, ushort> MissingGlyphAdvanceWidths);
}
