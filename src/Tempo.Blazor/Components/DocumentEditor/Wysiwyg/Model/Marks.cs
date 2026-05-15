namespace Tempo.Blazor.Components.DocumentEditor.Wysiwyg.Model;

/// <summary>Abstract base for formatting marks.</summary>
public abstract class Mark
{
    /// <summary>Mark type discriminator.</summary>
    public abstract string Type { get; }
}

/// <summary>Bold mark.</summary>
public class BoldMark : Mark
{
    /// <inheritdoc />
    public override string Type => "bold";
}

/// <summary>Italic mark.</summary>
public class ItalicMark : Mark
{
    /// <inheritdoc />
    public override string Type => "italic";
}

/// <summary>Underline mark.</summary>
public class UnderlineMark : Mark
{
    /// <inheritdoc />
    public override string Type => "underline";
}

/// <summary>Strikethrough mark.</summary>
public class StrikethroughMark : Mark
{
    /// <inheritdoc />
    public override string Type => "strikethrough";
}

/// <summary>Subscript mark.</summary>
public class SubscriptMark : Mark
{
    /// <inheritdoc />
    public override string Type => "subscript";
}

/// <summary>Superscript mark.</summary>
public class SuperscriptMark : Mark
{
    /// <inheritdoc />
    public override string Type => "superscript";
}

/// <summary>Font family and size mark.</summary>
public class FontMark : Mark
{
    /// <inheritdoc />
    public override string Type => "font";

    /// <summary>Font family name.</summary>
    public string Family { get; set; } = "Calibri";

    /// <summary>Font size (CSS value, e.g. "11pt").</summary>
    public string Size { get; set; } = "11pt";
}

/// <summary>Text color mark.</summary>
public class ColorMark : Mark
{
    /// <inheritdoc />
    public override string Type => "color";

    /// <summary>Color in hex format (e.g. "#000000").</summary>
    public string Color { get; set; } = "#000000";
}

/// <summary>Background highlight color mark.</summary>
public class HighlightMark : Mark
{
    /// <inheritdoc />
    public override string Type => "highlight";

    /// <summary>Highlight color name or hex.</summary>
    public string Color { get; set; } = "yellow";
}

/// <summary>Hyperlink mark.</summary>
public class LinkMark : Mark
{
    /// <inheritdoc />
    public override string Type => "link";

    /// <summary>Link URL.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>Optional link title.</summary>
    public string? Title { get; set; }
}
