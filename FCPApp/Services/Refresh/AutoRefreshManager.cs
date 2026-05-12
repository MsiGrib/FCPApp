using System;
using System.Threading;
using System.Threading.Tasks;

namespace FCPApp.Services.Refresh;

public class AutoRefreshManager : IAutoRefreshManager
{
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    private CancellationTokenSource? _cts = null;
    private Task? _refreshTask = null;
    private readonly TimeSpan _defaultInterval;

    public AutoRefreshManager(TimeSpan? interval = null)
    {
        _defaultInterval = interval ?? TimeSpan.FromSeconds(3);
    }

    public void Start(Func<Task> refreshAction, TimeSpan? interval = null)
    {
        Stop();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var refreshInterval = interval ?? _defaultInterval;

        _refreshTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(refreshInterval, token);
                    await refreshAction();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] AutoRefresh: {ex.Message}");
                }
            }
        }, token);
    }

    public void Start(Func<Task> refreshAction, TimeSpan interval)
    {
        Stop();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var refreshInterval = interval;

        _refreshTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(refreshInterval, token);
                    await refreshAction();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] AutoRefresh: {ex.Message}");
                }
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _refreshTask = null;
    }

    public void Dispose()
        => Stop();
}