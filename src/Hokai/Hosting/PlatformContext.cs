namespace Hokai.Hosting;

public sealed class PlatformContext
{
    public string ExecutablePath { get; init; } = "";
    public string UserName { get; init; } = "";
    public string HomeDirectory { get; init; } = "";
    public string SudoUserName { get; init; } = "";
    public bool IsElevated { get; init; }

    public PlatformContext() { }

    public static PlatformContext Detect()
    {
        return new PlatformContext
        {
            ExecutablePath = Environment.ProcessPath ?? "hokai",
            UserName = Environment.UserName,
            HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            SudoUserName = Environment.GetEnvironmentVariable("SUDO_USER") ?? "",
            IsElevated = Environment.UserName == "root"
        };
    }
}
