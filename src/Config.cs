using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DailyCheckIn
{
    sealed class Config
    {
        public string[] Cookies = new string[0];
        public string Webhook = "";

        public string Lang = "fr-fr";

        public string PingMode = "error";

        public bool NotifyAlready = true;

        public bool ShowNotes = true;
        public bool AllServers = false;

        public bool ToastOnSuccess = true;
        public bool ToastOnAlready = false;
        public bool ToastOnError = true;

        public int StartupDelaySec = 0;

        public static readonly string[] PingModes = { "never", "error", "always" };

        public static Config Load()
        {
            var cfg = ReadFrom(Paths.ConfigFile);
            if (cfg != null) return cfg;

            var legacy = ReadFrom(Path.Combine(Paths.LegacyData, "config.json"));
            if (legacy != null)
            {
                Log.Write("configuration reprise de la version precedente");
                legacy.Save();
                return legacy;
            }
            return new Config();
        }

        static Config ReadFrom(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var d = Json.Parse(File.ReadAllText(file, Encoding.UTF8));
                if (d == null) return null;

                var cfg = new Config();
                var arr = Json.Arr(d, "cookies");
                if (arr != null)
                {
                    var list = new List<string>(arr.Length);
                    foreach (var o in arr)
                    {
                        var s = o as string;
                        if (!string.IsNullOrEmpty(s)) list.Add(s);
                    }
                    cfg.Cookies = list.ToArray();
                }
                cfg.Webhook = Json.Str(d, "webhook", "");
                cfg.Lang = Json.Str(d, "lang", "fr-fr");
                cfg.PingMode = Json.Str(d, "pingMode", "error");
                cfg.NotifyAlready = Json.Bool(d, "notifyAlready", true);
                cfg.ShowNotes = Json.Bool(d, "showNotes", true);
                cfg.AllServers = Json.Bool(d, "allServers", false);
                cfg.ToastOnSuccess = Json.Bool(d, "toastOnSuccess", true);
                cfg.ToastOnAlready = Json.Bool(d, "toastOnAlready", false);
                cfg.ToastOnError = Json.Bool(d, "toastOnError", true);
                cfg.StartupDelaySec = Json.Int(d, "startupDelaySec", 0);
                cfg.Normalize();
                return cfg;
            }
            catch (Exception ex)
            {
                Log.Error("lecture de la configuration", ex);
                return null;
            }
        }

        public void Normalize()
        {
            if (Cookies == null) Cookies = new string[0];
            Webhook = (Webhook ?? "").Trim();
            if (string.IsNullOrEmpty(Lang)) Lang = "fr-fr";
            if (Array.IndexOf(PingModes, PingMode) < 0) PingMode = "error";
            if (StartupDelaySec < 0) StartupDelaySec = 0;
            if (StartupDelaySec > 300) StartupDelaySec = 300;
        }

        public List<string> Problems()
        {
            var errors = new List<string>();

            if (Cookies.Length == 0)
            {
                errors.Add("Aucun cookie HoYoLAB renseigne.");
            }
            else
            {
                for (var i = 0; i < Cookies.Length; i++)
                {
                    if (Cookies[i].IndexOf("ltoken_v2", StringComparison.Ordinal) < 0 ||
                        Cookies[i].IndexOf("ltuid_v2", StringComparison.Ordinal) < 0)
                    {
                        errors.Add("Cookie #" + (i + 1) + " : il doit contenir ltoken_v2 ET ltuid_v2.");
                    }
                }
            }

            if (!Discord.IsWebhookUrl(Webhook))
            {
                errors.Add("URL de webhook Discord manquante ou invalide.");
            }
            return errors;
        }

        public bool IsUsable { get { return Problems().Count == 0; } }

        public void Save()
        {
            Normalize();
            var json = new Json.Writer()
                .Add("cookies", Cookies)
                .Add("webhook", Webhook)
                .Add("lang", Lang)
                .Add("pingMode", PingMode)
                .Add("notifyAlready", NotifyAlready)
                .Add("showNotes", ShowNotes)
                .Add("allServers", AllServers)
                .Add("toastOnSuccess", ToastOnSuccess)
                .Add("toastOnAlready", ToastOnAlready)
                .Add("toastOnError", ToastOnError)
                .Add("startupDelaySec", StartupDelaySec)
                .ToString();

            Paths.EnsureData();
            AtomicFile.Write(Paths.ConfigFile, json);
        }
    }

    static class AtomicFile
    {
        public static void Write(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
