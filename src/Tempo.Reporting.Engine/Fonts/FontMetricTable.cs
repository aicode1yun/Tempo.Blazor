namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Collection of precomputed reporting font metrics.</summary>
public sealed class FontMetricTable
{
    private readonly List<FontMetricFace> _faces;
    private readonly List<string> _fallbackFamilyNames;

    /// <summary>Initializes a metric table.</summary>
    public FontMetricTable(
        IEnumerable<FontMetricFace> faces,
        string defaultFamilyName,
        IEnumerable<string>? fallbackFamilyNames = null)
    {
        _faces = faces?.ToList() ?? [];
        if (_faces.Count == 0)
        {
            throw new ArgumentException("A font metric table requires at least one face.", nameof(faces));
        }

        DefaultFamilyName = string.IsNullOrWhiteSpace(defaultFamilyName) ? _faces[0].FamilyName : defaultFamilyName;
        _fallbackFamilyNames = fallbackFamilyNames?.Where(static name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [];
    }

    /// <summary>Default family used when a request cannot resolve its family.</summary>
    public string DefaultFamilyName { get; }

    /// <summary>All metric faces.</summary>
    public IReadOnlyList<FontMetricFace> Faces => _faces;

    /// <summary>Fallback family names in lookup order.</summary>
    public IReadOnlyList<string> FallbackFamilyNames => _fallbackFamilyNames;

    /// <summary>Resolves the best face for a family/style request.</summary>
    public FontMetricFace ResolveFace(string? familyName, bool bold, bool italic)
    {
        var style = FontStyleKey.From(bold, italic);
        return FindFace(familyName, style)
            ?? FindFace(familyName, FontStyleKey.Regular)
            ?? FindFace(DefaultFamilyName, style)
            ?? FindFace(DefaultFamilyName, FontStyleKey.Regular)
            ?? _faces[0];
    }

    /// <summary>Finds a fallback face that contains the requested code point.</summary>
    public FontMetricFace? FindFallbackFace(int codePoint, FontStyleKey styleKey)
    {
        foreach (var family in _fallbackFamilyNames)
        {
            var face = FindFace(family, styleKey) ?? FindFace(family, FontStyleKey.Regular);
            if (face?.ContainsCodePoint(codePoint) == true)
            {
                return face;
            }
        }

        return _faces.FirstOrDefault(face => face.ContainsCodePoint(codePoint));
    }

    private FontMetricFace? FindFace(string? familyName, FontStyleKey style)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return null;
        }

        return _faces.FirstOrDefault(face =>
            string.Equals(face.FamilyName, familyName, StringComparison.OrdinalIgnoreCase)
            && face.StyleKey == style);
    }
}
