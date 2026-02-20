namespace ApiMocker.Models;

public class RouteConfigViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public string Mode { get; set; } = "Mock";

    public string? MockBody { get; set; }
    public int MockStatusCode { get; set; } = 200;
    public string MockContentType { get; set; } = "application/json";
    public string? ProxyDestination { get; set; }
    public bool TrustInsecureSsl { get; set; } = false;

    // Lua
    public string? RequestScript { get; set; }
    public string? ResponseScript { get; set; }

    // Health check
    public bool HealthCheckEnabled { get; set; } = false;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public bool? LastHealthCheckPassed { get; set; }
    public string? LastHealthCheckError { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }

    // Rate limit
    public bool RateLimitEnabled { get; set; } = false;
    public int RateLimitRequests { get; set; } = 100;
    public int RateLimitWindowSeconds { get; set; } = 60;

    // Retry
    public bool RetryEnabled { get; set; } = false;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 500;

    public bool IsEnabled { get; set; } = true;
    public bool IsTemplate { get; set; } = false;

    public int LogCount { get; set; }
    public DateTime? LastUsed { get; set; }
}

public class LogViewModel
{
    public int Id { get; set; }
    public string RouteName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string ClientIp { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string QueryString { get; set; } = "";

    // Before Lua request script
    public string OriginalRequestHeaders { get; set; } = "";
    public string OriginalRequestBody { get; set; } = "";

    // After Lua request script
    public string RequestHeaders { get; set; } = "";
    public string RequestBody { get; set; } = "";

    // Before Lua response script
    public int OriginalResponseStatusCode { get; set; }
    public string OriginalResponseHeaders { get; set; } = "";
    public string OriginalResponseBody { get; set; } = "";

    // After Lua response script
    public int ResponseStatusCode { get; set; }
    public string ResponseHeaders { get; set; } = "";
    public string ResponseBody { get; set; } = "";

    public long DurationMs { get; set; }
    public string Mode { get; set; } = "";
    public int RetryAttempts { get; set; }
    public bool RequestScriptRan { get; set; }
    public bool ResponseScriptRan { get; set; }
}
