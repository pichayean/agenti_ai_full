using System.Text;
using System.Text.Json;
using AgenticAI.Models;
using AgenticAI.Services;
using Azure;

namespace AgenticAI.Evaluation;

public class EvalRunner
{
    private readonly IWebHostEnvironment _env;
    private readonly OrchestratorService _orchestratorService;

    public EvalRunner(IWebHostEnvironment env, OrchestratorService orchestratorService)
    {
        _env = env;
        _orchestratorService = orchestratorService;
    }

    public async Task<List<CaseReport>> RunAllAsync(CancellationToken ct)
    {
        // 1️⃣ โหลด TestCase ทั้งหมด
        var filePath = Path.Combine(_env.ContentRootPath, "Evaluation", "question.json");
        if (!File.Exists(filePath))
            throw new Exception($"File not found: {filePath}");

        var json = await File.ReadAllTextAsync(filePath, ct);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var tests = JsonSerializer.Deserialize<TestCase[]>(json, options)
                    ?? throw new Exception("Failed to parse question.json");

        // 2️⃣ เตรียมที่เก็บผล
        var reports = new List<CaseReport>();

        // 3️⃣ รันแต่ละ test case
        foreach (var test in tests)
        {
            var runId = Guid.NewGuid().ToString();

            try
            {
                // เรียก orchestrator (model agent)
                //var resp = await _orchestratorService.Exec(
                //    new ChatRequest(test.Request.Prompt, runId),
                //    ct
                //);

                async Task SendStatus(string status)
                {
                    var data = $"data: {status}\n\n";
                    Console.WriteLine(data);
                }
                var resp = await _orchestratorService.ExecWithProgress(
                    new ChatRequest(test.Request.Prompt, runId),
                    ct, SendStatus
                );

                // ประเมินผลด้วย Evaluator
                var eval = Evaluator.Evaluate(resp, test);

                // เก็บผลเป็นรายงานสั้น ๆ
                reports.Add(new CaseReport
                {
                    TestCaseId = test.Id,
                    RunId = runId,
                    Passed = eval.Passed,
                    Wtr = eval.Tools.WtrStep,
                    PlanValidity = eval.PlanValidity.Score,
                    Accuracy = eval.Accuracy.Coverage,
                    Summary = eval.ToString(),
                    OverallScore = eval.OverallScore,
                    OverallPassed = eval.OverallPassed,
                    ChannelScore = eval.ChannelScore,
                    FinalComplianceScore = eval.FinalComplianceScore,
                    WtrScore = eval.WtrScore,

                    Evdt = resp,
                    Detail = eval
                });
            }
            catch
            {
            }
        }

        // 4️⃣ สรุปรวม
        PrintSummary(reports);
        return reports;
    }

    private static void PrintSummary(List<CaseReport> reports)
    {
        Console.WriteLine("\n=========== Evaluation Summary ===========");
        foreach (var r in reports)
        {
            var status = r.Passed ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"{r.TestCaseId} ({r.RunId}) → {status} | " +
                              $"WTR={r.Wtr:P0}, Validity={r.PlanValidity:P0}, Accuracy={r.Accuracy:P0}");
        }

        var total = reports.Count;
        var passed = reports.Count(r => r.Passed);
        Console.WriteLine($"------------------------------------------");
        Console.WriteLine($"TOTAL: {total}, PASSED: {passed}, FAILED: {total - passed}");
        Console.WriteLine($"==========================================\n");
    }
}

public class CaseReport
{
    public string TestCaseId { get; set; } = "";
    public string RunId { get; set; } = "";
    public bool Passed { get; set; }
    public double Wtr { get; set; }
    public double PlanValidity { get; set; }
    public double Accuracy { get; set; }
    public string Summary { get; set; } = "";

    public double OverallScore { get; set; }
    public bool OverallPassed { get; set; }
    public double FinalComplianceScore { get; set; }
    public double WtrScore { get; set; }
    public double ChannelScore { get; set; }

    public ChatResponse? Evdt { get; set; }
    public EvaluationReport? Detail { get; set; }
}