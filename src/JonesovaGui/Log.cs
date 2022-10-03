using System;
using System.Diagnostics;
using System.IO;

namespace JonesovaGui
{
    static class Log
    {
        public static readonly string RootPath = Path.GetFullPath("jjonesova.cz",
            basePath: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        public static readonly string LogsDirectoryPath = Path.Combine(RootPath, "logs");
        public static readonly string LogPath = Path.Combine(LogsDirectoryPath, $"{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss-fffffff}.txt");
        public static readonly string ErrorStampPath = Path.Combine(RootPath, "error.stamp");
        private static readonly StreamWriter file = Open();
        private static readonly object syncRoot = new object();

        public static void Write(LogLevel level, string message)
        {
            var line = $"{level} ({DateTime.UtcNow:O}): {message}";
            lock (syncRoot)
            {
                file.WriteLine(line);
                file.Flush();
                if (Debugger.IsAttached)
                    System.Diagnostics.Debug.WriteLine(line);
            }
        }

        public static void Write(LogLevel level, string prefix, string message)
        {
            Write(level, $"{prefix}: {message}");
        }

        public static void Error(string prefix, string message)
        {
            Write(LogLevel.Error, prefix, message);
        }

        public static void Warn(string prefix, string message)
        {
            Write(LogLevel.Warning, prefix, message);
        }

        public static void Info(string prefix, string message)
        {
            Write(LogLevel.Info, prefix, message);
        }

        public static void Debug(string prefix, string message)
        {
            Write(LogLevel.Debug, prefix, message);
        }

        private static StreamWriter Open()
        {
            Directory.CreateDirectory(LogsDirectoryPath);
            return new StreamWriter(LogPath);
        }
    }
}
