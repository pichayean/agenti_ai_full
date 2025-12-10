using Microsoft.Extensions.Configuration;

namespace AgenticAI.Infrastructure;

public record McpItem(string Name, string Endpoint, bool Enable);

public static class McpConfigReader
{
    public static List<McpItem> GetMcpAvalible(IConfiguration cfg)
    {
        var section = cfg.GetSection("McpServers");
        var list = new List<McpItem>();
        foreach (var child in section.GetChildren())
        {
            var name = child["Name"] ?? "mcp";
            var endpoint = child["Endpoint"] ?? "http://localhost:3000/sse";
            var enable = bool.TryParse(child["Enable"], out var e) ? e : true;
            list.Add(new McpItem(name, endpoint, enable));
        }
        return list;
    }
}
