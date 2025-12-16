using Microsoft.Extensions.Hosting;
using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Analysis;

namespace SiteMonitor.Api.Services.Queue;

public class ScanWorker : BackgroundService
{
    private readonly ScanQueue _queue;
    private readonly WebsiteAnalyzer _analyzer;
    private readonly ILogger<ScanWorker> _logger;

    public ScanWorker(ScanQueue queue, WebsiteAnalyzer analyzer, ILogger<ScanWorker> logger)
    {
        _queue = queue;
        _analyzer = analyzer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _queue.MarkProcessing(job.Id);
            try
            {
                var result = await _analyzer.AnalyzeAsync(job.Request, job.SaveHistory, stoppingToken);
                _queue.MarkCompleted(job.Id, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process queued scan {JobId}", job.Id);
                _queue.MarkFailed(job.Id, ex.Message);
            }
        }
    }
}
