
# API Mocker

A self-hosted, infrastructure-native HTTP traffic gateway built with ASP.NET Core 10. Sits inside your Docker network and acts as a transparent proxy between your services and external dependencies — with per-route control over mocking, transformation, rate limiting, retries, and real-time observability.
<img width="1913" height="383" alt="image" src="https://github.com/user-attachments/assets/e8839cde-d38a-4344-bd52-fc9f43f46e3b" />


---

## What it is

API Mocker is **not** a unit testing tool or a mock server for developers. It is a **deployable traffic control layer** that runs permanently inside your infrastructure. Every route can independently proxy traffic to a real upstream, return a static mock response, or do both depending on configuration.

It was built specifically for environments where external services are unreliable, behind internal networks, or require specific header manipulation before requests can reach them — such as ESB gateways, payment processors, or legacy SOAP/REST integration layers.
<img width="1913" height="692" alt="image" src="https://github.com/user-attachments/assets/32a91630-d77d-4fdc-b7cd-e4e999e1757c" />
<img width="1907" height="929" alt="image" src="https://github.com/user-attachments/assets/dee51fc0-5143-4952-a1ae-64162db79c01" />


---

## Features

### Routing
- Per-route configuration with method + path matching
- **Wildcard / template routes** using glob syntax (`*` for single segment, `**` for multiple)
- Method wildcard (`*`) to match any HTTP verb
- Exact routes always take priority over templates

### Proxy mode
- Transparent HTTP forwarding to any destination
- Full request and response header copying
- Configurable **retry policy** — retries on non-2xx responses and network errors/timeouts
- Per-route **SSL trust** — bypass certificate validation for internal services with self-signed certs
- **Health checks** — background HTTP ping per destination at a configurable interval, shown inline on the routes page

### Mock mode
- Configurable status code, Content-Type, and response body
- Instant response without touching any upstream

### Lua scripting
- Per-route Lua scripts for **request transformation** (runs before forwarding) and **response transformation** (runs before returning to caller)
- Full access to headers, body, path, query string
- Helper functions: `json_decode()`, `json_encode()`
- Scripts run on both Proxy and Mock mode routes
- Errors in scripts are caught and logged — a bad script never crashes a request

### Rate limiting
- Per-route sliding window rate limiter
- Returns `429 Too Many Requests` with `Retry-After` header when limit is exceeded

### Observability
- Full request/response logging with timestamps, client IP, duration, mode, retry count
- **Before/after Lua diff view** in log detail — shows original and transformed headers/body side by side when a script made changes
- **Real-time live feed** via SignalR — auto-scrolling request stream with pause/resume and live stats (total, 2xx, 4xx, 5xx, avg duration)
- **Timeline view** — 24h traffic charts (volume, duration, mock vs proxy) and a 7-day × 24-hour request heatmap
- Log sorting by newest or oldest first, filterable by route

---

## Getting Started

### Prerequisites
- Docker and Docker Compose
- .NET 8 SDK (for local development only)

### Run with Docker Compose

Add to your existing `docker-compose.yml`:

```yaml
services:
  api-mocker:
    container_name: api_mocker_container
    image: docker.yourdomain.com/api-mocker:latest
    ports:
      - "6010:8080"
    volumes:
      - api_mocker_data:/app/data
    environment:
      - ConnectionStrings__Default=Data Source=/app/data/apimocker.db
    networks:
      - app_network

volumes:
  api_mocker_data:
```

The web UI is available at `http://localhost:6010`.

### Build the Docker image

```bash
docker build -f ApiMocker/Dockerfile -t docker.yourdomain.com/api-mocker:latest .
docker push docker.yourdomain.com/api-mocker:latest
```

### Run locally

```bash
cd ApiMocker
dotnet run
```

---

## Offline / Air-gapped Environments

The UI has no runtime internet dependency. All frontend assets (Bootstrap, Chart.js, CodeMirror, SignalR) must be downloaded once and bundled into `wwwroot/lib/` before deployment.

**On a machine with internet access**, run from the project root:

```bash
# Linux / macOS
chmod +x download-assets.sh && ./download-assets.sh

# Windows (PowerShell)
.\download-assets.ps1
```

Then copy the full project (with the populated `wwwroot/lib/`) to your offline environment.

---

## Configuration

### Route seeding from environment / appsettings

Routes can be pre-seeded from `appsettings.json` or environment variables. Seeded routes are only inserted if the same path + method combination does not already exist in the database.

