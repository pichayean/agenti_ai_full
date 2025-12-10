namespace AgenticAI.Common
{
    public static class MCPExtension
    {
        public static bool IsMcpError(this object? output)
        {
            if (output is null) return false;
            try
            {
                // พยายาม parse เป็น JSON ถ้าเป็นไปได้
                var json = output is string s ? s : System.Text.Json.JsonSerializer.Serialize(output);
                if (string.IsNullOrWhiteSpace(json)) return false;

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // case รูปแบบ { "isError": true, "content": [...] }
                if (root.TryGetProperty("isError", out var isErr) && isErr.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;

                // case มี content[].text บอก An error occurred...
                if (root.TryGetProperty("content", out var content) && content.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var t) &&
                            t.GetString()?.StartsWith("An error occurred invoking") == true)
                            return true;
                    }
                }
            }
            catch
            {
                // ไม่ใช่ JSON ก็ข้าม
            }
            return false;
        }

    }
}
