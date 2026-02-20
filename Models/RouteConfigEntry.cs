namespace ApiMocker.Models;

/// <summary>
/// Represents a route entry as defined in appsettings.json under "MockerRoutes".
/// </summary>
public class RouteConfigEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string HttpMethod { get; set; } = "GET";
    public string Mode { get; set; } = "Mock";

    // Mock
    public string? MockBody { get; set; }
    public int MockStatusCode { get; set; } = 200;
    public string MockContentType { get; set; } = "application/json";

    // Proxy
    public string? ProxyDestination { get; set; }

    public bool IsEnabled { get; set; } = true;
}
