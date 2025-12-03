using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[System.Serializable]
public class PersonaData
{
    public string name;
    public string gender;
    public int age;
    public string occupation;
    public string description;
    public string socialStatus; // "junior" (사회초년생) 또는 "senior" (사회적 권위자)
}

[System.Serializable]
public class PersonaListWrapper
{
    public List<PersonaData> personas;
}

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
    public List<Message> messages;
    public float temperature;
}

public class PersonaGenerator : MonoBehaviour
{
    [Header("GPT API Settings")]
    [SerializeField] private OpenAIKeyConfig keyConfig; // ✅ ScriptableObject 참조

    [Header("UI")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject personaCardPrefab;
    [SerializeField] private GameObject LoadingPanel;

    private List<PersonaData> generatedPersonas = new List<PersonaData>();
    public List<PersonaData> GetGeneratedPersonas() => generatedPersonas;
    private void Awake() => LoadingPanel.SetActive(true);

    void Start()
    {
        if (keyConfig == null)
        {
            Debug.LogError("❌ OpenAIKeyConfig가 연결되지 않았습니다!");
            return;
        }

        StartCoroutine(GeneratePersonas());
    }

    IEnumerator GeneratePersonas()
    {
        var sm = SelectionManager.instance;
        int sampleCount = int.Parse(sm.sampleSize);

        generatedPersonas.Clear();

        // 1. 사회초년생 그룹 생성
        Debug.Log($"🔵 사회초년생 그룹 {sampleCount}명 생성 중...");
        yield return StartCoroutine(GeneratePersonaGroup(sm, sampleCount, "junior"));

        // 2. 사회적 권위자 그룹 생성
        Debug.Log($"🔴 사회적 권위자 그룹 {sampleCount}명 생성 중...");
        yield return StartCoroutine(GeneratePersonaGroup(sm, sampleCount, "senior"));

        Debug.Log($"✅ 총 {generatedPersonas.Count}명 페르소나 생성 완료 (초년생 + 권위자)");
        LoadingPanel.SetActive(false);
    }

    IEnumerator GeneratePersonaGroup(SelectionManager sm, int count, string socialStatus)
    {
        string prompt = BuildPrompt(sm, count, socialStatus);

        ChatRequest chatReq = new ChatRequest
        {
            model = "gpt-3.5-turbo",
            temperature = 0.8f,
            messages = new List<Message>
            {
                new Message { role = "user", content = prompt }
            }
        };

        string bodyJson = JsonConvert.SerializeObject(chatReq);
        Debug.Log($"🟡 [{socialStatus}] Request JSON\n" + bodyJson);

        using (UnityWebRequest req = new UnityWebRequest(keyConfig.apiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(bodyJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + keyConfig.openAIApiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ [{socialStatus}] GPT 요청 실패: {req.responseCode} | {req.error}");
                Debug.LogError("서버 응답: " + req.downloadHandler.text);
            }
            else
            {
                string result = req.downloadHandler.text;
                Debug.Log($"✅ [{socialStatus}] GPT 응답 수신됨");
                ParseAndCreatePersonas(result, socialStatus);
            }
        }
    }

    string BuildPrompt(SelectionManager sm, int count, string socialStatus)
    {
        string statusDescription;
        string ageHint;
        string fieldName = sm.method; // 사용자가 선택한 분야

        if (socialStatus == "junior")
        {
            statusDescription = "사회 초년생 (낮은 사회적 지위, 경력 초기)";
            ageHint = "20대 초반~30대 초반 (20-32세)";
        }
        else // senior
        {
            statusDescription = "사회적 권위자 (높은 사회적 지위, 경력 후기)";
            ageHint = "40대~60대 (40-65세)";
        }

        return
$@"You are an expert persona designer.
Generate {count} realistic personas who work in the ""{fieldName}"" field and are {statusDescription}.

Each persona should include:
- name (Korean name)
- gender (maintain roughly {sm.femaleRatio}% female and {sm.maleRatio}% male)
- age ({ageHint})
- occupation (must be a job within the ""{fieldName}"" field, appropriate for {statusDescription})
- a short 2~3 sentence description.

⚠️ IMPORTANT:
- All personas must work in the ""{fieldName}"" field.
- All personas must be {statusDescription}.
- For junior: entry-level positions like interns, new employees, junior staff
- For senior: high-level positions like executives, directors, professors, managers

⚠️ Return ONLY a valid JSON object like this:
{{
    ""personas"": [
        {{""name"":""김철수"", ""gender"":""Male"", ""age"":25, ""occupation"":""(해당 분야 초급 직업)"", ""description"":""...""}},
        {{""name"":""이영희"", ""gender"":""Female"", ""age"":28, ""occupation"":""(해당 분야 초급 직업)"", ""description"":""...""}}
    ]
}}";
    }

    void ParseAndCreatePersonas(string responseJson, string socialStatus)
    {
        try
        {
            string content = ExtractContentFromResponse(responseJson);
            Debug.Log($"📦 [{socialStatus}] Extracted JSON 최종 정제:\n" + content);

            PersonaListWrapper personaList = JsonConvert.DeserializeObject<PersonaListWrapper>(content);

            if (personaList == null || personaList.personas == null)
            {
                Debug.LogError($"❌ [{socialStatus}] JSON 파싱 실패 (응답 형식 불일치)");
                return;
            }

            // socialStatus 설정 및 리스트에 추가
            foreach (var p in personaList.personas)
            {
                p.socialStatus = socialStatus;
                generatedPersonas.Add(p);

                // UI 카드 생성
                GameObject card = Instantiate(personaCardPrefab, contentParent);
                card.transform.localScale = Vector3.one;

                card.transform.Find("Name").GetComponent<Text>().text = p.name;
                card.transform.Find("Gender").GetComponent<Text>().text = p.gender;
                card.transform.Find("Age").GetComponent<Text>().text = p.age.ToString();
                card.transform.Find("Occupation").GetComponent<Text>().text = p.occupation;
                card.transform.Find("Description").GetComponent<Text>().text = p.description;

                // 배경색으로 그룹 구분 (선택사항)
                var bg = card.GetComponent<UnityEngine.UI.Image>();
                if (bg != null)
                {
                    bg.color = socialStatus == "junior"
                        ? new Color(0.9f, 0.95f, 1f) // 연한 파랑 (초년생)
                        : new Color(1f, 0.95f, 0.9f); // 연한 주황 (권위자)
                }
            }

            Debug.Log($"✅ [{socialStatus}] 파싱 성공: {personaList.personas.Count}명 추가됨 (총 {generatedPersonas.Count}명)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [{socialStatus}] Parse Error: " + e.Message);
        }
    }

    string ExtractContentFromResponse(string fullJson)
    {
        try
        {
            JObject root = JObject.Parse(fullJson);
            string content = root["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("❌ content 추출 실패");
                return "";
            }

            content = content.Trim();
            content = content.Replace("```json", "").Replace("```", "");
            return content;
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ ExtractContent 실패: " + e.Message);
            return "";
        }
    }
}