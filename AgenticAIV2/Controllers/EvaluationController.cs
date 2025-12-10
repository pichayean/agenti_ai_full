using AgenticAI.Evaluation;
using AgenticAI.Models;
using AgenticAI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgenticAI.Controllers;

[ApiController]
[Route("[controller]")]
public class EvaluationController(EvalRunner evaluator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ChatResponse>> Get(CancellationToken ct)
    {
        var results = await evaluator.RunAllAsync(ct);

        return Ok(results);
    }
}
