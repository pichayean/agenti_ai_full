namespace AgenticAI.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgenticAI.Models;

#region TestCase Schema (minimal as discussed)
public class TestCase
{
    public string Id { get; set; } = "";
    public RequestBlock Request { get; set; } = new();
    public PolicyBlock Policy { get; set; } = new();
    public MetricsBlock Metrics { get; set; } = new();
    public ChannelsBlock Channels { get; set; } = new();
}

public class RequestBlock { public string Prompt { get; set; } = ""; public string? Lang { get; set; } = "th"; }

public class PolicyBlock { public ToolsPolicyBlock Tools { get; set; } = new(); }
public class ToolsPolicyBlock
{
    public List<string> Allowed { get; set; } = new();
    public List<string> Required { get; set; } = new();
    public List<string> Forbidden { get; set; } = new();
    public bool Strict { get; set; } = true;
}

public class MetricsBlock
{
    public WtrConfig Wtr { get; set; } = new();
    public PlanValidityConfig PlanValidity { get; set; } = new();
    public AccuracyConfig Accuracy { get; set; } = new();
}
public class WtrConfig { public bool Enabled { get; set; } = true; }
public class PlanValidityConfig
{
    public string Level { get; set; } = "L2"; // L1|L2
    public bool RequireAllSucceeded { get; set; } = true;
}
public class AccuracyConfig
{
    public ContainmentConfig Containment { get; set; } = new();
    public bool CaseInsensitive { get; set; } = true;
    public bool NormalizeWhitespace { get; set; } = true;
    public double Threshold { get; set; } = 1.0; // coverage needed to pass
}
public class ContainmentConfig
{
    public string Source { get; set; } = "final"; // "final" only in this minimal version
    public List<string> MustContain { get; set; } = new();
    public List<string> MustNotContain { get; set; } = new();
}

public class ChannelsBlock { public EmailBlock Email { get; set; } = new(); }
public class EmailBlock { public bool Allowed { get; set; } = true; public bool Required { get; set; } = false; public List<string>? To { get; set; } }
#endregion

#region Evaluation Report
public class EvaluationReport
{
    public string TestCaseId { get; set; } = "";
    public string RunId { get; set; } = "";
    public ToolSection Tools { get; set; } = new();
    public ValiditySection PlanValidity { get; set; } = new();
    public AccuracySection Accuracy { get; set; } = new();
    public ChannelSection Channels { get; set; } = new();

    //################################################
    public double OverallScore { get; set; }
    public string? OverallScoreTxt { get; set; }
    public bool OverallPassed { get; set; }
    public double FinalComplianceScore { get; set; }
    public double WtrScore { get; set; }
    public double ChannelScore { get; set; }

    public bool Passed =>
        Tools.FinalCompliance &&
        (!PlanValidity.RequiredAllSucceeded || PlanValidity.IsValid) &&
        Accuracy.Passed &&
        Channels.Passed;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Evaluation Report: {TestCaseId} | Run: {RunId} ===");
        sb.AppendLine($"[WTR] WTR={Tools.WtrStep:P2}, Coverage={Tools.RequiredCoverage:P2}, ForbiddenUsed={Tools.ForbiddenUsed}, FinalCompliance={Tools.FinalCompliance}, Replan={Tools.ReplanCount}");
        if (Tools.Violations?.Count > 0)
            sb.AppendLine("     Violations: " + string.Join("; ", Tools.Violations.Select(v => $"{v.StepId}:{v.Reason}({v.ToolFullName})")));
        sb.AppendLine($"[PlanValidity] Score={PlanValidity.Score:P2}, IsValid={PlanValidity.IsValid}, InvalidSteps=[{string.Join(",", PlanValidity.InvalidSteps)}]");
        sb.AppendLine($"[Accuracy] Coverage={Accuracy.Coverage:P2}, Passed={Accuracy.Passed}, Missing=[{string.Join(", ", Accuracy.Missing)}], ForbiddenHit=[{string.Join(", ", Accuracy.ForbiddenHit)}]");
        sb.AppendLine($"[Channels] EmailAllowed={Channels.EmailAllowed}, EmailRequired={Channels.EmailRequired}, EmailSatisfied={Channels.EmailSatisfied}");
        sb.AppendLine($"OVERALL PASSED: {Passed}");
        return sb.ToString();
    }
}

public class ToolSection
{
    public double WtrStep { get; set; }
    public double RequiredCoverage { get; set; }
    public bool ForbiddenUsed { get; set; }
    public bool FinalCompliance { get; set; }
    public int ReplanCount { get; set; }
    public int InvokedCount { get; set; }
    public int WrongInvocations { get; set; }
    public HashSet<string> UsedSet { get; set; } = new();
    public List<Violation> Violations { get; set; } = new();
}
public class Violation { public string StepId { get; set; } = ""; public string ToolFullName { get; set; } = ""; public string Reason { get; set; } = ""; }

public class ValiditySection
{
    public double Score { get; set; }
    public bool IsValid { get; set; }
    public bool RequiredAllSucceeded { get; set; }
    public List<string> InvalidSteps { get; set; } = new();
}

public class AccuracySection
{
    public double Coverage { get; set; }
    public bool Passed { get; set; }
    public List<string> Missing { get; set; } = new();
    public List<string> ForbiddenHit { get; set; } = new();
}

public class ChannelSection
{
    public bool Passed => (!EmailRequired || EmailSatisfied) && (EmailAllowed || !EmailSatisfied);
    public bool EmailAllowed { get; set; }
    public bool EmailRequired { get; set; }
    public bool EmailSatisfied { get; set; }
}
#endregion

