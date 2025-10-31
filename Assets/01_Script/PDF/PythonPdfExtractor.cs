using System.Diagnostics;
using System.Text;
using UnityEngine;

/// <summary>
/// 파이썬 실행해서 PDF → 페이지 JSON 뽑아오는 역할만 담당
/// 유니티에서 직접 PDF 파싱 안 하고, 외부 파이썬에 맡기는 구조
/// </summary>
public class PythonPdfExtractor
{
    private readonly string _pythonExePath;  // 실제 python.exe 경로
    private readonly string _scriptPath;     // 우리가 만든 survey_parser.py 경로

    public PythonPdfExtractor(string pythonExePath, string scriptPath)
    {
        _pythonExePath = pythonExePath;
        _scriptPath = scriptPath;
    }

    /// <summary>
    /// PDF 경로를 넘기면 파이썬을 돌려서 JSON 문자열을 리턴함
    /// </summary>
    public string Extract(string pdfPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            Arguments = $"\"{_scriptPath}\" \"{pdfPath}\"",  // "script.py" "C:\a.pdf"
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,    // 쉘 안 쓰고
            CreateNoWindow = true,      // 콘솔창 안 띄움
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // 윈도우에서 한글 깨짐 방지
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd(); // 파이썬이 print한 JSON
            string error = p.StandardError.ReadToEnd();  // 파이썬 에러로그
            p.WaitForExit(60000); // 60초까지 기다림

            if (!string.IsNullOrEmpty(error))
                UnityEngine.Debug.LogWarning("Python STDERR:\n" + error);

            return output;
        }
    }
}
