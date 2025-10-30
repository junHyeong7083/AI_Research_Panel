using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// 1) Python으로 PDF → page 텍스트/OCR 추출
/// 2) 그 결과를 GPT로 보내서 실제 설문문항 구조로 변환
/// 3) 페르소나별로 GPT 답변 생성
/// </summary>
public class SurveySimulator : MonoBehaviour
{
    [Header("GPT API Settings")]
    [SerializeField] private OpenAIKeyConfig keyConfig;
    [SerializeField] private PersonaGenerator personaGenerator;

    [Header("Python Settings")]
    [SerializeField] private string pythonExePath = @"C:\Users\user\Git\AI_Research_Panel\Assets\01_Script\Python\.venv\Scripts\python.exe";
    [SerializeField] private string scriptPath = @"C:\Users\user\Git\AI_Research_Panel\Assets\01_Script\Python\survey_parser.py";

    // GPT가 구조화해서 준 원본 문항들
    private List<QuestionData> questions;
    // table / multi 풀어서 만든 문항들 (이걸로 실제 응답 돌림)
    private List<FlattenedQuestion> flatQuestions;

    private string selectedPdfPath;

    // =========================
    // 데이터 구조
    // =========================
    [System.Serializable]
    public class QuestionData
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("question")] public string question;
        [JsonProperty("type")] public string type;      // text / table / multi / unknown
        [JsonProperty("options")] public List<string> options;
        [JsonProperty("rows")] public List<TableRow> rows;
        [JsonProperty("scale")] public List<string> scale;
        [JsonProperty("allow_multiple")] public bool allowMultiple;
    }

    public class TableRow
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("label")] public string label;
    }

    public class FlattenedQuestion
    {
        public string id;
        public string question;
        public List<string> options;
        public string type;      // text / multi / table_row
        public string rowLabel;
    }

    // 파이썬이 주는 구조
    class PythonPage
    {
        public int page;
        public string text;
        public List<PythonTable> tables;
    }

    class PythonTable
    {
        public List<float> bbox;
        public string ocr_text;
    }

    class PythonRawResult
    {
        public string status;
        public string error;
        public string pdf_path;
        public List<PythonPage> pages;
    }

    // =========================

    public void SetPdfPath(string path) => selectedPdfPath = path;

    public void OnClick_SimulateSurvey()
    {
        StartCoroutine(SimulateSurveyRoutine());
    }

    IEnumerator SimulateSurveyRoutine()
    {
        UnityEngine.Debug.Log("설문 시뮬레이션 시작");

        var personas = personaGenerator.GetGeneratedPersonas();
        if (personas == null || personas.Count == 0)
        {
            UnityEngine.Debug.LogError("페르소나가 없습니다. 먼저 PersonaGenerator로 생성하세요.");
            yield break;
        }

        if (string.IsNullOrEmpty(selectedPdfPath))
        {
            UnityEngine.Debug.LogError("PDF 경로가 지정되지 않았습니다.");
            yield break;
        }

        // 1) 파이썬 실행해서 PDF → pages 추출
        string pyOutput = RunPythonScript(selectedPdfPath);
        if (string.IsNullOrEmpty(pyOutput))
        {
            UnityEngine.Debug.LogError("Python 결과가 비어있습니다.");
            yield break;
        }

        PythonRawResult pyResult = null;
        try
        {
            pyResult = JsonConvert.DeserializeObject<PythonRawResult>(pyOutput);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Python JSON 역직렬화 실패: {e.Message}\n원본:\n{pyOutput}");
            yield break;
        }

        if (pyResult == null)
        {
            UnityEngine.Debug.LogError("Python 결과 파싱 실패 (null)");
            yield break;
        }

        if (!string.IsNullOrEmpty(pyResult.error))
        {
            UnityEngine.Debug.LogError($"Python에서 error 반환: {pyResult.error}");
            yield break;
        }

        if (pyResult.pages == null || pyResult.pages.Count == 0)
        {
            UnityEngine.Debug.LogError("Python에서 pages가 비었습니다.");
            yield break;
        }

        // 2) 페이지 → 문항 구조 (GPT)
        var structTask = StructureQuestionsWithGPT(pyOutput);
        while (!structTask.IsCompleted)
            yield return null;

        var structuredQuestions = structTask.Result;
        if (structuredQuestions == null || structuredQuestions.Count == 0)
        {
            UnityEngine.Debug.LogError("GPT 구조화 실패");
            yield break;
        }

        questions = structuredQuestions;

        // 2.5) table / multi 전개
        flatQuestions = FlattenQuestions(questions);
        UnityEngine.Debug.Log($"구조화된 문항 수(원본): {questions.Count} / 펼친 문항 수: {flatQuestions.Count}");

        // 3) 페르소나별 응답
        yield return RunPersonaAnswers(personas);
    }

    // =========================
    // 1. 파이썬 실행
    // =========================
    string RunPythonScript(string pdfPath)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = pythonExePath,
            Arguments = $"\"{scriptPath}\" \"{pdfPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // 윈도우 cp949 깨짐 방지
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit(60000);

            if (!string.IsNullOrEmpty(error))
                UnityEngine.Debug.LogWarning("Python STDERR:\n" + error);

            return output;
        }
    }

    // =========================
    // 2. GPT로 문항 구조화
    // =========================
    async Task<List<QuestionData>> StructureQuestionsWithGPT(string rawJsonFromPython)
    {
        // 여기 프롬프트가 핵심이다.
        string prompt =
            "너는 설문지를 구조화하는 도구다.\n" +
            "아래 JSON은 PDF에서 OCR로 추출한 '페이지 단위' 데이터다.\n" +
            "이걸 실제 설문 '문항' 단위로 변환해라.\n\n" +
            "중요 규칙 (이 패턴들을 보면 '새 문항 시작'으로 간주해라):\n" +
            "1. 접두어가 있는 경우: 'SQ숫자.', 'Q숫자.', 'DQ숫자.' → 이건 100% 문항이다.\n" +
            "2. 숫자 다음에 괄호/점이 오는 경우: '1)', '2)', '(1)', '(2)', '1.', '2.' → 이건 대부분 보기이지만 문장 길이가 길고 질문형이면 문항으로 올려라.\n" +
            "3. 동그라미 번호: '①', '②', '③' → 보기이거나 서브문항이다. 메인 문항이 없으면 이걸 문항으로 올려라.\n" +
            "4. 아래 표현이 있으면 type = \"table\" 으로 만들어라:\n" +
            "   - \"[행별 1개씩 선택]\"\n" +
            "   - \"행별 1개씩\"\n" +
            "   - \"각 유형의 채널을\"\n" +
            "   - \"아래 표와 같이\"\n" +
            "5. 아래 표현이 있으면 type = \"multi\" 로 만들어라 (중복선택):\n" +
            "   - \"[모두 선택]\"\n" +
            "   - \"모두 선택해 주십시오\"\n" +
            "   - \"해당되는 것을 모두\"\n" +
            "   - \"복수 응답\"\n" +
            "   - \"중복 선택\"\n" +
            "   이때 options에는 번호+내용을 전부 넣어라.\n" +
            "6. 표(table)일 때는 rows 배열을 꼭 만들어라. 예:\n" +
            "   {\n" +
            "     \"id\": \"SQ6\",\n" +
            "     \"question\": \"최근 6개월간 각 유형의 채널을 어느 정도 이용하십니까?\",\n" +
            "     \"type\": \"table\",\n" +
            "     \"rows\": [\n" +
            "        { \"id\": \"SQ6_1\", \"label\": \"방송사·언론사 채널\" },\n" +
            "        { \"id\": \"SQ6_2\", \"label\": \"정치 인플루언서 채널\" },\n" +
            "        { \"id\": \"SQ6_3\", \"label\": \"정치인·정당 채널\" }\n" +
            "     ],\n" +
            "     \"scale\": [\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\"]\n" +
            "   }\n" +
            "7. 선택지가 1) 2) 3) 이런 식이면 options에 그대로 넣어라.\n" +
            "8. 최종 출력은 반드시 다음 형태 하나만 있어야 한다:\n" +
            "{\n" +
            "  \"questions\": [\n" +
            "    {\n" +
            "      \"id\": \"SQ1\",\n" +
            "      \"question\": \"귀하의 성별은 어떻게 되십니까?\",\n" +
            "      \"type\": \"text\",\n" +
            "      \"options\": [\"남성\", \"여성\"]\n" +
            "    }\n" +
            "  ]\n" +
            "}\n\n" +
            "아래가 원본이다:\n" +
            "```json\n" +
            rawJsonFromPython +
            "\n```";

        var req = new
        {
            model = "gpt-4o-mini",
            messages = new[] {
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        string body = JsonConvert.SerializeObject(req);

        using (UnityWebRequest reqGPT = new UnityWebRequest(keyConfig.apiUrl, "POST"))
        {
            reqGPT.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            reqGPT.downloadHandler = new DownloadHandlerBuffer();
            reqGPT.SetRequestHeader("Content-Type", "application/json");
            reqGPT.SetRequestHeader("Authorization", "Bearer " + keyConfig.openAIApiKey);

            var op = reqGPT.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (reqGPT.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("구조화 GPT 요청 실패: " + reqGPT.error);
                return null;
            }

            string res = reqGPT.downloadHandler.text;

            string content;
            try
            {
                var root = JObject.Parse(res);
                content = root["choices"]?[0]?["message"]?["content"]?.ToString();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("구조화 GPT 응답 파싱 실패(루트): " + e.Message + "\n원본:\n" + res);
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                UnityEngine.Debug.LogError("구조화 GPT 응답이 비었음\n" + res);
                return null;
            }

            string cleaned = CleanToPureJson(content);

            try
            {
                var finalObj = JObject.Parse(cleaned);
                var list = finalObj["questions"]?.ToObject<List<QuestionData>>();
                return list ?? new List<QuestionData>();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"구조화 결과 파싱 실패: {e.Message}\n--- cleaned ---\n{cleaned}\n--- original ---\n{content}");
                return null;
            }
        }
    }

    // GPT가 코드펜스나 설명문 붙여버린 거 제거
    static string CleanToPureJson(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        s = s.Trim();

        if (s.StartsWith("```json"))
        {
            int end = s.IndexOf("```", 7);
            if (end > 0) return s.Substring(7, end - 7).Trim();
            return s.Substring(7).Trim();
        }

        if (s.StartsWith("```"))
        {
            int end = s.IndexOf("```", 3);
            if (end > 0) return s.Substring(3, end - 3).Trim();
            return s.Substring(3).Trim();
        }

        if (s.StartsWith("json", System.StringComparison.OrdinalIgnoreCase))
        {
            int nl = s.IndexOf('\n');
            if (nl > 0) s = s.Substring(nl + 1).Trim();
        }

        int braceIdx = s.IndexOf('{');
        if (braceIdx > 0) s = s.Substring(braceIdx).Trim();

        int triple = s.LastIndexOf("```", System.StringComparison.Ordinal);
        if (triple > 0) s = s.Substring(0, triple).Trim();

        return s.Trim();
    }

    // =========================
    // table / multi → 낱개 문항으로 풀기
    // =========================
    List<FlattenedQuestion> FlattenQuestions(List<QuestionData> src)
    {
        var list = new List<FlattenedQuestion>();

        foreach (var q in src)
        {
            if (q == null) continue;
            string qType = (q.type ?? "").ToLowerInvariant();

            // 1) table → 행마다 하나씩
            if (qType == "table")
            {
                if (q.rows != null && q.rows.Count > 0)
                {
                    int idx = 1;
                    foreach (var row in q.rows)
                    {
                        string rowId = !string.IsNullOrEmpty(row.id)
                            ? row.id
                            : $"{q.id}_{idx}";

                        string fullQ = $"{q.question} - {row.label}";

                        var opts = (q.scale != null && q.scale.Count > 0)
                            ? new List<string>(q.scale)
                            : (q.options ?? new List<string>());

                        list.Add(new FlattenedQuestion
                        {
                            id = rowId,
                            question = fullQ,
                            options = opts,
                            type = "table_row",
                            rowLabel = row.label
                        });

                        idx++;
                    }
                }
                else
                {
                    list.Add(new FlattenedQuestion
                    {
                        id = q.id,
                        question = q.question,
                        options = q.options ?? new List<string>(),
                        type = "table_row",
                        rowLabel = ""
                    });
                }
                continue;
            }

            // 2) multi → 그대로 1문항, 대신 나중에 GPT가 여러 개 고르게
            if (qType == "multi" || q.allowMultiple)
            {
                list.Add(new FlattenedQuestion
                {
                    id = q.id,
                    question = q.question,
                    options = q.options ?? new List<string>(),
                    type = "multi",
                    rowLabel = ""
                });
                continue;
            }

            // 3) 그 외 단일문항
            list.Add(new FlattenedQuestion
            {
                id = q.id,
                question = q.question,
                options = q.options ?? new List<string>(),
                type = "text",
                rowLabel = ""
            });
        }

        return list;
    }

    // =========================
    // 3. 페르소나별로 답변 생성
    // =========================
    IEnumerator RunPersonaAnswers(List<PersonaData> personas)
    {
        UnityEngine.Debug.Log($"🚀 {personas.Count}명의 페르소나 응답 생성 시작");

        var tasks = new List<Task>();
        foreach (var persona in personas)
            tasks.Add(SendGPTAnswer(persona));

        var all = Task.WhenAll(tasks);
        while (!all.IsCompleted)
            yield return null;

        UnityEngine.Debug.Log("✅ 모든 페르소나 응답 완료");
    }

    // 실제 응답 생성
    async Task SendGPTAnswer(PersonaData persona)
    {
        if (flatQuestions == null || flatQuestions.Count == 0)
        {
            UnityEngine.Debug.LogError("flatQuestions가 비어있습니다. 구조화가 먼저 되어야 합니다.");
            return;
        }

        var qSchema = flatQuestions.Select(q => new
        {
            id = q.id,
            question = q.question,
            type = q.type,
            options = q.options ?? new List<string>()
        }).ToList();

        string qSchemaJson = JsonConvert.SerializeObject(qSchema, Formatting.None);

        string prompt =
            "너는 지금부터 '" + persona.name + @"' 페르소나로 설문에 응답한다.
나이: " + persona.age + ", 성별: " + persona.gender + ", 직업: " + persona.occupation + @"

아래는 반드시 답해야 하는 문항 목록이다. 이 목록에 있는 id 말고는 절대 만들지 마라.
이미 표(table)는 행 단위로 분리되어 있으므로 각 id에 대해 1개만 답하라.

문항 목록(JSON):
" + qSchemaJson + @"

응답 규칙:
- 결과는 하나의 JSON만 출력한다.
- 각 항목은 항상 ""id"" 와 ""answer"" 를 가진다.
- type == ""multi"" 인 경우: options 중에서 1~3개를 골라 "", ""로 이어서 써라. (예: ""1) 영화, 3) 예능"")
- type == ""table_row"" 또는 ""text"" 인 경우: options가 있으면 그 중에서 하나만 골라라.
- id를 변형하지 마라.

