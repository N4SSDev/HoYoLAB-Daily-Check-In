using System;
using System.IO;

namespace DailyCheckIn
{
    /// <summary>
    /// Emplacements sur disque, resolus une seule fois.
    ///
    /// Le dossier de donnees est fige en dur : le faire deriver du nom du
    /// produit ferait perdre la configuration au moindre renommage.
    /// </summary>
    static class Paths
    {
        public const string AppName = "Daily Check-In";
        public const string TaskName = "Daily Check-In";
        public const string AppId = "com.n4ss.daily-checkin";

        /// <summary>Dossier de configuration : %APPDATA%\DailyCheckIn</summary>
        public static readonly string Data = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DailyCheckIn");

        public static readonly string ConfigFile = Path.Combine(Data, "config.json");
        public static readonly string StateFile = Path.Combine(Data, "state.json");
        public static readonly string LogFile = Path.Combine(Data, "checkin.log");

        /// <summary>
        /// Dossier de donnees de la version Electron, pour reprendre sa
        /// configuration au premier lancement plutot que de la faire ressaisir.
        /// </summary>
        public static readonly string LegacyData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "genshin-daily-checkin");

        /// <summary>Dossier d'installation : %LOCALAPPDATA%\Programs\DailyCheckIn</summary>
        public static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "DailyCheckIn");

        public static readonly string InstalledExe = Path.Combine(InstallDir, "DailyCheckIn.exe");

        public static string ShortcutFile
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs", AppName + ".lnk");
            }
        }

        /// <summary>Chemin de l'executable en cours.</summary>
        public static string CurrentExe
        {
            get { return System.Reflection.Assembly.GetEntryAssembly().Location; }
        }

        public static bool IsInstalled
        {
            get
            {
                return string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe),
                                     StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>Cree le dossier de donnees si besoin. Appele au plus tard possible.</summary>
        public static void EnsureData()
        {
            if (!Directory.Exists(Data)) Directory.CreateDirectory(Data);
        }
    }
}
