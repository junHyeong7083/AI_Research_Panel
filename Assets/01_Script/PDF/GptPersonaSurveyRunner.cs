using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// �丣�ҳ����� GPT�� ���� ������ ��û�ϴ� ����
/// 1) flatQuestions�� GPT�� "�� ID�� ���ض�" �ϰ� ���� ����
/// 2) GPT�� ���� �亯 �߿� ��¥ �ִ� ID�� �����
/// 3) GPT�� �� �� ID�� ������ ä���� ���� JSON���� �α� ����
/// </summary>
public class GptPersonaSurveyRunner
{
    private readonly OpenAIKeyConfig _keyConfig;

    public GptPersonaSurveyRunner(OpenAIKeyConfig keyConfig)
    {
        _keyConfig = keyConfig;
    }

    public async Task RunAsync(PersonaData persona, List<FlattenedQuestion> flatQuestions)
    {
        if (flatQuestions == null || flatQuestions.Count == 0)
        {
            Debug.LogError("flatQuestions�� ����ֽ��ϴ�. ����ȭ�� ���� �Ǿ�� �մϴ�.");
            return;
        }

        // GPT�� �ѱ� ��Ű��: id, ��������, Ÿ��, ������
        var qSchema = flatQuestions.Select(q => new
        {
            id = q.id,
            question = q.question,
            type = q.type,
            options = q.options ?? new List<string>()
        }).ToList();

        string qSchemaJson = JsonConvert.SerializeObject(qSchema, Formatting.None);
        string prompt = BuildPersonaPrompt(persona, qSchemaJson);

        var reqObj = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.4
        };

        string bodyJson = JsonConvert.SerializeObject(reqObj);

        using (var req = new UnityWebRequest(_keyConfig.apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _keyConfig.openAIApiKey);

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[{persona.name}] ���� ����: {req.error}");
                return;
            }

            string res = req.downloadHandler.text;

            try
            {
                // OpenAI ���� �Ľ�
                JObject root = JObject.Parse(res);
                string content = root["choices"]?[0]?["message"]?["content"]?.ToString();
                string cleaned = JsonCleaner.CleanToPureJson(content);

                // GPT�� ���� JSON
                var rawObj = JObject.Parse(cleaned);
                var rawAnswers = rawObj["answers"] as JArray;

                var finalAnswers = new JArray();
                // �츮�� ������ ���� �ִ� ���� id ���
                var validIds = new HashSet<string>(flatQuestions.Select(q => q.id));

                // GPT�� �� �� �߿��� ��¥ �ִ� id�� ����
                if (rawAnswers != null)
                {
                    foreach (var ans in rawAnswers)
                    {
                        string id = ans["id"]?.ToString();
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!validIds.Contains(id)) continue;
                        finalAnswers.Add(ans);
                    }
                }

                // GPT�� ������ id�� �� answer�� �־��� (��ó�� ����)
                foreach (var fq in flatQuestions)
                {
                    bool exists = finalAnswers.Any(a => a["id"]?.ToString() == fq.id);
                    if (!exists)
                    {
                        finalAnswers.Add(new JObject
                        {
                            ["id"] = fq.id,
                            ["answer"] = ""
                        });
                    }
                }

                var finalObj = new JObject
                {
                    ["persona"] = persona.name,
                    ["answers"] = finalAnswers
                };

                Debug.Log($"[{persona.name}] ����:\n{finalObj.ToString(Formatting.Indented)}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{persona.name}] ���� �Ľ� ����: {e.Message}\n����:\n{res}");
            }
        }
    }

    // �丣�ҳ� ���� + ���� ��Ű���� �ؽ�Ʈ ������Ʈ�� ������ִ� �Լ�
    private string BuildPersonaPrompt(PersonaData persona, string qSchemaJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"�ʴ� ���ݺ��� '{persona.name}' �丣�ҳ��� ������ �����Ѵ�.");
        sb.AppendLine($"����: {persona.age}, ����: {persona.gender}, ����: {persona.occupation}");
        sb.AppendLine();
        sb.AppendLine("�Ʒ��� �ݵ�� ���ؾ� �ϴ� ���� ����̴�. �� ��Ͽ� �ִ� id ����� ���� ������ ����.");
        sb.AppendLine("�̹� ǥ(table)�� �� ������ �и��Ǿ� �����Ƿ� �� id�� ���� 1���� ���϶�.");
        sb.AppendLine();
        sb.AppendLine("���� ���(JSON):");
        sb.AppendLine(qSchemaJson);
        sb.AppendLine();
        sb.AppendLine("���� ��Ģ:");
        sb.AppendLine("- ����� �ϳ��� JSON�� ����Ѵ�.");
        sb.AppendLine("- �� �׸��� �׻� \"id\" �� \"answer\" �� ������.");
        sb.AppendLine("- type == \"multi\" �� ���: options �߿��� 1~3���� ��� \", \"�� �̾ ���.");
        sb.AppendLine("- type == \"table_row\" �Ǵ� \"text\" �� ���: options�� ������ �� �߿��� �ϳ��� ����.");
        sb.AppendLine("- id�� �������� ����.");
        sb.AppendLine();
        sb.AppendLine("��� ����:");
        sb.AppendLine("{");
        sb.AppendLine($"  \"persona\": \"{persona.name}\",");
        sb.AppendLine("  \"answers\": [");
        sb.AppendLine("    { \"id\": \"SQ1\", \"answer\": \"����\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
