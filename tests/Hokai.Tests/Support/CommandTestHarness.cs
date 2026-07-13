using System.CommandLine;

namespace Hokai.Tests.Support;

[CollectionDefinition(nameof(CommandTestHarness))]
public sealed class CommandTestHarness
{
    public static async Task<(int ExitCode, string Output, string Error)> InvokeAsync(Command command, string args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var parseResult = command.Parse(args);
            var exitCode = await parseResult.InvokeAsync(
                new InvocationConfiguration(),
                CancellationToken.None);
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
