using UnityEngine;
using SFB; // StandaloneFileBrowser를 쓸 때

public class PDFUploader : MonoBehaviour
{
    [SerializeField] private SurveySimulator surveySimulator; // 연결 필요!

    private string selectedPath;

    public void OnClick_SelectPDF()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel("Select PDF", "", "pdf", false);
        if (paths.Length > 0)
        {
            selectedPath = paths[0];
            Debug.Log("✅ 선택된 PDF 경로: " + selectedPath);

            // 여기 추가!!
            if (surveySimulator != null)
                surveySimulator.SetPdfPath(selectedPath);
            else
                Debug.LogWarning("⚠️ SurveySimulator 참조가 없음!");
        }
    }
}
