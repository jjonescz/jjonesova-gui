using System.Diagnostics;
using System.Text;

namespace PublishTool;

internal class Program
{
    static void Main()
    {
        var tagName = GetProcessOutput("git", "describe --tags").TrimEnd();
        Console.WriteLine($"Tag: '{tagName}'");
    }

    static string GetProcessOutput(string fileName, string arguments)
    {
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        proc.Start();
        var sb = new StringBuilder();
        while (!proc.StandardOutput.EndOfStream)
        {
            var line = proc.StandardOutput.ReadLine();
            if (line != null)
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }
}
