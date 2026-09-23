using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DailyCheckIn
{
    /// <summary>
    /// Lancement a l'ouverture de session, via une tache planifiee.
    ///
    /// La cle de registre `Run` serait le choix evident, et le mauvais :
    /// Windows la declenche avec un retard variable, la decale derriere le
    /// chargement du Bureau, et n'en journalise rien — impossible de distinguer
    /// « pas encore lance » de « jamais lance ».
    ///
    /// On passe par l'API COM du Planificateur plutot que par `schtasks.exe` :
    /// pas de processus enfant a lancer, et surtout pas de fichier XML
    /// temporaire a ecrire puis effacer.
    /// </summary>
    static class AutoStart
    {
        // Constantes du Planificateur de taches.
        const int TriggerLogon = 9;
        const int ActionExec = 0;
        const int CreateOrUpdate = 6;
        const int LogonInteractiveToken = 3;
        const int RunLevelLua = 0;
        const int InstancesIgnoreNew = 2;

        /// <summary>Chemin de l'executable actuellement inscrit, ou null.</summary>
        public static string RegisteredCommand()
        {
            dynamic service = null, folder = null;
            try
            {
                service = Connect();
                folder = service.GetFolder("\\");
                dynamic task = folder.GetTask(Paths.TaskName);
                // La collection d'actions du Planificateur est indexee a partir de 1.
                return (string)task.Definition.Actions[1].Path;
            }
            catch
            {
                // Tache absente : ce n'est pas une erreur.
                return null;
            }
            finally { Release(folder); Release(service); }
        }

        public static bool IsEnabled { get { return RegisteredCommand() != null; } }

        /// <summary>Cree ou remplace la tache. Retourne null si tout s'est bien passe.</summary>
        public static string Enable(string exePath, string arguments)
        {
            dynamic service = null, folder = null, def = null;
            try
            {
                service = Connect();
                folder = service.GetFolder("\\");
                def = service.NewTask(0);

                def.RegistrationInfo.Description = "Check-in quotidien HoYoLAB, en arriere-plan.";
                def.RegistrationInfo.Author = Environment.UserName;

                dynamic trigger = def.Triggers.Create(TriggerLogon);
                trigger.Enabled = true;
                trigger.UserId = CurrentUser();

                dynamic action = def.Actions.Create(ActionExec);
                action.Path = exePath;
                action.Arguments = arguments;
                action.WorkingDirectory = Path.GetDirectoryName(exePath);

                def.Principal.UserId = CurrentUser();
                def.Principal.LogonType = LogonInteractiveToken;
                def.Principal.RunLevel = RunLevelLua;      // aucune elevation

                var s = def.Settings;
                s.Enabled = true;
                s.Hidden = false;
                s.StartWhenAvailable = true;               // rattrape une session ouverte trop tot
                s.DisallowStartIfOnBatteries = false;
                s.StopIfGoingOnBatteries = false;
                s.RunOnlyIfIdle = false;
                s.RunOnlyIfNetworkAvailable = false;
                s.MultipleInstances = InstancesIgnoreNew;  // jamais deux check-ins de front
                s.ExecutionTimeLimit = "PT10M";            // garde-fou absolu
                s.AllowDemandStart = true;
                s.AllowHardTerminate = true;

                folder.RegisterTaskDefinition(Paths.TaskName, def, CreateOrUpdate,
                                              null, null, LogonInteractiveToken, null);
                return null;
            }
            catch (Exception ex)
            {
                return Log.Describe(ex);
            }
            finally { Release(def); Release(folder); Release(service); }
        }

        /// <summary>Supprime la tache. Absente = succes.</summary>
        public static string Disable()
        {
            dynamic service = null, folder = null;
            try
            {
                service = Connect();
                folder = service.GetFolder("\\");
                folder.DeleteTask(Paths.TaskName, 0);
                return null;
            }
            catch (Exception ex)
            {
                return RegisteredCommand() == null ? null : Log.Describe(ex);
            }
            finally { Release(folder); Release(service); }
        }

        /// <summary>
        /// Repare une tache dont la cible a disparu.
        ///
        /// On verifie l'existence du fichier, pas l'egalite avec l'executable
        /// courant : sinon ouvrir une copie de l'app detournerait la tache de
        /// celle qui est installee, et reciproquement — les deux se voleraient
        /// le demarrage. Viser un autre executable *present* est un choix
        /// delibere, pas une panne.
        /// </summary>
        /// <returns>L'ancien chemin s'il a fallu reparer, sinon null.</returns>
        public static string Heal(string exePath, string arguments)
        {
            var registered = RegisteredCommand();
            if (registered == null) return null;
            if (File.Exists(registered)) return null;

            Log.Write("tâche de démarrage cassée (" + registered + "), réécriture vers " + exePath);
            Enable(exePath, arguments);
            return registered;
        }

        static dynamic Connect()
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            if (type == null) throw new PlatformNotSupportedException("Planificateur de tâches indisponible.");
            dynamic service = Activator.CreateInstance(type);
            service.Connect();
            return service;
        }

        static string CurrentUser()
        {
            var domain = Environment.UserDomainName;
            return string.IsNullOrEmpty(domain) ? Environment.UserName : domain + "\\" + Environment.UserName;
        }

        static void Release(object com)
        {
            if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
        }
    }
}
