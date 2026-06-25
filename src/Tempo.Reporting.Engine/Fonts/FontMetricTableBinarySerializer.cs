using System.Text;

namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Compact binary serializer for reporting font metric tables.</summary>
public static class FontMetricTableBinarySerializer
{
    private const int Magic = 0x46524d54;
    private const int Version = 2;

    /// <summary>Writes a metric table to a compact binary stream.</summary>
    public static void Write(FontMetricTable table, Stream output)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(output);

        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(table.DefaultFamilyName);
        writer.Write(table.FallbackFamilyNames.Count);
        foreach (var fallback in table.FallbackFamilyNames)
        {
            writer.Write(fallback);
        }

        writer.Write(table.Faces.Count);
        foreach (var face in table.Faces)
        {
            writer.Write(face.FamilyName);
            writer.Write(face.StyleKey.Weight);
            writer.Write(face.StyleKey.Italic);
            writer.Write(face.UnitsPerEm);
            writer.Write(face.Ascent);
            writer.Write(face.Descent);
            writer.Write(face.LineGap);
            writer.Write(face.MissingGlyphAdvanceWidth);

            writer.Write(face.AdvanceWidths.Count);
            foreach (var item in face.AdvanceWidths.OrderBy(static item => item.Key))
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }

            writer.Write(face.KerningPairs.Count);
            foreach (var item in face.KerningPairs.OrderBy(static item => item.Key.LeftCodePoint).ThenBy(static item => item.Key.RightCodePoint))
            {
                writer.Write(item.Key.LeftCodePoint);
                writer.Write(item.Key.RightCodePoint);
                writer.Write(item.Value);
            }

            writer.Write(face.HintedAdvanceWidths.Count);
            foreach (var hintedFace in face.HintedAdvanceWidths.OrderBy(static item => item.Key))
            {
                writer.Write(hintedFace.Key);
                writer.Write(hintedFace.Value.Count);
                foreach (var item in hintedFace.Value.OrderBy(static item => item.Key))
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }
            }

            writer.Write(face.MissingGlyphHintedAdvanceWidths.Count);
            foreach (var item in face.MissingGlyphHintedAdvanceWidths.OrderBy(static item => item.Key))
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }
        }
    }

    /// <summary>Reads a metric table from a compact binary stream.</summary>
    public static FontMetricTable Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException("The stream is not a Tempo reporting font metric table.");
        }

        var version = reader.ReadInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported Tempo reporting font metric table version {version}.");
        }

        var defaultFamily = reader.ReadString();
        var fallbackCount = reader.ReadInt32();
        var fallbacks = new List<string>(fallbackCount);
        for (var i = 0; i < fallbackCount; i++)
        {
            fallbacks.Add(reader.ReadString());
        }

        var faceCount = reader.ReadInt32();
        var faces = new List<FontMetricFace>(faceCount);
        for (var faceIndex = 0; faceIndex < faceCount; faceIndex++)
        {
            var family = reader.ReadString();
            var style = new FontStyleKey(reader.ReadInt32(), reader.ReadBoolean());
            var unitsPerEm = reader.ReadInt32();
            var ascent = reader.ReadInt16();
            var descent = reader.ReadInt16();
            var lineGap = reader.ReadInt16();
            var missing = reader.ReadUInt16();

            var advanceCount = reader.ReadInt32();
            var advances = new Dictionary<int, ushort>(advanceCount);
            for (var i = 0; i < advanceCount; i++)
            {
                advances[reader.ReadInt32()] = reader.ReadUInt16();
            }

            var kerningCount = reader.ReadInt32();
            var kerning = new Dictionary<FontKerningPair, short>(kerningCount);
            for (var i = 0; i < kerningCount; i++)
            {
                var left = reader.ReadInt32();
                var right = reader.ReadInt32();
                kerning[new FontKerningPair(left, right)] = reader.ReadInt16();
            }

            var hintedFaceCount = reader.ReadInt32();
            var hintedAdvances = new Dictionary<int, IReadOnlyDictionary<int, ushort>>(hintedFaceCount);
            for (var i = 0; i < hintedFaceCount; i++)
            {
                var pixelsPerEm = reader.ReadInt32();
                var hintedAdvanceCount = reader.ReadInt32();
                var widths = new Dictionary<int, ushort>(hintedAdvanceCount);
                for (var widthIndex = 0; widthIndex < hintedAdvanceCount; widthIndex++)
                {
                    widths[reader.ReadInt32()] = reader.ReadUInt16();
                }

                hintedAdvances[pixelsPerEm] = widths;
            }

            var missingHintedCount = reader.ReadInt32();
            var missingHintedAdvances = new Dictionary<int, ushort>(missingHintedCount);
            for (var i = 0; i < missingHintedCount; i++)
            {
                missingHintedAdvances[reader.ReadInt32()] = reader.ReadUInt16();
            }

            faces.Add(new FontMetricFace(family, style, unitsPerEm, ascent, descent, lineGap, missing, advances, kerning, hintedAdvances, missingHintedAdvances));
        }

        return new FontMetricTable(faces, defaultFamily, fallbacks);
    }
}
