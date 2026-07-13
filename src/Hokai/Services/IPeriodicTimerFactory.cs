namespace Hokai.Services;

/// <summary>Creates asynchronous periodic timers for monitor workers.</summary>
public interface IPeriodicTimerFactory
{
    IAsyncPeriodicTimer Create(TimeSpan period);
}

/// <summary>Exposes the asynchronous tick contract used by monitor loops.</summary>
public interface IAsyncPeriodicTimer : IAsyncDisposable
{
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
}

public sealed class PeriodicTimerFactory : IPeriodicTimerFactory
{
    public IAsyncPeriodicTimer Create(TimeSpan period) =>
        new PeriodicTimerAdapter(new PeriodicTimer(period));
}

internal sealed class PeriodicTimerAdapter(PeriodicTimer timer) : IAsyncPeriodicTimer
{
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken) =>
        timer.WaitForNextTickAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        timer.Dispose();
        return ValueTask.CompletedTask;
    }
}
