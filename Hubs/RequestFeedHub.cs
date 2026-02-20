using Microsoft.AspNetCore.SignalR;

namespace ApiMocker.Hubs;

public class RequestFeedHub : Hub
{
    // Clients connect and just listen — server pushes events via IHubContext
}

/// <summary>DTO pushed to all connected clients on every intercepted request.</summary>
public class RequestFeedEvent
{
    public int LogId { get; set; }
    public string RouteName { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string QueryString { get; set; } = "";
    public string ClientIp { get; set; } = "";
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string Mode { get; set; } = "";
    public int RetryAttempts { get; set; }
    public string Timestamp { get; set; } = "";
}
