namespace Hokai.Models;

public sealed class AppSettings
{
    public SmtpSettings Smtp { get; init; } = new();

    public string DataDirectory { get; init; } = "Data";

    public int RetentionDays { get; init; } = 30;
}
