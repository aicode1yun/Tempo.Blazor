using MailKit.Security;
using MimeKit;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Thin abstraction over a MailKit SMTP client for testability.</summary>
public interface ISmtpClientWrapper : IDisposable
{
    /// <summary>Connects to the SMTP server.</summary>
    Task ConnectAsync(string host, int port, SecureSocketOptions security, CancellationToken cancellationToken);

    /// <summary>Authenticates with the server.</summary>
    Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>Sends a message.</summary>
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);

    /// <summary>Disconnects from the server.</summary>
    Task DisconnectAsync(bool quit, CancellationToken cancellationToken);
}

/// <summary>Creates fresh SMTP client wrappers (a connection is not reused across sends).</summary>
public interface ISmtpClientFactory
{
    /// <summary>Creates a new client wrapper.</summary>
    ISmtpClientWrapper Create();
}
