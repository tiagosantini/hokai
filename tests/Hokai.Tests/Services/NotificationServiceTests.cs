using System.Net.Mail;
using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.Logging;

namespace Hokai.Tests.Services;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task NotifyDownAsync_Result_BuildsPlainTextAlert()
    {
        var sender = new RecordingSender();
        var service = CreateService(sender);

        await service.NotifyDownAsync(CreateEndpoint(), CreateResult(isUp: false));

        Assert.Equal("[HOKAI ALERT] https://example.com/health is DOWN", sender.Subject);
        Assert.Contains("Endpoint ID: endpoint", sender.Body);
        Assert.Contains("Expected status: 200", sender.Body);
        Assert.Contains("Actual status: Unavailable", sender.Body);
        Assert.Contains("Error: Connection refused", sender.Body);
        Assert.False(sender.IsBodyHtml);
    }

    [Fact]
    public async Task NotifyRecoveryAsync_Result_BuildsRecoveryMessage()
    {
        var sender = new RecordingSender();
        var service = CreateService(sender);

        await service.NotifyRecoveryAsync(CreateEndpoint(), CreateResult(isUp: true));

        Assert.Equal("[HOKAI RECOVERY] https://example.com/health is UP", sender.Subject);
        Assert.Contains("Actual status: 200", sender.Body);
        Assert.Contains("Response time: 25 ms", sender.Body);
        Assert.DoesNotContain("Error:", sender.Body);
    }

    [Fact]
    public async Task NotifyDownAsync_ConfiguredAddresses_UsesSenderAndEveryRecipient()
    {
        var sender = new RecordingSender();
        var service = CreateService(sender);

        await service.NotifyDownAsync(CreateEndpoint(), CreateResult(isUp: false));

        Assert.Equal("from@example.com", sender.From);
        Assert.Equal(["one@example.com", "two@example.com"], sender.To);
    }

    [Fact]
    public async Task NotifyDownAsync_EmptyRecipients_SkipsSendAndLogsWarning()
    {
        var sender = new RecordingSender();
        var logger = new RecordingLogger<NotificationService>();
        var service = CreateService(sender, logger, []);

        await service.NotifyDownAsync(CreateEndpoint(), CreateResult(isUp: false));

        Assert.Equal(0, sender.SendCount);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task NotifyDownAsync_SenderFailure_LogsWithoutThrowing()
    {
        var sender = new RecordingSender { Exception = new SmtpException("Unavailable") };
        var logger = new RecordingLogger<NotificationService>();
        var service = CreateService(sender, logger);

        await service.NotifyDownAsync(CreateEndpoint(), CreateResult(isUp: false));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task NotifyDownAsync_CallerCancellation_Propagates()
    {
        var sender = new RecordingSender { Exception = new OperationCanceledException() };
        var service = CreateService(sender);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.NotifyDownAsync(CreateEndpoint(), CreateResult(isUp: false), cancellation.Token));
    }

    private static NotificationService CreateService(
        RecordingSender sender,
        RecordingLogger<NotificationService>? logger = null,
        string[]? recipients = null) => new(
            sender,
            new SmtpSettings
            {
                FromAddress = "from@example.com",
                ToAddresses = recipients ?? ["one@example.com", "two@example.com"]
            },
            logger ?? new RecordingLogger<NotificationService>());

    private static EndpointConfig CreateEndpoint() => new()
    {
        Id = "endpoint",
        Url = new Uri("https://example.com/health"),
        Interval = TimeSpan.FromMinutes(1),
        Timeout = TimeSpan.FromSeconds(30),
        Method = "GET",
        ExpectedStatus = 200,
        CreatedAt = DateTimeOffset.UnixEpoch
    };

    private static CheckResult CreateResult(bool isUp) => new()
    {
        EndpointId = "endpoint",
        Timestamp = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero),
        IsUp = isUp,
        StatusCode = isUp ? 200 : null,
        ResponseTimeMs = 25,
        Error = isUp ? null : "Connection refused"
    };

    private sealed class RecordingSender : ISmtpMailSender
    {
        public int SendCount { get; private set; }
        public string? Subject { get; private set; }
        public string? Body { get; private set; }
        public string? From { get; private set; }
        public string[] To { get; private set; } = [];
        public bool IsBodyHtml { get; private set; }
        public Exception? Exception { get; init; }

        public Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
        {
            SendCount++;
            Subject = message.Subject;
            Body = message.Body;
            From = message.From?.Address;
            To = message.To.Select(address => address.Address).ToArray();
            IsBodyHtml = message.IsBodyHtml;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
