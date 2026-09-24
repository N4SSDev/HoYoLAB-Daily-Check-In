using System;
using System.IO;
using System.Text;

namespace DailyCheckIn
{
    static class Log
    {
        const long MaxBytes = 64 * 1024;

        static readonly object Gate = new object();

        static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Paths.EnsureData();
                    Rotate();
                    var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n";
                    File.AppendAllText(Paths.LogFile, line, Utf8);
                }
            }
            catch
            {
            }
        }

        public static void Error(string context, Exception ex)
        {
            Write("ERREUR " + context + " : " + Describe(ex));
        }

        public static string Describe(Exception ex)
        {
            if (ex == null) return "(inconnue)";
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            return inner == ex ? ex.Message : ex.Message + " -> " + inner.Message;
        }

        static void Rotate()
        {
            var fi = new FileInfo(Paths.LogFile);
            if (!fi.Exists || fi.Length <= MaxBytes) return;

            var previous = Paths.LogFile + ".1";
            if (File.Exists(previous)) File.Delete(previous);
            fi.MoveTo(previous);
        }
    }
}
