using UnityEngine;
using UnityEngine.UI;
using SFB; // StandaloneFileBrowser
using System.IO;

public class PDFUploader : MonoBehaviour
{
    [SerializeField] private Text fileNameText;  // 선택한 파일명 표시용 텍스트

    private string pdfPath;

    //  버튼 클릭 시 실행
    public void OnClick_SelectPDF()
    {
        var extensions = new[] { new ExtensionFilter("PDF Files", "pdf") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select Survey PDF", "", extensions, false);

        if (paths.Length > 0)
        {
            pdfPath = paths[0];
            string fileName = Path.GetFileName(pdfPath);

            // 텍스트에 파일명 표시
            if (fileNameText != null)
                fileNameText.text = $"파일: {fileName}";

            Debug.Log($"✅ 선택된 PDF 경로: {pdfPath}");
        }
        else
        {
            Debug.LogWarning("❌ PDF 선택 취소됨");
            if (fileNameText != null) fileNameText.text = "파일 : 비어있음";
        }
    }

    // 외부 접근용 getter
    public string GetSelectedPDFPath() => pdfPath;
}
