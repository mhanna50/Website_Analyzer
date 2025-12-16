using System;

namespace SiteMonitor.Api.Models;

public record AnalysisResult(
    string Url,
    DateTime CheckedAtUtc,
    NetworkResult Network,
    SeoResult Seo,
    ScoreResult Score,
    PerformanceResult? Performance,
    OffPageSeoResult? OffPageSeo,
    AiInsightsResult? AiInsights);
