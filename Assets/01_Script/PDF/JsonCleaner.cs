using System;

/// <summary>
/// GPT�� ```json ... ``` / ``` ... ``` / json\n{...} �̷� ������
/// ���� + �ڵ��潺�� ��� ���� ��
/// ���� { ... } JSON �κи� �̾��ִ� ��ƿ
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

        // "json\n{ ... }" ����
        if (s.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            int nl = s.IndexOf('\n');
            if (nl > 0) s = s.Substring(nl + 1).Trim();
        }

        // �տ� ����� ���� ������ ù ��° { ���� ���
        int braceIdx = s.IndexOf('{');
        if (braceIdx > 0)
            s = s.Substring(braceIdx).Trim();

        // �ڿ� ```�� ������ ����
        int triple = s.LastIndexOf("```", StringComparison.Ordinal);
        if (triple > 0)
            s = s.Substring(0, triple).Trim();

        return s.Trim();
    }
}
