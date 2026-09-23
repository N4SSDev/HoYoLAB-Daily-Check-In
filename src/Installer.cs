using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DailyCheckIn
{
    /// <summary>
    /// Installation et desinstallation, portees par l'executable lui-meme.
    ///
    /// Un installeur NSIS de 106 Mo pour deployer un fichier de quelques
    /// dizaines de kilo-octets n'aurait aucun sens : l'app se copie elle-meme,
    /// pose son raccourci et son entree de desinstallation. Rien a telecharger,
    /// rien a compiler en plus.
    /// </summary>
    static class Installer
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DailyCheckIn";

        /// <summary>Copie l'app dans %LOCALAPPDATA% et l'enregistre auprès de Windows.</summary>
        public static string Install()
        {
            try
            {
                Directory.CreateDirectory(Paths.InstallDir);

                var source = Paths.CurrentExe;
                if (!string.Equals(Path.GetFullPath(source), Path.GetFullPath(Paths.InstalledExe),
                                   StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(source, Paths.InstalledExe, true);
                }

                CreateShortcut();
                RegisterUninstall();
                Log.Write("installé dans " + Paths.InstallDir);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error("installation", ex);
                return Log.Describe(ex);
            }
        }

        /// <summary>Retire tout ce que l'installation a pose. La configuration est conservee.</summary>
        public static string Uninstall(bool removeData)
        {
            var problems = "";
            try { AutoStart.Disable(); } catch (Exception ex) { problems += Log.Describe(ex) + " "; }

            try { if (File.Exists(Paths.ShortcutFile)) File.Delete(Paths.ShortcutFile); }
            catch (Exception ex) { problems += Log.Describe(ex) + " "; }

            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }
            catch (Exception ex) { problems += Log.Describe(ex) + " "; }

            if (removeData)
            {
                try { if (Directory.Exists(Paths.Data)) Directory.Delete(Paths.Data, true); }
                catch (Exception ex) { problems += Log.Describe(ex) + " "; }
            }

            // Un executable ne peut pas se supprimer lui-meme pendant qu'il
            // tourne : on delegue a un cmd qui attend notre sortie.
            try
            {
                if (Directory.Exists(Paths.InstallDir))
                {
                    var psi = new ProcessStartInfo("cmd.exe",
                        "/c timeout /t 2 /nobreak >nul & rmdir /s /q \"" + Paths.InstallDir + "\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex) { problems += Log.Describe(ex) + " "; }

            return problems.Length == 0 ? null : problems.Trim();
        }

        public static bool IsRegistered
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    return k != null;
            }
        }

        static void CreateShortcut()
        {
            // WScript.Shell en liaison tardive : pas de reference a ajouter.
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return;

            dynamic shell = null;
            try
            {
                shell = Activator.CreateInstance(type);
                var dir = Path.GetDirectoryName(Paths.ShortcutFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                dynamic link = shell.CreateShortcut(Paths.ShortcutFile);
                link.TargetPath = Paths.InstalledExe;
                link.WorkingDirectory = Paths.InstallDir;
                link.IconLocation = Paths.InstalledExe + ",0";
                link.Description = "Check-in quotidien HoYoLAB";
                link.Save();
            }
            finally
            {
                if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }

        static void RegisterUninstall()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                if (k == null) return;
                var size = (int)(new FileInfo(Paths.InstalledExe).Length / 1024);
                k.SetValue("DisplayName", Paths.AppName);
                k.SetValue("DisplayIcon", Paths.InstalledExe);
                k.SetValue("DisplayVersion", Program.Version);
                k.SetValue("Publisher", "N4SS");
                k.SetValue("InstallLocation", Paths.InstallDir);
                k.SetValue("UninstallString", "\"" + Paths.InstalledExe + "\" --uninstall");
                k.SetValue("QuietUninstallString", "\"" + Paths.InstalledExe + "\" --uninstall --silent");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", size, RegistryValueKind.DWord);
            }
        }
    }
}
