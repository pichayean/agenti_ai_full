using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgenticAI.Infrastructure;

public class TraceHttpHandler : DelegatingHandler
{
    private readonly ILogger<TraceHttpHandler> _logger;
    public TraceHttpHandler(ILogger<TraceHttpHandler> logger) => _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP {Method} {Url}", request.Method, request.RequestUri);
        var res = await base.SendAsync(request, cancellationToken);
        _logger.LogInformation("HTTP {StatusCode} for {Url}", (int)res.StatusCode, request.RequestUri);
        return res;
    }
}
