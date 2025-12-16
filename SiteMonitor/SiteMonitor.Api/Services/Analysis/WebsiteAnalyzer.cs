using System.Diagnostics;
using System.Net.Http;
using SiteMonitor.Api.Infrastructure;
using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Scoring;
using SiteMonitor.Api.Services.Seo;
using SiteMonitor.Api.Utilities;

namespace SiteMonitor.Api.Services.Analysis;

public class WebsiteAnalyzer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SpaDomAnalyzer _spaDomAnalyzer;
    private readonly PerformanceService _performanceService;
    private readonly OffPageSeoService _offPageSeoService;
    private readonly HistoryStore _historyStore;
    private readonly AiInsightsService _aiInsightsService;
    private readonly LinkHealthAnalyzer _linkHealthAnalyzer;
    private readonly AnalysisThrottler _analysisThrottler;

    public WebsiteAnalyzer(
        IHttpClientFactory httpClientFactory,
        SpaDomAnalyzer spaDomAnalyzer,
        PerformanceService performanceService,
        OffPageSeoService offPageSeoService,
        HistoryStore historyStore,
        AiInsightsService aiInsightsService,
        LinkHealthAnalyzer linkHealthAnalyzer,
        AnalysisThrottler analysisThrottler)
    {
        _httpClientFactory = httpClientFactory;
        _spaDomAnalyzer = spaDomAnalyzer;
        _performanceService = performanceService;
        _offPageSeoService = offPageSeoService;
        _historyStore = historyStore;
        _aiInsightsService = aiInsightsService;
        _linkHealthAnalyzer = linkHealthAnalyzer;
        _analysisThrottler = analysisThrottler;
    }

    public async Task<AnalysisResult> AnalyzeAsync(
        WebsiteAnalysisRequest request,
        bool saveHistory,
        CancellationToken cancellationToken = default)
    {
        using var lease = await _analysisThrottler.AcquireAsync(cancellationToken);
        var originalUrl = request.Url?.Trim() ?? string.Empty;
        var mode = request.Mode;

        if (!UrlHelper.TryNormalizeUrl(originalUrl, out var normalizedUrl))
        {
            var timestamp = DateTime.UtcNow;
            var invalidNetwork = new NetworkResult(
                originalUrl,
                0,
                0,
                timestamp,
                "A valid URL is required.",
                0);

            return new AnalysisResult(
                originalUrl,
                timestamp,
                invalidNetwork,
                SeoResult.Empty,
                new ScoreResult(0, 0, 0),
                null,
                null,
                null);
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(mode == ScanMode.Fast ? 15 : 30);

        var stopwatch = Stopwatch.StartNew();
        var siteUri = new Uri(normalizedUrl);

        HttpResponseMessage? response = null;
        var redirectCount = 0;
        string html = string.Empty;
        NetworkResult network;
        DateTime checkedAtUtc;

        try
        {
            response = await client.GetAsync(normalizedUrl, cancellationToken);
            stopwatch.Stop();
            html = response.Content is not null
                ? await response.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;

            checkedAtUtc = DateTime.UtcNow;
            if (response.RequestMessage?.RequestUri is { } finalUri &&
                !string.Equals(finalUri.ToString(), normalizedUrl, StringComparison.OrdinalIgnoreCase))
            {
                redirectCount = 1;
            }
            network = new NetworkResult(
                normalizedUrl,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                checkedAtUtc,
                null,
                redirectCount);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            checkedAtUtc = DateTime.UtcNow;
            network = new NetworkResult(
                normalizedUrl,
                0,
                stopwatch.ElapsedMilliseconds,
                checkedAtUtc,
                ex.Message,
                0);
        }

        SpaDomAnalysisResult? spaDomResult = null;
        if (mode == ScanMode.Deep)
        {
            try
            {
                spaDomResult = await _spaDomAnalyzer.AnalyzeAsync(normalizedUrl, siteUri.Host);
            }
            catch
            {
                // Ignore errors from headless rendering and fall back to static HTML parsing.
            }
        }

        var seo = SeoBuilder.BuildSeoResult(siteUri, html, response, spaDomResult, out var crawlableLinks);
        response?.Dispose();

        try
        {
            var brokenLinks = await _linkHealthAnalyzer.FindBrokenLinksAsync(siteUri, crawlableLinks, mode, cancellationToken);
            if (brokenLinks.Count > 0)
            {
                seo = seo with
                {
                    BrokenLinkCount = brokenLinks.Count,
                    BrokenLinks = brokenLinks
                };
            }
        }
        catch
        {
            // Ignore link health issues to keep the scan resilient.
        }

        PerformanceResult? performance = null;
        if (mode != ScanMode.Fast)
        {
            try
            {
                performance = await _performanceService.AnalyzeAsync(normalizedUrl, cancellationToken);
            }
            catch
            {
                // Ignore performance API failures to keep response resilient.
            }
        }

        OffPageSeoResult? offPageSeo = null;
        if (mode == ScanMode.Deep)
        {
            try
            {
                offPageSeo = await _offPageSeoService.GetMetricsAsync(siteUri.Host, cancellationToken);
            }
            catch
            {
                // Ignore off-page SEO failures to keep response resilient.
            }
        }

        var score = ScoreCalculator.CalculateScores(seo, performance, network);

        if (saveHistory)
        {
            var scanRecord = new ScanRecord(
                normalizedUrl,
                network.CheckedAtUtc,
                network.StatusCode,
                network.ResponseTimeMs,
                performance?.OverallScore,
                seo.IsIndexable,
                seo.UsesHttps,
                score.Overall,
                score.Seo,
                score.Speed);

            await _historyStore.AddRecordAsync(scanRecord);
        }

        var result = new AnalysisResult(
            normalizedUrl,
            network.CheckedAtUtc,
            network,
            seo,
            score,
            performance,
            offPageSeo,
            null);

        try
        {
            var aiInsights = await _aiInsightsService.GenerateInsightsAsync(result, cancellationToken);
            if (aiInsights is not null)
            {
                result = result with { AiInsights = aiInsights };
            }
        }
        catch
        {
            // Ignore AI failures; insights are optional.
        }

        return result;
    }
}
