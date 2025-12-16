using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Scoring;
using Xunit;

namespace SiteMonitor.Tests.Unit;

public class ScoreCalculatorTests
{
    [Fact]
    public void CalculateSeoScore_FavorsIndexablePagesWithGoodMeta()
    {
        var seo = new SeoResult(
            Title: "Example title for SEO",
            TitleLength: 22,
            MetaDescription: "A detailed description that falls within ideal range",
            MetaDescriptionLength: 62,
            CanonicalUrl: "https://example.com",
            IsIndexable: true,
            H1Count: 1,
            H2Count: 2,
            HasViewportMeta: true,
            ViewportContent: "width=device-width, initial-scale=1",
            TotalImages: 4,
            ImagesWithoutAlt: 0,
            InternalLinkCount: 10,
            ExternalLinkCount: 2,
            UsesHttps: true,
            DomFromHeadlessBrowser: false,
            HasLanguageAttribute: true,
            HasSkipLink: true,
            LandmarkCount: 2,
            FormControlsWithoutLabels: 0,
            StructuredDataCount: 1,
            StructuredDataTypes: new[] { "Organization" },
            HasOpenGraphTags: true,
            HasTwitterCard: true,
            BrokenLinkCount: 0,
            BrokenLinks: Array.Empty<BrokenLink>());

        var score = ScoreCalculator.CalculateSeoScore(seo);

        Assert.InRange(score, 85, 100);
    }

    [Fact]
    public void CalculateSeoScore_PenaltiesAccumulateForIssues()
    {
        var seo = new SeoResult(
            Title: string.Empty,
            TitleLength: 0,
            MetaDescription: string.Empty,
            MetaDescriptionLength: 0,
            CanonicalUrl: string.Empty,
            IsIndexable: false,
            H1Count: 0,
            H2Count: 5,
            HasViewportMeta: false,
            ViewportContent: string.Empty,
            TotalImages: 10,
            ImagesWithoutAlt: 10,
            InternalLinkCount: 0,
            ExternalLinkCount: 0,
            UsesHttps: false,
            DomFromHeadlessBrowser: false,
            HasLanguageAttribute: false,
            HasSkipLink: false,
            LandmarkCount: 0,
            FormControlsWithoutLabels: 5,
            StructuredDataCount: 0,
            StructuredDataTypes: Array.Empty<string>(),
            HasOpenGraphTags: false,
            HasTwitterCard: false,
            BrokenLinkCount: 5,
            BrokenLinks: new[] { new BrokenLink("https://example.com/missing", true, 404, "Not Found") });

        var score = ScoreCalculator.CalculateSeoScore(seo);

        Assert.True(score < 40);
    }

    [Fact]
    public void CalculateSpeedScore_UsesNetworkWhenPerformanceUnavailable()
    {
        var network = new NetworkResult("https://example.com", 200, 1500, DateTime.UtcNow, null, 0);

        var score = ScoreCalculator.CalculateSpeedScore(null, network);

        Assert.InRange(score, 60, 90);
    }

    [Fact]
    public void CalculateSpeedScore_ReturnsZeroForFailures()
    {
        var network = new NetworkResult("https://example.com", 500, 1500, DateTime.UtcNow, "Server Error", 0);

        var score = ScoreCalculator.CalculateSpeedScore(null, network);

        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateScores_CombinesSeoAndSpeedWeighting()
    {
        var seo = SeoResult.Empty with { IsIndexable = true };
        var performance = new PerformanceResult(
            OverallScore: 90,
            MobileScore: 90,
            DesktopScore: 90,
            LargestContentfulPaintMs: 1200,
            FirstContentfulPaintMs: 800,
            CumulativeLayoutShift: 0.05,
            TotalBlockingTimeMs: 100,
            Suggestions: Array.Empty<PerformanceSuggestion>());
        var network = new NetworkResult("https://example.com", 200, 800, DateTime.UtcNow, null, 0);

        var result = ScoreCalculator.CalculateScores(seo, performance, network);

        Assert.True(result.Overall >= result.Seo);
        Assert.True(result.Overall >= result.Speed);
        Assert.InRange(result.Overall, 80, 100);
    }
}
