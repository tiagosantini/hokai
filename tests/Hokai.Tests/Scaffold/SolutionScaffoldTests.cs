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
