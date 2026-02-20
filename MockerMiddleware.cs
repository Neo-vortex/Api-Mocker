using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ApiMocker.Data;
using ApiMocker.Hubs;
using ApiMocker.Models;
using ApiMocker.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ApiMocker;

public class MockerMiddleware(RequestDelegate next)
{
    private static readonly string[] ManagedPrefixes =
    [
        "/routes", "/logs", "/home", "/api/routes", "/api/logs",
        "/_framework", "/css", "/js", "/lib", "/favicon",
        "/feed", "/timeline", "/hubs"
    ];

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db,
        ProxyService proxy,
        LuaScriptService lua,
        RateLimiterService rateLimiter,
        IHubContext<RequestFeedHub> hub)
    {
        var path = context.Request.Path.Value ?? "/";

        if (ManagedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || path == "/")
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();

        var method = context.Request.Method.ToUpper();
        var allRoutes = await db.RouteConfigs.Where(r => r.IsEnabled).ToListAsync();
        var route = RoutePathMatcher.FindBestMatch(allRoutes, path, method);

        if (route == null) { await next(context); return; }

        // ── Rate limiting ────────────────────────────────────────────────────
        if (route.RateLimitEnabled && !rateLimiter.IsAllowed(route.Id, route.RateLimitRequests, route.RateLimitWindowSeconds))
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Content-Type"] = "application/json";
            context.Response.Headers["Retry-After"] = route.RateLimitWindowSeconds.ToString();
            await context.Response.WriteAsync(
                $"{{\"error\":\"Rate limit exceeded. Max {route.RateLimitRequests} requests per {route.RateLimitWindowSeconds}s.\"}}");
            return;
        }

        // ── Read incoming request ────────────────────────────────────────────
        context.Request.Body.Position = 0;
        var rawBody = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Deep-copy headers so the original snapshot is never mutated by Lua
        var rawHeaders = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // ── Snapshot BEFORE Lua request script ───────────────────────────────
        var originalRequestHeaders = JsonSerializer.Serialize(rawHeaders);
        var originalRequestBody    = rawBody;

        // ── Run Lua request script ───────────────────────────────────────────
        // Pass a COPY of headers so Lua mutations don't affect the original snapshot
        var luaReqCtx = new LuaRequestContext
        {
            Method      = method,
            Path        = path,
            QueryString = context.Request.QueryString.ToString(),
            Headers     = rawHeaders.ToDictionary(kv => kv.Key, kv => kv.Value), // copy
            Body        = rawBody
        };

        bool requestScriptRan = !string.IsNullOrWhiteSpace(route.RequestScript);
        if (requestScriptRan)
            luaReqCtx = lua.RunRequestScript(route.RequestScript, luaReqCtx);

        // What was actually forwarded (after script)
        var transformedRequestHeaders = JsonSerializer.Serialize(luaReqCtx.Headers);
        var transformedRequestBody    = luaReqCtx.Body;

        var sw = Stopwatch.StartNew();

        int    responseStatus;
        string responseBody;
        string responseHeaders;
        string contentType;
        int    retryAttempts = 0;

        // ── Snapshots for response (before Lua) ──────────────────────────────
        int    originalResponseStatus;
        string originalResponseHeaders;
        string originalResponseBody;

        if (route.Mode == RouteMode.Mock)
        {
            originalResponseStatus  = route.MockStatusCode;
            originalResponseBody    = route.MockBody ?? "{}";
            originalResponseHeaders = JsonSerializer.Serialize(
                new Dictionary<string, string> { ["Content-Type"] = route.MockContentType });

            var luaRespCtx = new LuaResponseContext
            {
                StatusCode = originalResponseStatus,
                Headers    = new Dictionary<string, string> { ["Content-Type"] = route.MockContentType }, // already fresh
                Body       = originalResponseBody
            };

            bool responseScriptRan = !string.IsNullOrWhiteSpace(route.ResponseScript);
            if (responseScriptRan)
                luaRespCtx = lua.RunResponseScript(route.ResponseScript, luaRespCtx);

            sw.Stop();

            responseStatus  = luaRespCtx.StatusCode;
            responseBody    = luaRespCtx.Body;
            contentType     = luaRespCtx.Headers.TryGetValue("Content-Type", out var ct) ? ct : route.MockContentType;
            responseHeaders = JsonSerializer.Serialize(luaRespCtx.Headers);

            context.Response.StatusCode = responseStatus;
            foreach (var (k, v) in luaRespCtx.Headers)
                try { context.Response.Headers[k] = v; } catch { }
            if (!context.Response.Headers.ContainsKey("Content-Type"))
                context.Response.Headers["Content-Type"] = contentType;

            await context.Response.WriteAsync(responseBody);

            await PersistAndBroadcast(db, hub, route, clientIp, method, path, context,
                originalRequestHeaders, originalRequestBody,
                transformedRequestHeaders, transformedRequestBody,
                originalResponseStatus, originalResponseHeaders, originalResponseBody,
                responseStatus, responseHeaders, responseBody,
                sw.ElapsedMilliseconds, retryAttempts,
                requestScriptRan, responseScriptRan);
        }
        else // Proxy
        {
            var result = await proxy.ForwardAsync(
                luaReqCtx.Method, luaReqCtx.Path, luaReqCtx.QueryString,
                luaReqCtx.Headers, luaReqCtx.Body,
                route.ProxyDestination!,
                route.TrustInsecureSsl,
                route.RetryEnabled ? route.RetryCount : 0,
                route.RetryDelayMs);

            retryAttempts = result.RetryAttempts;

            // Snapshot BEFORE Lua response script
            originalResponseStatus  = result.StatusCode;
            originalResponseHeaders = JsonSerializer.Serialize(result.Headers);
            originalResponseBody    = result.Body;

            var luaRespCtx = new LuaResponseContext
            {
                StatusCode = result.StatusCode,
                Headers    = result.Headers.ToDictionary(kv => kv.Key, kv => kv.Value), // copy
                Body       = result.Body
            };

            bool responseScriptRan = !string.IsNullOrWhiteSpace(route.ResponseScript);
            if (responseScriptRan)
                luaRespCtx = lua.RunResponseScript(route.ResponseScript, luaRespCtx);

            sw.Stop();

            responseStatus  = luaRespCtx.StatusCode;
            responseBody    = luaRespCtx.Body;
            contentType     = luaRespCtx.Headers.TryGetValue("Content-Type", out var ct2) ? ct2 : result.ContentType;
            responseHeaders = JsonSerializer.Serialize(luaRespCtx.Headers);

            context.Response.StatusCode = responseStatus;
            var skipResp = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Transfer-Encoding", "Connection" };
            foreach (var (k, v) in luaRespCtx.Headers)
                if (!skipResp.Contains(k))
                    try { context.Response.Headers[k] = v; } catch { }
            if (!context.Response.Headers.ContainsKey("Content-Type"))
                context.Response.Headers["Content-Type"] = contentType;

            await context.Response.WriteAsync(responseBody);

            await PersistAndBroadcast(db, hub, route, clientIp, method, path, context,
                originalRequestHeaders, originalRequestBody,
                transformedRequestHeaders, transformedRequestBody,
                originalResponseStatus, originalResponseHeaders, originalResponseBody,
                responseStatus, responseHeaders, responseBody,
                sw.ElapsedMilliseconds, retryAttempts,
                requestScriptRan, responseScriptRan);
        }
    }

    private static async Task PersistAndBroadcast(
        AppDbContext db,
        IHubContext<RequestFeedHub> hub,
        RouteConfig route,
        string clientIp,
        string method,
        string path,
        HttpContext context,
        string originalRequestHeaders, string originalRequestBody,
        string transformedRequestHeaders, string transformedRequestBody,
        int originalResponseStatus, string originalResponseHeaders, string originalResponseBody,
        int responseStatus, string responseHeaders, string responseBody,
        long durationMs, int retryAttempts,
        bool requestScriptRan, bool responseScriptRan)
    {
        var log = new RequestLog
        {
            RouteConfigId          = route.Id,
            Timestamp              = DateTime.UtcNow,
            ClientIp               = clientIp,
            Method                 = method,
            Path                   = path,
            QueryString            = context.Request.QueryString.ToString(),

            OriginalRequestHeaders = originalRequestHeaders,
            OriginalRequestBody    = originalRequestBody,
            RequestHeaders         = transformedRequestHeaders,
            RequestBody            = transformedRequestBody,

            OriginalResponseStatusCode = originalResponseStatus,
            OriginalResponseHeaders    = originalResponseHeaders,
            OriginalResponseBody       = originalResponseBody,
            ResponseStatusCode         = responseStatus,
            ResponseHeaders            = responseHeaders,
            ResponseBody               = responseBody,

            DurationMs         = durationMs,
            Mode               = route.Mode.ToString(),
            RetryAttempts      = retryAttempts,
            RequestScriptRan   = requestScriptRan,
            ResponseScriptRan  = responseScriptRan
        };

        db.RequestLogs.Add(log);
        await db.SaveChangesAsync();

        await hub.Clients.All.SendAsync("RequestReceived", new RequestFeedEvent
        {
            LogId         = log.Id,
            RouteName     = route.Name,
            Method        = method,
            Path          = path,
            QueryString   = context.Request.QueryString.ToString(),
            ClientIp      = clientIp,
            StatusCode    = responseStatus,
            DurationMs    = durationMs,
            Mode          = route.Mode.ToString(),
            RetryAttempts = retryAttempts,
            Timestamp     = log.Timestamp.ToString("HH:mm:ss.fff")
        });
    }
}
