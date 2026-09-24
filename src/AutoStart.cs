using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DailyCheckIn
{
    static class AutoStart
    {
        const int TriggerLogon = 9;
        const int ActionExec = 0;
        const int CreateOrUpdate = 6;
        const int LogonInteractiveToken = 3;
        const int RunLevelLua = 0;
        const int InstancesIgnoreNew = 2;

        public static string RegisteredCommand()
        {
            dynamic service = null, folder = null;
            try
            {
                service = Connect();
                folder = service.GetFolder("\\");
                dynamic task = folder.GetTask(Paths.TaskName);
                return (string)task.Definition.Actions[1].Path;
            }
            catch
            {
                return null;
            }
            finally { Release(folder); Release(service); }
        }

        public static bool IsEnabled { get { return RegisteredCommand() != null; } }

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
                def.Principal.RunLevel = RunLevelLua;

                var s = def.Settings;
                s.Enabled = true;
                s.Hidden = false;
                s.StartWhenAvailable = true;
                s.DisallowStartIfOnBatteries = false;
                s.StopIfGoingOnBatteries = false;
                s.RunOnlyIfIdle = false;
                s.RunOnlyIfNetworkAvailable = false;
                s.MultipleInstances = InstancesIgnoreNew;
                s.ExecutionTimeLimit = "PT10M";
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
