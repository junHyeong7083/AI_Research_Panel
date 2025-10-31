using System;

/// <summary>
/// GPT가 ```json ... ``` / ``` ... ``` / json\n{...} 이런 식으로
/// 설명 + 코드펜스를 섞어서 보낼 때
/// 실제 { ... } JSON 부분만 뽑아주는 유틸
/// </summary>
public static class JsonCleaner
{
    public static string CleanToPureJson(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        s = s.Trim();

        // ```json ... ```
        if (s.StartsWith("```json"))
        {
            int end = s.IndexOf("```", 7, StringComparison.Ordinal);
            if (end > 0) return s.Substring(7, end - 7).Trim();
            return s.Substring(7).Trim();
        }

        // ``` ... ```
        if (s.StartsWith("```"))
        {
            int end = s.IndexOf("```", 3, StringComparison.Ordinal);
            if (end > 0) return s.Substring(3, end - 3).Trim();
            return s.Substring(3).Trim();
        }

        // "json\n{ ... }" 패턴
        if (s.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            int nl = s.IndexOf('\n');
            if (nl > 0) s = s.Substring(nl + 1).Trim();
        }

        // 앞에 설명글 섞여 있으면 첫 번째 { 부터 사용
        int braceIdx = s.IndexOf('{');
        if (braceIdx > 0)
            s = s.Substring(braceIdx).Trim();

        // 뒤에 ```로 끝나면 제거
        int triple = s.LastIndexOf("```", StringComparison.Ordinal);
        if (triple > 0)
            s = s.Substring(0, triple).Trim();

        return s.Trim();
    }
}
