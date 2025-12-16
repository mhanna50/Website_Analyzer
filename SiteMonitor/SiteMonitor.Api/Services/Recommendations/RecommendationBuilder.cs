using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services.Recommendations;

public static class RecommendationBuilder
{
    public static IReadOnlyList<Recommendation> BuildRecommendations(AnalysisResult analysis)
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

        if (analysis.Seo.BrokenLinkCount > 0)
        {
            var sample = analysis.Seo.BrokenLinks.Take(3).Select(l => l.Url);
            var example = string.Join(", ", sample);
            recommendations.Add(new Recommendation(
                "Repair broken links",
                $"Found {analysis.Seo.BrokenLinkCount} broken links (e.g., {example}). Update or remove them to avoid crawl waste and 404 UX traps.",
                "SEO"));
        }

        return recommendations;
    }

    public static IReadOnlyList<ChecklistSection> BuildChecklistSections(AiInsightsResult? aiInsights)
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
}
