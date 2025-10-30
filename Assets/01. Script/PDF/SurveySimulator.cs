using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class SurveySimulator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private string openAIApiKey;
    [SerializeField] private PDFUploader pdfUploader;
    [SerializeField] private PersonaGenerator personaGenerator;

    [Header("Simulation Settings")]
    [SerializeField] private int personasToSimulate = 5;

    [SerializeField] OpenAIKeyConfig openAI;

    public void OnClick_SimulateSurvey()
    {
        StartCoroutine(SimulateSurveyRoutine());
    }

    private IEnumerator SimulateSurveyRoutine()
    {
        // 1️. PDF 경로 확인
        string pdfPath = pdfUploader.GetSelectedPDFPath();
        if (string.IsNullOrEmpty(pdfPath))
        {
            Debug.LogError("❌ PDF가 선택되지 않았습니다.");
            yield break;
        }

        // 2️. PDF 텍스트 읽기
        string surveyText = PDFReader.ReadTextFromPDF(pdfPath);
        if (string.IsNullOrEmpty(surveyText))
        {
            Debug.LogError("❌ PDF 텍스트를 읽지 못했습니다.");
            yield break;
        }

        // 3️. 페르소나 목록 가져오기
        List<PersonaData> personas = personaGenerator.GetGeneratedPersonas();
        if (personas == null || personas.Count == 0)
        {
            Debug.LogError("❌ 생성된 페르소나가 없습니다.");
            yield break;
        }

        // 4. 시뮬레이션 실행
        Debug.Log("설문 시뮬레이션 시작");
        int count = Mathf.Min(personasToSimulate, personas.Count);

        for (int i = 0; i < count; i++)
        {
            PersonaData persona = personas[i];
            string prompt = BuildPrompt(persona, surveyText);
            yield return StartCoroutine(SendGPTRequest(persona.name, prompt));
            yield return new WaitForSeconds(0.5f); // API 딜레이 방지
        }

        Debug.Log("모든 페르소나 설문 완료!");
    }

    private string BuildPrompt(PersonaData persona, string surveyText)
    {
        // GPT가 스스로 질문 개수 파악하도록 설계
        string prompt =
            $"You are {persona.name}, a {persona.age}-year-old {persona.gender} working as a {persona.occupation}.\n" +
            $"Based on your persona's background, values, and personality traits, answer the following survey questions.\n\n" +
            $"Survey:\n" +
            $"\"\"\"\n{surveyText}\n\"\"\"\n\n" +
            "Please carefully read all questions above and provide answers to **each question** in order.\n" +
            "If there are N questions, return exactly N answers.\n\n" +
            "Use this format strictly:\n" +
            "Q1: [your answer]\n" +
            "Q2: [your answer]\n" +
            "Q3: [your answer]\n" +
            "... (continue until all questions are answered)\n\n" +
            "Keep each answer concise and consistent with the persona's tone and experience.";

        return prompt;
    }


    private IEnumerator SendGPTRequest(string personaName, string prompt)
    {
        // GPT API 요청 구조 (C# 7.3 호환)
        var messageList = new List<Dictionary<string, string>>()
        {
            new Dictionary<string, string>()
            {
                { "role", "user" },
                { "content", prompt }
            }
        };

        var body = new Dictionary<string, object>()
        {
            { "model", "gpt-3.5-turbo" },
            { "temperature", 0.7f },
            { "messages", messageList }
        };

        string bodyJson = JsonConvert.SerializeObject(body);

        using (UnityWebRequest req = new UnityWebRequest(openAI.apiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(bodyJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("❌ [" + personaName + "] GPT 요청 실패: " + req.error);
                yield break;
            }

            string res = req.downloadHandler.text;
            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(res);
                var choices = json["choices"] as Newtonsoft.Json.Linq.JArray;
                string content = choices[0]["message"]["content"].ToString();

                Debug.Log("🧾 [" + personaName + "] 응답:\n" + content);
            }
            catch (System.Exception e)
            {
                Debug.LogError("⚠️ [" + personaName + "] 응답 파싱 실패: " + e.Message);
            }
        }
    }
}
