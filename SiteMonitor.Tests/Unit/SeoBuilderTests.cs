using System.Net.Http;
using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Seo;
using Xunit;

namespace SiteMonitor.Tests.Unit;

public class SeoBuilderTests
{
    private const string BaseHtml = """
        <html lang="en">
          <head>
            <title>Example Page</title>
            <meta name="description" content="Sample description" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <link rel="canonical" href="https://example.com" />
            <script type="application/ld+json">{"@type":"Organization"}</script>
            <meta property="og:title" content="Example" />
            <meta name="twitter:card" content="summary" />
          </head>
          <body>
            <a href="#main" class="skip">Skip to content</a>
            <header></header>
            <main id="main">
              <h1>Example H1</h1>
              <h2>Example H2</h2>
              <img src="a.jpg" alt="alt text" />
              <img src="b.jpg" />
              <a href="/internal">Internal</a>
              <a href="https://external.com">External</a>
              <form>
                <input id="email" type="email" />
                <label for="email">Email</label>
              </form>
            </main>
          </body>
        </html>
        """;

    [Fact]
    public void BuildSeoResult_PopulatesFieldsFromStaticHtml()
    {
        var uri = new Uri("https://example.com/page");
        var result = SeoBuilder.BuildSeoResult(uri, BaseHtml, response: null, spaDomResult: null, out var crawlableLinks);

        Assert.True(result.HasLanguageAttribute);
        Assert.True(result.HasSkipLink);
        Assert.Equal(1, result.H1Count);
        Assert.Equal(1, result.StructuredDataCount);
        Assert.Equal(2, result.TotalImages);
        Assert.Equal(1, result.ImagesWithoutAlt);
        Assert.Equal(1, result.InternalLinkCount);
        Assert.Equal("Example Page", result.Title);
        Assert.Equal("Sample description", result.MetaDescription);
        Assert.Contains("https://example.com/internal", crawlableLinks);
        Assert.Contains("https://external.com", crawlableLinks);
    }

    [Fact]
    public void BuildSeoResult_PrefersSpaDomCountsWhenProvided()
    {
        var uri = new Uri("https://example.com/page");
        var spaDom = new SpaDomAnalysisResult(10, 5, 7, 3);
        var result = SeoBuilder.BuildSeoResult(uri, BaseHtml, response: null, spaDomResult: spaDom, out _);

        Assert.Equal(10, result.TotalImages);
        Assert.Equal(5, result.ImagesWithoutAlt);
        Assert.Equal(7, result.InternalLinkCount);
        Assert.Equal(3, result.ExternalLinkCount);
        Assert.True(result.DomFromHeadlessBrowser);
    }

    [Fact]
    public void BuildSeoResult_RespectsRobotsHeaders()
    {
        var uri = new Uri("https://example.com/page");
        var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("X-Robots-Tag", "noindex");

        var result = SeoBuilder.BuildSeoResult(uri, BaseHtml, response, spaDomResult: null, out _);

        Assert.False(result.IsIndexable);
    }

    [Fact]
    public void BuildSeoResult_FlagsMissingAccessibilitySignals()
    {
        const string html = """
            <html>
              <head><title>No Accessibility</title></head>
              <body>
                <img src="a.jpg" />
              </body>
            </html>
            """;
        var uri = new Uri("https://example.com");

        var result = SeoBuilder.BuildSeoResult(uri, html, response: null, spaDomResult: null, out _);

        Assert.False(result.HasLanguageAttribute);
        Assert.False(result.HasSkipLink);
        Assert.Equal(0, result.LandmarkCount);
        Assert.Equal(1, result.ImagesWithoutAlt);
    }
}
