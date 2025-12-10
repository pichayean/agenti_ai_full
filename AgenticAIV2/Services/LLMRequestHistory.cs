using AgenticAI.Models;

namespace AgenticAI.Services;


/// <summary>
/// Scoped LLM usage tracker (one per HTTP request)
/// </summary>
//public interface ILLMCounterStore
//{
//    LLMCounter Counter { get; }
//    void AddUsage(string taskName, int promptTokens, int completionTokens, string? model = null, string? preview = null);
//}

//public class LLMCounterStore : ILLMCounterStore
//{
//    public LLMCounter Counter { get; } = new();

//    public void AddUsage(string taskName, int promptTokens, int completionTokens, string? model = null, string? preview = null)
//    {
//        Counter.Add(taskName, promptTokens, completionTokens, model, preview);
//    }
//}






public interface ILLMCounterAccessor
{
    LLMCounter Current { get; } // ได้ instance ต่อ-request
}

public class HttpContextLLMCounterAccessor : ILLMCounterAccessor
{
    private readonly IHttpContextAccessor _http;
    private const string Key = "__llm_counter";
    private static readonly LLMCounter Fallback = new(); // ใช้นอก HTTP context (เช่น background)

    public HttpContextLLMCounterAccessor(IHttpContextAccessor http) => _http = http;

    public LLMCounter Current
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx?.Items == null) return Fallback;
            if (!ctx.Items.TryGetValue(Key, out var obj))
            {
                var c = new LLMCounter();
                ctx.Items[Key] = c;
                return c;
            }
            return (LLMCounter)obj!;
        }
    }
}