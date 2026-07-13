using System.Net.Mail;

namespace Hokai.Services;

/// <summary>Sends a prepared email through the configured SMTP transport.</summary>
public interface ISmtpMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
