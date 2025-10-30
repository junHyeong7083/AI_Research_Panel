using UnityEngine;

[CreateAssetMenu(fileName = "OpenAIKey", menuName = "Config/OpenAI Key Config")]
public class OpenAIKeyConfig : ScriptableObject
{
    public string openAIApiKey;
    public string apiUrl = "https://api.openai.com/v1/chat/completions";
}
