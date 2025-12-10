using EmailMcpServer.Config;
using EmailMcpServer.Models;
using EmailMcpServer.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Bind SMTP options from ENV/appsettings
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "SMTP host is required.")
    .Validate(o => o.Port > 0 && o.Port < 65536, "SMTP port must be 1-65535.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Username2), "SMTP username is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "SMTP password is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.From), "SMTP_FROM is required.")
    .Validate(o => EmailMcpServer.Utils.Validators.IsGmail(o.From), "SMTP_FROM must be a gmail address.")
    .ValidateOnStart();

builder.Services.AddSingleton<EmailService>();

// MCP server
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(); // discover tools via [McpServerTool]

var app = builder.Build();

app.MapMcp("/mcp");
app.MapGet("/", () => new { status = "ok", service = "Email MCP Server", link = "/health" });
app.MapGet("/health", async (IOptions<SmtpOptions> opt, EmailService svc) =>
{
    var cfg = opt.Value;
    var (ok, message) = await svc.ProbeAsync();
    return Results.Json(new
    {
        ok,
        message,
        smtp = new
        {
            cfg.Host,
            cfg.Port,
            cfg.Secure,
            cfg.Username2,
            From = cfg.From,
            FromName = cfg.FromName
        }
    });
});

app.MapGet("/send", async (IOptions<SmtpOptions> opt, EmailService svc) =>
{

    var to = new[] { "pichayeanyensiri@gmail.com" };
    IEnumerable<string>? cc = new[] { "marcus.work005@gmail.com" };

    string subject = "ทดสอบส่งอีเมล (HTML + Text)";
    string bodyText = "สวัสดีครับ\nนี่คืออีเมลทดสอบเวอร์ชันข้อความล้วน (fallback)\nขอบคุณครับ.";
    //    string bodyHtml =
    //    @"<!doctype html>
    //<html>
    //  <body style=""font-family:Segoe UI, sans-serif"">
    //    <h2 style=""margin:0 0 8px"">สวัสดีครับ 👋</h2>
    //    <p style=""margin:0 0 12px"">
    //      นี่คืออีเมลทดสอบที่มี <strong>HTML body</strong> และมีเวอร์ชันข้อความล้วนเป็น fallback
    //    </p>
    //    <table border=""1"" cellpadding=""6"" cellspacing=""0"">
    //      <tr><th>รายการ</th><th>ค่า</th></tr>
    //      <tr><td>Order</td><td>#A-1029</td></tr>
    //      <tr><td>Status</td><td>Paid</td></tr>
    //    </table>
    //    <p style=""margin-top:12px"">ขอบคุณครับ</p>
    //  </body>
    //</html>";

    //    var headers = new Dictionary<string, string>
    //    {
    //        ["X-Request-Id"] = Guid.NewGuid().ToString("N"),
    //        ["List-Unsubscribe"] = "<mailto:unsubscribe@example.com>"
    //    };

    //    string? replyTo = "marcus.work005@gmail.com";
    //    var priority = MailPriority.Normal;

    // ตั้ง timeout สำหรับยกเลิกได้
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var result = await svc.SendAsync(
        to: to,
        cc: cc,              // ถ้าไม่ต้องการ CC ให้ส่ง null
        subject: subject,
        bodyText: bodyText,        // ควรมีเป็น fallback สำหรับไคลเอนต์ที่ไม่รองรับ HTML
        bodyHtml: null,
        headers: null,         // ถ้าไม่ต้องการ header เพิ่มเติม ส่ง null
        replyTo: null,
        ct: cts.Token
    );
    //var result = await svc.SendAsync(
    //    to: to,
    //    cc: cc,              // ถ้าไม่ต้องการ CC ให้ส่ง null
    //    subject: subject,
    //    bodyText: bodyText,        // ควรมีเป็น fallback สำหรับไคลเอนต์ที่ไม่รองรับ HTML
    //    bodyHtml: bodyHtml,
    //    headers: headers,         // ถ้าไม่ต้องการ header เพิ่มเติม ส่ง null
    //    replyTo: replyTo,         // ถ้าไม่ต้องการ reply-to ส่ง null
    //    priority: priority,
    //    ct: cts.Token
    //);

    return Results.Json(new
    {
        ok = true
    });
});
app.Run();
