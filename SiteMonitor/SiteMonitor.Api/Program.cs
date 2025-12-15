using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using SiteMonitor.Api.Services;

LoadDotEnv();

static void LoadDotEnv()
{
    try
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            var envPath = Path.Combine(current, ".env");
            if (File.Exists(envPath))
            {
                foreach (var rawLine in File.ReadAllLines(envPath))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();
                    Environment.SetEnvironmentVariable(key, value);
                }

                break;
            }

            current = Directory.GetParent(current)?.FullName;
        }
    }
    catch
    {
        // Ignore dotenv errors; environment variables can be set elsewhere.
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpaDomAnalyzer>();
builder.Services.AddSingleton<PerformanceService>();
builder.Services.AddSingleton<OffPageSeoService>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<ReportRenderer>();
builder.Services.AddSingleton<AiInsightsService>();

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

app.MapPost("/api/analyze", async (
        WebsiteAnalysisRequest request,
        IHttpClientFactory httpClientFactory,
        SpaDomAnalyzer spaDomAnalyzer,
        PerformanceService performanceService,
        OffPageSeoService offPageSeoService,
        HistoryStore historyStore,
        AiInsightsService aiInsightsService) =>
    {
        var analysis = await AnalyzeWebsiteAsync(
            request,
            httpClientFactory,
            spaDomAnalyzer,
            performanceService,
            offPageSeoService,
            historyStore,
            aiInsightsService,
            saveHistory: true);

        return Results.Ok(analysis);
    })
    .WithName("AnalyzeWebsite");

app.MapPost("/api/report", async (
        WebsiteAnalysisRequest request,
        IHttpClientFactory httpClientFactory,
        SpaDomAnalyzer spaDomAnalyzer,
        PerformanceService performanceService,
        OffPageSeoService offPageSeoService,
        HistoryStore historyStore,
        ReportRenderer reportRenderer,
        AiInsightsService aiInsightsService) =>
    {
        var analysis = await AnalyzeWebsiteAsync(
            request,
            httpClientFactory,
            spaDomAnalyzer,
            performanceService,
            offPageSeoService,
            historyStore,
            aiInsightsService,
            saveHistory: false);

        var report = BuildReport(analysis);

        try
        {
            var screenshot = await reportRenderer.RenderScreenshotAsync(report);
            var fileName = "site-report.png";
            if (Uri.TryCreate(analysis.Url, UriKind.Absolute, out var parsedUri))
            {
                fileName = $"{parsedUri.Host}-report.png";
            }
            return Results.File(screenshot, "image/png", fileName);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                $"Unable to generate report screenshot: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    })
    .WithName("GenerateReport");

app.MapPost("/api/report/pdf", async (
        WebsiteAnalysisRequest request,
        IHttpClientFactory httpClientFactory,
        SpaDomAnalyzer spaDomAnalyzer,
        PerformanceService performanceService,
        OffPageSeoService offPageSeoService,
        HistoryStore historyStore,
        ReportRenderer reportRenderer,
        AiInsightsService aiInsightsService) =>
    {
        var analysis = await AnalyzeWebsiteAsync(
            request,
            httpClientFactory,
            spaDomAnalyzer,
            performanceService,
            offPageSeoService,
            historyStore,
            aiInsightsService,
            saveHistory: false);

        var report = BuildReport(analysis);

        try
        {
            var pdf = await reportRenderer.RenderPdfAsync(report);
            var fileName = "site-report.pdf";
            if (Uri.TryCreate(analysis.Url, UriKind.Absolute, out var parsedUri))
            {
                fileName = $"{parsedUri.Host}-report.pdf";
            }
            return Results.File(pdf, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                $"Unable to generate report PDF: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    })
    .WithName("GenerateReportPdf");

app.MapGet("/api/history", async (string url, HistoryStore historyStore) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest(new { message = "A URL is required." });
    }

    if (!TryNormalizeUrl(url, out var normalizedUrl))
    {
        return Results.BadRequest(new { message = "A valid URL is required." });
    }

    var history = await historyStore.GetHistoryAsync(normalizedUrl);
    return Results.Ok(history);
});

app.MapGet("/api/history/latest", async (string url, HistoryStore historyStore) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest(new { message = "A URL is required." });
    }

    if (!TryNormalizeUrl(url, out var normalizedUrl))
    {
        return Results.BadRequest(new { message = "A valid URL is required." });
    }

    var latest = await historyStore.GetLatestAsync(normalizedUrl);
    return latest is null ? Results.NoContent() : Results.Ok(latest);
});

app.Run();

static async Task<AnalysisResult> AnalyzeWebsiteAsync(
    WebsiteAnalysisRequest request,
    IHttpClientFactory httpClientFactory,
    SpaDomAnalyzer spaDomAnalyzer,
    PerformanceService performanceService,
    OffPageSeoService offPageSeoService,
    HistoryStore historyStore,
    AiInsightsService aiInsightsService,
    bool saveHistory)
{
    var originalUrl = request.Url?.Trim() ?? string.Empty;

    if (!TryNormalizeUrl(originalUrl, out var normalizedUrl))
    {
        var timestamp = DateTime.UtcNow;
        var invalidNetwork = new NetworkResult(
            originalUrl,
            0,
            0,
            timestamp,
            "A valid URL is required.");

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

    var client = httpClientFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(30);

    var stopwatch = Stopwatch.StartNew();
    var siteUri = new Uri(normalizedUrl);

    HttpResponseMessage? response = null;
    string html = string.Empty;
    NetworkResult network;
    DateTime checkedAtUtc;

    try
    {
        response = await client.GetAsync(normalizedUrl);
        stopwatch.Stop();
        html = response.Content is not null
            ? await response.Content.ReadAsStringAsync()
            : string.Empty;

        checkedAtUtc = DateTime.UtcNow;
        network = new NetworkResult(
            normalizedUrl,
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            checkedAtUtc,
            null);
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
            ex.Message);
    }

    SpaDomAnalysisResult? spaDomResult = null;
    try
    {
        spaDomResult = await spaDomAnalyzer.AnalyzeAsync(normalizedUrl, siteUri.Host);
    }
    catch
    {
        // Ignore errors from headless rendering and fall back to static HTML parsing.
    }

    var seo = BuildSeoResult(siteUri, html, response, spaDomResult);
    PerformanceResult? performance = null;
    try
    {
        performance = await performanceService.AnalyzeAsync(normalizedUrl);
    }
    catch
    {
        // Ignore performance API failures to keep response resilient.
    }

    OffPageSeoResult? offPageSeo = null;
    try
    {
        offPageSeo = await offPageSeoService.GetMetricsAsync(siteUri.Host);
    }
    catch
    {
        // Ignore off-page SEO failures to keep response resilient.
    }
    response?.Dispose();

    var score = CalculateScores(seo, performance, network);

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

        await historyStore.AddRecordAsync(scanRecord);
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
        var aiInsights = await aiInsightsService.GenerateInsightsAsync(result);
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

static bool TryNormalizeUrl(string? rawUrl, out string normalizedUrl)
{
    normalizedUrl = string.Empty;

    if (string.IsNullOrWhiteSpace(rawUrl))
    {
        return false;
    }

    var trimmed = rawUrl.Trim();

    if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
        !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        trimmed = $"https://{trimmed}";
    }

    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return false;
    }

    normalizedUrl = uri.ToString();
    return true;
}

static SeoResult BuildSeoResult(Uri siteUri, string html, HttpResponseMessage? response, SpaDomAnalysisResult? spaDomResult)
{
    var usesHttps = string.Equals(siteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    var domFromHeadless = spaDomResult is not null;

    if (string.IsNullOrWhiteSpace(html))
    {
        return SeoResult.Empty with
        {
            UsesHttps = usesHttps,
            TotalImages = spaDomResult?.TotalImages ?? 0,
            ImagesWithoutAlt = spaDomResult?.ImagesWithoutAlt ?? 0,
            InternalLinkCount = spaDomResult?.InternalLinkCount ?? 0,
            ExternalLinkCount = spaDomResult?.ExternalLinkCount ?? 0,
            DomFromHeadlessBrowser = domFromHeadless,
            HasLanguageAttribute = false,
            HasSkipLink = false,
            LandmarkCount = 0,
            FormControlsWithoutLabels = 0,
            StructuredDataCount = 0,
            StructuredDataTypes = Array.Empty<string>(),
            HasOpenGraphTags = false,
            HasTwitterCard = false
        };
    }

    var document = new HtmlDocument();
    document.LoadHtml(html);

    var titleNode = document.DocumentNode.SelectSingleNode("//title");
    var title = HtmlEntity.DeEntitize(titleNode?.InnerText ?? string.Empty).Trim();

    var descriptionNode = document.DocumentNode.SelectSingleNode("//meta[translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='description']");
    var metaDescription = HtmlEntity.DeEntitize(descriptionNode?.GetAttributeValue("content", string.Empty) ?? string.Empty).Trim();

    var canonicalNode = document.DocumentNode.SelectSingleNode("//link[translate(@rel,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='canonical']");
    var canonicalUrl = canonicalNode?.GetAttributeValue("href", string.Empty)?.Trim() ?? string.Empty;
    canonicalUrl = ResolveCanonicalUrl(siteUri, canonicalUrl);

    var h1Count = document.DocumentNode.SelectNodes("//h1")?.Count ?? 0;
    var h2Count = document.DocumentNode.SelectNodes("//h2")?.Count ?? 0;

    var isIndexable = !HasNoIndexDirective(document, response);

    var viewportNode = document.DocumentNode.SelectSingleNode("//meta[translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='viewport']");
    var hasViewport = viewportNode is not null;
    var viewportContent = viewportNode?.GetAttributeValue("content", string.Empty)?.Trim() ?? string.Empty;

    var imageNodes = document.DocumentNode.SelectNodes("//img");
    var calculatedTotalImages = imageNodes?.Count ?? 0;
    var calculatedImagesWithoutAlt = 0;
    if (imageNodes is not null)
    {
        foreach (var image in imageNodes)
        {
            var alt = image.GetAttributeValue("alt", string.Empty);
            if (string.IsNullOrWhiteSpace(alt))
            {
                calculatedImagesWithoutAlt++;
            }
        }
    }

    var linkNodes = document.DocumentNode.SelectNodes("//a[@href]");
    var calculatedInternalLinks = 0;
    var calculatedExternalLinks = 0;
    if (linkNodes is not null)
    {
        foreach (var link in linkNodes)
        {
            var href = link.GetAttributeValue("href", string.Empty).Trim();
            if (ShouldSkipLink(href))
            {
                continue;
            }

            if (Uri.TryCreate(href, UriKind.Absolute, out var hrefUri))
            {
                if (string.Equals(hrefUri.Host, siteUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    calculatedInternalLinks++;
                }
                else
                {
                    calculatedExternalLinks++;
                }
            }
            else
            {
                calculatedInternalLinks++;
            }
        }
    }

    var hasLanguageAttribute = HasLanguageAttribute(document);
    var hasSkipLink = HasSkipLink(document);
    var landmarkCount = CountLandmarks(document);
    var formControlIssues = CountFormControlsWithoutLabels(document);

    var structuredDataScripts = document.DocumentNode.SelectNodes("//script[translate(@type,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='application/ld+json']");
    var structuredDataCount = structuredDataScripts?.Count ?? 0;
    var structuredDataTypes = ExtractStructuredDataTypes(structuredDataScripts);
    var hasOpenGraphTags = HasOpenGraphTags(document);
    var hasTwitterCard = HasTwitterCard(document);

    var totalImages = spaDomResult?.TotalImages ?? calculatedTotalImages;
    var imagesWithoutAlt = spaDomResult?.ImagesWithoutAlt ?? calculatedImagesWithoutAlt;
    var internalLinks = spaDomResult?.InternalLinkCount ?? calculatedInternalLinks;
    var externalLinks = spaDomResult?.ExternalLinkCount ?? calculatedExternalLinks;

    return new SeoResult(
        title,
        title.Length,
        metaDescription,
        metaDescription.Length,
        canonicalUrl,
        isIndexable,
        h1Count,
        h2Count,
        hasViewport,
        viewportContent,
        totalImages,
        imagesWithoutAlt,
        internalLinks,
        externalLinks,
        usesHttps,
        domFromHeadless,
        hasLanguageAttribute,
        hasSkipLink,
        landmarkCount,
        formControlIssues,
        structuredDataCount,
        structuredDataTypes,
        hasOpenGraphTags,
        hasTwitterCard);
}

static bool HasNoIndexDirective(HtmlDocument document, HttpResponseMessage? response)
{
    var metaNodes = document.DocumentNode.SelectNodes("//meta[translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='robots']");
    if (metaNodes is not null)
    {
        foreach (var node in metaNodes)
        {
            var content = node.GetAttributeValue("content", string.Empty);
            if (ContainsNoIndex(content))
            {
                return true;
            }
        }
    }

    if (response?.Headers.TryGetValues("X-Robots-Tag", out var headerValues) == true &&
        headerValues.Any(ContainsNoIndex))
    {
        return true;
    }

    if (response?.Content is { Headers: { } contentHeaders } &&
        contentHeaders.TryGetValues("X-Robots-Tag", out var contentHeaderValues) &&
        contentHeaderValues.Any(ContainsNoIndex))
    {
        return true;
    }

    return false;
}

static bool ContainsNoIndex(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var directives = value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    foreach (var directive in directives)
    {
        if (directive.Equals("noindex", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static ScoreResult CalculateScores(SeoResult seo, PerformanceResult? performance, NetworkResult network)
{
    var seoScore = CalculateSeoScore(seo);
    var speedScore = CalculateSpeedScore(performance, network);
    var overall = (int)Math.Round((speedScore * 0.6) + (seoScore * 0.4));
    return new ScoreResult(
        Math.Clamp(overall, 0, 100),
        Math.Clamp(seoScore, 0, 100),
        Math.Clamp(speedScore, 0, 100));
}

static int CalculateSeoScore(SeoResult seo)
{
    double weightedScore = 0;
    double totalWeight = 0;

    void AddScore(double weight, double? value)
    {
        if (value is null)
        {
            return;
        }

        weightedScore += weight * ClampScore(value.Value);
        totalWeight += weight;
    }

    AddScore(0.25, seo.IsIndexable ? 100 : 0);

    var metadataScore = (ScoreTextLength(seo.TitleLength, 35, 65) * 0.5) +
                        (ScoreTextLength(seo.MetaDescriptionLength, 80, 155) * 0.5);
    AddScore(0.2, metadataScore);

    double technicalScore = 0;
    technicalScore += seo.UsesHttps ? 45 : 10;
    technicalScore += seo.HasViewportMeta ? 35 : 0;
    technicalScore += string.IsNullOrWhiteSpace(seo.CanonicalUrl) ? 10 : 20;
    AddScore(0.15, ClampScore(technicalScore));

    var headingScore = seo.H1Count switch
    {
        0 => 20,
        1 => 100,
        _ => 70
    };
    var altScore = seo.TotalImages == 0
        ? 100
        : ClampScore(100 - ((double)seo.ImagesWithoutAlt / seo.TotalImages) * 100);
    var structuredScore = seo.StructuredDataCount > 0 ? 100 : 60;
    var contentStructure = (headingScore * 0.4) + (altScore * 0.35) + (structuredScore * 0.25);
    AddScore(0.2, contentStructure);

    double accessibilityScore = 100;
    if (!seo.HasLanguageAttribute)
    {
        accessibilityScore -= 20;
    }
    if (!seo.HasSkipLink)
    {
        accessibilityScore -= 10;
    }
    if (seo.LandmarkCount == 0)
    {
        accessibilityScore -= 10;
    }
    if (seo.FormControlsWithoutLabels > 0)
    {
        accessibilityScore -= Math.Min(40, seo.FormControlsWithoutLabels * 4);
    }
    AddScore(0.15, accessibilityScore);

    var socialScore = 0d;
    if (seo.HasOpenGraphTags)
    {
        socialScore += 60;
    }
    if (seo.HasTwitterCard)
    {
        socialScore += 40;
    }
    AddScore(0.05, socialScore);

    return totalWeight <= 0 ? 0 : (int)Math.Round(weightedScore / totalWeight);
}

static int CalculateSpeedScore(PerformanceResult? performance, NetworkResult network)
{
    if (network.StatusCode == 0 || network.StatusCode >= 400)
    {
        return 0;
    }

    if (performance is not null)
    {
        double weighted = 0;
        double totalWeight = 0;

        void AddMetric(double weight, double? score)
        {
            if (score is null)
            {
                return;
            }

            weighted += weight * score.Value;
            totalWeight += weight;
        }

        AddMetric(0.35, ScoreFromRangeNullable(performance.LargestContentfulPaintMs, 2500, 6000));
        AddMetric(0.15, ScoreFromRangeNullable(performance.FirstContentfulPaintMs, 1800, 4000));
        AddMetric(0.2, ScoreFromRangeNullable(performance.TotalBlockingTimeMs, 200, 900));
        AddMetric(0.15, ScoreFromRangeNullable(performance.CumulativeLayoutShift, 0.1, 0.25));
        AddMetric(0.15, ScoreFromRangeNullable(network.ResponseTimeMs, 800, 4000));

        if (totalWeight > 0)
        {
            return (int)Math.Round(weighted / totalWeight);
        }
    }

    return (int)Math.Round(ScoreFromRange(network.ResponseTimeMs, 800, 4000));
}

static double ScoreTextLength(int length, int idealMin, int idealMax)
{
    if (length <= 0)
    {
        return 0;
    }

    if (length >= idealMin && length <= idealMax)
    {
        return 100;
    }

    var diff = length < idealMin ? idealMin - length : length - idealMax;
    var penalty = Math.Min(70, diff * 2);
    return ClampScore(100 - penalty);
}

static double ClampScore(double value)
{
    return Math.Max(0, Math.Min(100, value));
}

static double? ScoreFromRangeNullable(double? value, double goodThreshold, double poorThreshold)
{
    return value.HasValue ? ScoreFromRange(value.Value, goodThreshold, poorThreshold) : null;
}

static double ScoreFromRange(double value, double goodThreshold, double poorThreshold)
{
    if (value <= goodThreshold)
    {
        return 100;
    }

    if (value >= poorThreshold)
    {
        return 0;
    }

    var ratio = (value - goodThreshold) / (poorThreshold - goodThreshold);
    return ClampScore(100 - (ratio * 100));
}

static AnalysisReport BuildReport(AnalysisResult analysis)
{
    var recommendations = BuildRecommendations(analysis);
    var checklist = BuildChecklistSections(analysis.AiInsights);

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

static IReadOnlyList<Recommendation> BuildRecommendations(AnalysisResult analysis)
{
    var recommendations = new List<Recommendation>();

    if (analysis.Network.StatusCode == 0)
    {
        recommendations.Add(new Recommendation(
            "Site unreachable",
            "The site did not respond successfully. Confirm DNS/hosting and try again.",
            "Network"));
    }
    else if (analysis.Network.StatusCode >= 400)
    {
        recommendations.Add(new Recommendation(
            "HTTP errors detected",
            $"The site returned status code {analysis.Network.StatusCode}. Resolve application errors for a successful response.",
            "Network"));
    }

    if (analysis.Performance?.Suggestions is { Count: > 0 } suggestions)
    {
        var top = suggestions
            .Where(s => s.EstimatedSavingsMs.HasValue)
            .OrderByDescending(s => s.EstimatedSavingsMs!.Value)
            .FirstOrDefault() ?? suggestions.First();

        recommendations.Add(new Recommendation(
            $"Performance: {top.Title}",
            top.Description ?? "Address this opportunity to improve load speed.",
            "Performance"));
    }
    else if (analysis.Score.Speed < 70)
    {
        recommendations.Add(new Recommendation(
            "Improve load times",
            "Average response times are elevated. Review caching, media compression, and third-party scripts.",
            "Performance"));
    }

    if (!analysis.Seo.IsIndexable)
    {
        recommendations.Add(new Recommendation(
            "Page blocked from indexing",
            "Robots directives indicate search engines cannot index this URL. Remove the noindex directive if this page should rank.",
            "SEO"));
    }

    if (!analysis.Seo.HasViewportMeta)
    {
        recommendations.Add(new Recommendation(
            "Missing mobile viewport",
            "Add a `<meta name=\"viewport\">` tag so mobile devices render the layout correctly.",
            "SEO"));
    }

    if (!analysis.Seo.UsesHttps)
    {
        recommendations.Add(new Recommendation(
            "Upgrade to HTTPS",
            "Serve the page over HTTPS to boost trust, rankings, and security.",
            "SEO"));
    }

    if (analysis.Seo.TotalImages > 0 && analysis.Seo.ImagesWithoutAlt > 0)
    {
        recommendations.Add(new Recommendation(
            "Add image alt text",
            $"Found {analysis.Seo.ImagesWithoutAlt} images without descriptive `alt` text. Add alt attributes for accessibility and relevance.",
            "SEO"));
    }

    if (!analysis.Seo.HasLanguageAttribute)
    {
        recommendations.Add(new Recommendation(
            "Missing language attribute",
            "Add a `lang` attribute to the `<html>` element so assistive technologies know which language to use.",
            "Accessibility"));
    }

    if (analysis.Seo.FormControlsWithoutLabels > 0)
    {
        recommendations.Add(new Recommendation(
            "Add form labels",
            $"Detected {analysis.Seo.FormControlsWithoutLabels} form controls without associated labels. Provide labels or aria attributes to describe each field.",
            "Accessibility"));
    }

    return recommendations;
}

static IReadOnlyList<ChecklistSection> BuildChecklistSections(AiInsightsResult? aiInsights)
{
    if (aiInsights is null || string.IsNullOrWhiteSpace(aiInsights.Recommendations))
    {
        return Array.Empty<ChecklistSection>();
    }

    var sections = new List<ChecklistSection>();
    var currentTitle = "Checklist";
    var currentItems = new List<string>();

    void PushSection()
    {
        if (currentItems.Count > 0)
        {
            sections.Add(new ChecklistSection(currentTitle, currentItems.ToArray()));
            currentItems = new List<string>();
        }
    }

    var lines = aiInsights.Recommendations
        .Split(new[] { '\r', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    foreach (var rawLine in lines)
    {
        var line = rawLine?.Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var markdownHeading = Regex.Match(line, @"^#{1,6}\s*(.+)$");
        if (markdownHeading.Success)
        {
            PushSection();
            currentTitle = markdownHeading.Groups[1].Value.Trim();
            continue;
        }

        var explicitHeading = Regex.Match(line, @"^(.+?)[：:]\s*$");
        if (explicitHeading.Success && explicitHeading.Groups[1].Value.Trim().Length > 1)
        {
            PushSection();
            currentTitle = explicitHeading.Groups[1].Value.Trim();
            continue;
        }

        var bulletMatch = Regex.Match(line, @"^(?:[-*•]\s+|\d+\.\s+|\[\s?[xX]?\]\s+)(.+)$");
        var normalized = bulletMatch.Success ? bulletMatch.Groups[1].Value.Trim() : line;
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            currentItems.Add(normalized);
        }
    }

    PushSection();
    return sections;
}

static string ResolveCanonicalUrl(Uri normalizedUri, string canonicalUrl)
{
    if (string.IsNullOrWhiteSpace(canonicalUrl))
    {
        return string.Empty;
    }

    if (Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var absoluteUri))
    {
        return absoluteUri.ToString();
    }

    if (Uri.TryCreate(normalizedUri, canonicalUrl, out var combined))
    {
        return combined.ToString();
    }

    return canonicalUrl;
}

static bool ShouldSkipLink(string href)
{
    if (string.IsNullOrWhiteSpace(href))
    {
        return true;
    }

    if (href.StartsWith("#", StringComparison.Ordinal))
    {
        return true;
    }

    if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
        href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return false;
}

static bool HasLanguageAttribute(HtmlDocument document)
{
    var htmlNode = document.DocumentNode.SelectSingleNode("//html");
    if (htmlNode is null)
    {
        return false;
    }

    var lang = htmlNode.GetAttributeValue("lang", string.Empty);
    return !string.IsNullOrWhiteSpace(lang);
}

static bool HasSkipLink(HtmlDocument document)
{
    var links = document.DocumentNode.SelectNodes("//a[@href]");
    if (links is null)
    {
        return false;
    }

    foreach (var link in links)
    {
        var href = link.GetAttributeValue("href", string.Empty);
        var text = HtmlEntity.DeEntitize(link.InnerText ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (href.StartsWith("#", StringComparison.Ordinal) &&
            (href.Contains("main", StringComparison.OrdinalIgnoreCase) ||
             href.Contains("content", StringComparison.OrdinalIgnoreCase) ||
             href.Contains("skip", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(text) &&
            (text.Contains("skip to content") ||
             text.Contains("skip navigation") ||
             text.StartsWith("skip", StringComparison.Ordinal)))
        {
            return true;
        }
    }

    return false;
}

static int CountLandmarks(HtmlDocument document)
{
    var nodes = document.DocumentNode.SelectNodes("//header|//nav|//main|//aside|//footer|//*[@role='main']|//*[@role='banner']|//*[@role='navigation']|//*[@role='contentinfo']|//*[@role='complementary']");
    return nodes?.Count ?? 0;
}

static int CountFormControlsWithoutLabels(HtmlDocument document)
{
    var count = 0;

    var inputs = document.DocumentNode.SelectNodes("//input");
    if (inputs is not null)
    {
        foreach (var input in inputs)
        {
            var type = input.GetAttributeValue("type", "text").ToLowerInvariant();
            if (type is "hidden" or "submit" or "button" or "reset" or "image")
            {
                continue;
            }

            if (!IsFormControlLabeled(input, document))
            {
                count++;
            }
        }
    }

    var textareas = document.DocumentNode.SelectNodes("//textarea");
    if (textareas is not null)
    {
        foreach (var textarea in textareas)
        {
            if (!IsFormControlLabeled(textarea, document))
            {
                count++;
            }
        }
    }

    var selects = document.DocumentNode.SelectNodes("//select");
    if (selects is not null)
    {
        foreach (var select in selects)
        {
            if (!IsFormControlLabeled(select, document))
            {
                count++;
            }
        }
    }

    return count;
}

static bool IsFormControlLabeled(HtmlNode control, HtmlDocument document)
{
    var id = control.GetAttributeValue("id", string.Empty);
    if (!string.IsNullOrWhiteSpace(id))
    {
        var label = document.DocumentNode.SelectSingleNode($"//label[@for='{id}']");
        if (label is not null)
        {
            return true;
        }
    }

    if (control.Ancestors("label").Any())
    {
        return true;
    }

    var ariaLabel = control.GetAttributeValue("aria-label", string.Empty);
    var ariaLabelledBy = control.GetAttributeValue("aria-labelledby", string.Empty);
    if (!string.IsNullOrWhiteSpace(ariaLabel) || !string.IsNullOrWhiteSpace(ariaLabelledBy))
    {
        return true;
    }

    return false;
}

static IReadOnlyList<string> ExtractStructuredDataTypes(HtmlNodeCollection? scripts)
{
    if (scripts is null || scripts.Count == 0)
    {
        return Array.Empty<string>();
    }

    var types = new List<string>();
    foreach (var script in scripts)
    {
        var json = script.InnerText?.Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            continue;
        }

        foreach (var type in ParseSchemaTypes(json))
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                types.Add(type);
            }
        }
    }

    if (types.Count == 0)
    {
        return Array.Empty<string>();
    }

    return types
        .Select(t => t.Trim())
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(10)
        .ToArray();
}

static IEnumerable<string> ParseSchemaTypes(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        var collected = new List<string>();
        CollectSchemaTypes(document.RootElement, collected);
        return collected;
    }
    catch
    {
        return Array.Empty<string>();
    }
}

static void CollectSchemaTypes(JsonElement element, ICollection<string> output)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            {
                if (element.TryGetProperty("@type", out var typeElement))
                {
                    if (typeElement.ValueKind == JsonValueKind.String)
                    {
                        var value = typeElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            output.Add(value);
                        }
                    }
                    else if (typeElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var nested in typeElement.EnumerateArray())
                        {
                            if (nested.ValueKind == JsonValueKind.String)
                            {
                                var value = nested.GetString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    output.Add(value);
                                }
                            }
                        }
                    }
                }

                if (element.TryGetProperty("@graph", out var graphElement))
                {
                    CollectSchemaTypes(graphElement, output);
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectSchemaTypes(property.Value, output);
                }

                break;
            }
        case JsonValueKind.Array:
            {
                foreach (var child in element.EnumerateArray())
                {
                    CollectSchemaTypes(child, output);
                }

                break;
            }
    }
}

static bool HasOpenGraphTags(HtmlDocument document)
{
    var nodes = document.DocumentNode.SelectNodes("//meta[@property]");
    if (nodes is null)
    {
        return false;
    }

    foreach (var node in nodes)
    {
        var property = node.GetAttributeValue("property", string.Empty);
        if (property.StartsWith("og:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static bool HasTwitterCard(HtmlDocument document)
{
    var nodes = document.DocumentNode.SelectNodes("//meta[@name]");
    if (nodes is null)
    {
        return false;
    }

    foreach (var node in nodes)
    {
        var name = node.GetAttributeValue("name", string.Empty);
        if (name.StartsWith("twitter:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

public record WebsiteAnalysisRequest(string Url);

public record NetworkResult(
    string Url,
    int StatusCode,
    long ResponseTimeMs,
    DateTime CheckedAtUtc,
    string? ErrorMessage);

public record SeoResult(
    string Title,
    int TitleLength,
    string MetaDescription,
    int MetaDescriptionLength,
    string CanonicalUrl,
    bool IsIndexable,
    int H1Count,
    int H2Count,
    bool HasViewportMeta,
    string ViewportContent,
    int TotalImages,
    int ImagesWithoutAlt,
    int InternalLinkCount,
    int ExternalLinkCount,
    bool UsesHttps,
    bool DomFromHeadlessBrowser,
    bool HasLanguageAttribute,
    bool HasSkipLink,
    int LandmarkCount,
    int FormControlsWithoutLabels,
    int StructuredDataCount,
    IReadOnlyList<string> StructuredDataTypes,
    bool HasOpenGraphTags,
    bool HasTwitterCard)
{
    public static SeoResult Empty { get; } = new(
        string.Empty,
        0,
        string.Empty,
        0,
        string.Empty,
        true,
        0,
        0,
        false,
        string.Empty,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        false,
        0,
        0,
        0,
        Array.Empty<string>(),
        false,
        false);
}

public record AnalysisResult(
    string Url,
    DateTime CheckedAtUtc,
    NetworkResult Network,
    SeoResult Seo,
    ScoreResult Score,
    PerformanceResult? Performance,
    OffPageSeoResult? OffPageSeo,
    AiInsightsResult? AiInsights);

public record ScoreResult(int Overall, int Seo, int Speed);

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

public record OffPageSeoResult(
    double? DomainAuthority,
    int? Backlinks,
    int? ReferringDomains,
    double? SpamScore);

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

public record AiInsightsResult(string Recommendations);
