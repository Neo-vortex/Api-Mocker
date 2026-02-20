namespace ApiMocker.Models;

public class RouteConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public RouteMode Mode { get; set; } = RouteMode.Mock;
    public bool IsTemplate { get; set; } = false;

    // Mock settings
    public string? MockBody { get; set; }
    public int MockStatusCode { get; set; } = 200;
    public string MockContentType { get; set; } = "application/json";

    // Proxy settings
    public string? ProxyDestination { get; set; }
    public bool TrustInsecureSsl { get; set; } = false; // bypass cert validation for self-signed/internal certs

    // ── Lua transformation scripts ────────────────────────────────────────
    public string? RequestScript { get; set; }
    public string? ResponseScript { get; set; }

    // ── Health check ──────────────────────────────────────────────────────
    public bool HealthCheckEnabled { get; set; } = false;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public DateTime? LastHealthCheckAt { get; set; }
    public bool? LastHealthCheckPassed { get; set; }
    public string? LastHealthCheckError { get; set; }

    // ── Rate limiting ─────────────────────────────────────────────────────
    public bool RateLimitEnabled { get; set; } = false;
    public int RateLimitRequests { get; set; } = 100;
    public int RateLimitWindowSeconds { get; set; } = 60;

    // ── Retry policy (Proxy only) ─────────────────────────────────────────
    public bool RetryEnabled { get; set; } = false;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 500;

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RequestLog> RequestLogs { get; set; } = [];
}

public enum RouteMode
{
    Mock,
    Proxy
}

public class RequestLog
{
    public int Id { get; set; }
    public int RouteConfigId { get; set; }
    public RouteConfig RouteConfig { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ClientIp { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string QueryString { get; set; } = "";

    // ── Original (before Lua request script) ─────────────────────────────
    public string OriginalRequestHeaders { get; set; } = "";
    public string OriginalRequestBody { get; set; } = "";

    // ── After Lua request script (what was actually forwarded/used) ───────
    public string RequestHeaders { get; set; } = "";
    public string RequestBody { get; set; } = "";

    // ── Original (before Lua response script) ────────────────────────────
    public string OriginalResponseHeaders { get; set; } = "";
    public string OriginalResponseBody { get; set; } = "";
    public int OriginalResponseStatusCode { get; set; }

    // ── After Lua response script (what was actually returned to caller) ──
    public int ResponseStatusCode { get; set; }
    public string ResponseHeaders { get; set; } = "";
    public string ResponseBody { get; set; } = "";

    public long DurationMs { get; set; }
    public string Mode { get; set; } = "";
    public int RetryAttempts { get; set; } = 0;
    public bool RequestScriptRan { get; set; } = false;
    public bool ResponseScriptRan { get; set; } = false;
}
