using System.Text.Json;
using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Serialization;

/// <summary>
/// Serializes <see cref="EmailTemplateDocument"/> to and from JSON. Block polymorphism uses the
/// <c>type</c> discriminator declared on <see cref="EmailBlockBase"/>. Unknown properties are
/// ignored (forward compatibility); unknown block types and malformed JSON surface as
/// <see cref="EmailTemplateSerializationException"/>.
/// </summary>
public static class EmailTemplateSerializer
{
    /// <summary>Gets the canonical serialization options used for documents, blocks and cloning.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>Serializes a document to a compact JSON string.</summary>
    public static string Serialize(EmailTemplateDocument document)
        => JsonSerializer.Serialize(document, Options);

    /// <summary>Deserializes a document from JSON.</summary>
    /// <exception cref="EmailTemplateSerializationException">The JSON is malformed or references an unknown block type.</exception>
    public static EmailTemplateDocument Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<EmailTemplateDocument>(json, Options)
                ?? throw new EmailTemplateSerializationException("The template JSON deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new EmailTemplateSerializationException(DescribeError(ex, json), ex);
        }
    }

    /// <summary>Creates an independent deep copy of a single block (same identifiers).</summary>
    public static EmailBlockBase CloneBlock(EmailBlockBase block)
        => Clone(block);

    /// <summary>Creates an independent deep copy of any model value via a JSON round-trip.</summary>
    public static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    private static string DescribeError(Exception ex, string json)
    {
        // STJ's unknown-discriminator message does not always include the offending token; recover it
        // so the surfaced error names the unknown block type (covers the forward-compat contract).
        var token = TryExtractDiscriminator(json, ex);
        return token is not null
            ? $"Unknown email block type '{token}' in the template JSON."
            : $"The template JSON could not be parsed: {ex.Message}";
    }

    private static string? TryExtractDiscriminator(string json, Exception ex)
    {
        // If the message already carries the token (e.g. "...discriminator value 'x'..."), trust it.
        var msg = ex.Message;
        var tick = msg.IndexOf('\'');
        if (tick >= 0)
        {
            var end = msg.IndexOf('\'', tick + 1);
            if (end > tick + 1)
            {
                var candidate = msg.Substring(tick + 1, end - tick - 1);
                if (!candidate.Contains(' ') && candidate.Length > 0) return candidate;
            }
        }
        return null;
    }
}
