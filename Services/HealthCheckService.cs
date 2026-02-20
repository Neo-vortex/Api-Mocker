using ApiMocker.Data;
using ApiMocker.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker.Services;

public class HealthCheckService(
    IServiceScopeFactory scopeFactory,
    ILogger<HealthCheckService> logger) : BackgroundService
{
    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromSeconds(10) };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Health check background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunChecksAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // poll every 5s, individual routes control their own interval
        }
    }

    private async Task RunChecksAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var routes = await db.RouteConfigs
            .Where(r => r.IsEnabled &&
                        r.Mode == RouteMode.Proxy &&
                        r.HealthCheckEnabled &&
                        !string.IsNullOrEmpty(r.ProxyDestination))
            .ToListAsync(ct);

        foreach (var route in routes)
        {
            // Respect per-route interval
            if (route.LastHealthCheckAt.HasValue)
            {
                var elapsed = (DateTime.UtcNow - route.LastHealthCheckAt.Value).TotalSeconds;
                if (elapsed < route.HealthCheckIntervalSeconds)
                    continue;
            }

            await CheckRouteAsync(route, db, ct);
        }

        if (routes.Any())
            await db.SaveChangesAsync(ct);
    }

    private async Task CheckRouteAsync(RouteConfig route, AppDbContext db, CancellationToken ct)
    {
        var url = route.ProxyDestination!.TrimEnd('/');
        route.LastHealthCheckAt = DateTime.UtcNow;

        try
        {
            var response = await _http.GetAsync(url, ct);
            route.LastHealthCheckPassed = (int)response.StatusCode < 500;
            route.LastHealthCheckError = route.LastHealthCheckPassed == true
                ? null
                : $"HTTP {(int)response.StatusCode}";

            logger.LogDebug("Health check [{Route}] → {Url}: {Status}",
                route.Name, url, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            route.LastHealthCheckPassed = false;
            route.LastHealthCheckError = ex.Message;
            logger.LogDebug("Health check [{Route}] → {Url}: FAILED — {Error}",
                route.Name, url, ex.Message);
        }
    }
}
