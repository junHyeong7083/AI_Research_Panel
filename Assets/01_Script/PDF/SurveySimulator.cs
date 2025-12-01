// SurveySimulator.cs

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class SurveySimulator : MonoBehaviour
{
    [Header("GPT / Persona")]
    [SerializeField] private OpenAIKeyConfig keyConfig;
    [SerializeField] private PersonaGenerator personaGenerator;

    [Header("반복 설정")]
    [SerializeField] private int normalGptRepeatCount = 100;
    [SerializeField] private int personaRepeatCount = 100;

    [Header("Python")]
    [SerializeField] private string pythonExePath = @"C:\...\.venv\Scripts\python.exe";
    [SerializeField] private string pythonScriptPath = @"C:\...\survey_parser.py";

    private string selectedPdfPath;

    private GptSurveyStructurer _structurer;
    private QuestionFlattener _flattener;
    private GptPersonaSurveyRunner _personaRunner;
    private GptNormalSurveyRunner _normalRunner;
    private PythonPdfExtractor _python;

    public void Awake()
    {
        _structurer = new GptSurveyStructurer(keyConfig);
        _flattener = new QuestionFlattener();
        _personaRunner = new GptPersonaSurveyRunner(keyConfig);
        _normalRunner = new GptNormalSurveyRunner(keyConfig);
        _python = new PythonPdfExtractor(pythonExePath, pythonScriptPath);
    }

    public void SetPdfPath(string path) => selectedPdfPath = path;

    public void OnClick_SimulateSurvey()
    {
        StartCoroutine(SimulateSurveyRoutine());
    }

    private IEnumerator SimulateSurveyRoutine()
    {
        Debug.Log("설문 시뮬레이션 시작");
        Debug.Log($"설정: 일반 GPT {normalGptRepeatCount}회, 페르소나 {personaRepeatCount}회");

        var personas = personaGenerator.GetGeneratedPersonas();
        if (personas == null || personas.Count == 0)
        {
            Debug.LogError("페르소나가 없습니다.");
            yield break;
        }

        SavePersonaList(personas);

        if (string.IsNullOrEmpty(selectedPdfPath))
        {
            Debug.LogError("PDF 경로가 없습니다.");
            yield break;
        }

        string pyJson = _python.Run(selectedPdfPath);
        if (string.IsNullOrEmpty(pyJson))
        {
            Debug.LogError("파이썬 결과 비어있음");
            yield break;
        }

        var structTask = _structurer.StructureAsync(pyJson);
        while (!structTask.IsCompleted)
            yield return null;
        var questions = structTask.Result;
        if (questions == null || questions.Count == 0)
        {
            Debug.LogError("구조화된 설문이 비어있음");
            yield break;
        }

        var flat = _flattener.Flatten(questions);
        Debug.Log($"구조화 문항: {questions.Count} / 플래튼 문항: {flat.Count}");

        // 1. 일반 GPT로 설문 응답 (N회 반복)
        Debug.Log($"=== 일반 GPT 설문 시작 ({normalGptRepeatCount}회) ===");
        var normalTask = _normalRunner.RunMultipleAsync(flat, normalGptRepeatCount);
        while (!normalTask.IsCompleted)
            yield return null;
        Debug.Log("=== 일반 GPT 설문 완료 ===");

        // 2. 페르소나별 설문 응답 (N회 반복)
        Debug.Log($"=== 페르소나 GPT 설문 시작 ({personaRepeatCount}회 x {personas.Count}명) ===");
        var personaTask = _personaRunner.RunMultipleAsync(personas, flat, personaRepeatCount);
        while (!personaTask.IsCompleted)
            yield return null;
        Debug.Log("=== 페르소나 GPT 설문 완료 ===");

        Debug.Log("모든 설문 응답 완료!");
        Debug.Log($"저장 위치: {Path.Combine(Application.persistentDataPath, "SurveyExports")}");
    }

    private void SavePersonaList(List<PersonaData> personas)
    {
        var obj = new
        {
            personas = personas
        };

        string json = JsonConvert.SerializeObject(obj, Formatting.Indented);

        string folder = Path.Combine(Application.persistentDataPath, "SurveyExports");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, "personas.json");
        File.WriteAllText(path, json);

        Debug.Log("페르소나 리스트 저장됨: " + path);
    }
}
