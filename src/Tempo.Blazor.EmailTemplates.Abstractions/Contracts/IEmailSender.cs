namespace Tempo.Blazor.EmailTemplates.Abstractions.Contracts;

/// <summary>Delivery contract implemented by the host application (e.g. an SMTP sender).</summary>
public interface IEmailSender
{
    /// <summary>Sends the given message.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
