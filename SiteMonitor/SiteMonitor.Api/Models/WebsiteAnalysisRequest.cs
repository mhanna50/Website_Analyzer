namespace SiteMonitor.Api.Models;

public record WebsiteAnalysisRequest(string Url, ScanMode Mode = ScanMode.Deep);
