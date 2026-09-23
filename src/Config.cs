using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DailyCheckIn
{
    /// <summary>Reglages de l'utilisateur. Lu une fois par execution.</summary>
    sealed class Config
    {
        public string[] Cookies = new string[0];
        public string Webhook = "";

        /// <summary>Langue des noms de recompense. Pas exposee : francais partout.</summary>
        public string Lang = "fr-fr";

        /// <summary>never | error | always — mention @everyone dans le salon.</summary>
        public string PingMode = "error";

        /// <summary>Poster l'embed meme quand la journee est deja validee.</summary>
        public bool NotifyAlready = true;

        public bool ShowNotes = true;
        public bool AllServers = false;

        public bool ToastOnSuccess = true;
        public bool ToastOnAlready = false;
        public bool ToastOnError = true;

        /// <summary>Pause avant le check-in au demarrage. Zero par defaut.</summary>
        public int StartupDelaySec = 0;

        public static readonly string[] PingModes = { "never", "error", "always" };

        // ------------------------------------------------------------------ lecture

        /// <summary>
        /// Charge la configuration. En l'absence de fichier, tente de reprendre
        /// celle de la version Electron : c'est ce qui evite de faire ressaisir
        /// un cookie de 250 caracteres.
        /// </summary>
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
                // Fichier corrompu : on repart des defauts plutot que de planter.
                Log.Error("lecture de la configuration", ex);
                return null;
            }
        }

        // ------------------------------------------------------------------ validation

        /// <summary>Ramene les valeurs dans leur domaine. Sans effet de bord.</summary>
        public void Normalize()
        {
            if (Cookies == null) Cookies = new string[0];
            Webhook = (Webhook ?? "").Trim();
            if (string.IsNullOrEmpty(Lang)) Lang = "fr-fr";
            if (Array.IndexOf(PingModes, PingMode) < 0) PingMode = "error";
            if (StartupDelaySec < 0) StartupDelaySec = 0;
            if (StartupDelaySec > 300) StartupDelaySec = 300;
        }

        /// <summary>Liste des problemes bloquants. Vide = pret a tourner.</summary>
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

        // ------------------------------------------------------------------ ecriture

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

    /// <summary>
    /// Ecriture atomique : fichier temporaire puis remplacement. Une coupure
    /// pendant l'ecriture ne peut pas laisser un fichier tronque.
    /// </summary>
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
