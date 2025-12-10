using AgenticAI.Models;
using AgenticAI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticAI.Controllers;

[ApiController]
[Route("[controller]")]
public class PlanController : ControllerBase
{
    private readonly PlannerService _planner;
    private readonly ValidatorService _validator;
    private readonly RetentionStore _store;

    public PlanController(PlannerService planner, ValidatorService validator, RetentionStore store)
    { _planner = planner; _validator = validator; _store = store; }

    [HttpPost]
    public async Task<ActionResult<PlanResponse>> Post(PlanRequest req, CancellationToken ct)
    {
        var plan = await _planner.CreatePlanAsync(req.Message, ct);
        var validation = await _validator.ValidateAsync(plan, ct);
        var runId = _store.Put(new { plan, validation });
        return Ok(new PlanResponse(plan, validation));
    }
}
