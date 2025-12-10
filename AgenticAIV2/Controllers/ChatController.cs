using AgenticAI.Models;
using AgenticAI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Threading.Channels;

namespace AgenticAI.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatController : ControllerBase
{
    private readonly OrchestratorService _orchestratorService;
    public ChatController(OrchestratorService orchestratorService)
    {
        _orchestratorService = orchestratorService;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post(ChatRequest req, CancellationToken ct)
    {
        var resp = await _orchestratorService.Exec(req, ct);
        return Ok(resp);
    }

    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream; charset=utf-8";
        async Task SendStatus(string status)
        {
            var data = $"data: {status}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);
            await Response.Body.WriteAsync(bytes, 0, bytes.Length, ct);
            await Response.Body.FlushAsync(ct);
        }

        try
        {
            await _orchestratorService.ExecWithProgress(req, ct, SendStatus);
        }
        catch (Exception ex)
        {
            await SendStatus("เกิดข้อผิดพลาด: " + ex.Message);
        }
    }
}
