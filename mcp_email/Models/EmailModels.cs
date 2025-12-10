namespace EmailMcpServer.Models;

public sealed class SendEmailResult
{
    public string Status { get; set; } = "sent";
    public string? MessageId { get; set; }
}

public enum MailPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}
