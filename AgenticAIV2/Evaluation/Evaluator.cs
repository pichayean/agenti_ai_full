using System.Text;
using AgenticAI.Evaluation;
using AgenticAI.Models;

public static class Evaluator
{
    // Entry point
    public static EvaluationReport Evaluate(ChatResponse resp, TestCase tc)
    {
        var report = new EvaluationReport
        {
            TestCaseId = tc.Id,
            RunId = resp.RunId
        };

        // 1) WTR
        report.Tools = ComputeWtr(resp, tc.Policy.Tools);

        // 2) Plan Validity (L2)
        report.PlanValidity = ComputePlanValidityL2(resp, requireAllSucceeded: tc.Metrics.PlanValidity.RequireAllSucceeded);

        // 3) Accuracy (containment on final)
        report.Accuracy = ComputeAccuracyContainment(resp.Final ?? string.Empty, tc.Metrics.Accuracy);

        // 4) Channels (email simple check: allowed/required & present)
        report.Channels = EvaluateChannels(resp, tc.Channels);

        // 5) Overall score (NEW: ใช้คะแนนทศนิยมรวมทุก metric)
        ComputeOverallScore(report, tc);

        return report;
    }

    #region WTR
    private static ToolSection ComputeWtr(ChatResponse resp, ToolsPolicyBlock policy)
    {
        var result = new ToolSection();
        var plan = resp.Plan ?? new Plan();
        var toolSteps = (plan.Steps ?? new()).Where(s => string.Equals(s.Type, "tool", StringComparison.OrdinalIgnoreCase)).ToList();

        result.InvokedCount = toolSteps.Count;
        HashSet<string> used = new();

        foreach (var s in toolSteps)
        {
            var fullname = $"{s.Plugin}.{s.Tool}";
            used.Add(fullname);

            if (policy.Forbidden.Contains(fullname))
            {
                result.WrongInvocations++;
                result.Violations.Add(new Violation { StepId = s.Id, ToolFullName = fullname, Reason = "tool_forbidden" });
            }
            else if (!policy.Allowed.Contains(fullname))
            {
                result.WrongInvocations++;
                result.Violations.Add(new Violation { StepId = s.Id, ToolFullName = fullname, Reason = "tool_not_allowed" });
            }
        }

        result.UsedSet = used;
        result.ForbiddenUsed = used.Intersect(policy.Forbidden).Any();
        result.WtrStep = result.InvokedCount == 0 ? 0 : (double)result.WrongInvocations / result.InvokedCount;

        var reqHit = used.Intersect(policy.Required).Count();
        result.RequiredCoverage = policy.Required.Count == 0 ? 1.0 : (double)reqHit / policy.Required.Count;

        result.ReplanCount = Math.Max(0, (resp.PlanHistories?.Count ?? 0) - 1);

        // เดิม FinalCompliance เป็น boolean แบบ binary
        // ตอนนี้ให้ความหมายว่า "ไม่มี wrong tool และไม่ใช้ forbidden"
        // ส่วนเรื่อง requiredCoverage จะไปสะท้อนในคะแนนรวมแทน
        result.FinalCompliance = (result.WtrStep == 0) && !result.ForbiddenUsed;

        return result;
    }
    #endregion

