using System.Collections.Generic;

namespace SiteMonitor.Api.Models;

public record PerformanceResult(
    int? OverallScore,
    int? MobileScore,
    int? DesktopScore,
    double? LargestContentfulPaintMs,
    double? FirstContentfulPaintMs,
    double? CumulativeLayoutShift,
    double? TotalBlockingTimeMs,
    IReadOnlyList<PerformanceSuggestion> Suggestions);

public record PerformanceSuggestion(
    string Title,
    string? Description,
    double? Score,
    double? EstimatedSavingsMs);
