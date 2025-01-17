using System.IO;
using System;

namespace Microsoft.Dafny {

    public static class DafnyLogger {
        public static readonly string logFilePath;

        static DafnyLogger() {
            string defaultPath = Path.Combine(Path.GetTempPath(), "dafny_debug.md");
            string customPath = Environment.GetEnvironmentVariable("DAFNY_LOG_PATH");
            logFilePath = string.IsNullOrEmpty(customPath) ? defaultPath : customPath;
        }

        public static void Log(string message) {
            File.AppendAllText(logFilePath, $"{message}\n");
        }
    }

}