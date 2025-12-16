using System;

namespace SiteMonitor.Api.Models;

public record NetworkResult(
    string Url,
    int StatusCode,
    long ResponseTimeMs,
    DateTime CheckedAtUtc,
    string? ErrorMessage,
    int RedirectCount);
