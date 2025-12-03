// SurveySimulator.cs

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Newtonsoft.Json;

public class SurveySimulator : MonoBehaviour
{
    [Header("GPT / Persona")]
    [SerializeField] private OpenAIKeyConfig keyConfig;
    [SerializeField] private PersonaGenerator personaGenerator;

    [Header("표본 설정")]
    [Tooltip("일반 GPT 반복 횟수 = 표본수, 페르소나는 생성된 수만큼 1회씩")]
    [SerializeField] private int sampleSize = 30;

    [Header("Python")]
    [SerializeField] private string pythonExePath = @"C:\...\.venv\Scripts\python.exe";
    [SerializeField] private string pythonScriptPath = @"C:\...\survey_parser.py";

    [Header("RAG 설정")]
    [Tooltip("RAG 서버 사용 여부 (실제 통계 데이터 참조)")]
    [SerializeField] private bool useRag = true;
    [SerializeField] private string ragServerUrl = "http://127.0.0.1:8080";

    private string selectedPdfPath;
    private int _sessionVersion; // exe 실행 시 결정되는 버전

    private GptSurveyStructurer _structurer;
    private QuestionFlattener _flattener;
    private GptPersonaSurveyRunner _personaRunner;
    private GptNormalSurveyRunner _normalRunner;
    private PythonPdfExtractor _python;

    public void Awake()
    {
        _structurer = new GptSurveyStructurer(keyConfig);
        _flattener = new QuestionFlattener();
        _personaRunner = new GptPersonaSurveyRunner(keyConfig, useRag, ragServerUrl);
        _normalRunner = new GptNormalSurveyRunner(keyConfig);
        _python = new PythonPdfExtractor(pythonExePath, pythonScriptPath);

        // exe 실행 시점에 버전 결정 (세션 내 고정)
        _sessionVersion = GetNextVersion();
        Debug.Log($"[SurveySimulator] 세션 버전: v{_sessionVersion}, RAG: {(useRag ? "ON" : "OFF")}");
    }

    public void SetPdfPath(string path) => selectedPdfPath = path;

    public void OnClick_SimulateSurvey()
    {
        StartCoroutine(SimulateSurveyRoutine());
    }

    private IEnumerator SimulateSurveyRoutine()
    {
        Debug.Log("설문 시뮬레이션 시작");

        // 버전 번호 결정
        int version = GetNextVersion();
        Debug.Log($"=== 버전 v{version} 시뮬레이션 ===" );

        var allPersonas = personaGenerator.GetGeneratedPersonas();
        if (allPersonas == null || allPersonas.Count == 0)
        {
            Debug.LogError("페르소나가 없습니다.");
            yield break;
        }

        // 그룹별로 분리
        var juniorPersonas = allPersonas.FindAll(p => p.socialStatus == "junior");
        var seniorPersonas = allPersonas.FindAll(p => p.socialStatus == "senior");

        Debug.Log($"표본 설정: 일반 GPT {sampleSize}회, 사회초년생 {juniorPersonas.Count}명, 권위자 {seniorPersonas.Count}명");

        SavePersonaList(allPersonas, version);

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

        // 1. 일반 GPT로 설문 응답 (sampleSize회 반복)
        Debug.Log($"=== 일반 GPT 설문 시작 ({sampleSize}회) ===");
        var normalTask = _normalRunner.RunMultipleAsync(flat, sampleSize, version);
        while (!normalTask.IsCompleted)
            yield return null;
        Debug.Log("=== 일반 GPT 설문 완료 ===");

        // 2. 사회초년생 그룹 설문
        if (juniorPersonas.Count > 0)
        {
            Debug.Log($"=== 사회초년생 그룹 설문 시작 ({juniorPersonas.Count}명) ===");
            var juniorTask = _personaRunner.RunMultipleAsync(juniorPersonas, flat, 1, "junior", version);
            while (!juniorTask.IsCompleted)
                yield return null;
            Debug.Log("=== 사회초년생 그룹 설문 완료 ===");
        }

        // 3. 권위자 그룹 설문
        if (seniorPersonas.Count > 0)
        {
            Debug.Log($"=== 권위자 그룹 설문 시작 ({seniorPersonas.Count}명) ===");
            var seniorTask = _personaRunner.RunMultipleAsync(seniorPersonas, flat, 1, "senior", version);
            while (!seniorTask.IsCompleted)
                yield return null;
            Debug.Log("=== 권위자 그룹 설문 완료 ===");
        }

        Debug.Log("모든 설문 응답 완료!");
        Debug.Log($"저장 위치: {Path.Combine(Application.persistentDataPath, "SurveyExports")} (v{version})");
    }

    private int GetNextVersion()
    {
        string folder = Path.Combine(Application.persistentDataPath, "SurveyExports");
        if (!Directory.Exists(folder))
            return 1;

        int maxVersion = 0;
        var files = Directory.GetFiles(folder, "*.csv");

        // 파일명에서 _v숫자 패턴 찾기
        var regex = new Regex(@"_v(\d+)\.csv$");
        foreach (var file in files)
        {
            var match = regex.Match(Path.GetFileName(file));
            if (match.Success)
            {
                int ver = int.Parse(match.Groups[1].Value);
                if (ver > maxVersion)
                    maxVersion = ver;
            }
        }

        return maxVersion + 1;
    }

    private void SavePersonaList(List<PersonaData> personas, int version)
    {
        var obj = new
        {
            personas = personas
        };

        string json = JsonConvert.SerializeObject(obj, Formatting.Indented);

        string folder = Path.Combine(Application.persistentDataPath, "SurveyExports");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, $"personas_v{version}.json");
        File.WriteAllText(path, json);

        Debug.Log("페르소나 리스트 저장됨: " + path);
    }
}
