using System.Text.Json;
using Hokai.Models;

namespace Hokai.Tests.Scaffold;

public sealed class SolutionScaffoldTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void SolutionFile_Exists()
    {
        var solutionPath = Path.Combine(RepositoryRoot, "hokai.slnx");

        var exists = File.Exists(solutionPath);

        Assert.True(exists, $"Expected solution file at {solutionPath}");
    }

    [Fact]
    public void MainProject_Exists()
    {
        var projectPath = Path.Combine(RepositoryRoot, "src", "Hokai", "Hokai.csproj");

        var exists = File.Exists(projectPath);

        Assert.True(exists, $"Expected main project at {projectPath}");
    }

    [Fact]
    public void Configuration_IsDeserializable()
    {
        var configurationPath = Path.Combine(RepositoryRoot, "src", "Hokai", "appsettings.json");
        var json = File.ReadAllText(configurationPath);

        var settings = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(settings);
        Assert.Equal("Data", settings.DataDirectory);
        Assert.Equal(30, settings.RetentionDays);
        Assert.Equal("localhost", settings.Smtp.Host);
    }

    [Fact]
    public void MainProject_DeclaresSixReleaseRuntimeIdentifiers()
    {
        var projectPath = Path.Combine(RepositoryRoot, "src", "Hokai", "Hokai.csproj");
        var project = File.ReadAllText(projectPath);

        Assert.Contains("RuntimeIdentifiers", project, StringComparison.Ordinal);
        Assert.Contains("linux-x64", project, StringComparison.Ordinal);
        Assert.Contains("linux-arm64", project, StringComparison.Ordinal);
        Assert.Contains("osx-x64", project, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", project, StringComparison.Ordinal);
        Assert.Contains("win-x64", project, StringComparison.Ordinal);
        Assert.Contains("win-arm64", project, StringComparison.Ordinal);
    }

    [Fact]
    public void MainProject_UsesPublishSelfContained_NotGlobalSelfContained()
    {
        var projectPath = Path.Combine(RepositoryRoot, "src", "Hokai", "Hokai.csproj");
        var project = File.ReadAllText(projectPath);

        Assert.Contains("PublishSelfContained", project, StringComparison.Ordinal);

        var selfContainedOccurrences = CountOccurrences(project, "<SelfContained>true</SelfContained>");
        Assert.Equal(0, selfContainedOccurrences);
    }

    [Fact]
    public void LockFile_ContainsAllSixRidGraphs()
    {
        var lockPath = Path.Combine(RepositoryRoot, "src", "Hokai", "packages.lock.json");
        var lockJson = File.ReadAllText(lockPath);

        Assert.Contains("net10.0/linux-x64", lockJson, StringComparison.Ordinal);
        Assert.Contains("net10.0/linux-arm64", lockJson, StringComparison.Ordinal);
        Assert.Contains("net10.0/osx-x64", lockJson, StringComparison.Ordinal);
        Assert.Contains("net10.0/osx-arm64", lockJson, StringComparison.Ordinal);
        Assert.Contains("net10.0/win-x64", lockJson, StringComparison.Ordinal);
        Assert.Contains("net10.0/win-arm64", lockJson, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    [Fact]
    public void MainProject_ReferencesSystemCommandLine()
    {
        var projectPath = Path.Combine(RepositoryRoot, "src", "Hokai", "Hokai.csproj");
        var project = File.ReadAllText(projectPath);

        var containsPackageReference = project.Contains(
            "PackageReference Include=\"System.CommandLine\"",
            StringComparison.Ordinal);

        Assert.True(containsPackageReference);
    }
}
