using System.ComponentModel.DataAnnotations;
using Org.BouncyCastle.Crypto.Macs;

namespace EmailMcpServer.Config;

public sealed class SmtpOptions
{
    [Required] public string Host { get; init; } = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
    public int Port { get; init; } = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
    // Secure: None|StartTls|SslOnConnect
    public string Secure { get; init; } = Environment.GetEnvironmentVariable("SMTP_SECURE") ?? "StartTls";
    [Required] public string Username { get; init; } = "lazymarcus005@gmail.com"; // Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "lazymarcus005@gmail.com";
    [Required] public string Username2 { get; init; } = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "lazymarcus005@gmail.com";
    [Required] public string Password { get; init; } = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? "ipdk vnrr djqq qcan";
    // Default sender (cannot be overridden)
    [Required] public string From { get; init; } = Environment.GetEnvironmentVariable("SMTP_FROM") ?? "lazymarcus005@gmail.com";
    public string? FromName { get; init; } = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? "Email MCP";
    // 2 MB default
    public int MaxBodyBytes { get; init; } =
        int.TryParse(Environment.GetEnvironmentVariable("SMTP_MAX_BODY_BYTES"), out var bytes)
            ? bytes
            : 2 * 1024 * 1024;
    public bool RestrictToGmailOnly { get; init; } =
        (Environment.GetEnvironmentVariable("SMTP_ONLY_GMAIL") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
}
