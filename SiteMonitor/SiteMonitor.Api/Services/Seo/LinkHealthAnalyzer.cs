using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services.Seo;

public class LinkHealthAnalyzer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LinkHealthAnalyzer> _logger;

    public LinkHealthAnalyzer(
        IHttpClientFactory httpClientFactory,
        ILogger<LinkHealthAnalyzer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BrokenLink>> FindBrokenLinksAsync(
        Uri siteUri,
        IReadOnlyList<string> links,
        ScanMode mode,
        CancellationToken cancellationToken)
    {
        if (links.Count == 0)
        {
            return Array.Empty<BrokenLink>();
        }

        var distinctLinks = links
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var maxLinks = mode == ScanMode.Fast ? 10 : 30;
        var targets = distinctLinks.Take(maxLinks).ToArray();
        if (targets.Length == 0)
        {
            return Array.Empty<BrokenLink>();
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(mode == ScanMode.Fast ? 5 : 12);

        var broken = new ConcurrentBag<BrokenLink>();
        var concurrency = mode == ScanMode.Fast ? 2 : 5;
        using var semaphore = new SemaphoreSlim(concurrency);
        var tasks = new List<Task>();

        foreach (var link in targets)
        {
            await semaphore.WaitAsync(cancellationToken);
            tasks.Add(CheckLinkAsync(client, siteUri, link, broken, semaphore, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return broken
            .OrderByDescending(b => b.IsInternal)
            .ThenBy(b => b.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task CheckLinkAsync(
        HttpClient client,
        Uri siteUri,
        string url,
        ConcurrentBag<BrokenLink> broken,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(head, cancellationToken);
            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, url);
                using var getResponse = await client.SendAsync(get, cancellationToken);
                if (!getResponse.IsSuccessStatusCode)
                {
                    broken.Add(CreateBrokenLink(siteUri, url, (int)getResponse.StatusCode, getResponse.ReasonPhrase));
                }
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                broken.Add(CreateBrokenLink(siteUri, url, (int)response.StatusCode, response.ReasonPhrase));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Link check failed for {Url}", url);
            broken.Add(CreateBrokenLink(siteUri, url, 0, ex.Message));
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static BrokenLink CreateBrokenLink(Uri siteUri, string url, int statusCode, string? reason)
    {
        return new BrokenLink(url, IsInternal(siteUri, url), statusCode, reason);
    }

    private static bool IsInternal(Uri siteUri, string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            ? string.Equals(parsed.Host, siteUri.Host, StringComparison.OrdinalIgnoreCase)
            : false;
    }
}
