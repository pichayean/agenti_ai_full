namespace AgenticAI.Common
{
    public static class JsonExtension
    {
        public static string CleanJsonString(this string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            var txt = raw.Trim();

            // ลบ markdown code block เช่น ```json ... ```
            if (txt.StartsWith("```"))
            {
                var start = txt.IndexOf('{');
                var end = txt.LastIndexOf('}');
                if (start >= 0 && end > start)
                    txt = txt.Substring(start, end - start + 1);
            }

            // ลบข้อความแปลกๆ ก่อน/หลัง JSON
            if (!txt.StartsWith("{") && txt.Contains('{'))
                txt = txt.Substring(txt.IndexOf('{'));
            if (!txt.EndsWith("}") && txt.Contains('}'))
                txt = txt.Substring(0, txt.LastIndexOf('}') + 1);

            return txt.Trim();
        }
    }
}
