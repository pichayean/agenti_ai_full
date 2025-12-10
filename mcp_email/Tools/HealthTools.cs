using System.ComponentModel;
using EmailMcpServer.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EmailMcpServer.Tools;

public sealed class HealthTools
{
    private readonly EmailService _svc;

    public HealthTools(EmailService svc) => _svc = svc;

    [McpServerTool, Description("Check SMTP connectivity and readiness.")]
    public async Task<object> health_check()
    {
        var (ok, message) = await _svc.ProbeAsync();
        return new { ok, message };
    }
}
