using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// 페르소나별로 GPT에 설문 응답을 요청하는 역할
/// 1) flatQuestions를 GPT에 "이 ID만 답해라" 하고 던진 다음
/// 2) GPT가 만들어낸 답변 중에 진짜 있는 ID만 남기고
/// 3) GPT가 안 준 ID는 빈값으로 채워서 최종 JSON으로 로그 찍음
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
            Debug.LogError("flatQuestions가 비어있습니다. 구조화가 먼저 되어야 합니다.");
            return;
        }

        // GPT에 넘길 스키마: id, 질문내용, 타입, 선택지
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
                Debug.LogError($"[{persona.name}] 응답 실패: {req.error}");
                return;
            }

            string res = req.downloadHandler.text;

            try
            {
                // OpenAI 응답 파싱
                JObject root = JObject.Parse(res);
                string content = root["choices"]?[0]?["message"]?["content"]?.ToString();
                string cleaned = JsonCleaner.CleanToPureJson(content);

                // GPT가 만들어낸 JSON
                var rawObj = JObject.Parse(cleaned);
                var rawAnswers = rawObj["answers"] as JArray;

                var finalAnswers = new JArray();
                // 우리가 실제로 갖고 있는 문항 id 목록
                var validIds = new HashSet<string>(flatQuestions.Select(q => q.id));

                // GPT가 준 것 중에서 진짜 있는 id만 남김
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

                // GPT가 빼먹은 id는 빈 answer로 넣어줌 (후처리 용이)
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

                Debug.Log($"[{persona.name}] 응답:\n{finalObj.ToString(Formatting.Indented)}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[{persona.name}] 응답 파싱 실패: {e.Message}\n원본:\n{res}");
            }
        }
    }

    // 페르소나 정보 + 질문 스키마를 텍스트 프롬프트로 만들어주는 함수
    private string BuildPersonaPrompt(PersonaData persona, string qSchemaJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"너는 지금부터 '{persona.name}' 페르소나로 설문에 응답한다.");
        sb.AppendLine($"나이: {persona.age}, 성별: {persona.gender}, 직업: {persona.occupation}");
        sb.AppendLine();
        sb.AppendLine("아래는 반드시 답해야 하는 문항 목록이다. 이 목록에 있는 id 말고는 절대 만들지 마라.");
        sb.AppendLine("이미 표(table)는 행 단위로 분리되어 있으므로 각 id에 대해 1개만 답하라.");
        sb.AppendLine();
        sb.AppendLine("문항 목록(JSON):");
        sb.AppendLine(qSchemaJson);
        sb.AppendLine();
        sb.AppendLine("응답 규칙:");
        sb.AppendLine("- 결과는 하나의 JSON만 출력한다.");
        sb.AppendLine("- 각 항목은 항상 \"id\" 와 \"answer\" 를 가진다.");
        sb.AppendLine("- type == \"multi\" 인 경우: options 중에서 1~3개를 골라 \", \"로 이어서 써라.");
        sb.AppendLine("- type == \"table_row\" 또는 \"text\" 인 경우: options가 있으면 그 중에서 하나만 골라라.");
        sb.AppendLine("- id를 변형하지 마라.");
        sb.AppendLine();
        sb.AppendLine("출력 형식:");
        sb.AppendLine("{");
        sb.AppendLine($"  \"persona\": \"{persona.name}\",");
        sb.AppendLine("  \"answers\": [");
        sb.AppendLine("    { \"id\": \"SQ1\", \"answer\": \"여성\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
