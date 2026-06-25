namespace Tempo.Reporting.Engine.Fonts;

/// <summary>Normalized font style selector used by reporting metric tables.</summary>
public readonly record struct FontStyleKey(int Weight, bool Italic)
{
    /// <summary>Regular 400 normal style.</summary>
    public static FontStyleKey Regular { get; } = new(400, false);

    /// <summary>Bold 700 normal style.</summary>
    public static FontStyleKey Bold { get; } = new(700, false);

    /// <summary>Creates a style key from UI booleans.</summary>
    public static FontStyleKey From(bool bold, bool italic)
        => new(bold ? 700 : 400, italic);
}
