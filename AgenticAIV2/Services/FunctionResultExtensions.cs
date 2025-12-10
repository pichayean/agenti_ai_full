using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.SemanticKernel;

namespace AgenticAI.Services;
public static class FunctionResultExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping

    };
    public static ApiResult GetCustomerResult(this FunctionResult functionResult)
    {
        // เดิม: var raw = functionResult.GetValue<string>();
        var raw = functionResult.ToString(); // ✅ อันนี้ไม่ต้อง cast type ตรง ๆ แล้ว

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("FunctionResult value is empty.");
        }

        var wrapper = JsonSerializer.Deserialize<ApiWrapper>(raw, JsonOptions)
                      ?? throw new InvalidOperationException("Outer API wrapper is null.");

        if (wrapper.isError)
        {
            throw new InvalidOperationException("API returned error flag (isError = true).");
        }

        if (wrapper.content is null || wrapper.content.Count == 0)
        {
            throw new InvalidOperationException("API content is empty.");
        }

        var firstContent = wrapper.content
            .FirstOrDefault(c => string.Equals(c.type, "text", StringComparison.OrdinalIgnoreCase));

        if (firstContent is null || string.IsNullOrWhiteSpace(firstContent.text))
        {
            throw new InvalidOperationException("No valid text content found in API response.");
        }

        var result = JsonSerializer.Deserialize<ApiResult>(firstContent.text, JsonOptions)
                     ?? throw new InvalidOperationException("Inner API result is null.");

        if (result.returnValue != 0)
        {
            throw new InvalidOperationException($"API returnValue is not success (returnValue = {result.returnValue}).");
        }

        if (result.rows is null || result.rows.Count == 0)
        {
            throw new InvalidOperationException("API rows is empty.");
        }

        return result;
    }

}


public class ApiWrapper
{
    public List<ApiContent>? content { get; set; }
    public bool isError { get; set; }
}

public class ApiContent
{
    public string? type { get; set; }
    public string? text { get; set; }
}

public class ApiResult
{
    public List<dynamic>? rows { get; set; }
    public int returnValue { get; set; }
}

public class ApiRow
{
    public int customer_id { get; set; }
    public string? name { get; set; }
    public string? citizen_id { get; set; }
    public string? phone { get; set; }
    public string? email { get; set; }
    public string? address { get; set; }
    public int total_count { get; set; }
    public int page { get; set; }
    public int page_size { get; set; }
}