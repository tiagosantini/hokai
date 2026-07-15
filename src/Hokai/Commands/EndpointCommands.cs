using Hokai.Models;
using Hokai.Services;
using System.CommandLine;

namespace Hokai.Commands;

public static class EndpointCommands
{
    private static readonly string[] AllowedMethods =
        ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "TRACE"];

    public static Command Create(IEndpointStore endpointStore, ICheckStore checkStore)
    {
        var command = new Command("endpoint", "Manage monitored endpoints");
        command.Add(CreateAddCommand(endpointStore));
        command.Add(CreateListCommand(endpointStore, checkStore));
        command.Add(CreateRemoveCommand(endpointStore));
        return command;
    }

    private static Command CreateAddCommand(IEndpointStore store)
    {
        var urlArg = new Argument<string>("url")
        {
            Description = "The HTTP or HTTPS URL to monitor",
            Arity = ArgumentArity.ExactlyOne
        };

        var intervalOpt = new Option<string>("--interval", [])
        {
            DefaultValueFactory = _ => "5m",
            Description = "Check interval (e.g. 30s, 5m, 1h, 00:05:00)"
        };

        var timeoutOpt = new Option<string>("--timeout", [])
        {
            DefaultValueFactory = _ => "30s",
            Description = "Request timeout (e.g. 10s, 500ms, 00:00:10)"
        };

        var methodOpt = new Option<string>("--method", [])
        {
            DefaultValueFactory = _ => "GET",
            Description = "HTTP method"
        }.AcceptOnlyFromAmong(AllowedMethods);

        var expectOpt = new Option<int>("--expect", [])
        {
            DefaultValueFactory = _ => 200,
            Description = "Expected HTTP status code"
        };

        var command = new Command("add", "Add a new endpoint to monitor")
        {
            urlArg, intervalOpt, timeoutOpt, methodOpt, expectOpt
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
            await HandleAddAsync(
                store,
                urlArg: parseResult.GetValue(urlArg)!,
                intervalStr: parseResult.GetValue(intervalOpt)!,
                timeoutStr: parseResult.GetValue(timeoutOpt)!,
                method: parseResult.GetValue(methodOpt)!,
                expectedStatus: parseResult.GetValue(expectOpt),
                cancellationToken));

        return command;
    }

    internal static async Task<int> HandleAddAsync(
        IEndpointStore store,
        string urlArg,
        string intervalStr,
        string timeoutStr,
        string method,
        int expectedStatus,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(urlArg, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            await Console.Error.WriteLineAsync(
                $"Error: URL must be an absolute HTTP or HTTPS address. Received: {urlArg}");
            return 1;
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            await Console.Error.WriteLineAsync(
                "Error: URL must not contain embedded credentials (user:password@).");
            return 1;
        }

        if (!DurationParser.TryParse(intervalStr, out var interval))
        {
            await Console.Error.WriteLineAsync(
                $"Error: Monitoring interval must be a positive duration. Received: {intervalStr}");
            return 1;
        }

        if (!DurationParser.TryParse(timeoutStr, out var timeout))
        {
            await Console.Error.WriteLineAsync(
                $"Error: Request timeout must be a positive duration. Received: {timeoutStr}");
            return 1;
        }

        if (expectedStatus is < 100 or > 599)
        {
            await Console.Error.WriteLineAsync(
                $"Error: Expected HTTP status must be between 100 and 599. Received: {expectedStatus}");
            return 1;
        }

        var config = new EndpointConfig
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Url = url,
            Interval = interval,
            Timeout = timeout,
            Method = method,
            ExpectedStatus = expectedStatus,
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await store.AddAsync(config, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await Console.Error.WriteLineAsync($"Error: {exception.Message}");
            return 1;
        }

        await Console.Out.WriteLineAsync($"Endpoint {config.Id} added.");
        return 0;
    }

    private static Command CreateListCommand(IEndpointStore store, ICheckStore checkStore)
    {
        var command = new Command("list", "List all monitored endpoints and their uptime");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var endpoints = await store.GetAllAsync(cancellationToken);
            if (endpoints.Count == 0)
            {
                await Console.Out.WriteLineAsync("No endpoints configured.");
                return;
            }

            var summaries = await checkStore.GetBatchSummariesAsync(
                TimeSpan.FromHours(24), cancellationToken);
            var summaryMap = summaries.ToDictionary(
                s => s.EndpointId, StringComparer.Ordinal);

            const string header = "ID        URL                                               INTERVAL  TIMEOUT  METHOD  EXPECT  UPTIME";
            await Console.Out.WriteLineAsync(header);

            foreach (var endpoint in endpoints.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                summaryMap.TryGetValue(endpoint.Id, out var summary);
                var uptime = summary?.Uptime ?? 0d;
                var line = string.Create(null,
                    $"{endpoint.Id,-9} {UriDisplayFormatter.Format(endpoint.Url),-50} {endpoint.Interval,-9} {endpoint.Timeout,-8} {endpoint.Method,-7} {endpoint.ExpectedStatus,-7} {uptime:F1}%");
                await Console.Out.WriteLineAsync(line);
            }
        });

        return command;
    }

    private static Command CreateRemoveCommand(IEndpointStore store)
    {
        var idArg = new Argument<string>("id")
        {
            Description = "The endpoint identifier to remove",
            Arity = ArgumentArity.ExactlyOne
        };

        var command = new Command("remove", "Remove a monitored endpoint")
        {
            idArg
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var id = parseResult.GetValue(idArg)!;
            var removed = await store.RemoveAsync(id, cancellationToken);

            if (removed)
            {
                await Console.Out.WriteLineAsync($"Endpoint {id} removed.");
                return 0;
            }
            else
            {
                await Console.Error.WriteLineAsync($"Error: Endpoint '{id}' not found.");
                return 1;
            }
        });

        return command;
    }
}
