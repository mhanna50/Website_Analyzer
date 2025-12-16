using System;

namespace SiteMonitor.Api.Models;

public record ScanRecord(
    string Url,
    DateTime TimestampUtc,
    int StatusCode,
    long ResponseTimeMs,
    int? PerformanceScore,
    bool IsIndexable,
    bool UsesHttps,
    int OverallScore,
    int SeoScore,
    int SpeedScore);
