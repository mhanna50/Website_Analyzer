using Microsoft.Playwright;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services;

public class SpaDomAnalyzer
{
    public async Task<SpaDomAnalysisResult> AnalyzeAsync(string url, string targetHost)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000
            });

            var totalImages = 0;
            var imagesWithoutAlt = 0;
            var imageElements = await page.QuerySelectorAllAsync("img");
            totalImages = imageElements.Count;

            foreach (var image in imageElements)
            {
                var alt = await image.GetAttributeAsync("alt");
                if (string.IsNullOrWhiteSpace(alt))
                {
                    imagesWithoutAlt++;
                }
            }

            var internalLinks = 0;
            var externalLinks = 0;
            var linkElements = await page.QuerySelectorAllAsync("a[href]");

            foreach (var link in linkElements)
            {
                var href = (await link.GetAttributeAsync("href"))?.Trim();
                if (string.IsNullOrWhiteSpace(href) || ShouldSkipLink(href))
                {
                    continue;
                }

                if (Uri.IsWellFormedUriString(href, UriKind.Absolute))
                {
                    try
                    {
                        var uri = new Uri(href);
                        if (uri.Host.Equals(targetHost, StringComparison.OrdinalIgnoreCase))
                        {
                            internalLinks++;
                        }
                        else
                        {
                            externalLinks++;
                        }
                    }
                    catch
                    {
                        // ignore malformed absolute URLs
                    }
                }
                else
                {
                    internalLinks++;
                }
            }

            return new SpaDomAnalysisResult(totalImages, imagesWithoutAlt, internalLinks, externalLinks);
        }
        finally
        {
            try
            {
                await page.CloseAsync();
            }
            catch
            {
                // ignore page close failures
            }
        }
    }

    private static bool ShouldSkipLink(string href)
    {
        if (href.StartsWith("#", StringComparison.Ordinal) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