출력 형식:
{
  ""persona"": """ + persona.name + @""",
  ""answers"": [
    { ""id"": ""SQ1"",   ""answer"": ""남성"" },
    { ""id"": ""SQ4"",   ""answer"": ""1) 영화, 4) 정치·시사"" },
    { ""id"": ""SQ6_1"", ""answer"": ""5"" }
  ]
}";

        var req = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.4
        };

        string body = JsonConvert.SerializeObject(req);

        using (UnityWebRequest reqGPT = new UnityWebRequest(keyConfig.apiUrl, "POST"))
        {
            reqGPT.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            reqGPT.downloadHandler = new DownloadHandlerBuffer();
            reqGPT.SetRequestHeader("Content-Type", "application/json");
            reqGPT.SetRequestHeader("Authorization", "Bearer " + keyConfig.openAIApiKey);

            var op = reqGPT.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (reqGPT.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"[{persona.name}] 응답 실패: {reqGPT.error}");
                return;
            }

            string res = reqGPT.downloadHandler.text;

            try
            {
                JObject root = JObject.Parse(res);
                string content = root["choices"]?[0]?["message"]?["content"]?.ToString();
                string cleaned = CleanToPureJson(content);

                var rawObj = JObject.Parse(cleaned);
                var rawAnswers = rawObj["answers"] as JArray;

                var finalAnswers = new JArray();
                var validIds = new HashSet<string>(flatQuestions.Select(q => q.id));

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

                // 빠진 건 빈값으로
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

                UnityEngine.Debug.Log($"[{persona.name}] 응답:\n{finalObj.ToString(Formatting.Indented)}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[{persona.name}] 응답 파싱 실패: {e.Message}\n원본:\n{res}");
            }
        }
    }
}
