using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// PDF���� �̾ƿ� ������ JSON�� GPT�� ������
/// ���� ���� ���� ����(questions �迭)�� �ٲ��ִ� ����
/// </summary>
public class GptSurveyStructurer
{
    private readonly OpenAIKeyConfig _keyConfig; // API URL, Key ��

    public GptSurveyStructurer(OpenAIKeyConfig keyConfig)
    {
        _keyConfig = keyConfig;
    }

    /// <summary>
    /// rawJsonFromPython: ���̽��� �� JSON ���ڿ� �״��
    /// ��ȯ: List<QuestionData> ����
    /// </summary>
    public async Task<List<QuestionData>> BuildQuestionsAsync(string rawJsonFromPython)
    {
        // GPT�� �� ������Ʈ ����
        string prompt = BuildPrompt(rawJsonFromPython);

        var reqBody = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.2
        };

        string bodyJson = JsonConvert.SerializeObject(reqBody);

        using (var req = new UnityWebRequest(_keyConfig.apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _keyConfig.openAIApiKey);

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield(); // ����Ƽ���� �񵿱� ��ٸ��� ����

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("����ȭ GPT ��û ����: " + req.error);
                return null;
            }

            string res = req.downloadHandler.text;

            string content;
            try
            {
                // OpenAI ��Ÿ�� ���信�� content �κи� ����
                var root = JObject.Parse(res);
                content = root["choices"]?[0]?["message"]?["content"]?.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogError("����ȭ GPT ���� �Ľ� ����(��Ʈ): " + e.Message + "\n����:\n" + res);
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Debug.LogError("����ȭ GPT ������ �����\n" + res);
                return null;
            }

            // GPT�� ```json ...``` ������ �� �����ϱ� ������
            string cleaned = JsonCleaner.CleanToPureJson(content);

            try
            {
                // {"questions": [...]} ���� ���
                var finalObj = JObject.Parse(cleaned);
                var list = finalObj["questions"]?.ToObject<List<QuestionData>>();
                return list ?? new List<QuestionData>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����ȭ ��� �Ľ� ����: {e.Message}\n--- cleaned ---\n{cleaned}\n--- original ---\n{content}");
                return null;
            }
        }
    }

    // GPT�� ���� ���� ������Ʈ �ؽ�Ʈ ������ִ� �Լ�
    private string BuildPrompt(string rawJsonFromPython)
    {
        var sb = new StringBuilder();
        sb.AppendLine("�ʴ� �������� ����ȭ�ϴ� ������.");
        sb.AppendLine("�Ʒ� JSON�� PDF���� OCR�� ������ '������ ����' �����ʹ�.");
        sb.AppendLine("�̰� ���� ���� '����' ������ ��ȯ�ض�.");
        sb.AppendLine();
        sb.AppendLine("�߿� ��Ģ (�� ���ϵ��� ���� '�� ���� ����'���� �����ض�):");
        sb.AppendLine("1. 'SQ����.', 'Q����.', 'DQ����.' �� 100% ����.");
        sb.AppendLine("2. '1)', '2)', '(1)', '(2)', '1.' �� ��� �������̸� ����.");
        sb.AppendLine("3. '��', '��', '��' �� ���깮��. ���� ������ ����.");
        sb.AppendLine("4. '�ະ 1����', '�Ʒ� ǥ�� ����' �� type = \"table\" + rows + scale");
        sb.AppendLine("5. '��� ����', '���� ����' �� type = \"multi\"");
        sb.AppendLine();
        sb.AppendLine("���� ����� �ݵ�� �� ���� �ϳ��� �־�� �Ѵ�:");
        sb.AppendLine("{ \"questions\": [ { \"id\": \"...\", \"question\": \"...\", \"type\": \"text\", \"options\": [] } ] }");
        sb.AppendLine();
        sb.AppendLine("�Ʒ��� �����̴�:");
        sb.AppendLine("```json");
        sb.AppendLine(rawJsonFromPython);
        sb.AppendLine("```");
        return sb.ToString();
    }
}
