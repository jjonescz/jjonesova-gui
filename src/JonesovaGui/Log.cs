using LibGit2Sharp;
using System;
using System.IO;

namespace JonesovaGui
{
    static class Log
    {
        private static readonly StreamWriter file = Open();

        public static void Write(LogLevel level, string message)
        {
            file.WriteLine($"{level}: {message}");
            file.Flush();
        }

        public static void Flush()
        {
            file.Flush();
        }

        private static StreamWriter Open()
        {
            var logsPath = Path.GetFullPath("jjonesova.cz/logs");
            Directory.CreateDirectory(logsPath);
            var logPath = Path.Combine(logsPath, $"{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss-fffffff}.txt");
            return new StreamWriter(logPath);
        }
    }
}
