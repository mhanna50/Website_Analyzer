using System;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services.Scoring;

public static class ScoreCalculator
{
    public static ScoreResult CalculateScores(SeoResult seo, PerformanceResult? performance, NetworkResult network)
    {
        var seoScore = CalculateSeoScore(seo);
        var speedScore = CalculateSpeedScore(performance, network);
        var overall = (int)Math.Round((speedScore * 0.6) + (seoScore * 0.4));
        return new ScoreResult(
            Math.Clamp(overall, 0, 100),
            Math.Clamp(seoScore, 0, 100),
            Math.Clamp(speedScore, 0, 100));
    }

    public static int CalculateSeoScore(SeoResult seo)
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

    public static int CalculateSpeedScore(PerformanceResult? performance, NetworkResult network)
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

    private static double ScoreTextLength(int length, int idealMin, int idealMax)
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

    private static double ClampScore(double value)
    {
        return Math.Max(0, Math.Min(100, value));
    }

    private static double? ScoreFromRangeNullable(double? value, double goodThreshold, double poorThreshold)
    {
        return value.HasValue ? ScoreFromRange(value.Value, goodThreshold, poorThreshold) : null;
    }

    private static double ScoreFromRange(double value, double goodThreshold, double poorThreshold)
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
}
