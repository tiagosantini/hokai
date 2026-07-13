using Hokai.Services;

namespace Hokai.Tests.Services;

public sealed class ProcessRunnerTests
{
    private const string TestExecutable = "dotnet";

    [Fact]
    public async Task RunAsync_ExitCodeZero_ReturnsExitCodeAndOutput()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(TestExecutable, ["--version"], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_ExitCodeNonZero_ReturnsCorrectExitCode()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(TestExecutable, ["exec", "this-does-not-exist.dll"], CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_CapturesStdoutAndStderrSeparately()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(TestExecutable, ["--help"], CancellationToken.None);

        Assert.NotEmpty(result.StandardOutput);
        Assert.NotNull(result.StandardError);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelled_ThrowsOperationCanceledException()
    {
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(TestExecutable, ["--version"], cts.Token));
    }

}
