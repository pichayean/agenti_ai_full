//using System.ComponentModel;
//using System.Net.Mail;
//using EmailMcpServer.Models;
//using EmailMcpServer.Services;
//using Microsoft.Extensions.Logging;
//using ModelContextProtocol.Server;

//namespace EmailMcpServer.Tools;

//[McpServerToolType]
//public static class McpTools
//{
//    [McpServerTool, Description("Send an email via Gmail SMTP. Recipients must be @gmail.com. Attachments are not supported.")]
//    public static async Task<SendEmailResult> send_email(
//        EmailService svc,
//        ILogger<McpTools> logger,
//        [Description("List of recipient email addresses (gmail only).")]
//        string[] to,
//        [Description("Optional CC recipients (gmail only).")]
//        string[]? cc,
//        [Description("Email subject.")] string subject,
//        [Description("Plain-text body.")] string? body_text = null,
//        [Description("HTML body.")] string? body_html = null,
//        [Description("Optional Reply-To address (gmail only).")] string? reply_to = null,
//        [Description("Custom headers as key/value dictionary.")] Dictionary<string, string>? headers = null,
//        [Description("Priority: Low, Normal, High")] string? priority = "Normal"
//    )
//    {
//        try
//        {
//            var pr = Enum.TryParse<MailPriority>(priority ?? "Normal", true, out var p)
//                ? p
//                : MailPriority.Normal;

//            return await svc.SendAsync(
//                to: to ?? Array.Empty<string>(),
//                cc: cc,
//                subject: subject ?? "",
//                bodyText: body_text,
//                bodyHtml: body_html,
//                headers: headers,
//                replyTo: reply_to,
//                priority: pr);
//        }
//        catch (Exception ex)
//        {
//            logger.LogError(ex, "send_email failed");
//            throw;
//        }
//    }

//    [McpServerTool, Description("Check SMTP connectivity and readiness.")]
//    public static async Task<object> health_check(EmailService svc)
//    {
//        var (ok, message) = await svc.ProbeAsync();
//        return new { ok, message };
//    }
//}
