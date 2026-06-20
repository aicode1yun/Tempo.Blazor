namespace Tempo.Blazor.EmailTemplates.Abstractions.Model;

/// <summary>An imported web font (<c>mj-font</c>).</summary>
public sealed class EmailFont
{
    /// <summary>Gets or sets the font family name (<c>name</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the stylesheet URL providing the font (<c>href</c>).</summary>
    public string Href { get; set; } = string.Empty;
}
