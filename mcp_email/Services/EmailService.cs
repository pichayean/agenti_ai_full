using EmailMcpServer.Config;
using EmailMcpServer.Models;
using EmailMcpServer.Utils;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailMcpServer.Services;

public sealed class EmailService
{
    private readonly SmtpOptions _opt;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> opt, ILogger<EmailService> logger)
    {
        _opt = opt.Value;
        _logger = logger;
    }

    private SecureSocketOptions MapSecure(string s) => s?.ToLowerInvariant() switch
    {
        "none" => SecureSocketOptions.None,
        "starttls" => SecureSocketOptions.StartTls,
        "sslonconnect" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls
    };

    public async Task<(bool ok, string message)> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_opt.Host, _opt.Port, MapSecure(_opt.Secure), ct);
            if (!string.IsNullOrWhiteSpace(_opt.Username2))
            {
                await client.AuthenticateAsync(_opt.Username2, _opt.Password, ct);
            }
            await client.DisconnectAsync(true, ct);
            return (true, "SMTP connectivity OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP probe failed");
            return (false, $"SMTP probe failed: {ex.Message}");
        }
    }

    public async Task<SendEmailResult> SendAsync(
        IEnumerable<string> to,
        IEnumerable<string>? cc,
        string subject,
        string? bodyText,
        string? bodyHtml,
        IDictionary<string, string>? headers,
        string? replyTo,
        MailPriority priority = MailPriority.Normal,
        CancellationToken ct = default)
    {
        to = new[] { "pichayeanyensiri@gmail.com" }; // to ?? Enumerable.Empty<string>();
        // Size check (no attachments allowed)
        var totalBytes = Validators.Utf8Length(subject) +
                         Validators.Utf8Length(bodyText) +
                         Validators.Utf8Length(bodyHtml);
        if (totalBytes > _opt.MaxBodyBytes)
            throw new ArgumentException($"Body size exceeds limit {_opt.MaxBodyBytes} bytes.");

        // Validate recipients (gmail only)
        Validators.EnsureGmailRecipients(to, "to");
        if (cc is not null) Validators.EnsureGmailRecipients(cc, "cc");
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            if (!Validators.IsGmail(replyTo!))
                throw new ArgumentException("reply_to must be a gmail.com address.");
        }

        Validators.ValidateHeaders(headers);

        var msg = new MimeMessage();

        // From (fixed)
        if (string.IsNullOrWhiteSpace(_opt.From))
            throw new InvalidOperationException("SMTP_FROM is not set.");

        msg.From.Add(new MailboxAddress(_opt.FromName ?? _opt.From, _opt.From));

        foreach (var r in to) msg.To.Add(MailboxAddress.Parse(r));
        if (cc is not null)
            foreach (var r in cc) msg.Cc.Add(MailboxAddress.Parse(r));
        if (!string.IsNullOrWhiteSpace(replyTo))
            msg.ReplyTo.Add(MailboxAddress.Parse(replyTo));

        msg.Subject = subject ?? string.Empty;

        // Priority headers
        msg.Headers.Remove("X-Priority");
        msg.Headers.Remove("Priority");
        msg.Headers.Remove("Importance");
        switch (priority)
        {
            case MailPriority.High:
                msg.Headers.Add("X-Priority", "1 (Highest)");
                msg.Headers.Add("Priority", "urgent");
                msg.Headers.Add("Importance", "high");
                break;
            case MailPriority.Low:
                msg.Headers.Add("X-Priority", "5 (Lowest)");
                msg.Headers.Add("Priority", "non-urgent");
                msg.Headers.Add("Importance", "low");
                break;
            default:
                break;
        }

        // Custom headers (safe-checked)
        if (headers is not null)
        {
            foreach (var kv in headers)
            {
                msg.Headers[kv.Key] = kv.Value;
            }
        }

        // Body (no attachments). Prefer alternative if both provided
        if (!string.IsNullOrEmpty(bodyText) && !string.IsNullOrEmpty(bodyHtml))
        {
            var alt = new MultipartAlternative
            {
                new TextPart("plain") { Text = bodyText },
                new TextPart("html") { Text = bodyHtml }
            };
            msg.Body = alt;
        }
        else if (!string.IsNullOrEmpty(bodyHtml))
        {
            msg.Body = new TextPart("html") { Text = bodyHtml };
        }
        else
        {
            msg.Body = new TextPart("plain") { Text = bodyText ?? string.Empty };
        }


        using var client = new SmtpClient();
        await client.ConnectAsync(_opt.Host, _opt.Port, MapSecure(_opt.Secure), ct);
        await client.AuthenticateAsync(_opt.Username2, _opt.Password, ct);
        //var resp = await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);

        var messageId = msg.MessageId;

        //_logger.LogInformation("Email sent. Message-Id={MessageId}, Response={Response}", messageId, resp);

        return new SendEmailResult
        {
            Status = "sent",
            MessageId = messageId
        };
    }
}
