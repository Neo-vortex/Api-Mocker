using ApiMocker.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.TotalRoutes = await db.RouteConfigs.CountAsync();
        ViewBag.ActiveRoutes = await db.RouteConfigs.CountAsync(r => r.IsEnabled);
        ViewBag.TotalLogs = await db.RequestLogs.CountAsync();
        ViewBag.RecentLogs = await db.RequestLogs
            .Include(l => l.RouteConfig)
            .OrderByDescending(l => l.Timestamp)
            .Take(10)
            .ToListAsync();
        return View();
    }
}
