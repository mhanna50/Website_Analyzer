using System.Net.Http.Headers;
using System.Text.Json;

namespace SiteMonitor.Api.Services;

public class OffPageSeoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OffPageSeoService> _logger;

    public OffPageSeoService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OffPageSeoService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OffPageSeoResult?> GetMetricsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["SEO_API_KEY"];
        var baseUrl = _configuration["SEO_API_BASE_URL"];

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var requestUrl = $"{baseUrl}{separator}domain={Uri.EscapeDataString(domain)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Off-page SEO API returned {Status} for {Domain}", response.StatusCode, domain);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            var metricsSource = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataNode)
                ? dataNode
                : root;

            var domainAuthority = TryGetDouble(metricsSource, "domainAuthority");
            var backlinks = TryGetInt(metricsSource, "backlinks");
            var referringDomains = TryGetInt(metricsSource, "referringDomains");
            var spamScore = TryGetDouble(metricsSource, "spamScore");

            return new OffPageSeoResult(domainAuthority, backlinks, referringDomains, spamScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve off-page SEO metrics for {Domain}", domain);
            return null;
        }
    }

    private static double? TryGetDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number)
        {
            return property.GetDouble();
        }

        return null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt32(out var exact))
            {
                return exact;
            }

            return (int)Math.Round(property.GetDouble());
        }

        return null;
    }
}
