using System.Net;
using System.Text;
using Microsoft.Playwright;
using SiteMonitor.Api.Models;

namespace SiteMonitor.Api.Services;

public class ReportRenderer
{
    private readonly ILogger<ReportRenderer> _logger;

    public ReportRenderer(ILogger<ReportRenderer> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> RenderScreenshotAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1200, Height = 800 }
        });

        var html = BuildHtml(report);

        await page.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await page.WaitForTimeoutAsync(500);

        var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = true,
            Type = ScreenshotType.Png
        });

        await page.CloseAsync();

        if (screenshot is null)
        {
            throw new InvalidOperationException("Failed to capture report screenshot.");
        }

        return screenshot;
    }

    public async Task<byte[]> RenderPdfAsync(AnalysisReport report, CancellationToken cancellationToken = default)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1200, Height = 1600 }
        });

        var html = BuildHtml(report);

        await page.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await page.WaitForTimeoutAsync(500);

        var pdf = await page.PdfAsync(new PagePdfOptions
        {
            Format = "Letter",
            PrintBackground = true
        });

        await page.CloseAsync();

        if (pdf is null)
        {
            throw new InvalidOperationException("Failed to capture report PDF.");
        }

        return pdf;
    }

    private static string BuildHtml(AnalysisReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine("<title>Website Report</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body { font-family: 'Segoe UI', Roboto, sans-serif; background:#f3f4f6; margin:0; padding:24px; color:#0f172a; }");
        builder.AppendLine(".report { max-width:960px; margin:0 auto; background:#fff; border-radius:16px; padding:32px; box-shadow:0 20px 60px rgba(15,23,42,0.15); }");
        builder.AppendLine(".title { margin:0 0 4px; font-size:24px; font-weight:700; }");
        builder.AppendLine(".subtitle { margin:0; color:#475569; font-size:14px; }");
        builder.AppendLine(".score-grid { display:flex; gap:16px; margin:24px 0; }");
        builder.AppendLine(".score-card { flex:1; background:#f8fafc; border-radius:12px; padding:16px; border:1px solid #e2e8f0; }");
        builder.AppendLine(".score-card h3 { margin:0; font-size:14px; text-transform:uppercase; color:#475569; }");
        builder.AppendLine(".score-value { font-size:36px; margin:8px 0 0; font-weight:700; }");
        builder.AppendLine(".section { margin-top:32px; }");
        builder.AppendLine(".section h2 { margin:0 0 16px; font-size:18px; }");
        builder.AppendLine(".grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:16px; }");
        builder.AppendLine(".card { border:1px solid #e2e8f0; border-radius:12px; padding:16px; background:#fff; }");
        builder.AppendLine(".card h4 { margin:0 0 4px; font-size:14px; text-transform:uppercase; color:#475569; }");
        builder.AppendLine(".card p { margin:0; font-size:18px; font-weight:600; }");
        builder.AppendLine(".recommendations { list-style:none; margin:0; padding:0; display:flex; flex-direction:column; gap:12px; }");
        builder.AppendLine(".recommendations li { border:1px solid #e2e8f0; border-radius:12px; padding:16px; background:#f8fafc; }");
        builder.AppendLine(".recommendations strong { display:block; margin-bottom:4px; font-size:15px; }");
        builder.AppendLine(".checklist-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(280px,1fr)); gap:16px; }");
        builder.AppendLine(".checklist-section { border:1px solid #e2e8f0; border-radius:12px; padding:16px; background:#f8fafc; }");
        builder.AppendLine(".checklist-section h4 { margin:0 0 8px; font-size:14px; text-transform:uppercase; color:#475569; }");
        builder.AppendLine(".checklist-section ul { margin:0; padding-left:18px; color:#0f172a; }");
        builder.AppendLine(".checklist-section li { margin-bottom:8px; font-size:15px; line-height:1.4; }");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<div class=\"report\">");
        builder.AppendLine($"<p class=\"subtitle\">Checked {WebUtility.HtmlEncode(report.Summary.CheckedAtUtc.ToString("f"))}</p>");
        builder.AppendLine($"<h1 class=\"title\">{WebUtility.HtmlEncode(report.Summary.Url)}</h1>");

        builder.AppendLine("<div class=\"score-grid\">");
        builder.AppendLine(BuildScoreCard("Overall Score", report.Summary.Score.Overall));
        builder.AppendLine(BuildScoreCard("SEO Score", report.Summary.Score.Seo));
        builder.AppendLine(BuildScoreCard("Speed Score", report.Summary.Score.Speed));
        builder.AppendLine("</div>");

        builder.AppendLine("<div class=\"section\">");
        builder.AppendLine("<h2>Summary</h2>");
        builder.AppendLine("<div class=\"grid\">");
        builder.AppendLine(BuildSummaryCard("Status Code", report.Summary.StatusCode.ToString()));
        builder.AppendLine(BuildSummaryCard("Response Time", $"{report.Summary.ResponseTimeMs} ms"));
        builder.AppendLine(BuildSummaryCard("Indexable", report.Summary.IsIndexable ? "Yes" : "No"));
        builder.AppendLine(BuildSummaryCard("Protocol", report.Summary.UsesHttps ? "HTTPS" : "HTTP"));
        builder.AppendLine("</div>");
        if (!string.IsNullOrWhiteSpace(report.Summary.ErrorMessage))
        {
            builder.AppendLine("<p style=\"color:#b91c1c;margin-top:12px;font-weight:600;\">");
            builder.AppendLine(WebUtility.HtmlEncode(report.Summary.ErrorMessage));
            builder.AppendLine("</p>");
        }
        builder.AppendLine("</div>");

        builder.AppendLine("<div class=\"section\">");
        builder.AppendLine("<h2>Performance</h2>");
        builder.AppendLine("<div class=\"grid\">");
        builder.AppendLine(BuildSummaryCard("Status", report.Performance.Network.StatusCode.ToString()));
        builder.AppendLine(BuildSummaryCard("Response Time", $"{report.Performance.Network.ResponseTimeMs} ms"));
        builder.AppendLine(BuildSummaryCard("Redirects", report.Performance.Network.RedirectCount.ToString()));
        builder.AppendLine("</div>");

        if (report.Performance.Details is { } perf && (perf.Mobile is not null || perf.Desktop is not null))
        {
            builder.AppendLine("<div class=\"grid\">");
            if (perf.Mobile is not null)
            {
                builder.AppendLine(BuildSummaryCard("Mobile Score", FormatNumber(perf.Mobile.Score)));
                builder.AppendLine(BuildSummaryCard("Mobile LCP", FormatMs(perf.Mobile.LargestContentfulPaintMs)));
                builder.AppendLine(BuildSummaryCard("Mobile FCP", FormatMs(perf.Mobile.FirstContentfulPaintMs)));
                builder.AppendLine(BuildSummaryCard("Mobile CLS", FormatNumber(perf.Mobile.CumulativeLayoutShift)));
                builder.AppendLine(BuildSummaryCard("Mobile TBT", FormatMs(perf.Mobile.TotalBlockingTimeMs)));
            }
            if (perf.Desktop is not null)
            {
                builder.AppendLine(BuildSummaryCard("Desktop Score", FormatNumber(perf.Desktop.Score)));
                builder.AppendLine(BuildSummaryCard("Desktop LCP", FormatMs(perf.Desktop.LargestContentfulPaintMs)));
                builder.AppendLine(BuildSummaryCard("Desktop FCP", FormatMs(perf.Desktop.FirstContentfulPaintMs)));
                builder.AppendLine(BuildSummaryCard("Desktop CLS", FormatNumber(perf.Desktop.CumulativeLayoutShift)));
                builder.AppendLine(BuildSummaryCard("Desktop TBT", FormatMs(perf.Desktop.TotalBlockingTimeMs)));
            }
            builder.AppendLine("</div>");
        }

        builder.AppendLine("</div>");

        builder.AppendLine("<div class=\"section\">");
        builder.AppendLine("<h2>On-Page SEO</h2>");
        builder.AppendLine("<div class=\"grid\">");
        builder.AppendLine(BuildSummaryCard("Title Length", report.Seo.Details.TitleLength.ToString()));
        builder.AppendLine(BuildSummaryCard("Meta Description Length", report.Seo.Details.MetaDescriptionLength.ToString()));
        builder.AppendLine(BuildSummaryCard("Headings (H1)", report.Seo.Details.H1Count.ToString()));
        builder.AppendLine(BuildSummaryCard("Headings (H2)", report.Seo.Details.H2Count.ToString()));
        builder.AppendLine(BuildSummaryCard("Images", report.Seo.Details.TotalImages.ToString()));
        builder.AppendLine(BuildSummaryCard("Images Missing Alt", report.Seo.Details.ImagesWithoutAlt.ToString()));
        builder.AppendLine(BuildSummaryCard("Internal Links", report.Seo.Details.InternalLinkCount.ToString()));
        builder.AppendLine(BuildSummaryCard("External Links", report.Seo.Details.ExternalLinkCount.ToString()));
        builder.AppendLine("</div>");
        builder.AppendLine("</div>");

        if (report.Checklist.Count > 0)
        {
            builder.AppendLine("<div class=\"section\">");
            builder.AppendLine("<h2>AI Checklist</h2>");
            builder.AppendLine("<div class=\"checklist-grid\">");
            foreach (var section in report.Checklist)
            {
                builder.AppendLine("<div class=\"checklist-section\">");
                builder.AppendLine($"<h4>{WebUtility.HtmlEncode(section.Title)}</h4>");
                builder.AppendLine("<ul>");
                foreach (var item in section.Items)
                {
                    builder.AppendLine($"<li>{WebUtility.HtmlEncode(item)}</li>");
                }
                builder.AppendLine("</ul>");
                builder.AppendLine("</div>");
            }
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
        }

        if (report.OffPageSeo is not null)
        {
            builder.AppendLine("<div class=\"section\">");
            builder.AppendLine("<h2>Off-Page SEO</h2>");
            builder.AppendLine("<div class=\"grid\">");
            builder.AppendLine(BuildSummaryCard("Domain Authority", FormatNumber(report.OffPageSeo.Details.DomainAuthority)));
            builder.AppendLine(BuildSummaryCard("Backlinks", FormatNumber(report.OffPageSeo.Details.Backlinks)));
            builder.AppendLine(BuildSummaryCard("Referring Domains", FormatNumber(report.OffPageSeo.Details.ReferringDomains)));
            builder.AppendLine(BuildSummaryCard("Spam Score", FormatNumber(report.OffPageSeo.Details.SpamScore)));
            builder.AppendLine("</div>");
            builder.AppendLine("</div>");
        }

        if (report.Recommendations.Count > 0)
        {
            builder.AppendLine("<div class=\"section\">");
            builder.AppendLine("<h2>Recommendations</h2>");
            builder.AppendLine("<ul class=\"recommendations\">");
            foreach (var recommendation in report.Recommendations)
            {
                builder.AppendLine("<li>");
                builder.AppendLine($"<strong>{WebUtility.HtmlEncode(recommendation.Title)}</strong>");
                builder.AppendLine($"<span style=\"color:#475569;font-size:14px;\">{WebUtility.HtmlEncode(recommendation.Description)}</span>");
                builder.AppendLine($"<span style=\"display:block;margin-top:4px;font-size:12px;color:#94a3b8;\">{WebUtility.HtmlEncode(recommendation.Category)}</span>");
                builder.AppendLine("</li>");
            }
            builder.AppendLine("</ul>");
            builder.AppendLine("</div>");
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string BuildScoreCard(string label, int value)
    {
        return $"""
                <div class="score-card">
                    <h3>{WebUtility.HtmlEncode(label)}</h3>
                    <p class="score-value">{value}</p>
                </div>
                """;
    }

    private static string BuildSummaryCard(string label, string value)
    {
        return $"""
                <div class="card">
                    <h4>{WebUtility.HtmlEncode(label)}</h4>
                    <p>{WebUtility.HtmlEncode(value)}</p>
                </div>
                """;
    }

    private static string FormatMs(double? value)
    {
        return value.HasValue ? $"{Math.Round(value.Value)} ms" : "—";
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue ? Math.Round(value.Value, 2).ToString() : "—";
    }

    private static string FormatNumber(int? value)
    {
        return value.HasValue ? value.Value.ToString("N0") : "—";
    }
}
