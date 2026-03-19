using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudflareDdns.Services;

public class CloudflareService(HttpClient httpClient, DdnsSettings settings)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<string?> GetZoneIdAsync(string domain)
    {
        // Extract root domain (last two parts) for zone lookup
        var parts = domain.Split('.');
        var rootDomain = parts.Length >= 2
            ? string.Join('.', parts[^2..])
            : domain;

        var request = CreateRequest(HttpMethod.Get, $"zones?name={rootDomain}");
        var response = await httpClient.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<CloudflareResponse<List<ZoneResult>>>(JsonOptions);

        return json?.Result?.FirstOrDefault()?.Id;
    }

    public async Task<DnsRecord?> GetDnsRecordAsync(string zoneId, string domain)
    {
        var request = CreateRequest(HttpMethod.Get, $"zones/{zoneId}/dns_records?name={domain}&type=A");
        var response = await httpClient.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<CloudflareResponse<List<DnsRecord>>>(JsonOptions);

        return json?.Result?.FirstOrDefault();
    }

    public async Task<bool> UpdateDnsRecordAsync(string zoneId, string recordId, string ip)
    {
        var request = CreateRequest(HttpMethod.Patch, $"zones/{zoneId}/dns_records/{recordId}");
        request.Content = JsonContent.Create(new { content = ip });

        var response = await httpClient.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<CloudflareResponse<DnsRecord>>(JsonOptions);

        return json?.Success ?? false;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"https://api.cloudflare.com/client/v4/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.CloudflareToken);
        return request;
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
}

public class DnsRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public string Type { get; set; } = "";
}
