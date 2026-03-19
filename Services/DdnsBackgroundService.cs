using System.Net;

namespace CloudflareDdns.Services;

public class DdnsBackgroundService(
    CloudflareService cloudflare,
    DdnsState state,
    DdnsSettings settings,
    ILogger<DdnsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        state.AddLog("DDNS service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                var msg = $"Update cycle failed: {ex.Message}";
                logger.LogError(ex, "{Message}", msg);
                state.AddLog(msg, isError: true);
            }

            await Task.Delay(TimeSpan.FromMinutes(settings.IntervalMinutes), stoppingToken);
        }
    }

    private async Task UpdateAsync(CancellationToken ct)
    {
        var publicIp = await GetPublicIpAsync(ct);
        if (publicIp == null)
        {
            state.AddLog("Failed to get public IP", isError: true);
            return;
        }

        var previousIp = state.PublicIp;
        state.PublicIp = publicIp;

        if (previousIp != publicIp)
            state.AddLog($"Public IP: {publicIp}" + (previousIp != null ? $" (was {previousIp})" : ""));

        foreach (var domain in settings.GetDomainList())
        {
            try
            {
                await UpdateDomainAsync(domain, publicIp);
            }
            catch (Exception ex)
            {
                var msg = $"[{domain}] Error: {ex.Message}";
                logger.LogError(ex, "{Message}", msg);
                state.AddLog(msg, isError: true);
            }
        }
    }

    private async Task UpdateDomainAsync(string domain, string publicIp)
    {
        var info = state.GetDomain(domain);

        // Resolve zone and record IDs if not cached
        if (info?.ZoneId == null || info?.RecordId == null)
        {
            var zoneId = await cloudflare.GetZoneIdAsync(domain);
            if (zoneId == null)
            {
                state.AddLog($"[{domain}] Zone not found", isError: true);
                return;
            }

            var record = await cloudflare.GetDnsRecordAsync(zoneId, domain);
            if (record == null || !IPAddress.TryParse(record.Content, out _))
            {
                state.AddLog($"[{domain}] A record not found or content is not an IP address", isError: true);
                return;
            }

            info = new DomainInfo
            {
                Domain = domain,
                ZoneId = zoneId,
                RecordId = record.Id,
                CurrentIp = record.Content
            };
            state.SetDomain(domain, info);
            state.AddLog($"[{domain}] Resolved: zone={zoneId}, record={record.Id}, IP={record.Content}");
        }

        // Already up to date
        if (info!.CurrentIp == publicIp)
            return;

        // Update needed
        state.AddLog($"[{domain}] Updating {info.CurrentIp} -> {publicIp}");
        var success = await cloudflare.UpdateDnsRecordAsync(info.ZoneId!, info.RecordId!, publicIp);

        if (success)
        {
            info.CurrentIp = publicIp;
            info.LastUpdated = DateTime.UtcNow;
            state.SetDomain(domain, info);
            state.AddLog($"[{domain}] Updated to {publicIp}");
        }
        else
        {
            state.AddLog($"[{domain}] Update failed", isError: true);
        }
    }

    private static async Task<string?> GetPublicIpAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var ip = (await http.GetStringAsync("https://api.ipify.org", ct)).Trim();
        return IPAddress.TryParse(ip, out _) ? ip : null;
    }
}
