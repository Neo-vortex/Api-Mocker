using ApiMocker.Data;
using ApiMocker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker.Controllers;

public class LogsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? routeId, int page = 1, string sort = "desc")
    {
        const int pageSize = 50;

        var query = db.RequestLogs.Include(l => l.RouteConfig).AsQueryable();

        if (routeId.HasValue)
            query = query.Where(l => l.RouteConfigId == routeId);

        query = sort == "asc"
            ? query.OrderBy(l => l.Timestamp)
            : query.OrderByDescending(l => l.Timestamp);

        var total = await query.CountAsync();
        var logs = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new LogViewModel
            {
                Id = l.Id,
                RouteName = l.RouteConfig.Name,
                Timestamp = l.Timestamp,
                ClientIp = l.ClientIp,
                Method = l.Method,
                Path = l.Path,
                QueryString = l.QueryString,
                RequestHeaders = l.RequestHeaders,
                RequestBody = l.RequestBody,
                ResponseStatusCode = l.ResponseStatusCode,
                ResponseHeaders = l.ResponseHeaders,
                ResponseBody = l.ResponseBody,
                DurationMs = l.DurationMs,
                Mode = l.Mode,
                RetryAttempts = l.RetryAttempts
            })
            .ToListAsync();

        ViewBag.Total = total;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.RouteId = routeId;
        ViewBag.Sort = sort;
        ViewBag.Routes = await db.RouteConfigs.Select(r => new { r.Id, r.Name }).ToListAsync();

        return View(logs);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var log = await db.RequestLogs.Include(l => l.RouteConfig).FirstOrDefaultAsync(l => l.Id == id);
        if (log == null) return NotFound();

        return View(new LogViewModel
        {
            Id                         = log.Id,
            RouteName                  = log.RouteConfig.Name,
            Timestamp                  = log.Timestamp,
            ClientIp                   = log.ClientIp,
            Method                     = log.Method,
            Path                       = log.Path,
            QueryString                = log.QueryString,
            OriginalRequestHeaders     = log.OriginalRequestHeaders,
            OriginalRequestBody        = log.OriginalRequestBody,
            RequestHeaders             = log.RequestHeaders,
            RequestBody                = log.RequestBody,
            OriginalResponseStatusCode = log.OriginalResponseStatusCode,
            OriginalResponseHeaders    = log.OriginalResponseHeaders,
            OriginalResponseBody       = log.OriginalResponseBody,
            ResponseStatusCode         = log.ResponseStatusCode,
            ResponseHeaders            = log.ResponseHeaders,
            ResponseBody               = log.ResponseBody,
            DurationMs                 = log.DurationMs,
            Mode                       = log.Mode,
            RetryAttempts              = log.RetryAttempts,
            RequestScriptRan           = log.RequestScriptRan,
            ResponseScriptRan          = log.ResponseScriptRan
        });
    }

    [HttpPost]
    public async Task<IActionResult> Clear(int? routeId)
    {
        var query = db.RequestLogs.AsQueryable();
        if (routeId.HasValue) query = query.Where(l => l.RouteConfigId == routeId);
        db.RequestLogs.RemoveRange(query);
        await db.SaveChangesAsync();
        TempData["Success"] = "Logs cleared.";
        return RedirectToAction(nameof(Index), routeId.HasValue ? new { routeId } : null);
    }
}
