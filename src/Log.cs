using System;
using System.IO;
using System.Text;

namespace DailyCheckIn
{
    /// <summary>
    /// Journal volontairement avare.
    ///
    /// L'ancienne version ecrivait cinq lignes par execution. En fonctionnement
    /// normal celui-ci n'en ecrit qu'une, resumant le run — le detail
    /// n'apparait qu'en cas d'erreur, quand il sert vraiment a quelque chose.
    /// Le fichier est ouvert, ecrit et referme en une fois : aucun descripteur
    /// n'est conserve, aucun tampon en memoire.
    /// </summary>
    static class Log
    {
        /// <summary>Au-dela, on repart d'un fichier neuf. 64 Ko = des mois d'historique.</summary>
        const long MaxBytes = 64 * 1024;

        static readonly object Gate = new object();

        /// <summary>UTF-8 sans BOM : un marqueur d'octets en tete d'un journal est du bruit.</summary>
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Une ligne. A n'utiliser que pour un evenement qui merite une trace.</summary>
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
                // Disque plein, droits refuses : le journal n'est pas assez
                // important pour faire echouer un check-in.
            }
        }

        /// <summary>Detail d'une erreur : contexte + exception, sur une seule ligne.</summary>
        public static void Error(string context, Exception ex)
        {
            Write("ERREUR " + context + " : " + Describe(ex));
        }

        /// <summary>Message d'exception lisible, cause profonde comprise.</summary>
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

            // Un seul fichier de secours : garder plus n'apporterait rien.
            var previous = Paths.LogFile + ".1";
            if (File.Exists(previous)) File.Delete(previous);
            fi.MoveTo(previous);
        }
    }
}
