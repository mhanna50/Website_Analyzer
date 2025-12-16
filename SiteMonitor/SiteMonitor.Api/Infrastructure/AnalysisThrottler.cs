using System.Threading;

namespace SiteMonitor.Api.Infrastructure;

public class AnalysisThrottler
{
    private readonly SemaphoreSlim _semaphore;

    public AnalysisThrottler(IConfiguration configuration)
    {
        var maxConcurrent = configuration.GetValue<int?>("Analysis:MaxConcurrentScans") ?? 4;
        _semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrent));
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _semaphore.Release();
            }
        }
    }
}
