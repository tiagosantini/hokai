namespace Hokai.Models;

public sealed class AppSettings
{
    public SmtpSettings Smtp { get; init; } = new();

    public string DataDirectory { get; init; } = "Data";

    public int RetentionDays { get; init; } = 30;
}

public sealed class SmtpSettings
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 25;

    public bool UseSsl { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = "hokai@localhost";

    public string[] ToAddresses { get; init; } = [];
}
