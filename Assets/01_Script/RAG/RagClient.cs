// RagClient.cs
// KOSIS 실제 설문조사 데이터 검색 클라이언트

using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

public class RagClient
{
    private readonly string _baseUrl;

    public RagClient(string baseUrl = "http://127.0.0.1:8080")
    {
        _baseUrl = baseUrl;
    }

    /// <summary>
    /// KOSIS 실제 설문조사 데이터 검색
    /// </summary>
    public async Task<List<KosisResult>> SearchKosisAsync(string query, string gender = "", int year = 0, int nResults = 3)
    {
        if (string.IsNullOrEmpty(query))
            return new List<KosisResult>();

        var reqBody = new KosisQuery
        {
            query = query,
            gender = gender ?? "",
            year = year
        };

        string json = JsonConvert.SerializeObject(reqBody);
        string url = $"{_baseUrl}/kosis/search?n_results={nResults}";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RAG] 검색 실패: {req.error}");
                return new List<KosisResult>();
            }

            string responseText = req.downloadHandler.text;
            if (string.IsNullOrEmpty(responseText))
            {
                Debug.LogWarning("[RAG] 응답이 비어있음");
                return new List<KosisResult>();
            }

            try
            {
                var response = JsonConvert.DeserializeObject<KosisResponse>(responseText);
                return response?.results ?? new List<KosisResult>();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RAG] JSON 파싱 실패: {e.Message}");
                return new List<KosisResult>();
            }
        }
    }

    /// <summary>
    /// RAG 서버 상태 확인
    /// </summary>
    public async Task<bool> CheckServerAsync()
    {
        try
        {
            using (var req = UnityWebRequest.Get($"{_baseUrl}/stats"))
            {
                req.timeout = 5;
                var op = req.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                return req.result == UnityWebRequest.Result.Success;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 질문에 관련된 실제 통계 데이터를 컨텍스트 문자열로 변환
    /// </summary>
    public async Task<string> GetContextForQuestion(string question, string gender = "")
    {
        var results = await SearchKosisAsync(question, gender, 0, 3);

        if (results == null || results.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("[참고: 실제 한국인 설문조사 통계]");

        foreach (var r in results)
        {
            if (r != null && !string.IsNullOrEmpty(r.text))
            {
                sb.AppendLine($"- {r.text}");
            }
        }

        return sb.ToString();
    }
}

[System.Serializable]
public class KosisQuery
{
    public string query;
    public string gender;
    public int year;
}

[System.Serializable]
public class KosisResult
{
    public string text;
    public string stat_name;
    public string source;
    public int year;
    public string gender;
    public float similarity;
}

[System.Serializable]
public class KosisResponse
{
    public List<KosisResult> results;
    public int count;
}
