using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class GptSurveyStructurer
{
    private readonly OpenAIKeyConfig _keyConfig;

    public GptSurveyStructurer(OpenAIKeyConfig cfg)
    {
        _keyConfig = cfg;
    }

    // SurveySimulator에서 쓰는 DTO 복붙
    public class QuestionData
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("question")] public string question;
        [JsonProperty("type")] public string type;
        [JsonProperty("options")] public List<string> options;
        [JsonProperty("rows")] public List<RowData> rows;
        [JsonProperty("scale")] public List<string> scale;
    }

    public class RowData
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("label")] public string label;
    }

    public async Task<List<QuestionData>> StructureAsync(string pythonJson)
    {
        string prompt =
$@"너는 설문지를 구조화하는 도구다.
아래 JSON은 PDF에서 OCR로 추출한 페이지 단위 데이터다.
이걸 실제 설문 '문항' 단위로 변환해라.

규칙:
- id는 원래 문항(SQ1, SQ2, Q1, DQ1)을 최대한 유지해라.
- type이 ""table""이면 rows를 꼭 넣어라.
- table의 공통 척도는 scale 배열로 넣어라.
- 선택지 1), 2), 3) 이 보이면 options에 넣어라.
- 최종 출력은 이 형식 하나만:
{{
  ""questions"": [ ... ]
}}

원본:
```json
{pythonJson}
```";

        var reqObj = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.2
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
                Debug.LogError("구조화 GPT 실패: " + req.error);
                return new List<QuestionData>();
            }

            string res = req.downloadHandler.text;

            string content;
            try
            {
                var root = JObject.Parse(res);
                content = root["choices"]?[0]?["message"]?["content"]?.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogError("구조화 GPT 응답 파싱 실패: " + e.Message + "\n" + res);
                return new List<QuestionData>();
            }

            string cleaned = JsonCleaner.CleanToPureJson(content);

            try
            {
                var finalObj = JObject.Parse(cleaned);
                var list = finalObj["questions"]?.ToObject<List<QuestionData>>();
                return list ?? new List<QuestionData>();
            }
            catch (System.Exception e)
            {
                Debug.LogError("구조화 결과 JSON 파싱 실패: " + e.Message + "\n---cleaned---\n" + cleaned);
                return new List<QuestionData>();
            }
        }
    }
}
