using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SiteMonitor.Api.Models;
using SiteMonitor.Api.Services.Seo;
using Xunit;

namespace SiteMonitor.Tests.Unit;

public class LinkHealthAnalyzerTests
{
    [Fact]
    public async Task FindBrokenLinksAsync_ReturnsOnlyFailingLinks()
    {
        var urls = new[]
        {
            "https://example.com/good",
            "https://example.com/bad"
        };
        var handler = new StubMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("bad", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        var factory = new StubHttpClientFactory(new HttpClient(handler));
        var analyzer = new LinkHealthAnalyzer(factory, NullLogger<LinkHealthAnalyzer>.Instance);

        var result = await analyzer.FindBrokenLinksAsync(new Uri("https://example.com"), urls, ScanMode.Deep, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("https://example.com/bad", result[0].Url);
        Assert.True(result[0].IsInternal);
        Assert.Equal(404, result[0].StatusCode);
    }

    [Fact]
    public async Task FindBrokenLinksAsync_RespectsFastModeLimit()
    {
        var links = Enumerable.Range(0, 25)
            .Select(i => $"https://example.com/link{i}")
            .ToArray();
        var requestCount = 0;
        var handler = new StubMessageHandler(_ =>
        {
            Interlocked.Increment(ref requestCount);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var factory = new StubHttpClientFactory(new HttpClient(handler));
        var analyzer = new LinkHealthAnalyzer(factory, NullLogger<LinkHealthAnalyzer>.Instance);

        await analyzer.FindBrokenLinksAsync(new Uri("https://example.com"), links, ScanMode.Fast, CancellationToken.None);

        Assert.True(requestCount <= 12, $"Expected <=12 requests, saw {requestCount}");
    }

    [Fact]
    public async Task FindBrokenLinksAsync_MarksExternalLinks()
    {
        var handler = new StubMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "external.com")
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var factory = new StubHttpClientFactory(new HttpClient(handler));
        var analyzer = new LinkHealthAnalyzer(factory, NullLogger<LinkHealthAnalyzer>.Instance);
        var links = new[] { "https://external.com/bad" };

        var result = await analyzer.FindBrokenLinksAsync(new Uri("https://example.com"), links, ScanMode.Deep, CancellationToken.None);

        Assert.Single(result);
        Assert.False(result[0].IsInternal);
        Assert.Equal(500, result[0].StatusCode);
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _handler(request);
            return Task.FromResult(response);
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }
}
