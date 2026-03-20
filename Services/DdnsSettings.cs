namespace CloudflareDdns.Services;

public class DdnsSettings
{
    public string CloudflareToken { get; set; } = "";
    public string Domains { get; set; } = "";
    public int IntervalMinutes { get; set; } = 10;
    public string HeDnsPassword { get; set; } = "";
    public string HeDnsDomains { get; set; } = "";

    public List<string> GetDomainList() =>
        Domains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .ToList();

    public List<string> GetHeDnsDomainList() =>
        string.IsNullOrWhiteSpace(HeDnsDomains)
            ? GetDomainList()
            : HeDnsDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .ToList();
}
