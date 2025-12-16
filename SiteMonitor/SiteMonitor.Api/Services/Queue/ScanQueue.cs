using System.Collections.Concurrent;
using System.Threading.Channels;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services.Queue;

public class ScanQueue
{
    private readonly Channel<ScanJob> _channel;
    private readonly ConcurrentDictionary<Guid, ScanJobStatus> _statuses = new();

    public ScanQueue()
    {
        _channel = Channel.CreateUnbounded<ScanJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false
        });
    }

    public Guid Enqueue(WebsiteAnalysisRequest request, bool saveHistory)
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob(jobId, request, saveHistory);
        _statuses[jobId] = new ScanJobStatus(jobId, ScanJobState.Pending, null, null);
        _channel.Writer.TryWrite(job);
        return jobId;
    }

    public ScanJobStatus? GetStatus(Guid jobId)
    {
        return _statuses.TryGetValue(jobId, out var status) ? status : null;
    }

    internal ChannelReader<ScanJob> Reader => _channel.Reader;

    internal void MarkProcessing(Guid jobId)
    {
        _statuses.AddOrUpdate(jobId,
            _ => new ScanJobStatus(jobId, ScanJobState.Processing, null, null),
            (_, _) => new ScanJobStatus(jobId, ScanJobState.Processing, null, null));
    }

    internal void MarkCompleted(Guid jobId, AnalysisResult result)
    {
        _statuses[jobId] = new ScanJobStatus(jobId, ScanJobState.Completed, result, null);
    }

    internal void MarkFailed(Guid jobId, string error)
    {
        _statuses[jobId] = new ScanJobStatus(jobId, ScanJobState.Failed, null, error);
    }
}
