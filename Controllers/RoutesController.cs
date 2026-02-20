using ApiMocker.Data;
using ApiMocker.Models;
using ApiMocker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker.Controllers;

public class RoutesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var routes = await db.RouteConfigs
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new RouteConfigViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Path = r.Path,
                HttpMethod = r.HttpMethod,
                Mode = r.Mode.ToString(),
                IsTemplate = r.IsTemplate,
                IsEnabled = r.IsEnabled,
                TrustInsecureSsl = r.TrustInsecureSsl,
                HealthCheckEnabled = r.HealthCheckEnabled,
                LastHealthCheckPassed = r.LastHealthCheckPassed,
                LastHealthCheckError = r.LastHealthCheckError,
                LastHealthCheckAt = r.LastHealthCheckAt,
                RateLimitEnabled = r.RateLimitEnabled,
                RateLimitRequests = r.RateLimitRequests,
                RateLimitWindowSeconds = r.RateLimitWindowSeconds,
                RetryEnabled = r.RetryEnabled,
                RetryCount = r.RetryCount,
                LogCount = r.RequestLogs.Count,
                LastUsed = r.RequestLogs.OrderByDescending(l => l.Timestamp)
                            .Select(l => (DateTime?)l.Timestamp).FirstOrDefault()
            })
            .ToListAsync();

        return View(routes);
    }

    public IActionResult Create() => View(new RouteConfigViewModel
    {
        MockStatusCode = 200,
        MockContentType = "application/json",
        HealthCheckIntervalSeconds = 30,
        RateLimitRequests = 100,
        RateLimitWindowSeconds = 60,
        RetryCount = 3,
        RetryDelayMs = 500
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RouteConfigViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        db.RouteConfigs.Add(MapToEntity(vm, new RouteConfig()));
        await db.SaveChangesAsync();
        TempData["Success"] = $"Route '{vm.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var r = await db.RouteConfigs.FindAsync(id);
        if (r == null) return NotFound();
        return View(MapToViewModel(r));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RouteConfigViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var r = await db.RouteConfigs.FindAsync(id);
        if (r == null) return NotFound();

        MapToEntity(vm, r);
        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = $"Route '{r.Name}' updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await db.RouteConfigs.FindAsync(id);
        if (r == null) return NotFound();
        db.RouteConfigs.Remove(r);
        await db.SaveChangesAsync();
        TempData["Success"] = "Route deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id)
    {
        var r = await db.RouteConfigs.FindAsync(id);
        if (r == null) return NotFound();
        r.IsEnabled = !r.IsEnabled;
        r.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Json(new { enabled = r.IsEnabled });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static RouteConfig MapToEntity(RouteConfigViewModel vm, RouteConfig r)
    {
        r.Name = vm.Name;
        r.Path = vm.Path.StartsWith('/') ? vm.Path : "/" + vm.Path;
        r.HttpMethod = vm.HttpMethod.ToUpper();
        r.Mode = Enum.Parse<RouteMode>(vm.Mode);
        r.IsTemplate = RoutePathMatcher.IsTemplate(vm.Path) || vm.HttpMethod == "*";
        r.MockBody = vm.MockBody;
        r.MockStatusCode = vm.MockStatusCode;
        r.MockContentType = vm.MockContentType;
        r.ProxyDestination = vm.ProxyDestination;
        r.TrustInsecureSsl = vm.TrustInsecureSsl;
        r.RequestScript = vm.RequestScript;
        r.ResponseScript = vm.ResponseScript;
        r.HealthCheckEnabled = vm.HealthCheckEnabled;
        r.HealthCheckIntervalSeconds = vm.HealthCheckIntervalSeconds;
        r.RateLimitEnabled = vm.RateLimitEnabled;
        r.RateLimitRequests = vm.RateLimitRequests;
        r.RateLimitWindowSeconds = vm.RateLimitWindowSeconds;
        r.RetryEnabled = vm.RetryEnabled;
        r.RetryCount = vm.RetryCount;
        r.RetryDelayMs = vm.RetryDelayMs;
        r.IsEnabled = vm.IsEnabled;
        return r;
    }

    private static RouteConfigViewModel MapToViewModel(RouteConfig r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Path = r.Path,
        HttpMethod = r.HttpMethod,
        Mode = r.Mode.ToString(),
        IsTemplate = r.IsTemplate,
        MockBody = r.MockBody,
        MockStatusCode = r.MockStatusCode,
        MockContentType = r.MockContentType,
        ProxyDestination = r.ProxyDestination,
        TrustInsecureSsl = r.TrustInsecureSsl,
        RequestScript = r.RequestScript,
        ResponseScript = r.ResponseScript,
        HealthCheckEnabled = r.HealthCheckEnabled,
        HealthCheckIntervalSeconds = r.HealthCheckIntervalSeconds,
        LastHealthCheckPassed = r.LastHealthCheckPassed,
        LastHealthCheckError = r.LastHealthCheckError,
        LastHealthCheckAt = r.LastHealthCheckAt,
        RateLimitEnabled = r.RateLimitEnabled,
        RateLimitRequests = r.RateLimitRequests,
        RateLimitWindowSeconds = r.RateLimitWindowSeconds,
        RetryEnabled = r.RetryEnabled,
        RetryCount = r.RetryCount,
        RetryDelayMs = r.RetryDelayMs,
        IsEnabled = r.IsEnabled,
    };
}
