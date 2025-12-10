using System.Text.Json;
using AgenticAI.Common;
using AgenticAI.Models;
using Microsoft.SemanticKernel;

namespace AgenticAI.Services;

public class ComposerService
{
    private readonly Kernel _kernel;
    private readonly ILogger<ComposerService> _logger;
    private readonly string _prompt;
    private readonly TokenEstimator _est;
    private readonly ILLMCounterAccessor _llm;

    public ComposerService(Kernel kernel, ILogger<ComposerService> logger, ILLMCounterAccessor llm)
    {
        _kernel = kernel;
        _logger = logger;
        _est = new TokenEstimator
        {
            LatinCharsPerToken = 3.8,
            ThaiCharsPerToken = 1.6,
            CjkCharsPerToken = 1.4
        };
        _prompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", "composer.system.md"));
        _llm = llm;

    }

    public async Task<(string screenMarkdown, object? email)> ComposeAsync(string userTask, Journal journal, CancellationToken ct = default)
    {
        var emailTemplate = "Subject: {{Subject}} – {{date}}\n\n{{body}}";
        var policy = "ความยาวโดยรวมไม่เกิน 300 คำ";
        var sysRaw = $"{_prompt}\n<USER_TASK/>{userTask}</USER_TASK>\n" +
            $"<EXECUTION_JOURNAL/>{System.Text.Json.JsonSerializer.Serialize(journal)}</EXECUTION_JOURNAL>\n" +
            $"<EMAIL_TEMPLATE/>{emailTemplate}</EMAIL_TEMPLATE>\n<POLICY/>{policy}</POLICY>";

        var prompt = PromptEscaper.EscapeForSemanticKernel(sysRaw);   // <— สำคัญ
        var res = await _kernel.InvokePromptAsync(prompt);

        var estimate = _est.Estimate(prompt);
        _llm.Current.Add("composer", estimate.Tokens, 0, "gpt-4o-mini");

        var json = res.ToString().CleanJsonString();
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var screen = root.TryGetProperty("screen_markdown", out var sm) ? sm.GetString() ?? "" : json;
            var email = root.TryGetProperty("email", out var em) ? em.Deserialize<object>() : null;
            return (screen, email);
        }
        catch
        {
            return (json, null);
        }
    }
}
