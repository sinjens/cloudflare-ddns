namespace CloudflareDdns.Services;

public record DomainInfo(
    string Domain,
    string? CurrentIp = null,
    string? ZoneId = null,
    string? RecordId = null,
    DateTime? LastUpdated = null);

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = "";
    public bool IsError { get; set; }
}

public class DdnsState
{
    private readonly object _lock = new();
    private readonly List<LogEntry> _log = new();
    private readonly Dictionary<string, DomainInfo> _domains = new();
    private readonly SemaphoreSlim _refreshSignal = new(0, 1);
    private string? _publicIp;

    public event Action? OnChange;

    public string? PublicIp
    {
        get { lock (_lock) return _publicIp; }
        set
        {
            lock (_lock)
            {
                if (_publicIp == value) return;
                _publicIp = value;
            }
            NotifyChange();
        }
    }

    public IReadOnlyList<LogEntry> GetLog()
    {
        lock (_lock) return _log.ToList();
    }

    public IReadOnlyDictionary<string, DomainInfo> GetDomains()
    {
        lock (_lock) return _domains.ToDictionary(k => k.Key, v => v.Value);
    }

    public DomainInfo? GetDomain(string domain)
    {
        lock (_lock) return _domains.GetValueOrDefault(domain);
    }

    public void SetDomain(string domain, DomainInfo info)
    {
        lock (_lock) _domains[domain] = info;
        NotifyChange();
    }

    public void AddLog(string message, bool isError = false)
    {
        lock (_lock)
        {
            _log.Add(new LogEntry { Message = message, IsError = isError });
            if (_log.Count > 500) _log.RemoveAt(0);
        }
        NotifyChange();
    }

    public void RequestRefresh()
    {
        try { _refreshSignal.Release(); }
        catch (SemaphoreFullException) { }
    }

    public async Task WaitForRefreshAsync(TimeSpan timeout, CancellationToken ct)
    {
        await _refreshSignal.WaitAsync(timeout, ct);
    }

    private void NotifyChange() => OnChange?.Invoke();
}
