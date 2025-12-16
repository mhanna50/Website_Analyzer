using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using HtmlAgilityPack;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services.Seo;

public static class SeoBuilder
{
    public static SeoResult BuildSeoResult(
        Uri siteUri,
        string html,
        HttpResponseMessage? response,
        SpaDomAnalysisResult? spaDomResult,
        out IReadOnlyList<string> crawlableLinks)
    {
        crawlableLinks = Array.Empty<string>();
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
        var crawlableLinkTargets = new List<string>();
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
                crawlableLinkTargets.Add(hrefUri.ToString());
                if (string.Equals(hrefUri.Host, siteUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    calculatedInternalLinks++;
                }
                else
                {
                    calculatedExternalLinks++;
                }
            }
            else if (Uri.TryCreate(siteUri, href, out var resolved))
            {
                crawlableLinkTargets.Add(resolved.ToString());
                calculatedInternalLinks++;
            }
            else
            {
                calculatedInternalLinks++;
            }
        }
        }
        crawlableLinks = crawlableLinkTargets;

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
            hasTwitterCard,
            0,
            Array.Empty<BrokenLink>());
    }

    private static bool HasNoIndexDirective(HtmlDocument document, HttpResponseMessage? response)
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

    private static bool ContainsNoIndex(string? value)
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

    private static string ResolveCanonicalUrl(Uri normalizedUri, string canonicalUrl)
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

    private static bool ShouldSkipLink(string href)
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

    private static bool HasLanguageAttribute(HtmlDocument document)
    {
        var htmlNode = document.DocumentNode.SelectSingleNode("//html");
        if (htmlNode is null)
        {
            return false;
        }

        var lang = htmlNode.GetAttributeValue("lang", string.Empty);
        return !string.IsNullOrWhiteSpace(lang);
    }

    private static bool HasSkipLink(HtmlDocument document)
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

    private static int CountLandmarks(HtmlDocument document)
    {
        var nodes = document.DocumentNode.SelectNodes("//header|//nav|//main|//aside|//footer|//*[@role='main']|//*[@role='banner']|//*[@role='navigation']|//*[@role='contentinfo']|//*[@role='complementary']");
        return nodes?.Count ?? 0;
    }

    private static int CountFormControlsWithoutLabels(HtmlDocument document)
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

    private static bool IsFormControlLabeled(HtmlNode control, HtmlDocument document)
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

    private static IReadOnlyList<string> ExtractStructuredDataTypes(HtmlNodeCollection? scripts)
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

    private static IEnumerable<string> ParseSchemaTypes(string json)
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

    private static void CollectSchemaTypes(JsonElement element, ICollection<string> output)
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

    private static bool HasOpenGraphTags(HtmlDocument document)
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

    private static bool HasTwitterCard(HtmlDocument document)
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
}
