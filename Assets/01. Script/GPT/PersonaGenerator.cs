using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

// ------------------- 데이터 구조 -------------------
[System.Serializable]
public class PersonaData
{
    public string name;
    public string gender;
    public int age;
    public string occupation;
    public string description;
}

[System.Serializable]
public class PersonaListWrapper
{
    public PersonaData[] personas;
}

// ✅ GPT 요청용 구조 (JsonUtility 직렬화용)
[System.Serializable]
public class Message
{
    public string role;
    public string content;
}

[System.Serializable]
public class ChatRequest
{
    public string model;
    public Message[] messages;
    public float temperature;
}

// ------------------- 메인 스크립트 -------------------
public class PersonaGenerator : MonoBehaviour
{
    [Header("GPT API Settings")]
    [SerializeField] private OpenAIKeyConfig keyConfig;
    private string openAIApiKey;
    private string apiUrl = "https://api.openai.com/v1/chat/completions";

    [Header("UI")]
    [SerializeField] private Transform contentParent;        // ScrollView의 Content
    [SerializeField] private GameObject personaCardPrefab;   // 페르소나 카드 프리팹

    void Start()
    {
        openAIApiKey = keyConfig.openAIApiKey;
        StartCoroutine(GeneratePersonas());
    }

    IEnumerator GeneratePersonas()
    {
        var sm = SelectionManager.instance;
        int sampleCount = int.Parse(sm.sampleSize);

        // 🎯 프롬프트 생성
        string prompt = BuildPrompt(sm, sampleCount);

        // ✅ 구조화된 요청 생성
        ChatRequest chatReq = new ChatRequest
        {
            model = "gpt-3.5-turbo",
            temperature = 0.8f,
            messages = new Message[]
            {
                new Message { role = "user", content = prompt }
            }
        };

        string bodyJson = JsonUtility.ToJson(chatReq);
        Debug.Log("🟡 [Request JSON]\n" + bodyJson); // 디버깅용

        using (UnityWebRequest req = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(bodyJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ GPT 요청 실패: {req.responseCode} | {req.error}");
                Debug.LogError("서버 응답: " + req.downloadHandler.text);
            }
            else
            {
                string result = req.downloadHandler.text;
                Debug.Log($"✅ GPT 응답 수신됨\n{result}");
                ParseAndCreatePersonas(result);
            }
        }
    }

    string BuildPrompt(SelectionManager sm, int count)
    {
        return
$@"You are an expert persona designer. 
Generate {count} realistic personas for a research project in the field of {sm.method}.
Each persona should include:
- name
- gender (maintain roughly {sm.femaleRatio}% female and {sm.maleRatio}% male)
- age (distribute approximately according to: 
10s: {sm.age10Ratio}%,
20s: {sm.age20Ratio}%,
30s: {sm.age30Ratio}%,
40s: {sm.age40Ratio}%,
50s: {sm.age50Ratio}%)
- occupation (relevant to {sm.method})
- a short 2~3 sentence description.

⚠️ Return ONLY a valid JSON object like this:
{{
    ""personas"": [
        {{""name"":""John Kim"", ""gender"":""Male"", ""age"":29, ""occupation"":""Marketing Analyst"", ""description"":""A detail-oriented marketer with 5 years of experience...""}},
        {{""name"":""Lisa Park"", ""gender"":""Female"", ""age"":35, ""occupation"":""Brand Manager"", ""description"":""Creative and passionate about consumer engagement...""}}
    ]
}}";
    }

    // ✅ Newtonsoft.Json 버전
    void ParseAndCreatePersonas(string responseJson)
    {
        try
        {
            string content = ExtractContentFromResponse(responseJson);
            Debug.Log("📦 Extracted JSON:\n" + content);

            // 🧹 문자열 정리
            content = content.Trim();
            content = content.Replace("```json", "").Replace("```", "");

            // ✅ Newtonsoft로 안전하게 파싱
            PersonaListWrapper personaList = JsonConvert.DeserializeObject<PersonaListWrapper>(content);

            if (personaList == null || personaList.personas == null)
            {
                Debug.LogError("❌ JSON 파싱 실패 (구조 불일치 또는 빈 데이터)");
                Debug.LogError("💬 Raw JSON:\n" + content);
                return;
            }

            Debug.Log($"✅ 파싱 성공: {personaList.personas.Length}명 생성됨");

            // 🧩 UI 카드 생성
            foreach (var p in personaList.personas)
            {
                GameObject card = Instantiate(personaCardPrefab, contentParent);
                card.transform.localScale = Vector3.one;

                card.transform.Find("Name").GetComponent<Text>().text = p.name;
                card.transform.Find("Gender").GetComponent<Text>().text = p.gender;
                card.transform.Find("Age").GetComponent<Text>().text = p.age.ToString();
                card.transform.Find("Occupation").GetComponent<Text>().text = p.occupation;
                card.transform.Find("Description").GetComponent<Text>().text = p.description;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Parse Error: " + e.Message);
        }
    }

    // ✅ GPT 응답에서 JSON 본문 추출
    string ExtractContentFromResponse(string fullJson)
    {
        int startIndex = fullJson.IndexOf("\"content\":");
        if (startIndex == -1)
        {
            Debug.LogError("❌ content 필드 없음");
            return "";
        }

        string sub = fullJson.Substring(startIndex);

        // 📌 JSON 본문 ({ ... }) 추출
        Match match = Regex.Match(sub, "\\{[\\s\\S]*?\\}\\s*(?=\"|\\})", RegexOptions.Singleline);
        if (!match.Success)
        {
            Debug.LogError("❌ JSON 블록을 찾을 수 없음");
            return "";
        }

        string extracted = match.Value;

        // 🧹 불필요한 이스케이프 제거
        extracted = extracted
            .Replace("\\n", "")
            .Replace("\\r", "")
            .Replace("\\t", "")
            .Replace("\\\"", "\"")
            .Replace("```json", "")
            .Replace("```", "")
            .Trim()
            .TrimStart('"')
            .TrimEnd('"');

        Debug.Log("📦 Extracted JSON 최종 정제:\n" + extracted);
        return extracted;
    }
}
