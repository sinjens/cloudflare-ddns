using System.Diagnostics;
using System.Net;

namespace CloudflareDdns.Services;

public class DdnsBackgroundService(
    CloudflareService cloudflare,
    DdnsState state,
    DdnsSettings settings,
    IHttpClientFactory httpClientFactory,
    ILogger<DdnsBackgroundService> logger) : BackgroundService
{
    private string? _accountId;
    private DateTime _lastTokenCheck = DateTime.MinValue;
    private bool _tokenExpiryNotified;
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

            await state.WaitForRefreshAsync(TimeSpan.FromMinutes(settings.IntervalMinutes), stoppingToken);
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

        await CheckTokenExpiryAsync();
    }

    private async Task UpdateDomainAsync(string domain, string publicIp)
    {
        var info = state.GetDomain(domain);

        // Resolve zone and record IDs if not cached
        if (info?.ZoneId == null || info?.RecordId == null)
        {
            var (zoneId, accountId) = await cloudflare.GetZoneInfoAsync(domain);
            if (zoneId == null)
            {
                state.AddLog($"[{domain}] Zone not found", isError: true);
                return;
            }

            _accountId ??= accountId;

            var record = await cloudflare.GetDnsRecordAsync(zoneId, domain);
            if (record == null || !IPAddress.TryParse(record.Content, out _))
            {
                state.AddLog($"[{domain}] A record not found or content is not an IP address", isError: true);
                return;
            }

            info = new DomainInfo(domain, CurrentIp: record.Content, ZoneId: zoneId, RecordId: record.Id);
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
            state.SetDomain(domain, info with { CurrentIp = publicIp, LastUpdated = DateTime.UtcNow });
            state.AddLog($"[{domain}] Updated to {publicIp}");
        }
        else
        {
            state.AddLog($"[{domain}] Update failed", isError: true);
        }
    }

    private async Task CheckTokenExpiryAsync()
    {
        if (_accountId == null) return;
        var checkInterval = state.TokenInfo?.ExpiresOn is { } exp
            && exp - DateTime.UtcNow < TimeSpan.FromDays(30)
            ? TimeSpan.FromDays(1)
            : TimeSpan.FromDays(7);
        if (DateTime.UtcNow - _lastTokenCheck < checkInterval) return;

        _lastTokenCheck = DateTime.UtcNow;

        try
        {
            var result = await cloudflare.VerifyTokenAsync(_accountId);
            if (result == null)
            {
                state.TokenInfo = new TokenInfo("unknown", null, DateTime.UtcNow);
                state.AddLog("API token verification failed", isError: true);
                return;
            }

            var previousExpiry = state.TokenInfo?.ExpiresOn;
            state.TokenInfo = new TokenInfo(result.Status, result.ExpiresOn, DateTime.UtcNow);
            state.AddLog($"API token verified: status={result.Status}"
                + (result.ExpiresOn.HasValue ? $", expires={result.ExpiresOn:yyyy-MM-dd}" : ", no expiry"));

            if (result.ExpiresOn != previousExpiry)
                _tokenExpiryNotified = false;

            if (result.ExpiresOn.HasValue
                && result.ExpiresOn.Value - DateTime.UtcNow < TimeSpan.FromDays(30)
                && !_tokenExpiryNotified)
            {
                _tokenExpiryNotified = true;
                var daysLeft = (int)(result.ExpiresOn.Value - DateTime.UtcNow).TotalDays;
                var message = $"Cloudflare API token expires in {daysLeft} day{(daysLeft != 1 ? "s" : "")} ({result.ExpiresOn:yyyy-MM-dd})";
                state.AddLog(message, isError: true);
                SendUnraidNotification(message);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token verification failed");
            state.AddLog($"Token verification error: {ex.Message}", isError: true);
        }
    }

    private void SendUnraidNotification(string message)
    {
        const string notifyScript = "/usr/local/emhttp/webGui/scripts/notify";
        if (!File.Exists(notifyScript)) return;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = notifyScript,
                ArgumentList = { "-s", "Cloudflare DDNS", "-d", message, "-i", "warning" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            state.AddLog("Unraid notification sent for token expiry");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Unraid notification");
        }
    }

    private async Task<string?> GetPublicIpAsync(CancellationToken ct)
    {
        using var http = httpClientFactory.CreateClient("Ipify");
        var ip = (await http.GetStringAsync("https://api.ipify.org", ct)).Trim();
        return IPAddress.TryParse(ip, out _) ? ip : null;
    }
}
