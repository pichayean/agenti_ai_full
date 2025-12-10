using System.Text.RegularExpressions;

namespace EmailMcpServer.Utils;

public static class Validators
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsEmail(string s) => !string.IsNullOrWhiteSpace(s) && EmailRegex.IsMatch(s);

    public static bool IsGmail(string s) =>
        IsEmail(s) && s.Trim().EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);

    public static void ValidateHeaders(IDictionary<string, string>? headers)
    {
        if (headers is null) return;
        foreach (var kv in headers)
        {
            var k = kv.Key;
            var v = kv.Value;
            if (k.Contains('\r') || k.Contains('\n') || v.Contains('\r') || v.Contains('\n'))
                throw new ArgumentException("Header injection detected.");
        }
    }

    public static void EnsureGmailRecipients(IEnumerable<string> emails, string fieldName)
    {
        foreach (var e in emails)
        {
            if (!IsGmail(e))
                throw new ArgumentException($"{fieldName} must contain only gmail.com addresses.");
        }
    }

    public static int Utf8Length(string? s) =>
        s is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(s);
}
