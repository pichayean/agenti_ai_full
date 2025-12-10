using System.Text.Json;
using System.Text.RegularExpressions;
using AgenticAI.Common;
using AgenticAI.Models;
using Azure;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace AgenticAI.Services;

public class ToolsExecutor
{
    private readonly Kernel _kernel;
    private readonly ILogger<ToolsExecutor> _logger;
    private readonly ExecutionPolicy _policy;

    private static readonly Regex VarToken = new(@"\[\[([a-zA-Z0-9_\-:]+)\]\]", RegexOptions.Compiled);

    public ToolsExecutor(Kernel kernel, ILogger<ToolsExecutor> logger, IOptions<ExecutionPolicy> policy)
    {
        _kernel = kernel;
        _logger = logger;
        _policy = policy.Value;
    }

    public async Task<Journal> ExecuteAsync(Plan plan, CancellationToken ct = default)
    {
        var journal = new Journal();

        // เก็บผลลัพธ์สองแบบ
        var outputsByVar = new Dictionary<string, object?>();     // key = Output (เช่น "report_body")
        var outputsByStepId = new Dictionary<string, object?>();  // key = step.Id

        // map เพื่อหา Output name ของแต่ละ step จาก id
        var outputNameByStepId = plan.Steps.ToDictionary(s => s.Id, s => s.Output);

        foreach (var step in plan.Steps)
        {
            // ต้องมี deps ครบและทุกตัวสำเร็จก่อน
            if (step.DependsOn is { Count: > 0 })
            {
                foreach (var depId in step.DependsOn)
                {
                    if (!outputsByStepId.ContainsKey(depId))
                        throw new InvalidOperationException($"Step '{step.Id}' depends on '{depId}' but it has not run/succeeded.");
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                object? output = null;

                // ===== เตรียม scope ของตัวแปรจาก DependsOn เท่านั้น =====
                var scope = BuildScope(step, outputsByVar, outputsByStepId, outputNameByStepId);

                if (step.Type == "tool")
                {
                    // แทนค่าใน Params ด้วย scope
                    var resolvedParams = ResolveParamsTemplates(step.Params, scope);
                    var args = new KernelArguments();
                    if (resolvedParams != null)
                        foreach (var kv in resolvedParams)
                            args[kv.Key] = kv.Value;

                    int attempt = 0;
                    Exception? lastEx = null;
                    while (attempt <= _policy.MaxRetryPerStep)
                    {
                        try
                        {
                            var result = await _kernel.InvokeAsync(step.Plugin!, step.Tool!, args, ct);
                            output = result.GetValue<object?>();

                            if (output.IsMcpError())
                                throw new Exception($"MCP tool {step.Plugin}.{step.Tool} returned isError=true");

                            lastEx = null;
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastEx = ex;
                            attempt++;
                            if (attempt > _policy.MaxRetryPerStep) break;
                            var delay = TimeSpan.FromSeconds(_policy.RetryInitialDelaySec * attempt);
                            _logger.LogWarning(ex, "Retry {Attempt}/{Max} for step {Id} after {Delay}s",
                                attempt, _policy.MaxRetryPerStep, step.Id, delay.TotalSeconds);
                            await Task.Delay(delay, ct);
                        }
                    }
                    if (lastEx != null) throw lastEx;
                }
                else if (step.Type == "llm")
                {
                    var prompt = step.Prompt ?? string.Empty;
                    prompt = SubstituteVars(prompt, scope);
                    prompt = prompt += "; <Important> หาก data มีโครงสร้าง { \"content\": [ { \"type\": \"text\", \"text\": \"{ \\\"rows\\\": [ { ... } ] }\" } ] } ให้ไปดูข้อมูลใน content.text จะเจอ JsonString ที่เหมาะสม.</Important>";
                    var safe = PromptEscaper.EscapeForSemanticKernel(prompt);
                    var result = await _kernel.InvokePromptAsync(safe, new KernelArguments(), cancellationToken: ct);
                    output = result.ToString();
                }

                // บันทึกผลลัพธ์ทั้งสองดิกชันนารี
                outputsByVar[step.Output] = output;
                outputsByStepId[step.Id] = output;

                journal.Steps.Add(new JournalStep
                {
                    Id = step.Id,
                    Status = "succeeded",
                    DurationMs = sw.ElapsedMilliseconds,
                    Output = output
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {Id} failed", step.Id);
                journal.Steps.Add(new JournalStep
                {
                    Id = step.Id,
                    Status = "failed",
                    DurationMs = sw.ElapsedMilliseconds,
                    Error = ex.Message
                });
                break;
            }
        }

        return journal;
    }

    public async Task<Journal> ExecuteAsyncWithProgress(Plan plan, CancellationToken ct, Func<string, Task> progress)
    {
        var journal = new Journal();
        var outputsByVar = new Dictionary<string, object?>();
        var outputsByStepId = new Dictionary<string, object?>();
        var outputNameByStepId = plan.Steps.ToDictionary(s => s.Id, s => s.Output);

        foreach (var step in plan.Steps)
        {
            if (step.DependsOn is { Count: > 0 })
            {
                foreach (var depId in step.DependsOn)
                {
                    if (!outputsByStepId.ContainsKey(depId))
                        throw new InvalidOperationException($"Step '{step.Id}' depends on '{depId}' but it has not run/succeeded.");
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            object? input = null;
            try
            {
                object? output = null;
                var scope = BuildScope(step, outputsByVar, outputsByStepId, outputNameByStepId);

                await progress($"ดำเนินการ {step.Tool}...");
                if (step.Type == "tool")
                {
                    string dataString = JsonSerializer.Serialize(new
                    {
                        TargetParams = step.Params,
                        DependencyData = scope
                    },
                    new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    var args = new KernelArguments();
                    if (step.DependsOn?.Count > 0)
                    {
                        var promptDependsOn = "ช่วย ReWrite Parameter ในการเรียกใช้ tool โดยให้แทนค่าตัวแปรใน `[[]]`; \n" +
                            $"Target Params And DependencyData: {dataString}";
                        promptDependsOn = promptDependsOn += "; <Important> ให้ตอบเป็น Json value ของ TargetParams เท่านั้น ให้เอา data จาก DependencyData ไปแทนค่าตัวแปร `[[]]` ใน TargetParams.</Important>";
                        var safeDependsOn = PromptEscaper.EscapeForSemanticKernel(promptDependsOn);
                        var resultDependsOn = await _kernel.InvokePromptAsync(safeDependsOn, new KernelArguments(), cancellationToken: ct);
                        var outputDependsOn = resultDependsOn.ToString();
                        string FixToValidJson(string input)
                        {
                            input = input.Trim();

                            int start = input.IndexOf('{');
                            if (start >= 0)
                                input = input.Substring(start);

                            int end = input.LastIndexOf('}');
                            if (end >= 0)
                                input = input.Substring(0, end + 1);

                            return input;
                        }
                        string raw = FixToValidJson(resultDependsOn.ToString());

                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(raw);

                        foreach (var kv in dict)
                            args[kv.Key] = kv.Value;
                    }
                    else
                    {
                        var resolvedParams = ResolveParamsTemplates(step.Params, scope);
                        if (resolvedParams != null)
                            foreach (var kv in resolvedParams)
                                args[kv.Key] = kv.Value;

                    }

                    int attempt = 0;
                    Exception? lastEx = null;
                    while (attempt <= _policy.MaxRetryPerStep)
                    {
                        try
                        {
                            string jsonString = JsonSerializer.Serialize(new
                            {
                                Plugin = step.Plugin,
                                Tool = step.Tool,
                                Args = args.ToDictionary()
                            },
                            new JsonSerializerOptions
                            {
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });
                            input = jsonString;
                            var result = await _kernel.InvokeAsync(step.Plugin!, step.Tool!, args, ct);

                            if (output.IsMcpError())
                                throw new Exception($"MCP tool {step.Plugin}.{step.Tool} returned isError=true");

                            if (!result.IsMcpError())
                            {
                                if (step.Plugin?.Equals("mail_mcp") ?? false)
                                {
                                    output = result.GetValue<object?>();
                                }
                                else
                                {
                                    ApiResult apiResult = result.GetCustomerResult();
                                    output = apiResult.rows;
                                }
                            }
                            lastEx = null;
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastEx = ex;
                            attempt++;
                            if (attempt > _policy.MaxRetryPerStep) break;
                            var delay = TimeSpan.FromSeconds(_policy.RetryInitialDelaySec * attempt);
                            _logger.LogWarning(ex, "Retry {Attempt}/{Max} for step {Id} after {Delay}s",
                                attempt, _policy.MaxRetryPerStep, step.Id, delay.TotalSeconds);
                            await Task.Delay(delay, ct);
                        }
                    }
                    if (lastEx != null) throw lastEx;

                    // รายงานสถานะ step
                    var isMail = (step.Plugin?.ToLowerInvariant().Contains("mail") ?? false) || (step.Tool?.ToLowerInvariant().Contains("mail") ?? false);
                    var emoji = isMail ? " ✉️" : "";
                    await progress($"ดำเนินการ {step.Tool}{emoji} : สำเร็จ");
                }
                else if (step.Type == "llm")
                {
                    var prompt = step.Prompt ?? string.Empty;
                    prompt = SubstituteVars(prompt, scope);
                    input = prompt;
                    prompt = prompt += "; <Important> หาก data มีโครงสร้าง { \"content\": [ { \"type\": \"text\", \"text\": \"{ \\\"rows\\\": [ { ... } ] }\" } ] } ให้ไปดูข้อมูลใน content.text จะเจอ JsonString ที่เหมาะสม.</Important>";
                    var safe = PromptEscaper.EscapeForSemanticKernel(prompt);
                    var result = await _kernel.InvokePromptAsync(safe, new KernelArguments(), cancellationToken: ct);
                    output = result.ToString();

                    await progress($"ดำเนินการ LLM : สำเร็จ");
                }

                outputsByVar[step.Output] = output;
                outputsByStepId[step.Id] = output;

                journal.Steps.Add(new JournalStep
                {
                    Id = step.Id,
                    Status = "succeeded",
                    Input = input,
                    DurationMs = sw.ElapsedMilliseconds,
                    Output = output
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step {Id} failed", step.Id);
                journal.Steps.Add(new JournalStep
                {
                    Id = step.Id,
                    Status = "failed",
                    DurationMs = sw.ElapsedMilliseconds,
                    Input = input,
                    Error = ex.Message
                });
                await progress($"ดำเนินการ {step.Tool ?? step.Type} : ล้มเหลว ({ex.Message})");
                break;
            }
        }

        return journal;
    }

    /// <summary>
    /// สร้าง scope ของตัวแปรที่อนุญาตให้ใช้ใน step นี้ จาก DependsOn เท่านั้น
    /// - key แบบ "report_body"  (ชื่อ Output ของ dep)
    /// - key แบบ "step:get-loan-overview" (อ้างด้วย step-id)
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildScope(
        PlanStep current,
        IReadOnlyDictionary<string, object?> outputsByVar,
        IReadOnlyDictionary<string, object?> outputsByStepId,
        IReadOnlyDictionary<string, string> outputNameByStepId)
    {
        var scope = new Dictionary<string, object?>();

        if (current.DependsOn is { Count: > 0 })
        {
            foreach (var depId in current.DependsOn)
            {
                if (outputsByStepId.TryGetValue(depId, out var depValue))
                {
                    // 1) เปิดให้เรียกด้วย step-id
                    scope[$"step:{depId}"] = depValue;

                    // 2) เปิดให้เรียกด้วย output var name ของสเต็ปนั้น
                    if (outputNameByStepId.TryGetValue(depId, out var depOutVar)
                        && outputsByVar.TryGetValue(depOutVar, out var depOutVal))
                    {
                        scope[depOutVar] = depOutVal;
                    }
                }
            }
        }

        return scope;
    }

    // ---------- Template resolvers ----------

    private static Dictionary<string, object?>? ResolveParamsTemplates(
        Dictionary<string, object?>? raw,
        IReadOnlyDictionary<string, object?> scope)
    {
        if (raw == null) return null;

        object? Walk(object? node)
        {
            if (node is null) return null;

            // string → exact / inline
            if (node is string s)
            {
                var m = VarToken.Match(s);
                if (m.Success && m.Value == s) // ทั้งสตริงเป็น [[token]]
                {
                    var key = m.Groups[1].Value;
                    return scope.TryGetValue(key, out var val) ? val : null;
                }

                // inline หลาย token → เป็น string
                return VarToken.Replace(s, m =>
                {
                    var key = m.Groups[1].Value;
                    if (!scope.TryGetValue(key, out var val) || val is null) return "";
                    return val is string sv ? sv : JsonSerializer.Serialize(val);
                });
            }

            // JsonElement → เดินต่อ
            if (node is JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case JsonValueKind.Object:
                        {
                            var dict = new Dictionary<string, object?>();
                            foreach (var p in je.EnumerateObject())
                                dict[p.Name] = Walk(p.Value);
                            return dict;
                        }
                    case JsonValueKind.Array:
                        {
                            var list = new List<object?>();
                            foreach (var it in je.EnumerateArray())
                                list.Add(Walk(it));
                            return list;
                        }
                    case JsonValueKind.String:
                        // ✅ สำคัญ: ให้เข้า logic ของสตริงที่รองรับ [[var]]
                        return Walk(je.GetString());
                    default:
                        // number/bool/null → เดิม
                        return JsonSerializer.Deserialize<object?>(je.GetRawText());
                }
            }
            //if (node is JsonElement je)
            //{
            //    switch (je.ValueKind)
            //    {
            //        case JsonValueKind.Object:
            //            var dict = new Dictionary<string, object?>();
            //            foreach (var p in je.EnumerateObject())
            //                dict[p.Name] = Walk(p.Value);
            //            return dict;

            //        case JsonValueKind.Array:
            //            var list = new List<object?>();
            //            foreach (var it in je.EnumerateArray())
            //                list.Add(Walk(it));
            //            return list;

            //        default:
            //            return JsonSerializer.Deserialize<object?>(je.GetRawText());
            //    }
            //}

            // Dictionary ปกติ
            if (node is IDictionary<string, object?> d)
            {
                var dict = new Dictionary<string, object?>(d.Count);
                foreach (var kv in d)
                    dict[kv.Key] = Walk(kv.Value);
                return dict;
            }

            // IEnumerable (non-string)
            if (node is System.Collections.Generic.IEnumerable<object?> gseq)
                return gseq.Select(Walk).ToList();

            if (node is System.Collections.IEnumerable seq && node is not string)
            {
                var outList = new List<object?>();
                foreach (var it in seq) outList.Add(Walk(it));
                return outList;
            }

            // number/bool/null → คงเดิม
            return node;
        }

        var resolved = new Dictionary<string, object?>(raw.Count);
        foreach (var kv in raw)
            resolved[kv.Key] = Walk(kv.Value);
        return resolved;
    }

    private static string SubstituteVars(string text, IReadOnlyDictionary<string, object?> scope)
    {
        return VarToken.Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            if (!scope.TryGetValue(key, out var val) || val is null) return "";
            return val is string sv ? sv : JsonSerializer.Serialize(val);
        });
    }
}
