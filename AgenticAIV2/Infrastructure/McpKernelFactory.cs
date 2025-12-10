using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace AgenticAI.Infrastructure;

public static class SkKernelFactory
{
    public static void AddAddMCPServer(this IServiceCollection services, IConfiguration cfg)
    {
        var mcpList = McpConfigReader.GetMcpAvalible(cfg);
        services.AddTransient<TraceHttpHandler>();

        foreach (var mcp in mcpList)
        {
            if (mcp.Enable)
            {
                services.AddKeyedSingleton<IMcpClient>(mcp.Name, (sp, key) =>
                {
                    var transport = new SseClientTransport(new SseClientTransportOptions
                    {
                        Name = mcp.Name,
                        Endpoint = new Uri(mcp.Endpoint),
                        TransportMode = HttpTransportMode.StreamableHttp
                    });
                    return McpClientFactory.CreateAsync(transport).GetAwaiter().GetResult();
                });
            }
        }
    }

    public static void AddModelAiProviderKernel(this IServiceCollection services, IConfiguration cfg)
    {
        var mcpList = McpConfigReader.GetMcpAvalible(cfg);
        services.AddSingleton<ToolsAvailable>();
        services.AddSingleton(sp =>
        {
            var kb = Kernel.CreateBuilder();
            var useOllama = cfg.GetValue<bool>("UseOllama");
            if (useOllama)
            {
                kb.AddOpenAIChatCompletion(
                    modelId: cfg["Ollama:ChatModel"] ?? "gpt-oss:20b",
                    apiKey: "ollama",
                    serviceId: "ollama",
                    httpClient: new HttpClient { BaseAddress = new Uri(cfg["Ollama:BaseUrl"] ?? "http://localhost:11434/v1") }
                );
            }
            else
            {
                kb.AddOpenAIChatCompletion(
                    modelId: cfg["OpenRouter:ChatModel"] ?? "gpt-4o-mini",
                    apiKey: cfg["OpenRouter:ApiKey"] ?? "YOUR_KEY",
                    serviceId: "openrouter",
                    httpClient: new HttpClient { BaseAddress = new Uri(cfg["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1") }
                );
            }

            var toolsAvailable = sp.GetRequiredService<ToolsAvailable>();
            foreach (var mcpClient in mcpList)
            {
                if (mcpClient.Enable)
                {
                    var mcpServer = sp.GetRequiredKeyedService<IMcpClient>(mcpClient.Name);
                    var mcpTools = mcpServer.ListToolsAsync().GetAwaiter().GetResult();

                    kb.Plugins.AddFromFunctions(pluginName: mcpClient.Name, functions: mcpTools.Select(x => x.AsKernelFunction()));
                    toolsAvailable.SetMcpAvailable(mcpTools, mcpClient.Name);
                }
            }

            return kb.Build();
        });
    }
}
