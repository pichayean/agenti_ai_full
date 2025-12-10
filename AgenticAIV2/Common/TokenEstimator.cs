using System.Text.RegularExpressions;
using System.Globalization;

namespace AgenticAI.Common;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

public class TokenEstimator
{
    // ปรับได้ตามรสนิยม/โมเดลที่ใช้
    public double LatinCharsPerToken { get; set; } = 4.0;
    public double ThaiCharsPerToken { get; set; } = 1.8;
    public double CjkCharsPerToken { get; set; } = 1.6;
    public double LatinTokensPerWord { get; set; } = 0.75; // กันเคสคำสั้น ๆ

    public class TokenResult
    {
        public int Tokens { get; set; }
        public string Mode { get; set; } = "mixed";
        public Breakdown Parts { get; set; } = new Breakdown();
        public int Length { get; set; }
    }

    public class Breakdown
    {
        public int ThaiGraphemes { get; set; }
        public int CjkGraphemes { get; set; }
        public int LatinGraphemes { get; set; }
        public int LatinWords { get; set; }
        public int TotalGraphemes => ThaiGraphemes + CjkGraphemes + LatinGraphemes;
        public int TokensThai { get; set; }
        public int TokensCjk { get; set; }
        public int TokensLatin { get; set; }
    }

    // ===== main =====
    public TokenResult Estimate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TokenResult { Tokens = 0, Length = 0 };

        // normalize space
        var stripped = Regex.Replace(text, "\\s+", " ").Trim();

        // 1) นับ grapheme ตามประเภทสคริปต์
        var parts = CountByScript(stripped);

        // 2) กะ token ตามสัดส่วนแต่ละกลุ่ม
        int thaiTokens = (int)Math.Ceiling(parts.ThaiGraphemes / ThaiCharsPerToken);
        int cjkTokens = (int)Math.Ceiling(parts.CjkGraphemes / CjkCharsPerToken);

        // Latin: ใช้ max(ตามตัวอักษร, ตามจำนวนคำ * factor)
        int latinCharBased = (int)Math.Ceiling(parts.LatinGraphemes / LatinCharsPerToken);
        int latinWordBased = (int)Math.Ceiling(parts.LatinWords * LatinTokensPerWord);
        int latinTokens = Math.Max(latinCharBased, latinWordBased);

        parts.TokensThai = thaiTokens;
        parts.TokensCjk = cjkTokens;
        parts.TokensLatin = latinTokens;

        return new TokenResult
        {
            Tokens = thaiTokens + cjkTokens + latinTokens,
            Mode = "mixed",
            Parts = parts,
            Length = text.Length
        };
    }

    // ===== helpers =====
    private Breakdown CountByScript(string s)
    {
        var b = new Breakdown();

        // จับคำ Latin (ใช้สำหรับ word-based)
        b.LatinWords = Regex.Matches(s, @"[A-Za-z][A-Za-z0-9_'’\-]*").Count;

        // ไล่ตาม grapheme (รองรับสระไทย/อีโมจิ ฯลฯ)
        var enumG = StringInfo.GetTextElementEnumerator(s);
        while (enumG.MoveNext())
        {
            string elem = enumG.GetTextElement();
            if (Regex.IsMatch(elem, "[\u0E00-\u0E7F]")) b.ThaiGraphemes++;
            else if (Regex.IsMatch(elem, "[\u4E00-\u9FFF\u3400-\u4DBF\uF900-\uFAFF\u3040-\u30FF\u1100-\u11FF]"))
                b.CjkGraphemes++;
            else if (Regex.IsMatch(elem, @"[\p{L}\p{Nd}]")) b.LatinGraphemes++; // รวมตัวเลข/สัญลักษณ์ที่เป็นตัวอักษร
            else b.LatinGraphemes++; // อื่น ๆ นับฝั่งนี้แบบคร่าว ๆ
        }
        return b;
    }
}

