using System.Net;
using System.Net.Mail;
using Hokai.Models;
using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class SmtpMailSenderTests
{
    [Fact]
    public void Constructor_ValidSettings_CreatesSender()
    {
        var sender = new SmtpMailSender(CreateSettings());

        Assert.NotNull(sender);
    }

    [Fact]
    public void Constructor_NullSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SmtpMailSender(null!));
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SmtpMailSender(CreateSettings(), null!));
    }

    [Fact]
    public void ClientFactory_Settings_ConfiguresSmtpClient()
    {
        var settings = CreateSettings(username: "user", password: "password");
        var factory = new SmtpClientFactory();

        using var adapter = Assert.IsType<SmtpClientAdapter>(factory.Create(settings));

        Assert.Equal("smtp.example.com", adapter.Client.Host);
        Assert.Equal(587, adapter.Client.Port);
        Assert.True(adapter.Client.EnableSsl);
        var credentials = Assert.IsType<NetworkCredential>(adapter.Client.Credentials);
        Assert.Equal("user", credentials.UserName);
        Assert.Equal("password", credentials.Password);
    }

    [Fact]
    public void ClientFactory_EmptyUsername_DoesNotSetCredentials()
    {
        var factory = new SmtpClientFactory();

        using var adapter = Assert.IsType<SmtpClientAdapter>(factory.Create(CreateSettings()));

        Assert.Null(adapter.Client.Credentials);
    }

    [Fact]
    public async Task SendAsync_Message_ForwardsMessageAndCancellation()
    {
        var client = new RecordingSmtpClient();
        var sender = new SmtpMailSender(CreateSettings(), new RecordingFactory(client));
        using var message = new MailMessage("from@example.com", "to@example.com");
        using var cancellation = new CancellationTokenSource();

        await sender.SendAsync(message, cancellation.Token);

        Assert.Same(message, client.Message);
        Assert.Equal(cancellation.Token, client.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_EachSend_CreatesAndDisposesSeparateClient()
    {
        var first = new RecordingSmtpClient();
        var second = new RecordingSmtpClient();
        var factory = new RecordingFactory(first, second);
        var sender = new SmtpMailSender(CreateSettings(), factory);
        using var message = new MailMessage("from@example.com", "to@example.com");

        await sender.SendAsync(message);
        await sender.SendAsync(message);

        Assert.Equal(2, factory.CreateCount);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
    }

    [Fact]
    public async Task ClientAdapter_CanceledSend_PropagatesCancellation()
    {
        using var adapter = new SmtpClientAdapter(new SmtpClient("localhost", 25));
        using var message = new MailMessage("from@example.com", "to@example.com");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.SendMailAsync(message, cancellation.Token));
    }

    private static SmtpSettings CreateSettings(
        string username = "",
        string password = "") => new()
    {
        Host = "smtp.example.com",
        Port = 587,
        UseSsl = true,
        Username = username,
        Password = password,
        FromAddress = "from@example.com",
        ToAddresses = ["to@example.com"]
    };

    private sealed class RecordingFactory(params RecordingSmtpClient[] clients) : ISmtpClientFactory
    {
        public int CreateCount { get; private set; }

        public ISmtpClient Create(SmtpSettings settings) => clients[CreateCount++];
    }

    private sealed class RecordingSmtpClient : ISmtpClient
    {
        public MailMessage? Message { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public bool IsDisposed { get; private set; }

        public Task SendMailAsync(MailMessage message, CancellationToken cancellationToken)
        {
            Message = message;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }
}
