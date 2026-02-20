using NLua;
using System.Text.Json;

namespace ApiMocker.Services;

/// <summary>
/// Mutable context passed into Lua scripts.
/// Scripts can read and write Headers and Body freely.
/// </summary>
public class LuaRequestContext
{
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string QueryString { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = "";

    // Helpers exposed to Lua
    public string GetHeader(string key) =>
        Headers.TryGetValue(key, out var v) ? v : "";

    public void SetHeader(string key, string value) =>
        Headers[key] = value;

    public void RemoveHeader(string key) =>
        Headers.Remove(key);
}

public class LuaResponseContext
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = "";

    public string GetHeader(string key) =>
        Headers.TryGetValue(key, out var v) ? v : "";

    public void SetHeader(string key, string value) =>
        Headers[key] = value;

    public void RemoveHeader(string key) =>
        Headers.Remove(key);
}

public class LuaScriptService(ILogger<LuaScriptService> logger)
{
    /// <summary>
    /// Runs a Lua request script. Returns the (possibly modified) context.
    /// If script is null/empty or errors, the original context is returned unchanged.
    /// </summary>
    public LuaRequestContext RunRequestScript(string? script, LuaRequestContext ctx)
    {
        if (string.IsNullOrWhiteSpace(script)) return ctx;

        try
        {
            using var lua = new Lua();
            lua.State.Encoding = System.Text.Encoding.UTF8;

            // Expose context object
            lua["request"] = ctx;

            // Expose json helpers
            lua.RegisterFunction("json_decode", typeof(LuaScriptService)
                .GetMethod(nameof(JsonDecode), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
            lua.RegisterFunction("json_encode", typeof(LuaScriptService)
                .GetMethod(nameof(JsonEncode), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));

            lua.DoString(script);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Request Lua script error: {Error}", ex.Message);
        }

        return ctx;
    }

    /// <summary>
    /// Runs a Lua response script. Returns the (possibly modified) context.
    /// </summary>
    public LuaResponseContext RunResponseScript(string? script, LuaResponseContext ctx)
    {
        if (string.IsNullOrWhiteSpace(script)) return ctx;

        try
        {
            using var lua = new Lua();
            lua.State.Encoding = System.Text.Encoding.UTF8;

            lua["response"] = ctx;

            lua.RegisterFunction("json_decode", typeof(LuaScriptService)
                .GetMethod(nameof(JsonDecode), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));
            lua.RegisterFunction("json_encode", typeof(LuaScriptService)
                .GetMethod(nameof(JsonEncode), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));

            lua.DoString(script);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Response Lua script error: {Error}", ex.Message);
        }

        return ctx;
    }

    // ── Lua-exposed helpers ──────────────────────────────────────────────

    public static LuaTable? JsonDecode(Lua lua, string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonElementToLuaTable(lua, doc.RootElement);
        }
        catch { return null; }
    }

    public static string JsonEncode(LuaTable table)
    {
        var dict = LuaTableToDict(table);
        return JsonSerializer.Serialize(dict);
    }

    private static LuaTable JsonElementToLuaTable(Lua lua, JsonElement element)
    {
        lua.DoString("__tmp = {}");
        var table = (LuaTable)lua["__tmp"];

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                table[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    JsonValueKind.Null   => null,
                    _                    => prop.Value.ToString()
                };
            }
        }

        return table;
    }

    private static Dictionary<string, object?> LuaTableToDict(LuaTable table)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var key in table.Keys)
        {
            var val = table[key];
            dict[key.ToString()!] = val is LuaTable lt ? LuaTableToDict(lt) : val;
        }
        return dict;
    }
}
