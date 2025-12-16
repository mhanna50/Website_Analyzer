using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Recommendations;
using Xunit;

namespace SiteMonitor.Tests.Unit;

public class RecommendationBuilderTests
{
    [Fact]
    public void BuildRecommendations_AddsIssuesForMissingViewportAndAlt()
    {
        var analysis = new AnalysisResult(
            Url: "https://example.com",
            CheckedAtUtc: DateTime.UtcNow,
            Network: new NetworkResult("https://example.com", 200, 500, DateTime.UtcNow, null, 0),
            Seo: SeoResult.Empty with
            {
                HasViewportMeta = false,
                ImagesWithoutAlt = 3,
                TotalImages = 3,
                UsesHttps = false
            },
            Score: new ScoreResult(50, 50, 50),
            Performance: null,
            OffPageSeo: null,
            AiInsights: null);

        var recommendations = RecommendationBuilder.BuildRecommendations(analysis);

        Assert.Contains(recommendations, r => r.Title.Contains("Missing mobile viewport", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(recommendations, r => r.Title.Contains("Add image alt text", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(recommendations, r => r.Title.Contains("Upgrade to HTTPS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildChecklistSections_SplitsHeadingsAndBullets()
    {
        var ai = new AiInsightsResult("""
        ## Performance
        - [ ] Fix LCP
        - [ ] Reduce blocking scripts
        ## SEO
        - [ ] Add meta description
        """);

        var sections = RecommendationBuilder.BuildChecklistSections(ai);

        Assert.Equal(2, sections.Count);
        Assert.Equal("Performance", sections[0].Title);
        Assert.Contains("Fix LCP", sections[0].Items[0]);
        Assert.Equal("SEO", sections[1].Title);
    }

    [Fact]
    public void BuildRecommendations_AddsNetworkErrors()
    {
        var analysis = new AnalysisResult(
            Url: "https://example.com",
            CheckedAtUtc: DateTime.UtcNow,
            Network: new NetworkResult("https://example.com", 500, 1200, DateTime.UtcNow, "Server Error", 0),
            Seo: SeoResult.Empty,
            Score: new ScoreResult(10, 10, 10),
            Performance: null,
            OffPageSeo: null,
            AiInsights: null);

        var recommendations = RecommendationBuilder.BuildRecommendations(analysis);

        Assert.Contains(recommendations, r => r.Category == "Network");
    }
}
