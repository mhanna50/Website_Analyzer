using System.Collections.Generic;
using System.Text.Json;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services;

public class PerformanceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PerformanceService> _logger;

    public PerformanceService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PerformanceService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PerformanceResult?> AnalyzeAsync(string url, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["PERFORMANCE_API_KEY"];
        var baseUrl = _configuration["PERFORMANCE_API_BASE_URL"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var preferredStrategy = _configuration["PERFORMANCE_API_STRATEGY"];
        var strategy = string.IsNullOrWhiteSpace(preferredStrategy)
            ? "mobile"
            : preferredStrategy;

        var snapshot = await FetchSnapshotAsync(baseUrl, apiKey, url, strategy, cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        return new PerformanceResult(
            snapshot.Score,
            strategy.Equals("mobile", StringComparison.OrdinalIgnoreCase) ? snapshot.Score : null,
            strategy.Equals("desktop", StringComparison.OrdinalIgnoreCase) ? snapshot.Score : null,
            snapshot.LargestContentfulPaintMs,
            snapshot.FirstContentfulPaintMs,
            snapshot.CumulativeLayoutShift,
            snapshot.TotalBlockingTimeMs,
            snapshot.Suggestions);
    }

    private async Task<PerformanceSnapshot?> FetchSnapshotAsync(
        string baseUrl,
        string apiKey,
        string url,
        string strategy,
        CancellationToken cancellationToken)
    {
        try
        {
            var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            var requestUrl =
                $"{baseUrl}{separator}url={Uri.EscapeDataString(url)}&strategy={strategy}&key={Uri.EscapeDataString(apiKey)}";

            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Performance API returned {Status} for {Url} ({Strategy})",
                    response.StatusCode, url, strategy);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!TryGetLighthouseResult(root, out var lighthouse))
            {
                return null;
            }

            var score = TryGetScore(lighthouse);
            var lcp = TryGetAuditMetric(lighthouse, "largest-contentful-paint");
            var fcp = TryGetAuditMetric(lighthouse, "first-contentful-paint");
            var cls = TryGetAuditMetric(lighthouse, "cumulative-layout-shift");
            var tbt = TryGetAuditMetric(lighthouse, "total-blocking-time");

            var suggestions = ExtractSuggestions(lighthouse);

            return new PerformanceSnapshot(score, lcp, fcp, cls, tbt, suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve performance metrics for {Url} ({Strategy})", url, strategy);
            return null;
        }
    }

    private static bool TryGetLighthouseResult(JsonElement root, out JsonElement lighthouseResult)
    {
        if (root.TryGetProperty("lighthouseResult", out lighthouseResult))
        {
            return true;
        }

        lighthouseResult = default;
        return false;
    }

    private static int? TryGetScore(JsonElement lighthouseResult)
    {
        if (lighthouseResult.TryGetProperty("categories", out var categories) &&
            categories.TryGetProperty("performance", out var performance) &&
            performance.TryGetProperty("score", out var scoreElement) &&
            scoreElement.ValueKind == JsonValueKind.Number)
        {
            var scoreValue = scoreElement.GetDouble();
            return (int)Math.Round(scoreValue * 100);
        }

        return null;
    }

    private static double? TryGetAuditMetric(JsonElement lighthouseResult, string auditName)
    {
        if (lighthouseResult.TryGetProperty("audits", out var audits) &&
            audits.TryGetProperty(auditName, out var audit) &&
            audit.TryGetProperty("numericValue", out var metricValue) &&
            metricValue.ValueKind == JsonValueKind.Number)
        {
            return metricValue.GetDouble();
        }

        return null;
    }

    private static IReadOnlyList<PerformanceSuggestion> ExtractSuggestions(JsonElement lighthouseResult)
    {
        if (!lighthouseResult.TryGetProperty("audits", out var audits))
        {
            return Array.Empty<PerformanceSuggestion>();
        }

        var suggestions = new List<PerformanceSuggestion>();

        foreach (var auditProperty in audits.EnumerateObject())
        {
            var audit = auditProperty.Value;

            if (!audit.TryGetProperty("details", out var details) ||
                details.ValueKind != JsonValueKind.Object ||
                !details.TryGetProperty("type", out var typeElement) ||
                typeElement.GetString() is not { } type ||
                !type.Equals("opportunity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var title = audit.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString() ?? auditProperty.Name
                : auditProperty.Name;

            var description = audit.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString()
                : null;

            double? score = null;
            if (audit.TryGetProperty("score", out var scoreElement) && scoreElement.ValueKind == JsonValueKind.Number)
            {
                score = Math.Round(scoreElement.GetDouble() * 100);
            }

            double? savings = null;
            if (details.TryGetProperty("overallSavingsMs", out var savingsElement) &&
                savingsElement.ValueKind == JsonValueKind.Number)
            {
                savings = savingsElement.GetDouble();
            }

            suggestions.Add(new PerformanceSuggestion(title, description, score, savings));
        }

        return suggestions;
    }

    private sealed record PerformanceSnapshot(
        int? Score,
        double? LargestContentfulPaintMs,
        double? FirstContentfulPaintMs,
        double? CumulativeLayoutShift,
        double? TotalBlockingTimeMs,
        IReadOnlyList<PerformanceSuggestion> Suggestions);
}
