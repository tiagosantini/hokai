using Hokai.Models;
using Hokai.Services;
using System.CommandLine;

namespace Hokai.Commands;

public static class StatusCommand
{
    public static Command Create(IEndpointStore endpointStore, ICheckStore checkStore)
    {
        var command = new Command("status", "Show the current status of all endpoints");

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var endpoints = await endpointStore.GetAllAsync(cancellationToken);
            if (endpoints.Count == 0)
            {
                await Console.Out.WriteLineAsync("No endpoints configured.");
                return;
            }

            const string header = "ID        URL                                               LAST CHECK           STATUS  CODE  RT(ms)  UPTIME";
            await Console.Out.WriteLineAsync(header);

            foreach (var endpoint in endpoints.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var lastCheck = await checkStore.GetLastCheckAsync(endpoint.Id, cancellationToken);
                var uptime = await checkStore.GetUptimeAsync(
                    endpoint.Id, TimeSpan.FromHours(24), cancellationToken);

                var lastCheckStr = lastCheck?.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
                var status = lastCheck?.IsUp == true ? "UP" : lastCheck?.IsUp == false ? "DOWN" : "—";
                var statusCode = lastCheck?.StatusCode?.ToString() ?? "—";
                var responseTime = lastCheck is not null ? lastCheck.ResponseTimeMs.ToString() : "—";

                var line = $"{endpoint.Id,-9} {UriDisplayFormatter.Format(endpoint.Url),-50} {lastCheckStr,-20} {status,-6} {statusCode,-5} {responseTime,-7} {uptime:F1}%";
                await Console.Out.WriteLineAsync(line);
            }
        });

        return command;
    }
}
