namespace CloudflareDdns.Services;

public class DdnsSettings
{
    public string CloudflareToken { get; set; } = "";
    public string Domains { get; set; } = "";
    public int IntervalMinutes { get; set; } = 10;

    public List<string> GetDomainList() =>
        Domains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .ToList();
}
