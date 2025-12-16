using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services;

public class AiInsightsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiInsightsService> _logger;

    public AiInsightsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiInsightsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiInsightsResult?> GenerateInsightsAsync(
        AnalysisResult analysis,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var endpoint = _configuration["OPENAI_API_BASE_URL"] ?? "https://api.openai.com/v1/chat/completions";
        var model = _configuration["OPENAI_MODEL"] ?? "gpt-4o-mini";

        const string systemPrompt =
            "You are an expert website performance and SEO analyst. Use the provided crawl metrics to craft a site-specific optimization checklist. Cite the measured values or markup (e.g., 4.2s LCP, missing viewport meta). Output Markdown with '## Performance' and '## SEO' sections. Keep each step concise but actionable. Never start the response with {} or [] and avoid any leading special characters besides ':' in the final text.";

        var payload = new
        {
            model,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = BuildPrompt(analysis) }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI API returned {StatusCode}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentElement))
                {
                    var content = contentElement.GetString();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        return new AiInsightsResult(content.Trim());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI recommendations.");
        }

        return null;
    }

    private static string BuildPrompt(AnalysisResult analysis)
    {
        var host = ExtractHost(analysis.Url);
        var builder = new StringBuilder();
        builder.AppendLine($"Host: {host}");
        builder.AppendLine($"URL: {analysis.Url}");
        builder.AppendLine($"HTTP status: {analysis.Network.StatusCode}");
        builder.AppendLine($"Network error: {analysis.Network.ErrorMessage ?? "None"}");
        builder.AppendLine($"Response time (ms): {analysis.Network.ResponseTimeMs}");
        builder.AppendLine($"Redirect count: {analysis.Network.RedirectCount}");
        builder.AppendLine($"Performance score: {analysis.Score.Speed}");
        builder.AppendLine($"SEO score: {analysis.Score.Seo}");

        if (analysis.Performance?.Mobile is { } mobile)
        {
            builder.AppendLine($"Mobile score: {mobile.Score ?? 0}");
            builder.AppendLine($"Mobile Largest Contentful Paint (ms): {mobile.LargestContentfulPaintMs ?? 0}");
            builder.AppendLine($"Mobile First Contentful Paint (ms): {mobile.FirstContentfulPaintMs ?? 0}");
            builder.AppendLine($"Mobile Cumulative Layout Shift: {mobile.CumulativeLayoutShift ?? 0}");
            builder.AppendLine($"Mobile Total Blocking Time (ms): {mobile.TotalBlockingTimeMs ?? 0}");
        }

        if (analysis.Performance?.Desktop is { } desktop)
        {
            builder.AppendLine($"Desktop score: {desktop.Score ?? 0}");
            builder.AppendLine($"Desktop Largest Contentful Paint (ms): {desktop.LargestContentfulPaintMs ?? 0}");
            builder.AppendLine($"Desktop First Contentful Paint (ms): {desktop.FirstContentfulPaintMs ?? 0}");
            builder.AppendLine($"Desktop Cumulative Layout Shift: {desktop.CumulativeLayoutShift ?? 0}");
            builder.AppendLine($"Desktop Total Blocking Time (ms): {desktop.TotalBlockingTimeMs ?? 0}");
        }

        if (analysis.Performance?.Suggestions is { Count: > 0 } perfSuggestions)
        {
            builder.AppendLine("PageSpeed suggestions:");
            foreach (var suggestion in perfSuggestions.Take(5))
            {
                builder.AppendLine(
                    $"- {suggestion.Title} :: {suggestion.Description} (estimated savings ms: {suggestion.EstimatedSavingsMs?.ToString() ?? "n/a"})");
            }
        }

        builder.AppendLine($"Title text: {FormatSnippet(analysis.Seo.Title)}");
        builder.AppendLine($"Title length: {analysis.Seo.TitleLength}");
        builder.AppendLine($"Meta description: {FormatSnippet(analysis.Seo.MetaDescription)}");
        builder.AppendLine($"Meta description length: {analysis.Seo.MetaDescriptionLength}");
        builder.AppendLine($"Canonical URL: {FormatSnippet(analysis.Seo.CanonicalUrl)}");
        builder.AppendLine($"Indexable: {analysis.Seo.IsIndexable}");
        builder.AppendLine($"Viewport meta present: {analysis.Seo.HasViewportMeta}");
        builder.AppendLine($"Viewport content: {FormatSnippet(analysis.Seo.ViewportContent)}");
        builder.AppendLine($"HTTPS: {analysis.Seo.UsesHttps}");
        builder.AppendLine($"H1 count: {analysis.Seo.H1Count}");
        builder.AppendLine($"H2 count: {analysis.Seo.H2Count}");
        builder.AppendLine($"Internal links: {analysis.Seo.InternalLinkCount}");
        builder.AppendLine($"External links: {analysis.Seo.ExternalLinkCount}");
        builder.AppendLine($"Images without alt text: {analysis.Seo.ImagesWithoutAlt}/{analysis.Seo.TotalImages}");
        builder.AppendLine($"Structured data blocks: {analysis.Seo.StructuredDataCount}");
        builder.AppendLine($"Structured data types: {string.Join(", ", analysis.Seo.StructuredDataTypes)}");
        builder.AppendLine($"Open Graph tags: {analysis.Seo.HasOpenGraphTags}");
        builder.AppendLine($"Twitter card: {analysis.Seo.HasTwitterCard}");
        builder.AppendLine($"Language attribute present: {analysis.Seo.HasLanguageAttribute}");
        builder.AppendLine($"Skip link present: {analysis.Seo.HasSkipLink}");
        builder.AppendLine($"Landmark count: {analysis.Seo.LandmarkCount}");
        builder.AppendLine($"Form controls without labels: {analysis.Seo.FormControlsWithoutLabels}");

        if (analysis.OffPageSeo is { } offPage)
        {
            builder.AppendLine($"Domain authority: {offPage.DomainAuthority ?? 0}");
            builder.AppendLine($"Backlinks: {offPage.Backlinks ?? 0}");
            builder.AppendLine($"Referring domains: {offPage.ReferringDomains ?? 0}");
            builder.AppendLine($"Spam score: {offPage.SpamScore ?? 0}");
        }

        builder.AppendLine();
        builder.AppendLine("Instructions:");
        builder.AppendLine($"- Reference \"{host}\" or \"{analysis.Url}\" explicitly in every checklist item.");
        builder.AppendLine("- Quote the measured values or markup names when describing the fix.");
        builder.AppendLine("- Explain why resolving the issue matters for that metric.");
        builder.AppendLine("- Include at least one concrete step that describes how to execute the fix for each issue.");
        builder.AppendLine("- Only highlight issues that the above data exposes; avoid generic filler.");
        builder.AppendLine("- Format output exactly as markdown with '## Performance' and '## SEO' sections containing '- [ ]' bullets.");
        builder.AppendLine("- Return as many or as few checklist bullets as the data requires; skip padding.");
        builder.AppendLine("- Do not output code fences or raw HTML snippets; summarize the required fix in plain language.");

        return builder.ToString();
    }

    private static string ExtractHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
    }

    private static string FormatSnippet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Missing";
        }

        var singleLine = value.ReplaceLineEndings(" ").Trim();
        return singleLine.Length > 280 ? $"{singleLine[..277]}..." : singleLine;
    }
}
