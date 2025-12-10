using AgenticAI.Models;
using AgenticAI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticAI.Controllers;

[ApiController]
[Route("[controller]")]
public class ExecuteController : ControllerBase
{
    private readonly ToolsExecutor _executor;
    private readonly RetentionStore _store;
    public ExecuteController(ToolsExecutor executor, RetentionStore store) { _executor = executor; _store = store; }

    [HttpPost]
    public async Task<ActionResult<ExecuteResponse>> Post(ExecuteRequest req, CancellationToken ct)
    {
        var journal = await _executor.ExecuteAsync(req.Plan, ct);
        var runId = _store.Put(journal);
        return Ok(new ExecuteResponse(journal, journal.Steps.Any(s => s.Status=="failed") ? "failed" : "succeeded"));
    }
}
