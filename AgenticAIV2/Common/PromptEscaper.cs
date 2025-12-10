namespace AgenticAI.Common;
public static class PromptEscaper
{
    public static string EscapeForSemanticKernel(string s)
        => s.Replace("{{", "[[").Replace("}}", "]]");
}
