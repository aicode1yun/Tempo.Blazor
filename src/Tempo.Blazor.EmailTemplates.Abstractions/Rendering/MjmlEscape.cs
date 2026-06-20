using System.Text;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Rendering;

/// <summary>XML escaping helpers for safely emitting attribute values and text into MJML markup.</summary>
internal static class MjmlEscape
{
    /// <summary>Escapes a value for use inside a double-quoted XML attribute.</summary>
    public static string Attribute(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Escapes text content (used for table cells and other plain text in markup).</summary>
    public static string Text(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
