using AgenticAI.Services;

namespace AgenticAI.Models;

public record ChatRequest(string Message, string? SessionId);
public record ChatResponse(string Final, object? Email
    , Plan Plan, List<Plan> PlanHistories, Journal Journal, string RunId, LLMCounter LLMHistory);
public record PlanRequest(string Message);
public record PlanResponse(Plan Plan, ValidationResult Validation);
public record ExecuteRequest(Plan Plan);
public record ExecuteResponse(Journal Journal, string Status);




public class LLMRequestEntry
{
    public string TaskName { get; set; } = "";
    public string? Model { get; set; }
    public string? PromptPreview { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int TotalTokens => PromptTokens + CompletionTokens;
}

public class LLMCounter
{
    private readonly List<LLMRequestEntry> _entries = new();

    public void Add(string taskName, int promptTokens, int completionTokens, string? model = null, string? preview = null)
    {
        _entries.Add(new LLMRequestEntry
        {
            TaskName = taskName,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            Model = model,
            PromptPreview = preview,
            Timestamp = DateTime.UtcNow
        });
    }

    public int TotalPromptTokens => _entries.Sum(e => e.PromptTokens);
    public int TotalCompletionTokens => _entries.Sum(e => e.CompletionTokens);
    public int TotalTokens => _entries.Sum(e => e.TotalTokens);
    public IReadOnlyList<LLMRequestEntry> Entries => _entries.AsReadOnly();
}

