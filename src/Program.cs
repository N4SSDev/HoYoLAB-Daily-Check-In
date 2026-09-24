using System;
using System.Threading;
using System.Windows.Forms;

namespace DailyCheckIn
{
    static class Program
    {
        public const string Version = "2.0.0";

        [STAThread]
        static int Main(string[] args)
        {
            var mode = ModeOf(args);

            if (mode == Mode.Background || mode == Mode.Window)
            {
                bool created;
                using (var mutex = new Mutex(true, "Local\\" + Paths.AppId, out created))
                {
                    if (!created)
                    {
                        Log.Write("une autre instance est déjà en cours, arrêt");
                        return 0;
                    }
                    try { return Dispatch(mode); }
                    finally { mutex.ReleaseMutex(); }
                }
            }
            return Dispatch(mode);
        }

        enum Mode { Window, Background, Install, Uninstall }

        static Mode ModeOf(string[] args)
        {
            foreach (var a in args)
            {
                switch ((a ?? "").ToLowerInvariant())
                {
                    case "--background": return Mode.Background;
                    case "--install": return Mode.Install;
                    case "--uninstall": return Mode.Uninstall;
                }
            }
            return Mode.Window;
        }

        static bool HasFlag(string[] args, string flag)
        {
            foreach (var a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static int Dispatch(Mode mode)
        {
            var args = Environment.GetCommandLineArgs();
            switch (mode)
            {
                case Mode.Background: return RunBackground();
                case Mode.Install: return RunInstall();
                case Mode.Uninstall: return RunUninstall(HasFlag(args, "--silent"));
                default: return RunWindow();
            }
        }

        static int RunBackground()
        {
            Config cfg;
            try { cfg = Config.Load(); }
            catch (Exception ex) { Log.Error("chargement de la configuration", ex); return 1; }

            RunOutcome outcome;
            try
            {
                outcome = Runner.Run(cfg, false, true);
            }
            catch (Exception ex)
            {
                Log.Error("check-in", ex);
                if (cfg.ToastOnError) Toast("Échec du check-in", Log.Describe(ex));
                return 1;
            }

            Notify(cfg, outcome);
            return outcome.HasError ? 1 : 0;
        }

        static void Toast(string title, string body)
        {
            if (!Notifier.Show(title, body))
                Log.Write("notification non affichée par Windows : " + title);
        }

        static void Notify(Config cfg, RunOutcome o)
        {
            if (o.HasError)
            {
                if (cfg.ToastOnError) Toast("Échec du check-in", o.Error);
                return;
            }

            if (o.Results.Count == 0)
            {
                if (cfg.ToastOnAlready && o.Cached.Count > 0)
                {
                    var c = o.Cached[0];
                    Toast("Déjà récupéré", Line(c.Award, c.Day, c.Total, null));
                }
                return;
            }

            CheckInResult failed = null;
            foreach (var r in o.Results)
                if (r.Status == RunStatus.Error) { failed = r; break; }

            if (failed != null)
            {
                if (cfg.ToastOnError) Toast("Échec du check-in", failed.StatusText);
                return;
            }

            var first = o.Results[0];
            var body = Line(first.AwardLabel, first.Day, first.Total, first.Notes);

            if (first.Status == RunStatus.Already)
            {
                if (cfg.ToastOnAlready) Toast("Déjà récupéré", body);
            }
            else if (cfg.ToastOnSuccess)
            {
                Toast("Récompense réclamée", body);
            }
        }

        static string Line(string award, int day, int total, System.Collections.Generic.List<NotesData> notes)
        {
            var sb = new System.Text.StringBuilder(120);
            sb.Append(string.IsNullOrEmpty(award) ? "Récompense du jour" : award);
            if (day > 0) sb.Append(" — jour ").Append(day).Append('/').Append(total);

            if (notes != null && notes.Count > 0)
            {
                var n = notes[0];
                sb.Append("\nRésine ").Append(n.Resin).Append('/').Append(n.MaxResin);
                sb.Append(" · Commissions ").Append(n.CommissionsDone).Append('/').Append(n.CommissionsTotal);
                if (n.CommissionsTotal > 0 && n.CommissionsDone >= n.CommissionsTotal) sb.Append(" ✓");
                if (n.BossMax > 0) sb.Append(" · Boss ").Append(n.BossDone).Append('/').Append(n.BossMax);
            }
            return sb.ToString();
        }

        static int RunWindow()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ConfigForm());
            return 0;
        }

        static int RunInstall()
        {
            var error = Installer.Install();
            if (error != null)
            {
                MessageBox.Show("Installation impossible :\n\n" + error, Paths.AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            System.Diagnostics.Process.Start(Paths.InstalledExe);
            return 0;
        }

        static int RunUninstall(bool silent)
        {
            if (!silent)
            {
                var answer = MessageBox.Show(
                    "Désinstaller " + Paths.AppName + " ?\n\n" +
                    "La tâche de démarrage et le raccourci seront retirés.\n" +
                    "Choisis « Oui » pour effacer aussi ta configuration (cookie, webhook).",
                    Paths.AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (answer == DialogResult.Cancel) return 1;
                var error = Installer.Uninstall(answer == DialogResult.Yes);
                MessageBox.Show(error == null ? "Désinstallation terminée." : "Terminé, avec des réserves :\n\n" + error,
                                Paths.AppName, MessageBoxButtons.OK,
                                error == null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return 0;
            }
            Installer.Uninstall(false);
            return 0;
        }
    }
}
