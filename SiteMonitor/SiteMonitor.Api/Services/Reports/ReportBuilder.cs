using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Recommendations;

namespace SiteMonitor.Api.Services.Reports;

public static class ReportBuilder
{
    public static AnalysisReport BuildReport(AnalysisResult analysis)
    {
        var recommendations = RecommendationBuilder.BuildRecommendations(analysis);
        var checklist = RecommendationBuilder.BuildChecklistSections(analysis.AiInsights);

        var summary = new ReportSummary(
            analysis.Url,
            analysis.CheckedAtUtc,
            analysis.Network.StatusCode,
            analysis.Network.ResponseTimeMs,
            analysis.Score,
            analysis.Seo.IsIndexable,
            analysis.Seo.UsesHttps,
            analysis.Network.ErrorMessage);

        var performance = new ReportPerformance(
            analysis.Score.Speed,
            analysis.Network,
            analysis.Performance);

        var seo = new ReportSeo(
            analysis.Score.Seo,
            analysis.Seo);

        ReportOffPageSeo? offPageSeo = null;
        if (analysis.OffPageSeo is not null)
        {
            offPageSeo = new ReportOffPageSeo(analysis.OffPageSeo);
        }

        return new AnalysisReport(
            summary,
            performance,
            seo,
            offPageSeo,
            recommendations,
            checklist);
    }
}
