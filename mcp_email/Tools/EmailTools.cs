using System.ComponentModel;
using EmailMcpServer.Models;
using EmailMcpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EmailMcpServer.Tools;
[McpServerToolType]
public sealed class EmailTools
{
    private readonly EmailService _svc;
    private readonly ILogger<EmailTools> _logger;

    public EmailTools(EmailService svc, ILogger<EmailTools> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [McpServerTool, Description(
    "Send an email via Gmail SMTP. Recipients must be @gmail.com. Attachments are not supported. " +
    "Parameters: " +
    "[require]to - List of recipient email addresses (gmail only). " +
    "cc - Optional CC recipients (gmail only). " +
    "[require]subject - Email subject. " +
    "[require]body_text - Plain-text body. " +
    "body_html - HTML body. " +
    "reply_to - Optional Reply-To address (gmail only). " +
    "headers - Custom headers as key/value dictionary. " +
    "priority - Priority: Low, Normal, High." +
    "Email Info: { accounting_team: accounting_team@gmail.com, debt_collection_team: debt_collection_team@gmail.com, finance_team: finance_team@gmail.com, auditor_team: auditor_team@gmail.com }"
    )]
    public async Task<SendEmailResult> send_email(
        [Description("List of recipient email addresses (gmail only).")]
        string[] to,
        [Description("Email subject.")]
        string subject,
        [Description("Plain-text body.")] string? body_text = null
    //[Description("HTML body.")] string? body_html = null,
    //[Description("Optional Reply-To address (gmail only).")] string? reply_to = null,
    //[Description("Custom headers as key/value dictionary.")] Dictionary<string, string>? headers = null,
    //[Description("Priority: Low, Normal, High")] string? priority = "Normal"
    )
    {
        try
        {
            //var pr = Enum.TryParse<MailPriority>(priority ?? "Normal", true, out var p) ? p : MailPriority.Normal;
            //return await _svc.SendAsync(
            //    to: to ?? Array.Empty<string>(),
            //    cc: cc,
            //    subject: subject ?? "",
            //    bodyText: body_text,
            //    bodyHtml: body_html,
            //    headers: headers,
            //    replyTo: reply_to,
            //    priority: pr);
            return await _svc.SendAsync(
               to: to ?? Array.Empty<string>(),
               cc: null,
               subject: subject ?? "",
               bodyText: body_text,
               bodyHtml: null,
               headers: null,
               replyTo: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "send_email failed");
            throw;
        }
    }
}
