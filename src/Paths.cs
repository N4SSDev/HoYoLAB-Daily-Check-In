using System;
using System.IO;

namespace DailyCheckIn
{
    static class Paths
    {
        public const string AppName = "Daily Check-In";
        public const string TaskName = "Daily Check-In";
        public const string AppId = "com.n4ss.daily-checkin";

        public static readonly string Data = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DailyCheckIn");

        public static readonly string ConfigFile = Path.Combine(Data, "config.json");
        public static readonly string StateFile = Path.Combine(Data, "state.json");
        public static readonly string LogFile = Path.Combine(Data, "checkin.log");

        public static readonly string LegacyData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "genshin-daily-checkin");

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

        public static void EnsureData()
        {
            if (!Directory.Exists(Data)) Directory.CreateDirectory(Data);
        }
    }
}
