namespace AgenticAI.Common;

public static class NormalizeExtension
{
    public static string SubstituteVars(this string prompt, IReadOnlyDictionary<string, object?> outputs)
    {
        var p = prompt ?? "";
        foreach (var kv in outputs)
        {
            var val = kv.Value is string s ? s
                     : System.Text.Json.JsonSerializer.Serialize(kv.Value);
            val ??= "";

            p = p.Replace("[[" + kv.Key + "]]", val);
            p = p.Replace("{{" + kv.Key + "}}", val);
        }
        return p;
    }
}
