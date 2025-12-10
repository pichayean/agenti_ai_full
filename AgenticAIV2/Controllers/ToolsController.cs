using AgenticAI.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AgenticAI.Controllers;

[ApiController]
[Route("[controller]")]
public class ToolsController : ControllerBase
{
    private readonly ToolsAvailable _tools;
    public ToolsController(ToolsAvailable tools) => _tools = tools;

    [HttpGet]
    public IActionResult Get()
        => Ok(_tools.AllTools().Select(t => new { plugin = t.plugin, tool = t.tool.Name, t.tool.Description }));
}
