using System;
using System.Threading.Tasks;

namespace FCPApp.Services.Refresh;

public interface IAutoRefreshManager : IDisposable
{
    public bool IsRunning { get; }

    public void Start(Func<Task> refreshAction, TimeSpan? interval = null);
    public void Stop();
}