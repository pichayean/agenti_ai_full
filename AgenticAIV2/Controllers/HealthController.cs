using Microsoft.AspNetCore.Mvc;

namespace AgenticAI.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status="ok", time=DateTimeOffset.UtcNow });
}
