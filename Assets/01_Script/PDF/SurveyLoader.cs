using UnityEngine;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

public class SurveyLoader : MonoBehaviour
{
    [SerializeField] private string fileName = "survey_raw.json";

    void Start()
    {
        LoadSurvey();
    }

    public void LoadSurvey()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"❌ File not found: {path}");
            return;
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.Log("✅ Loaded 0 questions from survey_raw.json (empty)");
            return;
        }

        try
        {
            JObject root = JObject.Parse(json);
            if (root["questions"] != null)
            {
                Debug.Log("✅ Already structured questions JSON. (from file)");
            }
            else if (root["pages"] != null)
            {
                Debug.Log("📄 Raw PDF pages JSON loaded. (needs GPT structuring)");
            }
            else
            {
                Debug.LogWarning("⚠️ JSON does not contain 'questions' or 'pages'");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ JSON parse error: " + e.Message);
        }
    }
}
