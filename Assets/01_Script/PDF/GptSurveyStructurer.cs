using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// PDF에서 뽑아온 페이지 JSON을 GPT에 던져서
/// 실제 설문 문항 구조(questions 배열)로 바꿔주는 역할
/// </summary>
public class GptSurveyStructurer
{
    private readonly OpenAIKeyConfig _keyConfig; // API URL, Key 등

    public GptSurveyStructurer(OpenAIKeyConfig keyConfig)
    {
        _keyConfig = keyConfig;
    }

    /// <summary>
    /// rawJsonFromPython: 파이썬이 준 JSON 문자열 그대로
    /// 반환: List<QuestionData> 형태
    /// </summary>
    public async Task<List<QuestionData>> BuildQuestionsAsync(string rawJsonFromPython)
    {
        // GPT에 줄 프롬프트 구성
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
                await Task.Yield(); // 유니티에서 비동기 기다리는 패턴

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("구조화 GPT 요청 실패: " + req.error);
                return null;
            }

            string res = req.downloadHandler.text;

            string content;
            try
            {
                // OpenAI 스타일 응답에서 content 부분만 빼옴
                var root = JObject.Parse(res);
                content = root["choices"]?[0]?["message"]?["content"]?.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogError("구조화 GPT 응답 파싱 실패(루트): " + e.Message + "\n원본:\n" + res);
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Debug.LogError("구조화 GPT 응답이 비었음\n" + res);
                return null;
            }

            // GPT가 ```json ...``` 씌웠을 수 있으니까 깨끗이
            string cleaned = JsonCleaner.CleanToPureJson(content);

            try
            {
                // {"questions": [...]} 형태 기대
                var finalObj = JObject.Parse(cleaned);
                var list = finalObj["questions"]?.ToObject<List<QuestionData>>();
                return list ?? new List<QuestionData>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"구조화 결과 파싱 실패: {e.Message}\n--- cleaned ---\n{cleaned}\n--- original ---\n{content}");
                return null;
            }
        }
    }

    // GPT에 던질 실제 프롬프트 텍스트 만들어주는 함수
    private string BuildPrompt(string rawJsonFromPython)
    {
        var sb = new StringBuilder();
        sb.AppendLine("너는 설문지를 구조화하는 도구다.");
        sb.AppendLine("아래 JSON은 PDF에서 OCR로 추출한 '페이지 단위' 데이터다.");
        sb.AppendLine("이걸 실제 설문 '문항' 단위로 변환해라.");
        sb.AppendLine();
        sb.AppendLine("중요 규칙 (이 패턴들을 보면 '새 문항 시작'으로 간주해라):");
        sb.AppendLine("1. 'SQ숫자.', 'Q숫자.', 'DQ숫자.' → 100% 문항.");
        sb.AppendLine("2. '1)', '2)', '(1)', '(2)', '1.' → 길고 질문형이면 문항.");
        sb.AppendLine("3. '①', '②', '③' → 서브문항. 메인 없으면 문항.");
        sb.AppendLine("4. '행별 1개씩', '아래 표와 같이' → type = \"table\" + rows + scale");
        sb.AppendLine("5. '모두 선택', '복수 응답' → type = \"multi\"");
        sb.AppendLine();
        sb.AppendLine("최종 출력은 반드시 이 형태 하나만 있어야 한다:");
        sb.AppendLine("{ \"questions\": [ { \"id\": \"...\", \"question\": \"...\", \"type\": \"text\", \"options\": [] } ] }");
        sb.AppendLine();
        sb.AppendLine("아래가 원본이다:");
        sb.AppendLine("```json");
        sb.AppendLine(rawJsonFromPython);
        sb.AppendLine("```");
        return sb.ToString();
    }
}
