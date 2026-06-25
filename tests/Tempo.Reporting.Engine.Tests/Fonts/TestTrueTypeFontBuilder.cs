using System.Text;

namespace Tempo.Reporting.Engine.Tests.Fonts;

internal static class TestTrueTypeFontBuilder
{
    public static byte[] BuildMinimalFont(
        ushort unitsPerEm,
        short ascent,
        short descent,
        short lineGap,
        IReadOnlyDictionary<int, ushort> advances,
        IReadOnlyDictionary<(int Left, int Right), short> kerning,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, byte>>? hintedAdvances = null)
    {
        var orderedCodePoints = advances.Keys.OrderBy(static codePoint => codePoint).ToArray();
        var glyphByCodePoint = orderedCodePoints
            .Select((codePoint, index) => new { codePoint, glyph = (ushort)(index + 1) })
            .ToDictionary(static item => item.codePoint, static item => item.glyph);
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cmap"] = BuildCmapFormat12(orderedCodePoints, glyphByCodePoint),
            ["head"] = BuildHead(unitsPerEm),
            ["hhea"] = BuildHhea(ascent, descent, lineGap, (ushort)(orderedCodePoints.Length + 1)),
            ["hmtx"] = BuildHmtx(orderedCodePoints, advances),
            ["kern"] = BuildKern(glyphByCodePoint, kerning),
            ["maxp"] = BuildMaxp((ushort)(orderedCodePoints.Length + 1))
        };
        if (hintedAdvances is not null)
        {
            tables["hdmx"] = BuildHdmx(orderedCodePoints, glyphByCodePoint, hintedAdvances);
        }

        var directoryLength = 12 + tables.Count * 16;
        var offset = Align4(directoryLength);
        var records = new List<(string Tag, int Offset, int Length)>(tables.Count);
        foreach (var table in tables)
        {
            records.Add((table.Key, offset, table.Value.Length));
            offset = Align4(offset + table.Value.Length);
        }

        using var output = new MemoryStream(new byte[offset], writable: true);
        WriteUInt32(output, 0x00010000);
        WriteUInt16(output, (ushort)tables.Count);
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);

        foreach (var record in records)
        {
            WriteTag(output, record.Tag);
            WriteUInt32(output, 0);
            WriteUInt32(output, (uint)record.Offset);
            WriteUInt32(output, (uint)record.Length);
        }

        foreach (var record in records)
        {
            output.Position = record.Offset;
            output.Write(tables[record.Tag], 0, tables[record.Tag].Length);
        }

        return output.ToArray();
    }

    private static byte[] BuildHead(ushort unitsPerEm)
    {
        using var stream = new MemoryStream(new byte[54], writable: true);
        stream.Position = 18;
        WriteUInt16(stream, unitsPerEm);
        return stream.ToArray();
    }

    private static byte[] BuildHhea(short ascent, short descent, short lineGap, ushort metricCount)
    {
        using var stream = new MemoryStream(new byte[36], writable: true);
        stream.Position = 4;
        WriteInt16(stream, ascent);
        WriteInt16(stream, descent);
        WriteInt16(stream, lineGap);
        stream.Position = 34;
        WriteUInt16(stream, metricCount);
        return stream.ToArray();
    }

    private static byte[] BuildMaxp(ushort glyphCount)
    {
        using var stream = new MemoryStream();
        WriteUInt32(stream, 0x00010000);
        WriteUInt16(stream, glyphCount);
        return stream.ToArray();
    }

    private static byte[] BuildHmtx(IReadOnlyList<int> orderedCodePoints, IReadOnlyDictionary<int, ushort> advances)
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, 500);
        WriteInt16(stream, 0);
        foreach (var codePoint in orderedCodePoints)
        {
            WriteUInt16(stream, advances[codePoint]);
            WriteInt16(stream, 0);
        }

        return stream.ToArray();
    }

    private static byte[] BuildCmapFormat12(
        IReadOnlyList<int> orderedCodePoints,
        IReadOnlyDictionary<int, ushort> glyphByCodePoint)
    {
        using var subtable = new MemoryStream();
        WriteUInt16(subtable, 12);
        WriteUInt16(subtable, 0);
        WriteUInt32(subtable, (uint)(16 + orderedCodePoints.Count * 12));
        WriteUInt32(subtable, 0);
        WriteUInt32(subtable, (uint)orderedCodePoints.Count);
        foreach (var codePoint in orderedCodePoints)
        {
            WriteUInt32(subtable, (uint)codePoint);
            WriteUInt32(subtable, (uint)codePoint);
            WriteUInt32(subtable, glyphByCodePoint[codePoint]);
        }

        using var cmap = new MemoryStream();
        WriteUInt16(cmap, 0);
        WriteUInt16(cmap, 1);
        WriteUInt16(cmap, 3);
        WriteUInt16(cmap, 10);
        WriteUInt32(cmap, 12);
        cmap.Write(subtable.ToArray(), 0, (int)subtable.Length);
        return cmap.ToArray();
    }

    private static byte[] BuildKern(
        IReadOnlyDictionary<int, ushort> glyphByCodePoint,
        IReadOnlyDictionary<(int Left, int Right), short> kerning)
    {
        using var pairs = new MemoryStream();
        foreach (var item in kerning.OrderBy(static item => item.Key.Left).ThenBy(static item => item.Key.Right))
        {
            WriteUInt16(pairs, glyphByCodePoint[item.Key.Left]);
            WriteUInt16(pairs, glyphByCodePoint[item.Key.Right]);
            WriteInt16(pairs, item.Value);
        }

        var subtableLength = 14 + (int)pairs.Length;
        using var stream = new MemoryStream();
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, (ushort)subtableLength);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, (ushort)kerning.Count);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        stream.Write(pairs.ToArray(), 0, (int)pairs.Length);
        return stream.ToArray();
    }

    private static byte[] BuildHdmx(
        IReadOnlyList<int> orderedCodePoints,
        IReadOnlyDictionary<int, ushort> glyphByCodePoint,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, byte>> hintedAdvances)
    {
        var glyphCount = orderedCodePoints.Count + 1;
        var recordSize = Align4(2 + glyphCount);
        using var stream = new MemoryStream();
        WriteUInt16(stream, 0);
        WriteUInt16(stream, (ushort)hintedAdvances.Count);
        WriteUInt32(stream, (uint)recordSize);
        foreach (var record in hintedAdvances.OrderBy(static item => item.Key))
        {
            var bytes = new byte[recordSize];
            bytes[0] = (byte)record.Key;
            foreach (var codePoint in orderedCodePoints)
            {
                if (record.Value.TryGetValue(codePoint, out var width))
                {
                    bytes[2 + glyphByCodePoint[codePoint]] = width;
                    bytes[1] = Math.Max(bytes[1], width);
                }
            }

            if (record.Value.TryGetValue(0, out var missingWidth))
            {
                bytes[2] = missingWidth;
                bytes[1] = Math.Max(bytes[1], missingWidth);
            }

            stream.Write(bytes, 0, bytes.Length);
        }

        return stream.ToArray();
    }

    private static int Align4(int value)
        => (value + 3) & ~3;

    private static void WriteTag(Stream stream, string tag)
    {
        var bytes = Encoding.ASCII.GetBytes(tag);
        stream.Write(bytes, 0, 4);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteInt16(Stream stream, short value)
        => WriteUInt16(stream, unchecked((ushort)value));

    private static void WriteUInt32(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
