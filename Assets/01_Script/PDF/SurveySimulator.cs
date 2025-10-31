using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 전체 시뮬레이션 흐름:
/// 1) PDF 경로를 받아서
/// 2) Python으로 PDF → 원시 JSON 추출하고
/// 3) 그 JSON을 GPT로 구조화하고
/// 4) table/multi를 펼친 다음
/// 5) 페르소나마다 GPT에 던져서 응답을 받는 역할
/// -> 즉 “조립”만 담당하는 MonoBehaviour
/// </summary>
public class SurveySimulator : MonoBehaviour
{
    [Header("GPT API Settings")]
    [SerializeField] private OpenAIKeyConfig keyConfig;      // OpenAI API 키/URL 넣어둔 ScriptableObject 같은 거
    [SerializeField] private PersonaGenerator personaGenerator; // 페르소나 목록 만들어주는 컴포넌트

    [Header("Python Settings")]
    // 실제 파이썬 실행 파일 경로
    [SerializeField] private string pythonExePath = @"C:\Users\user\Git\AI_Research_Panel\Assets\01_Script\Python\.venv\Scripts\python.exe";
    // 우리가 만든 PDF 파서 파이썬 스크립트 경로
    [SerializeField] private string scriptPath = @"C:\Users\user\Git\AI_Research_Panel\Assets\01_Script\Python\survey_parser.py";

    // GPT가 구조화한 원본 문항
    private List<QuestionData> _questions;
    // table / multi까지 펼친 최종 문항
    private List<FlattenedQuestion> _flatQuestions;
    // PDF 선택 UI에서 넘겨줄 경로
    private string _selectedPdfPath;

    // PDFUploader가 여기로 경로를 꽂아줌
    public void SetPdfPath(string path) => _selectedPdfPath = path;

    // 버튼에 연결할 함수
    public void OnClick_SimulateSurvey()
    {
        StartCoroutine(SimulateSurveyRoutine());
    }

    private IEnumerator SimulateSurveyRoutine()
    {
        Debug.Log("설문 시뮬레이션 시작");

        // 0. 페르소나 있는지 확인
        var personas = personaGenerator.GetGeneratedPersonas();
        if (personas == null || personas.Count == 0)
        {
            Debug.LogError("페르소나가 없습니다. 먼저 PersonaGenerator로 생성하세요.");
            yield break;
        }

        // 0. PDF 경로 있는지 확인
        if (string.IsNullOrEmpty(_selectedPdfPath))
        {
            Debug.LogError("PDF 경로가 지정되지 않았습니다.");
            yield break;
        }

        // 1. Python으로 PDF 파싱
        var extractor = new PythonPdfExtractor(pythonExePath, scriptPath);
        string pyOutput = extractor.Extract(_selectedPdfPath);
        if (string.IsNullOrEmpty(pyOutput))
        {
            Debug.LogError("Python 결과가 비어있습니다.");
            yield break;
        }

        // 1-1. 파이썬이 준 JSON을 C# 모델로 역직렬화
        PythonRawResult pyResult = null;
        try
        {
            pyResult = Newtonsoft.Json.JsonConvert.DeserializeObject<PythonRawResult>(pyOutput);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Python JSON 역직렬화 실패: {e.Message}\n원본:\n{pyOutput}");
            yield break;
        }

        // 파이썬 쪽에서 에러 나거나 페이지가 없으면 중단
        if (pyResult == null || !string.IsNullOrEmpty(pyResult.error) || pyResult.pages == null || pyResult.pages.Count == 0)
        {
            Debug.LogError("Python에서 pages가 비었거나 error가 반환됐습니다.");
            yield break;
        }

        // 2. GPT로 설문 구조화 (비동기)
        var structurer = new GptSurveyStructurer(keyConfig);
        var structTask = structurer.BuildQuestionsAsync(pyOutput);
        while (!structTask.IsCompleted)
            yield return null;  // 유니티 코루틴에서 Task 기다리는 패턴

        var questions = structTask.Result;
        if (questions == null || questions.Count == 0)
        {
            Debug.LogError("GPT 구조화 실패");
            yield break;
        }
        _questions = questions;

        // 3. table / multi 전개
        var flattener = new DefaultQuestionFlattener();
        _flatQuestions = flattener.Flatten(_questions);
        Debug.Log($"구조화된 문항 수(원본): {_questions.Count} / 펼친 문항 수: {_flatQuestions.Count}");

        // 4. 페르소나별로 GPT에 응답 요청
        var runner = new GptPersonaSurveyRunner(keyConfig);
        var runAllTask = RunAllPersonas(runner, personas, _flatQuestions);
        while (!runAllTask.IsCompleted)
            yield return null;

        Debug.Log("✅ 모든 페르소나 응답 완료");
    }

    // Task.WhenAll을 유니티 코루틴에서 기다리기 쉽게 빼놓은 함수
    private async Task RunAllPersonas(GptPersonaSurveyRunner runner, List<PersonaData> personas, List<FlattenedQuestion> flatQuestions)
    {
        var tasks = new List<Task>(personas.Count);
        foreach (var p in personas)
            tasks.Add(runner.RunAsync(p, flatQuestions));

        await Task.WhenAll(tasks);
    }
}
