using Hokai.Models;
using Microsoft.Extensions.Logging;
using System.Net.Mail;

namespace Hokai.Services;

public sealed class NotificationService(
    ISmtpMailSender sender,
    SmtpSettings settings,
    ILogger<NotificationService> logger) : INotificationService
{
    public Task NotifyDownAsync(
        EndpointConfig endpoint,
        CheckResult result,
        CancellationToken cancellationToken = default) =>
        SendAsync(endpoint, result, isRecovery: false, cancellationToken);

    public Task NotifyRecoveryAsync(
        EndpointConfig endpoint,
        CheckResult result,
        CancellationToken cancellationToken = default) =>
        SendAsync(endpoint, result, isRecovery: true, cancellationToken);

    private async Task SendAsync(
        EndpointConfig endpoint,
        CheckResult result,
        bool isRecovery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        if (settings.ToAddresses.Length == 0)
        {
            logger.LogWarning("Notification skipped because no SMTP recipients are configured.");
            return;
        }

        try
        {
            using var message = CreateMessage(endpoint, result, isRecovery);
            await sender.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Delivery is best-effort; advancing monitor state prevents an alert storm on each check.
            logger.LogError(exception, "Failed to send notification for endpoint {EndpointId}.", endpoint.Id);
        }
    }

    private MailMessage CreateMessage(
        EndpointConfig endpoint,
        CheckResult result,
        bool isRecovery)
    {
        var state = isRecovery ? "UP" : "DOWN";
        var prefix = isRecovery ? "[HOKAI RECOVERY]" : "[HOKAI ALERT]";
        var lines = new List<string>
        {
            $"Endpoint ID: {endpoint.Id}",
            $"URL: {endpoint.Url}",
            $"Timestamp: {result.Timestamp:O}",
            $"Expected status: {endpoint.ExpectedStatus}",
            $"Actual status: {result.StatusCode?.ToString() ?? "Unavailable"}",
            $"Response time: {result.ResponseTimeMs} ms"
        };

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            lines.Add($"Error: {result.Error}");
        }

        var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress),
            Subject = $"{prefix} {endpoint.Url} is {state}",
            Body = string.Join(Environment.NewLine, lines),
            IsBodyHtml = false
        };

        foreach (var recipient in settings.ToAddresses)
        {
            message.To.Add(new MailAddress(recipient));
        }

        return message;
    }
}
