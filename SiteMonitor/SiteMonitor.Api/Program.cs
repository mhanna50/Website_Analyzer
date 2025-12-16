using SiteMonitor.Api.Configuration;
using SiteMonitor.Api.Diagnostics;
using SiteMonitor.Api.Infrastructure;
using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services;
using SiteMonitor.Api.Services.Analysis;
using SiteMonitor.Api.Services.Queue;
using SiteMonitor.Api.Services.Recommendations;
using SiteMonitor.Api.Services.Reports;
using SiteMonitor.Api.Services.Scoring;
using SiteMonitor.Api.Services.Seo;
using SiteMonitor.Api.Utilities;

DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SpaDomAnalyzer>();
builder.Services.AddSingleton<PerformanceService>();
builder.Services.AddSingleton<OffPageSeoService>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<ReportRenderer>();
builder.Services.AddSingleton<AiInsightsService>();
builder.Services.AddSingleton<LinkHealthAnalyzer>();
builder.Services.AddSingleton<AnalysisThrottler>();
builder.Services.AddSingleton<WebsiteAnalyzer>();
builder.Services.AddSingleton<ScanQueue>();
builder.Services.AddHostedService<ScanWorker>();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseRequestLogging();

app.MapPost("/api/analyze", async (WebsiteAnalysisRequest request, WebsiteAnalyzer analyzer) =>
    {
        var analysis = await analyzer.AnalyzeAsync(request, saveHistory: true);
        return Results.Ok(analysis);
    })
    .WithName("AnalyzeWebsite");

app.MapPost("/api/report", async (WebsiteAnalysisRequest request, WebsiteAnalyzer analyzer, ReportRenderer reportRenderer) =>
    {
        var analysis = await analyzer.AnalyzeAsync(request, saveHistory: false);
        var report = ReportBuilder.BuildReport(analysis);

        try
        {
            var screenshot = await reportRenderer.RenderScreenshotAsync(report);
            var fileName = BuildReportFileName(analysis.Url, "png");
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

app.MapPost("/api/report/pdf", async (WebsiteAnalysisRequest request, WebsiteAnalyzer analyzer, ReportRenderer reportRenderer) =>
    {
        var analysis = await analyzer.AnalyzeAsync(request, saveHistory: false);
        var report = ReportBuilder.BuildReport(analysis);

        try
        {
            var pdf = await reportRenderer.RenderPdfAsync(report);
            var fileName = BuildReportFileName(analysis.Url, "pdf");
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

    if (!UrlHelper.TryNormalizeUrl(url, out var normalizedUrl))
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

    if (!UrlHelper.TryNormalizeUrl(url, out var normalizedUrl))
    {
        return Results.BadRequest(new { message = "A valid URL is required." });
    }

    var latest = await historyStore.GetLatestAsync(normalizedUrl);
    return latest is null ? Results.NoContent() : Results.Ok(latest);
});

app.Run();

static string BuildReportFileName(string url, string extension)
{
    var fileName = $"site-report.{extension}";
    if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
    {
        fileName = $"{parsedUri.Host}-report.{extension}";
    }

    return fileName;
}
app.MapPost("/api/analyze/async", (WebsiteAnalysisRequest request, ScanQueue queue) =>
    {
        var jobId = queue.Enqueue(request, saveHistory: true);
        return Results.Accepted($"/api/analyze/async/{jobId}", new { jobId });
    })
    .WithName("QueueAnalysis");

app.MapGet("/api/analyze/async/{jobId:guid}", (Guid jobId, ScanQueue queue) =>
    {
        var status = queue.GetStatus(jobId);
        return status is null ? Results.NotFound(new { message = "Scan not found." }) : Results.Ok(status);
    })
    .WithName("GetQueuedAnalysis");
