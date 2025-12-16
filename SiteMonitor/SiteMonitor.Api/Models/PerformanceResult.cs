using System.Collections.Generic;

namespace SiteMonitor.Api.Models;

public record PerformanceResult(
    PerformanceChannelResult? Mobile,
    PerformanceChannelResult? Desktop,
    IReadOnlyList<PerformanceSuggestion> Suggestions);

public record PerformanceChannelResult(
    string Strategy,
    int? Score,
    double? LargestContentfulPaintMs,
    double? FirstContentfulPaintMs,
    double? CumulativeLayoutShift,
    double? TotalBlockingTimeMs);

public record PerformanceSuggestion(
    string Title,
    string? Description,
    double? Score,
    double? EstimatedSavingsMs);
