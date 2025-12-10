using AgenticAI.Common;
using AgenticAI.Infrastructure;
using AgenticAI.Models;
using Microsoft.SemanticKernel;

namespace AgenticAI.Services;

public class PlannerService
{
    private readonly Kernel _kernel;
    private readonly ToolsAvailable _tools;
    private readonly ILogger<PlannerService> _logger;
    private readonly string _prompt;
    private readonly TokenEstimator _est;
    private readonly ILLMCounterAccessor _llm;

    public PlannerService(Kernel kernel, ToolsAvailable tools,
        ILogger<PlannerService> logger, ILLMCounterAccessor llm)
    {
        _kernel = kernel;
        _tools = tools;
        _logger = logger;
        _llm = llm;
        _est = new TokenEstimator
        {
            LatinCharsPerToken = 3.8,
            ThaiCharsPerToken = 1.6,
            CjkCharsPerToken = 1.4
        };
        _prompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "planner.system.md"));
    }

    private string BuildSystemPrompt(string userTask, string? feedback = null)
    {
        var toolsCatalog = string.Join("\n", _tools.AllTools().Select(t => $"- {t.plugin}.{t.tool.Name}: {t.tool.Description}"));
        var sysRaw = _prompt + $"\n\n<TOOLS_CATALOG/>\n{toolsCatalog}\n</TOOLS_CATALOG>\n<USER_TASK/>{userTask}</USER_TASK>";
        if (!string.IsNullOrWhiteSpace(feedback))
            sysRaw += $"\n<FEEDBACK/>แผนเดิมมีปัญหา: {feedback}</FEEDBACK>";

        var sys = PromptEscaper.EscapeForSemanticKernel(sysRaw);   // <— สำคัญ
        return sys;
    }

    public async Task<Plan> CreatePlanAsync(string userTask, CancellationToken ct = default)
    {
        var sys = BuildSystemPrompt(userTask);
        var result = await _kernel.InvokePromptAsync(sys, new KernelArguments(), cancellationToken: ct);

        var estimate = _est.Estimate(sys);
        _llm.Current.Add("planner", estimate.Tokens, 0, "gpt-4o-mini");

        var json = result.ToString();
        _logger.LogInformation("Planner raw: {json}", json);
        return System.Text.Json.JsonSerializer.Deserialize<Plan>(json.CleanJsonString(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new Plan { Goal = "empty" };
    }

    public async Task<Plan> RePlanAsync(string userTask, string feedback, CancellationToken ct = default)
    {
        var sys = BuildSystemPrompt(userTask, feedback);
        var result = await _kernel.InvokePromptAsync(sys, new KernelArguments(), cancellationToken: ct);

        var estimate = _est.Estimate(sys);
        _llm.Current.Add("planner", estimate.Tokens, 0, "gpt-4o-mini");

        var json = result.ToString();
        _logger.LogInformation("Re-Plan raw: {json}", json);
        return System.Text.Json.JsonSerializer.Deserialize<Plan>(json.CleanJsonString(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new Plan { Goal = "empty" };
    }
}
