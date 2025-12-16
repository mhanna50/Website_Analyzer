namespace SiteMonitor.Api.Models;

public record SpaDomAnalysisResult(
    int TotalImages,
    int ImagesWithoutAlt,
    int InternalLinkCount,
    int ExternalLinkCount);
