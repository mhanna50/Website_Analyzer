namespace SiteMonitor.Api.Models;

public record ScanJob(Guid Id, WebsiteAnalysisRequest Request, bool SaveHistory);

public record ScanJobStatus(Guid Id, ScanJobState State, AnalysisResult? Result, string? Error);

public enum ScanJobState
{
    Pending,
    Processing,
    Completed,
    Failed
}
