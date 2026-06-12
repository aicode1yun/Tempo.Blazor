using MailKit.Security;
using MimeKit;

namespace Tempo.Blazor.Demo.Api.Services;

/// <summary>Creates real MailKit-backed SMTP client wrappers.</summary>
public sealed class MailKitSmtpClientFactory : ISmtpClientFactory
{
    /// <inheritdoc />
    public ISmtpClientWrapper Create() => new MailKitSmtpClientWrapper();

    private sealed class MailKitSmtpClientWrapper : ISmtpClientWrapper
    {
        private readonly MailKit.Net.Smtp.SmtpClient _client = new();

        public Task ConnectAsync(string host, int port, SecureSocketOptions security, CancellationToken cancellationToken)
            => _client.ConnectAsync(host, port, security, cancellationToken);

        public Task AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
            => _client.AuthenticateAsync(username, password, cancellationToken);

        public Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
            => _client.SendAsync(message, cancellationToken);

        public Task DisconnectAsync(bool quit, CancellationToken cancellationToken)
            => _client.DisconnectAsync(quit, cancellationToken);

        public void Dispose() => _client.Dispose();
    }
}
