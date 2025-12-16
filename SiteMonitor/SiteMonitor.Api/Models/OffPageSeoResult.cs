namespace SiteMonitor.Api.Models;

public record OffPageSeoResult(
    double? DomainAuthority,
    int? Backlinks,
    int? ReferringDomains,
    double? SpamScore);
