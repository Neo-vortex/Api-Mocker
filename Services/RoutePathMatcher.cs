namespace ApiMocker.Services;

/// <summary>
/// Matches incoming request paths against route patterns.
/// 
/// Supported wildcards:
///   *   matches any characters within a single path segment (no slashes)
///         e.g. /api/*/users  matches  /api/123/users  but NOT /api/123/456/users
///   **  matches any characters across multiple path segments (including slashes)
///         e.g. /api/**       matches  /api/foo  AND  /api/foo/bar/baz
///
/// Priority (highest to lowest):
///   1. Exact match          /api/users
///   2. Single-segment glob  /api/*/users
///   3. Multi-segment glob   /api/**
/// </summary>
public static class RoutePathMatcher
{
    public static bool IsTemplate(string path) =>
        path.Contains('*');

    /// <summary>
    /// Returns true if <paramref name="requestPath"/> matches <paramref name="pattern"/>.
    /// </summary>
    public static bool Matches(string pattern, string requestPath)
    {
        // Normalise — case-insensitive, no trailing slash
        pattern     = pattern.TrimEnd('/').ToLowerInvariant();
        requestPath = requestPath.TrimEnd('/').ToLowerInvariant();

        return GlobMatch(pattern, requestPath);
    }

    /// <summary>
    /// From a list of enabled routes, find the best match for the incoming path + method.
    /// Exact routes beat templates; among templates, longer (more specific) patterns win.
    /// </summary>
    public static Models.RouteConfig? FindBestMatch(
        IEnumerable<Models.RouteConfig> routes,
        string requestPath,
        string httpMethod)
    {
        var method = httpMethod.ToUpper();

        Models.RouteConfig? exactMatch    = null;
        Models.RouteConfig? templateMatch = null;
        int templateSpecificity = -1;

        foreach (var route in routes)
        {
            var routeMethod = route.HttpMethod.ToUpper();

            // Method must match exactly or route method is wildcard *
            if (routeMethod != "*" && routeMethod != method)
                continue;

            if (!route.IsTemplate)
            {
                // Exact path match
                if (string.Equals(route.Path.TrimEnd('/'), requestPath.TrimEnd('/'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    exactMatch = route;
                    break; // can't do better than exact
                }
            }
            else
            {
                // Template match — pick the most specific (longest pattern)
                if (Matches(route.Path, requestPath))
                {
                    var specificity = route.Path.Length;
                    if (specificity > templateSpecificity)
                    {
                        templateSpecificity = specificity;
                        templateMatch = route;
                    }
                }
            }
        }

        return exactMatch ?? templateMatch;
    }

    // ── Glob engine ──────────────────────────────────────────────────────────

    private static bool GlobMatch(string pattern, string input)
    {
        // Convert pattern into segments and match iteratively
        // ** is handled as a special "consume anything" token

        int pi = 0, ii = 0;
        int patternStar = -1, inputStar = -1;

        while (ii < input.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == input[ii] || pattern[pi] == '?'))
            {
                pi++; ii++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                // Check if this is **
                bool isDoublestar = pi + 1 < pattern.Length && pattern[pi + 1] == '*';

                if (isDoublestar)
                {
                    // ** — skip both asterisks, remember position for backtrack
                    patternStar = pi;
                    inputStar   = ii;
                    pi += 2;
                    // skip optional trailing slash after **
                    if (pi < pattern.Length && pattern[pi] == '/') pi++;
                }
                else
                {
                    // * — remember position for backtrack
                    patternStar = pi;
                    inputStar   = ii;
                    pi++;
                }
            }
            else if (patternStar >= 0)
            {
                // Backtrack: was last wildcard a ** or *?
                bool wasDoublestar = patternStar + 1 < pattern.Length
                                     && pattern[patternStar + 1] == '*';

                if (wasDoublestar)
                {
                    // ** can consume slashes — advance input by one
                    inputStar++;
                    ii = inputStar;
                    pi = patternStar + 2;
                    if (pi < pattern.Length && pattern[pi] == '/') pi++;
                }
                else
                {
                    // * cannot consume '/'
                    if (input[inputStar] == '/')
                        return false; // single * can't cross segment boundary

                    inputStar++;
                    ii = inputStar;
                    pi = patternStar + 1;
                }
            }
            else
            {
                return false;
            }
        }

        // Consume remaining pattern tokens (trailing ** or *)
        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        if (pi < pattern.Length && pattern[pi] == '*') pi++;

        return pi >= pattern.Length;
    }
}
