using System.Diagnostics;
using System.IO;
using UnityEngine;

public static class PythonExecutor
{


    public static string RunPythonScript(string pythonPath, string scriptPath, string pdfPath)
    {
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = pythonPath, // ¿¹: "C:/Users/user/miniconda3/python.exe"
            Arguments = $"\"{scriptPath}\" \"{pdfPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        using (Process process = Process.Start(start))
        {
            using (StreamReader reader = process.StandardOutput)
            {
                string result = reader.ReadToEnd();
                process.WaitForExit();
                return result;
            }
        }
    }
}
