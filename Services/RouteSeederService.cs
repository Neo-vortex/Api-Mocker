using ApiMocker.Data;
using ApiMocker.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker.Services;

public class RouteSeederService(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<RouteSeederService> logger)
{
    public async Task SeedAsync()
    {
        var entries = configuration
            .GetSection("MockerRoutes")
            .Get<List<RouteConfigEntry>>();

        if (entries == null || entries.Count == 0)
        {
            logger.LogInformation("No routes found in configuration to seed.");
            return;
        }

        int added = 0;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Path) || string.IsNullOrWhiteSpace(entry.Name))
            {
                logger.LogWarning("Skipping config route with missing Name or Path.");
                continue;
            }

            // Normalise path
            var path = entry.Path.StartsWith('/') ? entry.Path : "/" + entry.Path;
            var method = entry.HttpMethod.ToUpper();
            var isTemplate = RoutePathMatcher.IsTemplate(path) || entry.HttpMethod == "*";

            // Duplicate check — match on path + method + template flag
            var exists = await db.RouteConfigs
                .AnyAsync(r => r.Path == path &&
                               r.HttpMethod.ToUpper() == method &&
                               r.IsTemplate == isTemplate);

            if (exists)
            {
                logger.LogDebug("Route {Method} {Path} already exists in DB, skipping seed.", method, path);
                continue;
            }

            if (!Enum.TryParse<RouteMode>(entry.Mode, ignoreCase: true, out var mode))
            {
                logger.LogWarning("Invalid Mode '{Mode}' for route '{Name}', defaulting to Mock.", entry.Mode, entry.Name);
                mode = RouteMode.Mock;
            }

            db.RouteConfigs.Add(new RouteConfig
            {
                Name = entry.Name,
                Path = path,
                HttpMethod = method,
                Mode = mode,
                IsTemplate = isTemplate,
                MockBody = entry.MockBody,
                MockStatusCode = entry.MockStatusCode,
                MockContentType = entry.MockContentType,
                ProxyDestination = entry.ProxyDestination,
                IsEnabled = entry.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            added++;
            logger.LogInformation("Seeded route from config: [{Method}] {Path} ({Mode}{Template})",
                method, path, mode, isTemplate ? ", template" : "");
        }

        if (added > 0)
            await db.SaveChangesAsync();

        logger.LogInformation("Route seeding complete. {Added} route(s) added from config.", added);
    }
}
