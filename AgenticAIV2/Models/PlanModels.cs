namespace AgenticAI.Models;

public class Plan
{
    public string Version { get; set; } = "1.0";
    public string Goal { get; set; } = "";
    public Constraints Constraints { get; set; } = new();
    public List<PlanStep> Steps { get; set; } = new();
}
public class Constraints { public int MaxSteps { get; set; } = 8; public int TimeoutSec { get; set; } = 120; }
public class PlanStep
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "tool"; // tool | llm
    public string? Plugin { get; set; }
    public string? Tool { get; set; }
    public Dictionary<string, object?>? Params { get; set; }
    public string? Prompt { get; set; }
    public string Output { get; set; } = "result";
    public List<string>? DependsOn { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; } = false;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Plan? NormalizedPlan { get; set; }
}

public class Journal
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public List<JournalStep> Steps { get; set; } = new();
}
public class JournalStep
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "succeeded";
    public long DurationMs { get; set; }
    public object? Output { get; set; }
    public object? Input { get; set; }
    public string? Error { get; set; }
}
