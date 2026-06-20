namespace Tempo.Blazor.EmailTemplates.Abstractions.Serialization;

/// <summary>
/// Thrown when an email template document cannot be (de)serialized — for example malformed JSON or
/// an unknown block type discriminator. Carries a clear, surfaced message instead of crashing.
/// </summary>
public sealed class EmailTemplateSerializationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="EmailTemplateSerializationException"/> class.</summary>
    public EmailTemplateSerializationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
