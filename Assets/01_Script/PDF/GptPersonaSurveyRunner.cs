// GptPersonaSurveyRunner.cs

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class GptPersonaSurveyRunner
{
    private readonly OpenAIKeyConfig _keyConfig;
    private readonly RagClient _ragClient;
    private readonly bool _useRag;
    private const int ChunkSize = 25;
    private const int MaxRetry = 3;
    private const int RetryDelayMs = 2000;
    private string _csvPath;

    public GptPersonaSurveyRunner(OpenAIKeyConfig keyConfig, bool useRag = true, string ragServerUrl = "http://127.0.0.1:8080")
    {
        _keyConfig = keyConfig;
        _useRag = useRag;
        _ragClient = new RagClient(ragServerUrl);
    }

    public async Task RunMultipleAsync(List<PersonaData> personas, List<QuestionFlattener.FlattenedQuestion> flat, int repeatCount, string groupName = "", int version = 1)
    {
        string folder = Path.Combine(Application.persistentDataPath, "SurveyExports");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        // 그룹명과 버전을 파일명에 포함
        string fileName = string.IsNullOrEmpty(groupName)
            ? $"persona_results_v{version}.csv"
            : $"persona_{groupName}_results_v{version}.csv";
        _csvPath = Path.Combine(folder, fileName);

        // 파일 초기화 (새로 시작)
        File.WriteAllText(_csvPath, "", Encoding.UTF8);

        for (int run = 1; run <= repeatCount; run++)
        {
            Debug.Log($"[페르소나] === {run}/{repeatCount} 회차 시작 ===");

            foreach (var p in personas)
            {
                try
                {
                    Debug.Log($"[페르소나] {p.name} 응답 중... (회차 {run})");
                    var answers = await RunOnceAsync(p, flat);

                    // CSV에 append
                    string header = $"{p.name}_Run{run}";
                    AppendToCsv(header, flat, answers);

                    Debug.Log($"[페르소나] {p.name} 완료 ✓ (회차 {run})");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[페르소나] {p.name} 실패 (회차 {run}): {e.Message}");
                    // 에러가 나도 다음 페르소나 계속 진행
                }
            }

            Debug.Log($"[페르소나] === {run}/{repeatCount} 회차 완료 ===");
        }

        Debug.Log($"[페르소나] 전체 {repeatCount}회 x {personas.Count}명 완료. 저장됨: {_csvPath}");
    }

    private async Task<Dictionary<string, string>> RunOnceAsync(PersonaData persona, List<QuestionFlattener.FlattenedQuestion> flatQuestions)
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
                    chunkObj = await AskOneChunk(persona, chunk);
                    if (chunkObj != null) break;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[{persona.name}] chunk 요청 실패 (시도 {retry + 1}/{MaxRetry}): {e.Message}");
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

    private void AppendToCsv(string header, List<QuestionFlattener.FlattenedQuestion> flatQuestions, Dictionary<string, string> answers)
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
                File.AppendAllText(_csvPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[페르소나] CSV 저장됨: {header}");
                return;
            }
            catch (IOException)
            {
                if (i < 4)
                    System.Threading.Thread.Sleep(500);
            }
        }
        Debug.LogError($"[페르소나] CSV 저장 실패 (파일 사용 중): {header}");
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

    private async Task<JObject> AskOneChunk(PersonaData persona, List<QuestionFlattener.FlattenedQuestion> chunk)
    {
        var qSchema = chunk.Select(q => new
        {
            id = q.id,
            question = q.question,
            type = q.type,
            options = q.options ?? new List<string>()
        }).ToList();

        string qSchemaJson = JsonConvert.SerializeObject(qSchema, Formatting.None);

        // RAG: 질문과 관련된 실제 통계 데이터 검색
        string ragContext = "";
        if (_useRag && _ragClient != null)
        {
            try
            {
                // 질문 키워드 추출하여 RAG 검색
                string queryKeywords = ExtractKeywords(chunk);
                string gender = persona?.gender ?? "";
                ragContext = await _ragClient.GetContextForQuestion(queryKeywords, gender);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RAG] 컨텍스트 검색 실패: {e.Message}");
            }
        }

        string prompt = BuildPersonaPrompt(persona, qSchemaJson, chunk.Count, ragContext);

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
                ["persona"] = persona.name,
                ["answers"] = finalArr
            };
        }
    }

    private string ExtractKeywords(List<QuestionFlattener.FlattenedQuestion> chunk)
    {
        // 질문에서 핵심 키워드 추출 (만족도, 직장, 생활 등)
        var keywords = new List<string>();
        foreach (var q in chunk.Take(5)) // 처음 5개 질문만 사용
        {
            if (q.question.Contains("만족")) keywords.Add("만족도");
            if (q.question.Contains("직장") || q.question.Contains("회사")) keywords.Add("직장");
            if (q.question.Contains("임금") || q.question.Contains("급여")) keywords.Add("임금");
            if (q.question.Contains("근로") || q.question.Contains("일")) keywords.Add("근로");
            if (q.question.Contains("생활")) keywords.Add("생활");
            if (q.question.Contains("학교")) keywords.Add("학교");
        }

        if (keywords.Count == 0)
            keywords.Add("생활 만족도"); // 기본값

        return string.Join(" ", keywords.Distinct().Take(3));
    }

    private string BuildPersonaPrompt(PersonaData persona, string qSchemaJson, int questionCount, string ragContext = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"너는 지금부터 '{persona.name}' 페르소나로 설문에 응답한다.");
        sb.AppendLine($"나이: {persona.age}, 성별: {persona.gender}, 직업: {persona.occupation}");
        sb.AppendLine();

        // RAG 컨텍스트 추가 (실제 통계 데이터)
        if (!string.IsNullOrEmpty(ragContext))
        {
            sb.AppendLine("=== 참고 자료: 실제 한국인 설문조사 통계 ===");
            sb.AppendLine(ragContext);
            sb.AppendLine("위 통계를 참고하여 현실적인 응답 분포를 반영하되, 페르소나의 특성에 맞게 답변하라.");
            sb.AppendLine();
        }

        sb.AppendLine("아래는 반드시 답해야 하는 문항 목록이다.");
        sb.AppendLine("⚠️ 주의:");
        sb.AppendLine("- 이미 표(table)는 C#에서 행 단위로 분리되어 있다.");
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
        sb.AppendLine($"  \"persona\": \"{persona.name}\",");
        sb.AppendLine("  \"answers\": [");
        sb.AppendLine("    { \"id\": \"(받은 id1)\", \"answer\": \"(해당 id1의 답)\" },");
        sb.AppendLine("    { \"id\": \"(받은 id2)\", \"answer\": \"(해당 id2의 답)\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
