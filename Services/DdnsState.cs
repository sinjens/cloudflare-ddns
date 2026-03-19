namespace CloudflareDdns.Services;

public class DomainInfo
{
    public string Domain { get; set; } = "";
    public string? CurrentIp { get; set; }
    public string? ZoneId { get; set; }
    public string? RecordId { get; set; }
    public DateTime? LastUpdated { get; set; }
}

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
    private string? _publicIp;

    public event Action? OnChange;

    public string? PublicIp
    {
        get { lock (_lock) return _publicIp; }
        set { lock (_lock) _publicIp = value; NotifyChange(); }
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

    private void NotifyChange() => OnChange?.Invoke();
}
