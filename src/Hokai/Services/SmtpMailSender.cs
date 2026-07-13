using System.Net;
using System.Net.Mail;
using Hokai.Models;

namespace Hokai.Services;

public sealed class SmtpMailSender : ISmtpMailSender
{
    private readonly SmtpSettings _settings;
    private readonly ISmtpClientFactory _clientFactory;

    public SmtpMailSender(SmtpSettings settings)
        : this(settings, new SmtpClientFactory())
    {
    }

    internal SmtpMailSender(SmtpSettings settings, ISmtpClientFactory clientFactory)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // SmtpClient rejects concurrent sends, so each endpoint notification owns a short-lived client.
        using var client = _clientFactory.Create(_settings);
        await client.SendMailAsync(message, cancellationToken);
    }
}

internal interface ISmtpClientFactory
{
    ISmtpClient Create(SmtpSettings settings);
}

internal interface ISmtpClient : IDisposable
{
    Task SendMailAsync(MailMessage message, CancellationToken cancellationToken);
}

internal sealed class SmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create(SmtpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        return new SmtpClientAdapter(client);
    }
}

internal sealed class SmtpClientAdapter(SmtpClient client) : ISmtpClient
{
    internal SmtpClient Client => client;

    public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken) =>
        client.SendMailAsync(message, cancellationToken);

    public void Dispose() => client.Dispose();
}
