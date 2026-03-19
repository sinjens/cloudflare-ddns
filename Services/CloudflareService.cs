using System.Text.Json;

namespace CloudflareDdns.Services;

public class CloudflareService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<(string? ZoneId, string? AccountId)> GetZoneInfoAsync(string domain)
    {
        // Extract root domain (last two parts) for zone lookup
        var parts = domain.Split('.',';');
        var rootDomain = parts.Length >= 2
            ? string.Join('.', parts[^2..])
            : domain;

        var json = await httpClient.GetFromJsonAsync<CloudflareResponse<List<ZoneResult>>>(
            $"zones?name={rootDomain}", JsonOptions);

        var zone = json?.Result?.FirstOrDefault();
        return (zone?.Id, zone?.Account?.Id);
    }

    public async Task<TokenVerifyResult?> VerifyTokenAsync(string accountId)
    {
        var json = await httpClient.GetFromJsonAsync<CloudflareResponse<TokenVerifyResult>>(
            $"accounts/{accountId}/tokens/verify", JsonOptions);

        return json?.Success == true ? json.Result : null;
    }

    public async Task<DnsRecord?> GetDnsRecordAsync(string zoneId, string domain)
    {
        var json = await httpClient.GetFromJsonAsync<CloudflareResponse<List<DnsRecord>>>(
            $"zones/{zoneId}/dns_records?name={domain}&type=A", JsonOptions);

        return json?.Result?.FirstOrDefault();
    }

    public async Task<bool> UpdateDnsRecordAsync(string zoneId, string recordId, string ip)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"zones/{zoneId}/dns_records/{recordId}", new { content = ip });
        var json = await response.Content.ReadFromJsonAsync<CloudflareResponse<DnsRecord>>(JsonOptions);

        return json?.Success ?? false;
    }
}

public class CloudflareResponse<T>
{
    public T? Result { get; set; }
    public bool Success { get; set; }
}

public class ZoneResult
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ZoneAccount? Account { get; set; }
}

public class ZoneAccount
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class DnsRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
}

public class TokenVerifyResult
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? NotBefore { get; set; }
    public DateTime? ExpiresOn { get; set; }
}
