using Hokai.Commands;
using Hokai.Hosting;
using Hokai.Models;
using Hokai.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hokai.Hosting;

public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "Hokai.HealthChecks";

    public static IServiceCollection AddHokaiCore(
        this IServiceCollection services,
        AppSettings settings,
        ServiceManagerContext serviceContext)
    {
        services.AddSingleton(settings);
        services.AddSingleton(settings.Smtp);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IEndpointStore>(_ =>
            new EndpointStore(settings.DataDirectory));
        services.AddSingleton<ICheckStore>(sp =>
            new CheckStore(settings.DataDirectory, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IServiceManager>(_ =>
            new ServiceManager(serviceContext));

        return services;
    }

    public static IServiceCollection AddHokaiMonitoring(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName, client =>
        {
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<ISmtpMailSender, SmtpMailSender>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IPeriodicTimerFactory, PeriodicTimerFactory>();

        return services;
    }

    public static IServiceCollection AddHokaiDaemon(this IServiceCollection services)
    {
        services.AddHostedService<MonitorService>();
        return services;
    }
}
