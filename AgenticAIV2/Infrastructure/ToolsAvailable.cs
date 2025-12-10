using System.Collections.Concurrent;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace AgenticAI.Infrastructure;

public class ToolsAvailable
{
    private readonly ConcurrentDictionary<string, List<McpClientTool>> _tools = new();
    public void SetMcpAvailable(IList<McpClientTool> tools, string pluginName)
        => _tools[pluginName] = tools.ToList();

    public IEnumerable<(string plugin, McpClientTool tool)> AllTools()
        => _tools.SelectMany(kv => kv.Value.Select(t => (kv.Key, t)));
}
