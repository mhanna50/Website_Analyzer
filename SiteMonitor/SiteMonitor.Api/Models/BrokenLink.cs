namespace SiteMonitor.Api.Models;

public record BrokenLink(
    string Url,
    bool IsInternal,
    int StatusCode,
    string? Reason);
