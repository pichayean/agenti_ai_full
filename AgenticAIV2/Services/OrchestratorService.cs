using System.Text.Json;
using AgenticAI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgenticAI.Services;

public class OrchestratorService
{
    #region Fields
    private readonly PlannerService _planner;
    private readonly ValidatorService _validator;
    private readonly ToolsExecutor _executor;
    private readonly ComposerService _composer;
    private readonly RetentionStore _store;
    private readonly ExecutionPolicy _policy;
    private readonly ILLMCounterAccessor _llm;
    #endregion

    public OrchestratorService(PlannerService planner,
        ValidatorService validator, ToolsExecutor executor,
        ComposerService composer, RetentionStore store,
        IOptions<ExecutionPolicy> policy, ILLMCounterAccessor llm)
    {
        _planner = planner;
        _validator = validator;
        _executor = executor;
        _composer = composer;
        _store = store;
        _policy = policy.Value;
        _llm = llm;

    }

    public async Task<ChatResponse> Exec(ChatRequest req, CancellationToken ct)
    {
        List<Plan> planHistories = new List<Plan>();
        Plan? plan = null;
        Journal? journal = null;
        ValidationResult? val = null;

        int replanCount = 0;
        string feedback = "";

        while (replanCount <= _policy.MaxReplan)
        {
            plan = replanCount == 0
                ? await _planner.CreatePlanAsync(req.Message, ct)
                : await _planner.RePlanAsync(req.Message, feedback, ct);
            planHistories.Add(plan);

            val = await _validator.ValidateAsync(plan, ct);

            if (!val.IsValid)
            {
                feedback = string.Join("; ", val.Errors);
                replanCount++;
                continue;
            }

            journal = await _executor.ExecuteAsync(plan, ct);
            if (journal.Steps.Any(s => s.Status == "failed"))
            {
                feedback = string.Join("; ", journal.Steps
                    .Where(s => s.Status == "failed")
                    .Select(s => $"step {s.Id} ผิดพลาด: {s.Error}"));
                replanCount++;
                continue;
            }

            break;
        }

        if (plan == null || journal == null)
            throw new Exception("ไม่สามารถสร้างแผนได้");

        var (screen, email) = await _composer.ComposeAsync(req.Message, journal, ct);
        var runId = _store.Put(new { plan, journal, screen, email });
        var llmHistory = _llm.Current;
        return new ChatResponse(screen, email, plan, planHistories, journal, runId, llmHistory);
    }

    public async Task<ChatResponse> ExecWithProgress(ChatRequest req, CancellationToken ct, Func<string, Task> progress)
    {
        List<Plan> planHistories = new List<Plan>();
        Plan? plan = null;
        Journal? journal = null;
        ValidationResult? val = null;

        int replanCount = 0;
        string feedback = "";

        while (replanCount <= _policy.MaxReplan)
        {
            await progress($"เริ่มวางแผน (รอบที่ {replanCount + 1})...");
            plan = replanCount == 0
                ? await _planner.CreatePlanAsync(req.Message, ct)
                : await _planner.RePlanAsync(req.Message, feedback, ct);
            planHistories.Add(plan);

            await progress("กำลังตรวจสอบแผน...");
            val = await _validator.ValidateAsync(plan, ct);

            if (!val.IsValid)
            {
                feedback = string.Join("; ", val.Errors);
                await progress($"แผนไม่ผ่าน: {feedback} -> วางแผนใหม่");
                replanCount++;
                continue;
            }
            await progress($"แผน {plan.Steps.Count} steps พร้อมดำเนินการ...");

            await progress("กำลังดำเนินการตามแผน...");
            // ส่ง callback ให้ ToolsExecutor เพื่อรายงานสถานะ step
            journal = await _executor.ExecuteAsyncWithProgress(plan, ct, progress);
            if (journal.Steps.Any(s => s.Status == "failed"))
            {
                feedback = string.Join("; ", journal.Steps
                    .Where(s => s.Status == "failed")
                    .Select(s => $"step {s.Id} ผิดพลาด: {s.Error}"));
                await progress($"มีข้อผิดพลาด: {feedback} -> วางแผนใหม่");
                replanCount++;
                continue;
            }

            break;
        }

        if (plan == null || journal == null)
            throw new Exception("ไม่สามารถสร้างแผนได้");

        await progress("กำลังสรุปผลลัพธ์...");
        var (screen, email) = await _composer.ComposeAsync(req.Message, journal, ct);
        var runId = _store.Put(new { plan, journal, screen, email });
        var llmHistory = _llm.Current;
        await progress("เสร็จสิ้น");

        var findEmailStep = plan.Steps.FirstOrDefault(_ => _.Plugin == "mail_mcp");
        if (findEmailStep is not null)
        {
            var emailPreview = journal.Steps.FirstOrDefault(_ => _.Id == findEmailStep.Id);

            var emailSend = JsonSerializer.Serialize(emailPreview?.Input,
                new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            var response = new ChatResponse(screen, emailPreview?.Input, plan, planHistories, journal, runId, llmHistory);
            var json = JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }
            );

            await progress($"FINAL: {json}\n\n");

            return response;
        }
        else
        {

            var response = new ChatResponse(screen, null, plan, planHistories, journal, runId, llmHistory);
            var json = JsonSerializer.Serialize(
                response,
                new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }
            );

            await progress($"FINAL: {json}\n\n");

            return response;
        }
        //var response = new ChatResponse(screen, email, plan, planHistories, journal, runId, llmHistory);
        // ส่งผลลัพธ์สุดท้าย (serialize เป็น JSON)
        //await progress("FINAL: " + System.Text.Json.JsonSerializer.Serialize(response));
    }
}
