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

        string prompt = BuildPrompt(sm, sampleCount);

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
        Debug.Log("🟡 [Request JSON]\n" + bodyJson);

        // ✅ keyConfig에서 API Key, URL 가져옴
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

    void ParseAndCreatePersonas(string responseJson)
    {
        try
        {
            string content = ExtractContentFromResponse(responseJson);
            Debug.Log("📦 Extracted JSON 최종 정제:\n" + content);

            PersonaListWrapper personaList = JsonConvert.DeserializeObject<PersonaListWrapper>(content);

            if (personaList == null || personaList.personas == null)
            {
                Debug.LogError("❌ JSON 파싱 실패 (응답 형식 불일치)");
                return;
            }

            generatedPersonas = new List<PersonaData>(personaList.personas);
            Debug.Log($"✅ 파싱 성공: {generatedPersonas.Count}명 생성됨");

            foreach (var p in generatedPersonas)
            {
                GameObject card = Instantiate(personaCardPrefab, contentParent);
                card.transform.localScale = Vector3.one;

                card.transform.Find("Name").GetComponent<Text>().text = p.name;
                card.transform.Find("Gender").GetComponent<Text>().text = p.gender;
                card.transform.Find("Age").GetComponent<Text>().text = p.age.ToString();
                card.transform.Find("Occupation").GetComponent<Text>().text = p.occupation;
                card.transform.Find("Description").GetComponent<Text>().text = p.description;
            }
            LoadingPanel.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Parse Error: " + e.Message);
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