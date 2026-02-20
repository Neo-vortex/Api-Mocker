using ApiMocker.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker.Controllers;

public class FeedController(AppDbContext db) : Controller
{
    public IActionResult Index() => View();

    public async Task<IActionResult> Timeline(string granularity = "minute")
    {
        ViewBag.Granularity = granularity;

        // Load last 24h of logs
        var since = DateTime.UtcNow.AddHours(-24);
        var logs = await db.RequestLogs
            .Where(l => l.Timestamp >= since)
            .Select(l => new { l.Timestamp, l.ResponseStatusCode, l.DurationMs, l.Mode })
            .ToListAsync();

        // Group by time bucket
        var grouped = granularity == "hour"
            ? logs.GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month,
                                             l.Timestamp.Day, l.Timestamp.Hour, 0, 0))
            : logs.GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month,
                                             l.Timestamp.Day, l.Timestamp.Hour, l.Timestamp.Minute, 0));

        var buckets = grouped.Select(g => new TimelineBucket
        {
            Time = g.Key,
            Total = g.Count(),
            Success = g.Count(x => x.ResponseStatusCode >= 200 && x.ResponseStatusCode < 300),
            Errors = g.Count(x => x.ResponseStatusCode >= 400),
            AvgDurationMs = g.Any() ? (long)g.Average(x => x.DurationMs) : 0,
            MockCount = g.Count(x => x.Mode == "Mock"),
            ProxyCount = g.Count(x => x.Mode == "Proxy")
        })
        .OrderBy(b => b.Time)
        .ToList();

        return View(buckets);
    }
}

public class TimelineBucket
{
    public DateTime Time { get; set; }
    public int Total { get; set; }
    public int Success { get; set; }
    public int Errors { get; set; }
    public long AvgDurationMs { get; set; }
    public int MockCount { get; set; }
    public int ProxyCount { get; set; }
}
