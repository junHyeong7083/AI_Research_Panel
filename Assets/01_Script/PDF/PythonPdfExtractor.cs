using System.Diagnostics;
using System.Text;
using UnityEngine;

/// <summary>
/// ���̽� �����ؼ� PDF �� ������ JSON �̾ƿ��� ���Ҹ� ���
/// ����Ƽ���� ���� PDF �Ľ� �� �ϰ�, �ܺ� ���̽㿡 �ñ�� ����
/// </summary>
public class PythonPdfExtractor
{
    private readonly string _pythonExePath;  // ���� python.exe ���
    private readonly string _scriptPath;     // �츮�� ���� survey_parser.py ���

    public PythonPdfExtractor(string pythonExePath, string scriptPath)
    {
        _pythonExePath = pythonExePath;
        _scriptPath = scriptPath;
    }

    /// <summary>
    /// PDF ��θ� �ѱ�� ���̽��� ������ JSON ���ڿ��� ������
    /// </summary>
    public string Extract(string pdfPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _pythonExePath,
            Arguments = $"\"{_scriptPath}\" \"{pdfPath}\"",  // "script.py" "C:\a.pdf"
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,    // �� �� ����
            CreateNoWindow = true,      // �ܼ�â �� ���
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // �����쿡�� �ѱ� ���� ����
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd(); // ���̽��� print�� JSON
            string error = p.StandardError.ReadToEnd();  // ���̽� �����α�
            p.WaitForExit(60000); // 60�ʱ��� ��ٸ�

            if (!string.IsNullOrEmpty(error))
                UnityEngine.Debug.LogWarning("Python STDERR:\n" + error);

            return output;
        }
    }
}