**appsettings.json:**
```json
{
  "MockerRoutes": [
    {
      "Name": "Publish Issue Draft",
      "Path": "/gateway-sync/send/message",
      "HttpMethod": "POST",
      "Mode": "Proxy",
      "ProxyDestination": "http://10.100.8.224:9080",
      "IsEnabled": true
    },
    {
      "Name": "ESB Gateway Wildcard",
      "Path": "/gateway-sync/send/message/**",
      "HttpMethod": "POST",
      "Mode": "Proxy",
      "ProxyDestination": "http://10.100.8.224:9080",
      "IsEnabled": true
    }
  ]
}
```

**Via environment variables** (Docker / `.env`):
```dotenv
MockerRoutes__0__Name=Publish Issue Draft
MockerRoutes__0__Path=/gateway-sync/send/message
MockerRoutes__0__HttpMethod=POST
MockerRoutes__0__Mode=Proxy
MockerRoutes__0__ProxyDestination=http://10.100.8.224:9080
MockerRoutes__0__IsEnabled=true
```

---

## Wildcard / Template Routes

Use `*` or `**` in the path to match multiple endpoints with one route:

| Pattern | Matches | Does not match |
|---|---|---|
| `/api/*/users` | `/api/123/users` | `/api/123/456/users` |
| `/api/**` | `/api/foo`, `/api/foo/bar/baz` | `/other/path` |
| `/gateway-sync/send/message/**` | Any path under that prefix | Anything outside it |

Exact routes always win over templates. Among multiple matching templates, the longest (most specific) pattern wins.

Template routes are automatically detected when you save — any path containing `*` is marked as a template.

---

## Lua Scripting

Each route can have two independent Lua scripts: one that runs before the request is forwarded (or mocked), and one that runs before the response is returned to the caller.

### Available objects

**In the request script:**
```lua
request.Method        -- HTTP method (string)
request.Path          -- request path (string)
request.QueryString   -- query string including ? (string)
request.Body          -- request body (string, read/write)
request.Headers       -- headers table (read/write via methods below)

request:GetHeader("key")         -- returns value or ""
request:SetHeader("key", "val")  -- add or overwrite a header
request:RemoveHeader("key")      -- remove a header
```

**In the response script:**
```lua
response.StatusCode   -- HTTP status code (number, read/write)
response.Body         -- response body (string, read/write)
response.Headers      -- headers (read/write via methods below)

response:GetHeader("key")
response:SetHeader("key", "val")
response:RemoveHeader("key")
```

**Helper functions (both scripts):**
```lua
json_decode(str)    -- parse JSON string into a Lua table
json_encode(table)  -- serialize a Lua table to JSON string
os.time()           -- Unix timestamp
os.date(fmt)        -- formatted date string
```

### Example: inject authentication headers
```lua
-- Request script
request:SetHeader("Authorization", "Bearer eyJhbGciOiJIUzI1NiJ9.your-token")
request:SetHeader("X-Api-Key", "my-secret-key")
request:SetHeader("X-Request-Time", os.date("!%Y-%m-%dT%H:%M:%SZ"))
```

### Example: add correlation ID if missing
```lua
-- Request script
if request:GetHeader("X-Correlation-Id") == "" then
    request:SetHeader("X-Correlation-Id", "mocker-" .. os.time())
end
```

### Example: modify request body
```lua
-- Request script
local data = json_decode(request.Body)
if data ~= nil then
    data["source"] = "api-mocker"
    data["env"] = "staging"
    request.Body = json_encode(data)
end
```

### Example: normalize error responses
```lua
-- Response script
if response.StatusCode >= 400 then
    local body = json_decode(response.Body)
    local message = "Unknown error"
    if body ~= nil and body["message"] ~= nil then
        message = body["message"]
    end
    response.Body = json_encode({
        success = false,
        error = message,
        statusCode = response.StatusCode
    })
    response:SetHeader("Content-Type", "application/json")
end
```

### Example: add CORS headers
```lua
-- Response script
response:SetHeader("Access-Control-Allow-Origin", "*")
response:SetHeader("Access-Control-Allow-Headers", "Content-Type, Authorization")
```

### Example: remove sensitive fields from response
```lua
-- Response script
local data = json_decode(response.Body)
if data ~= nil then
    data["password"] = nil
    data["internalToken"] = nil
    response.Body = json_encode(data)
end
```

Script errors are silently caught and logged — a broken script will never cause a request to fail. The original request/response passes through unchanged if a script errors.

---

## Health Checks

