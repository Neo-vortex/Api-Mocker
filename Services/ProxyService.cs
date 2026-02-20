using System.Net;
using System.Text;

namespace ApiMocker.Services;

public class ProxyService(IHttpClientFactory httpClientFactory, ILogger<ProxyService> logger)
{
    private static readonly HashSet<string> SkipRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Transfer-Encoding", "Connection", "Keep-Alive",
        "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailers", "Upgrade"
    };

    // Reusable client that accepts any certificate (for internal/self-signed certs)
    private static readonly HttpClient _insecureClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    public async Task<ProxyResult> ForwardAsync(
        string method,
        string path,
        string queryString,
        Dictionary<string, string> headers,
        string body,
        string destination,
        bool trustInsecureSsl = false,
        int retryCount = 0,
        int retryDelayMs = 500)
    {
        // Use insecure client when SSL bypass requested, otherwise use the named factory client
        var client = trustInsecureSsl
            ? _insecureClient
            : httpClientFactory.CreateClient("proxy");

        var destUri = destination.TrimEnd('/') + path + queryString;
        var maxAttempts = retryCount + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var request = BuildRequest(method, destUri, headers, body);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await client.SendAsync(request);
                sw.Stop();

                var statusCode = (int)response.StatusCode;
                var responseBody = await response.Content.ReadAsStringAsync();
                var responseHeaders = response.Headers
                    .Concat(response.Content.Headers)
                    .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                if ((statusCode < 200 || statusCode >= 300) && attempt < maxAttempts)
                {
                    logger.LogWarning("Proxy {Method} {Uri} → {Status}. Retry {Attempt}/{Max}",
                        method, destUri, statusCode, attempt, maxAttempts);
                    await Task.Delay(retryDelayMs);
                    continue;
                }

                return new ProxyResult
                {
                    StatusCode = statusCode,
                    Body = responseBody,
                    Headers = responseHeaders,
                    DurationMs = sw.ElapsedMilliseconds,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    RetryAttempts = attempt - 1
                };
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("Proxy {Method} {Uri} threw {Error}. Retry {Attempt}/{Max}",
                    method, destUri, ex.Message, attempt, maxAttempts);
                await Task.Delay(retryDelayMs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Proxy {Method} {Uri} failed after {Attempts} attempt(s)", method, destUri, attempt);
                return new ProxyResult
                {
                    StatusCode = 502,
                    Body = $"{{\"error\":\"Proxy error after {attempt} attempt(s): {ex.Message}\"}}",
                    Headers = [],
                    DurationMs = 0,
                    ContentType = "application/json",
                    RetryAttempts = attempt - 1
                };
            }
        }

        return new ProxyResult { StatusCode = 502, Body = "{\"error\":\"Unknown proxy error\"}", ContentType = "application/json" };
    }

    private static HttpRequestMessage BuildRequest(string method, string uri,
        Dictionary<string, string> headers, string body)
    {
        var request = new HttpRequestMessage
        {
            Method = new HttpMethod(method),
            RequestUri = new Uri(uri)
        };

        foreach (var (key, value) in headers)
        {
            if (!SkipRequestHeaders.Contains(key))
                request.Headers.TryAddWithoutValidation(key, value);
        }

        if (!string.IsNullOrEmpty(body))
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            if (headers.TryGetValue("Content-Type", out var ct))
                content.Headers.TryAddWithoutValidation("Content-Type", ct);
            request.Content = content;
        }

        return request;
    }
}

public class ProxyResult
{
    public int StatusCode { get; set; }
    public string Body { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = [];
    public long DurationMs { get; set; }
    public string ContentType { get; set; } = "";
    public int RetryAttempts { get; set; } = 0;
}

