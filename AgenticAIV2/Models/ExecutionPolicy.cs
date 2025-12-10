namespace AgenticAI.Models;
public class ExecutionPolicy
{
    public int MaxReplan { get; set; } = 2;
    public int MaxRetryPerStep { get; set; } = 2;
    public int RetryInitialDelaySec { get; set; } = 2;
}
