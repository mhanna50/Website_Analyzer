using System;

namespace SiteMonitor.Api.Utilities;

public static class UrlHelper
{
    public static bool TryNormalizeUrl(string? rawUrl, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        var trimmed = rawUrl.Trim();

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }
}
