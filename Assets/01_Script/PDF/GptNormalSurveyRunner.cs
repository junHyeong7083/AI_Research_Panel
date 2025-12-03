// GptNormalSurveyRunner.cs
// 페르소나 없이 일반 GPT로 설문에 응답하는 러너

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class GptNormalSurveyRunner
{
    private readonly OpenAIKeyConfig _keyConfig;
    private const int ChunkSize = 25;
    private const int MaxRetry = 3;
    private const int RetryDelayMs = 2000;

    public GptNormalSurveyRunner(OpenAIKeyConfig keyConfig)
    {
        _keyConfig = keyConfig;
    }

    public async Task RunMultipleAsync(List<QuestionFlattener.FlattenedQuestion> flatQuestions, int repeatCount, int version = 1)
    {
        string folder = Path.Combine(Application.persistentDataPath, "SurveyExports");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string csvPath = Path.Combine(folder, $"normal_gpt_results_v{version}.csv");

        // 파일 초기화 (새로 시작)
        File.WriteAllText(csvPath, "", Encoding.UTF8);

        for (int run = 1; run <= repeatCount; run++)
        {
            try
            {
                Debug.Log($"[일반 GPT] {run}/{repeatCount} 회차 시작");

                var answers = await RunOnceAsync(flatQuestions);

                // CSV에 append
                AppendToCsv(csvPath, $"일반GPT_Run{run}", flatQuestions, answers);

                Debug.Log($"[일반 GPT] {run}/{repeatCount} 회차 완료 ✓");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[일반 GPT] {run}/{repeatCount} 회차 실패: {e.Message}");
                // 에러가 나도 다음 회차 계속 진행
            }
        }

        Debug.Log($"[일반 GPT] 전체 {repeatCount}회 완료. 저장됨: {csvPath}");
    }

    private async Task<Dictionary<string, string>> RunOnceAsync(List<QuestionFlattener.FlattenedQuestion> flatQuestions)
    {
        var merged = new Dictionary<string, string>();

        for (int i = 0; i < flatQuestions.Count; i += ChunkSize)
        {
            var chunk = flatQuestions.Skip(i).Take(ChunkSize).ToList();

            JObject chunkObj = null;
            for (int retry = 0; retry < MaxRetry; retry++)
            {
                try
                {
                    chunkObj = await AskOneChunk(chunk);
                    if (chunkObj != null) break;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[일반 GPT] chunk 요청 실패 (시도 {retry + 1}/{MaxRetry}): {e.Message}");
                    if (retry < MaxRetry - 1)
                        await Task.Delay(RetryDelayMs);
                }
            }

            if (chunkObj == null) continue;

            var arr = chunkObj["answers"] as JArray;
            if (arr == null) continue;

            foreach (var ans in arr)
            {
                string id = ans["id"]?.ToString();
                string val = ans["answer"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(id)) continue;
                merged[id] = val;
            }
        }

        foreach (var fq in flatQuestions)
        {
            if (!merged.ContainsKey(fq.id))
                merged[fq.id] = "";
        }

        return merged;
    }

    private void AppendToCsv(string path, string header, List<QuestionFlattener.FlattenedQuestion> flatQuestions, Dictionary<string, string> answers)
    {
        var sb = new StringBuilder();

        // 헤더 구분선
        sb.AppendLine($"=== {header} ===");
        sb.AppendLine("id,question,type,answer");

        foreach (var fq in flatQuestions)
        {
            string answer = answers.ContainsKey(fq.id) ? answers[fq.id] : "";
            string escapedQuestion = EscapeCsvField(fq.question);
            string escapedAnswer = EscapeCsvField(answer);
            sb.AppendLine($"{fq.id},{escapedQuestion},{fq.type},{escapedAnswer}");
        }

        sb.AppendLine(); // 빈 줄로 구분

        // 파일 쓰기 재시도 (Sharing violation 대응)
        for (int i = 0; i < 5; i++)
        {
            try
            {
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[일반 GPT] CSV 저장됨: {header}");
                return;
            }
            catch (IOException)
            {
                if (i < 4)
                    System.Threading.Thread.Sleep(500);
            }
        }
        Debug.LogError($"[일반 GPT] CSV 저장 실패 (파일 사용 중): {header}");
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    private async Task<JObject> AskOneChunk(List<QuestionFlattener.FlattenedQuestion> chunk)
    {
        var qSchema = chunk.Select(q => new
        {
            id = q.id,
            question = q.question,
            type = q.type,
            options = q.options ?? new List<string>()
        }).ToList();

        string qSchemaJson = JsonConvert.SerializeObject(qSchema, Formatting.None);
        string prompt = BuildNormalPrompt(qSchemaJson, chunk.Count);

        var reqObj = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.4
        };
        string body = JsonConvert.SerializeObject(reqObj);

        using (var req = new UnityWebRequest(_keyConfig.apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _keyConfig.openAIApiKey);

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new System.Exception($"HTTP 요청 실패: {req.error}");
            }

            string res = req.downloadHandler.text;

            JObject root = JObject.Parse(res);
            string content = root["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrEmpty(content))
            {
                throw new System.Exception("GPT 응답이 비어있음");
            }

            string cleaned = JsonCleaner.CleanToPureJson(content);
            var obj = JObject.Parse(cleaned);

            var validIds = new HashSet<string>(chunk.Select(c => c.id));
            var rawArr = obj["answers"] as JArray;
            var finalArr = new JArray();

            if (rawArr != null)
            {
                foreach (var a in rawArr)
                {
                    string id = a["id"]?.ToString();
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!validIds.Contains(id)) continue;
                    finalArr.Add(a);
                }
            }

            foreach (var q in chunk)
            {
                bool exists = finalArr.Any(x => x["id"]?.ToString() == q.id);
                if (!exists)
                {
                    finalArr.Add(new JObject
                    {
                        ["id"] = q.id,
                        ["answer"] = ""
                    });
                }
            }

            return new JObject
            {
                ["mode"] = "normal_gpt",
                ["answers"] = finalArr
            };
        }
    }

    private string BuildNormalPrompt(string qSchemaJson, int questionCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("너는 설문조사에 응답하는 일반적인 응답자이다.");
        sb.AppendLine("특별한 페르소나 없이, 일반적이고 중립적인 관점에서 설문에 답변해라.");
        sb.AppendLine();
        sb.AppendLine("아래는 반드시 답해야 하는 문항 목록이다.");
        sb.AppendLine("⚠️ 주의:");
        sb.AppendLine("- 이미 표(table)는 행 단위로 분리되어 있다.");
        sb.AppendLine("- 그러므로 'SQ6', 'Q4' 같은 부모 id 하나로 묶어서 답하면 안 된다.");
        sb.AppendLine("- 예: 'SQ6_1', 'SQ6_2', 'SQ6_3' 이 있으면 이 3개를 각각 따로 답해라.");
        sb.AppendLine("- 내가 준 id 말고는 절대 만들지 마라.");
        sb.AppendLine($"- 이번에 네가 출력해야 하는 answers 원소 개수는 정확히 {questionCount}개다.");
        sb.AppendLine();
        sb.AppendLine("문항 목록(JSON):");
        sb.AppendLine(qSchemaJson);
        sb.AppendLine();
        sb.AppendLine("응답 규칙:");
        sb.AppendLine("- 결과는 하나의 JSON만 출력한다.");
        sb.AppendLine("- 각 항목은 항상 \"id\" 와 \"answer\" 를 가진다.");
        sb.AppendLine("- type == \"multi\" 이고 options가 여러 개면 1~3개까지 선택해서 \", \"로 이어서 써라.");
        sb.AppendLine("- type == \"table_row\" 인데 options가 있으면 그중 1개만 그대로 써라.");
        sb.AppendLine("- options가 비어 있으면 짧은 한국어 문장으로 상황에 맞게 써라.");
        sb.AppendLine("- id를 변형하지 마라.");
        sb.AppendLine();
        sb.AppendLine("출력 형식 예시:");
        sb.AppendLine("{");
        sb.AppendLine("  \"mode\": \"normal_gpt\",");
        sb.AppendLine("  \"answers\": [");
        sb.AppendLine("    { \"id\": \"(받은 id1)\", \"answer\": \"(해당 id1의 답)\" },");
        sb.AppendLine("    { \"id\": \"(받은 id2)\", \"answer\": \"(해당 id2의 답)\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
