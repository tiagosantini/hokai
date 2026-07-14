namespace Hokai.Models;

public sealed class AppSettings
{
    public SmtpSettings Smtp { get; set; } = new();
    public string DataDirectory { get; set; } = "Data";
    public int RetentionDays { get; set; } = 30;
}
