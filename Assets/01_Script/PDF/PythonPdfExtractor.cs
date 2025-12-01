using System.Diagnostics;
using System.Text;
using UnityEngine;

public class PythonPdfExtractor
{
    private readonly string _pythonExe;
    private readonly string _scriptPath;

    public PythonPdfExtractor(string pythonExe, string scriptPath)
    {
        _pythonExe = pythonExe;
        _scriptPath = scriptPath;
    }

    public string Run(string pdfPath)
    {
        // 1) 사전 체크
        if (!System.IO.File.Exists(_pythonExe))
        {
            UnityEngine.Debug.LogError($"🐍 Python exe 없음: {_pythonExe}");
            return null;
        }
        if (!System.IO.File.Exists(_scriptPath))
        {
            UnityEngine.Debug.LogError($"🐍 Python 스크립트 없음: {_scriptPath}");
            return null;
        }
        if (!System.IO.File.Exists(pdfPath))
        {
            UnityEngine.Debug.LogError($"🐍 PDF 없음: {pdfPath}");
            return null;
        }

        var psi = new ProcessStartInfo
        {
            FileName = _pythonExe,
            Arguments = $"\"{_scriptPath}\" \"{pdfPath}\"",   // 꼭 따옴표
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // 윈도우에서 한글 깨짐 방지
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        UnityEngine.Debug.Log($"🐍 실행: {_pythonExe} {_scriptPath} {pdfPath}");

        using (var p = Process.Start(psi))
        {
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(60000);

            if (!string.IsNullOrEmpty(stderr))
            {
                UnityEngine.Debug.LogError("🐍 STDERR from python:\n" + stderr);
            }
            if (string.IsNullOrEmpty(stdout))
            {
                UnityEngine.Debug.LogError("🐍 파이썬이 stdout을 비우고 끝났음 (JSON 안 나옴)");
            }
            else
            {
                UnityEngine.Debug.Log("🐍 STDOUT from python:\n" + stdout);
            }

            return stdout;
        }
    }
}
