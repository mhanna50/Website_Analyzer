using System.Collections.Generic;

namespace SiteMonitor.Api.Models;

public record AnalysisReport(
    ReportSummary Summary,
    ReportPerformance Performance,
    ReportSeo Seo,
    ReportOffPageSeo? OffPageSeo,
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<ChecklistSection> Checklist);

public record ReportSummary(
    string Url,
    DateTime CheckedAtUtc,
    int StatusCode,
    long ResponseTimeMs,
    ScoreResult Score,
    bool IsIndexable,
    bool UsesHttps,
    string? ErrorMessage);

public record ReportPerformance(
    int SpeedScore,
    NetworkResult Network,
    PerformanceResult? Details);

public record ReportSeo(
    int SeoScore,
    SeoResult Details);

public record ReportOffPageSeo(
    OffPageSeoResult Details);

public record Recommendation(
    string Title,
    string Description,
    string Category);

public record ChecklistSection(
    string Title,
    IReadOnlyList<string> Items);