    #region Plan Validity L2
    private static ValiditySection ComputePlanValidityL2(ChatResponse resp, bool requireAllSucceeded)
    {
        var plan = resp.Plan ?? new Plan();
        var steps = plan.Steps ?? new List<PlanStep>();
        var order = steps.Select(s => s.Id).ToList();

        var dependsOn = steps.ToDictionary(
            s => s.Id,
            s => (s.DependsOn ?? new List<string>()));

        var journalMap = (resp.Journal?.Steps ?? new List<JournalStep>())
            .GroupBy(j => j.Id)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DurationMs).First()); // last by duration as tie-break

        bool Succeeded(string id) =>
            journalMap.TryGetValue(id, out var j) &&
            string.Equals(j.Status, "succeeded", StringComparison.OrdinalIgnoreCase);

        var indexById = order.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        int total = order.Count;
        int validCount = 0;
        var invalid = new List<string>();

        foreach (var sid in order)
        {
            bool ok = true;

            // self succeeded
            if (!Succeeded(sid)) ok = false;

            // deps: exist, come before, succeeded
            foreach (var dep in dependsOn[sid])
            {
                if (!indexById.ContainsKey(dep)) { ok = false; break; }
                if (indexById[dep] >= indexById[sid]) { ok = false; break; }
                if (!Succeeded(dep)) { ok = false; break; }
            }

            if (ok) validCount++; else invalid.Add(sid);
        }

        var score = total == 0 ? 1.0 : (double)validCount / total;
        var isValid = invalid.Count == 0;

        // ไม่บีบให้ fail ทั้งเคสจากตรงนี้แล้ว
        // requireAllSucceeded ยังเก็บไว้เป็น flag แต่นำไปใช้ใน overall score แทน
        return new ValiditySection
        {
            Score = score,
            IsValid = isValid,
            RequiredAllSucceeded = requireAllSucceeded,
            InvalidSteps = invalid
        };
    }
    #endregion

    #region Accuracy (Containment)
    private static AccuracySection ComputeAccuracyContainment(string finalText, AccuracyConfig cfg)
    {
        var text = finalText ?? string.Empty;
        if (cfg.NormalizeWhitespace) text = NormalizeWs(text);

        var comp = cfg.CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        int total = cfg.Containment.MustContain?.Count ?? 0;
        if (total == 0) return new AccuracySection { Coverage = 1.0, Passed = true };

        int found = 0;
        var missing = new List<string>();
        foreach (var token in cfg.Containment.MustContain)
        {
            var t = cfg.NormalizeWhitespace ? NormalizeWs(token ?? "") : token ?? "";
            if (!string.IsNullOrWhiteSpace(t) && text.Contains(t, comp)) found++;
            else missing.Add(token);
        }

        var forbiddenHit = new List<string>();
        foreach (var f in cfg.Containment.MustNotContain ?? new List<string>())
        {
            var t = cfg.NormalizeWhitespace ? NormalizeWs(f ?? "") : f ?? "";
            if (!string.IsNullOrWhiteSpace(t) && text.Contains(t, comp)) forbiddenHit.Add(f);
        }

        double coverage = (double)found / total;
        bool passed = coverage >= cfg.Threshold && forbiddenHit.Count == 0;

        return new AccuracySection
        {
            Coverage = coverage,
            Passed = passed,
            Missing = missing,
            ForbiddenHit = forbiddenHit
        };
    }

    private static string NormalizeWs(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        bool inWs = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!inWs) { sb.Append(' '); inWs = true; }
            }
            else { sb.Append(ch); inWs = false; }
        }
        return sb.ToString().Trim();
    }
    #endregion

    #region Channels
    private static ChannelSection EvaluateChannels(ChatResponse resp, ChannelsBlock ch)
    {
        bool emailPresent = resp.Email != null;
        return new ChannelSection
        {
            EmailAllowed = ch.Email.Allowed,
            EmailRequired = ch.Email.Required,
            EmailSatisfied = emailPresent
        };
    }
    #endregion

    //#endregion
    #region Overall Score (NEW)
    private static void ComputeOverallScore(EvaluationReport report, TestCase tc)
    {
        var tools = report.Tools;
        var plan = report.PlanValidity;
        var acc = report.Accuracy;
        var ch = report.Channels;

        // 1) WTR → 0..1
        double wtrScore = tools.InvokedCount == 0 ? 1.0 : 1.0 - tools.WtrStep;
        if (wtrScore < 0) wtrScore = 0.0;

        // 2) Final-compliance แบบทศนิยม (ใช้สะท้อนคุณภาพการใช้ tools)
        double finalComplianceScore = 0.5 * wtrScore + 0.5 * tools.RequiredCoverage;

        // 3) semantic coverage จาก containment (ความครอบคลุม/ถูกต้องตาม expected)
        double coverageScore = acc.Coverage;      // 0..1

        // 4) Plan validity
        double planScore = plan.Score;            // 0..1

        // 5) Channels (เน้นเรื่อง email ตาม config)
        double channelScore = 1.0;
        if (ch.EmailRequired)
        {
            channelScore = ch.EmailSatisfied ? 1.0 : 0.0;
        }
        else if (!ch.EmailAllowed && ch.EmailSatisfied)
        {
            // มีส่ง email ทั้งที่ not allowed → หักหน่อย
            channelScore = 0.5;
        }

        // 6) รวมเป็น "final accuracy" ที่เอา containment + plan + tools + channels มาคิดรวม
        double finalAccuracy =
            0.15 * coverageScore +
            0.3 * wtrScore +
            0.35 * planScore +
            0.15 * finalComplianceScore +
            0.05 * channelScore;

        var summaryTxt = $@"0.15 * {coverageScore} +
            0.3 * {wtrScore} +
            0.35 * {planScore} +
            0.15 * {finalComplianceScore} +
            0.05 * {channelScore};";
        // hard fail rule
        bool hardFail =
            tools.ForbiddenUsed ||
            (acc.ForbiddenHit != null && acc.ForbiddenHit.Count > 0);

        // ใช้ Threshold เดิมจาก accuracy config
        double threshold = tc.Metrics?.Accuracy?.Threshold ?? 0.75;
        bool passed = !hardFail && finalAccuracy >= threshold;

        // map กลับเข้า report
        report.OverallScore = finalAccuracy;
        report.OverallPassed = passed;
        report.FinalComplianceScore = finalComplianceScore;
        report.WtrScore = wtrScore;
        report.ChannelScore = channelScore;

        // ใช้ Accuracy เป็นตัวแทน "ความถูกต้องรวม" ตามที่ต้องการ
        report.Accuracy.Coverage = finalAccuracy;
        report.Accuracy.Passed = passed;
        report.OverallScoreTxt = summaryTxt;
    }
    #endregion
}