Enable health checks on any Proxy mode route. The background service pings the destination URL (HTTP GET) at the configured interval and updates the route status.

**Current check type: Ping** (HTTP GET to the destination base URL). Additional check types (custom endpoint, TCP, etc.) will be added in future versions.

Status is shown inline on the Routes page:
- 🟢 **Up** — last ping returned a non-5xx response
- 🔴 **Down** — last ping failed or returned 5xx (hover for the error message and last check time)
- ⚪ **Pending** — no check has run yet since the route was enabled

---

## Rate Limiting

Per-route sliding window rate limiter. When the limit is exceeded:
- Returns `429 Too Many Requests`
- Includes `Retry-After: <window_seconds>` header
- Returns a JSON error body

---

## Retry Policy

Applies to Proxy mode routes only. Retries on:
- Any non-2xx HTTP response from the upstream
- Network errors (connection refused, timeout, DNS failure)

Retry count and delay between retries are configurable per route. The number of retry attempts is stored in the log and visible in both the log list and detail view.

---

## Log Detail — Before/After Lua View

When a route has Lua scripts configured and a script modifies headers or body, the log detail page automatically switches to a two-column layout:

- **Left column** — the original request/response as it arrived (before script)
- **Right column** — the transformed version that was actually forwarded or returned (after script)

Changed headers are marked with a `◆` indicator and a subtle highlight. Changed body content gets a colored left border.

If a script ran but made no changes, a badge indicates this without splitting the layout.

---

## SSL / Certificate Trust

For proxy routes targeting internal services with self-signed or corporate CA certificates, enable **Trust insecure SSL** on the route. This bypasses certificate validation for that route only. A warning badge is shown on the routes list whenever this option is active.

> ⚠️ Only enable this for known internal services. Do not use against untrusted external hosts.

---

## Project Structure

```
ApiMocker/
├── Controllers/
│   ├── HomeController.cs       # Dashboard
│   ├── RoutesController.cs     # Route CRUD
│   ├── LogsController.cs       # Log list + detail
│   └── FeedController.cs       # Live feed + timeline
├── Data/
│   └── AppDbContext.cs         # EF Core SQLite context
├── Hubs/
│   └── RequestFeedHub.cs       # SignalR hub
├── Models/
│   ├── RouteConfig.cs          # DB entities
│   ├── ViewModels.cs           # MVC view models
│   ├── LogPanelModel.cs        # Partial view model
│   └── RouteConfigEntry.cs     # appsettings POCO
├── Services/
│   ├── ProxyService.cs         # HTTP forwarding with retry
│   ├── LuaScriptService.cs     # NLua script execution
│   ├── HealthCheckService.cs   # Background health pinger
│   ├── RateLimiterService.cs   # Sliding window rate limiter
│   ├── RoutePathMatcher.cs     # Glob path matching
│   └── RouteSeederService.cs   # Config → DB seeding
├── Views/
│   ├── Routes/                 # Route list, create, edit forms
│   ├── Logs/                   # Log list, detail, panel partial
│   ├── Feed/                   # Live feed, timeline/heatmap
│   └── Shared/_Layout.cshtml   # Dark theme layout
├── MockerMiddleware.cs         # Core intercept/dispatch pipeline
├── Program.cs                  # DI + app configuration
├── download-assets.sh          # Offline asset downloader (Linux/macOS)
├── download-assets.ps1         # Offline asset downloader (Windows)
└── Dockerfile
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 / ASP.NET Core |
| Database | SQLite via Entity Framework Core |
| Lua engine | NLua |
| Real-time | SignalR |
| UI framework | Bootstrap 5.3 (dark mode) |
| Charts | Chart.js 4 |
| Code editor | CodeMirror 5 |
| Container | Docker |

---

## Database

SQLite is used for simplicity and portability. The database file is stored at `/app/data/apimocker.db` inside the container. Mount a named volume to persist it across container restarts.

The schema is created automatically on first startup via `EnsureCreated()`. **When adding new columns** in development (new features), the existing database will not be automatically migrated — either delete the file to recreate it, or apply the column additions manually via `sqlite3`.

---

## Roadmap

- [ ] Capture & replay — record real proxy traffic and replay it as a mock
- [ ] Circuit breaker — auto-switch to mock fallback when upstream fails repeatedly
- [ ] Health check types — custom endpoint, TCP port check
- [ ] Request/response body diffing in log detail
- [ ] Route export/import as JSON
- [ ] Route groups/tags
- [ ] Scenario switching — toggle named sets of routes at once
- [ ] Management REST API
