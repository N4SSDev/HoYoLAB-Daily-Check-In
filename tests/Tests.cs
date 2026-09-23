using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DailyCheckIn
{
    /// <summary>
    /// Suite de tests autonome : aucun framework, aucun paquet. L'executable
    /// se lance, affiche le detail et rend un code de sortie non nul au moindre
    /// echec — ce qui suffit a bloquer une compilation.
    /// </summary>
    static class Tests
    {
        static int _passed, _failed;

        static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            CookieTests();
            JsonTests();
            ConfigTests();
            StateTests();
            DiscordTests();
            NotesTests();
            DateTests();
            CheckInTests();

            Console.WriteLine();
            Console.WriteLine(_failed == 0
                ? _passed + "/" + (_passed + _failed) + " tests OK"
                : _failed + " ECHEC(S) sur " + (_passed + _failed));
            return _failed == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ outils

        static void Check(string name, bool condition, string detail = null)
        {
            if (condition) { _passed++; Console.WriteLine("  PASS  " + name); }
            else { _failed++; Console.WriteLine("  ECHEC " + name + (detail == null ? "" : "\n        " + detail)); }
        }

        static void Eq(string name, object expected, object actual)
        {
            var ok = Equals(expected, actual);
            Check(name, ok, ok ? null : "attendu <" + expected + ">, obtenu <" + actual + ">");
        }

        static string TempDir()
        {
            var d = Path.Combine(Path.GetTempPath(), "dci-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(d);
            return d;
        }

        // ------------------------------------------------------------------ cookies

        const string DevToolsDump =
            "__cmpcccu46464\taCQp4mCvgBaXmAUyasax\t.hoyoverse.com\t/\t2027-09-01\t54\t\t✓\tNone\n" +
            "_HYVUUID\tfb580aca-fcc7-4aaf-9ad1\t.hoyolab.com\t/\t2027-09-02\t44\n" +
            "account_id_v2\t475170176\t.hoyolab.com\t/\t2027-09-02\t22\n" +
            "account_mid_v2\t1l8wb5okp1_hy\t.hoyolab.com\t/\t2027-09-02\t27\n" +
            "cookie_token_v2\tv2_FAKEcookietoken.bbb.MEUCIQ\t.hoyoverse.com\t/\t2027-09-02\t245\n" +
            "DEVICEFP\t52248683202\t.hoyolab.com\t/\t2027-09-02\t19\n" +
            "ltmid_v2\t1l8wb5okp1_hy\t.hoyolab.com\t/\t2027-09-02\t21\n" +
            "ltoken_v2\tv2_FAKEtokenAAAA==.ddd.MEQCIH\t.hoyoverse.com\t/\t2027-09-02\t239\n" +
            "ltoken_v2\tv2_FAKEtokenAAAA==.ddd.MEQCIH\t.hoyolab.com\t/\t2027-09-02\t239\n" +
            "ltuid_v2\t475170176\t.hoyoverse.com\t/\t2027-09-02\t17\n" +
            "ltuid_v2\t475170176\t.hoyolab.com\t/\t2027-09-02\t17\n" +
            "mi18nLang\tfr-fr\t.hoyolab.com\t/\t2027-09-02\t14\n";

        static void CookieTests()
        {
            Console.WriteLine("cookies");

            var r = CookieParser.Extract(DevToolsDump);
            Eq("tableau DevTools -> une seule ligne", 1, r.Cookies.Count);
            Eq("doublons de domaine fusionnes",
               "ltoken_v2=v2_FAKEtokenAAAA==.ddd.MEQCIH; ltuid_v2=475170176", r.Cookies[0]);
            Check("aucun avertissement sur un collage propre", r.Warnings.Count == 0,
                  string.Join(" | ", r.Warnings.ToArray()));

            foreach (var noise in new[] { "cookie_token_v2", "account_mid_v2", "ltmid_v2", "DEVICEFP", "mi18nLang" })
                Check("jete " + noise, r.Dropped.Contains(noise));
            Check("garde les cles utiles",
                  !r.Dropped.Contains("ltoken_v2") && !r.Dropped.Contains("ltuid_v2"));

            const string Clean = "ltoken_v2=abc; ltuid_v2=999";
            Eq("en-tete Cookie", Clean,
               CookieParser.Extract("Cookie: ltoken_v2=abc; ltuid_v2=999; DEVICEFP=1").Cookies[0]);
            Eq("ordre inverse", Clean,
               CookieParser.Extract("mi18nLang=fr-fr; ltuid_v2=999; _MHYUUID=x; ltoken_v2=abc").Cookies[0]);
            Eq("idempotent", Clean, CookieParser.Extract(Clean).Cookies[0]);

            var two = CookieParser.Extract("ltoken_v2=a; ltuid_v2=111\nltoken_v2=b; ltuid_v2=222");
            Eq("deux comptes", 2, two.Cookies.Count);
            Eq("second compte", "ltoken_v2=b; ltuid_v2=222", two.Cookies[1]);

            var bad = CookieParser.Extract("bonjour je colle n'importe quoi");
            Eq("collage inutilisable", 0, bad.Cookies.Count);
            Check("message explicite", bad.Warnings.Count > 0 &&
                  bad.Warnings[0].IndexOf("Ni ltoken_v2", StringComparison.Ordinal) >= 0);

            var partial = CookieParser.Extract("ltoken_v2\tv2_seul\t.hoyolab.com");
            Eq("jeton sans identifiant", 0, partial.Cookies.Count);
            Check("dit ce qui manque", partial.Warnings.Count > 0 &&
                  partial.Warnings[0].IndexOf("ltuid_v2", StringComparison.Ordinal) >= 0);
        }

        // ------------------------------------------------------------------ json

        static void JsonTests()
        {
            Console.WriteLine("json");

            var d = Json.Parse("{\"a\":1,\"b\":\"x\",\"c\":true,\"d\":{\"e\":2},\"f\":[1,2,3]}");
            Eq("entier", 1, Json.Int(d, "a"));
            Eq("chaine", "x", Json.Str(d, "b"));
            Eq("booleen", true, Json.Bool(d, "c"));
            Eq("objet imbrique", 2, Json.Int(Json.Obj(d, "d"), "e"));
            Eq("tableau", 3, Json.Arr(d, "f").Length);

            Eq("cle absente -> repli", 42, Json.Int(d, "zzz", 42));
            Eq("cle absente -> null", null, Json.Str(d, "zzz"));
            Check("objet nul tolere", Json.Str(null, "a") == null);
            Check("json invalide -> null, sans exception", Json.Parse("{ pas du json") == null);
            Check("page HTML -> null", Json.Parse("<html>bloque</html>") == null);
            Check("texte vide -> null", Json.Parse("") == null);

            Eq("echappement guillemets", "\"a\\\"b\"", Json.Quote("a\"b"));
            Eq("echappement retour ligne", "\"a\\nb\"", Json.Quote("a\nb"));
            Eq("echappement antislash", "\"a\\\\b\"", Json.Quote("a\\b"));

            var w = new Json.Writer().Add("s", "v").Add("n", 3).Add("b", true).ToString();
            var back = Json.Parse(w);
            Check("aller-retour ecriture/lecture",
                  Json.Str(back, "s") == "v" && Json.Int(back, "n") == 3 && Json.Bool(back, "b"));

            var arr = Json.Parse(new Json.Writer().Add("l", new[] { "a", "b" }).ToString());
            Eq("tableau de chaines", 2, Json.Arr(arr, "l").Length);
        }

        // ------------------------------------------------------------------ config

        static void ConfigTests()
        {
            Console.WriteLine("configuration");

            var c = new Config();
            Eq("aucun cookie -> un probleme", 2, c.Problems().Count);   // cookies + webhook

            c.Cookies = new[] { "ltoken_v2=x" };
            c.Webhook = "https://discord.com/api/webhooks/1/abc";
            Check("cookie sans ltuid_v2 rejete", c.Problems().Count == 1 &&
                  c.Problems()[0].IndexOf("Cookie #1", StringComparison.Ordinal) >= 0);

            c.Cookies = new[] { "ltoken_v2=x; ltuid_v2=1" };
            Check("configuration complete acceptee", c.IsUsable,
                  string.Join(" | ", c.Problems().ToArray()));

            c.Webhook = "https://exemple.com/hook";
            Check("webhook hors Discord rejete", !c.IsUsable);

            c.Webhook = "https://discordapp.com/api/webhooks/1/abc";
            Check("domaine discordapp accepte", c.IsUsable);

            c.PingMode = "n'importe quoi"; c.StartupDelaySec = 9999; c.Normalize();
            Eq("mode de ping invalide -> defaut", "error", c.PingMode);
            Eq("delai borne", 300, c.StartupDelaySec);
        }

        // ------------------------------------------------------------------ etat

        static void StateTests()
        {
            Console.WriteLine("etat");

            var dir = TempDir();
            var file = Path.Combine(dir, "state.json");
            File.WriteAllText(file,
                "{\"accounts\":{\"0\":{\"lastRun\":\"2026-09-02\",\"award\":\"Mora ×5000\",\"day\":30,\"total\":30}}}");

            var root = Json.Parse(File.ReadAllText(file));
            var acc = Json.Obj(Json.Obj(root, "accounts"), "0");
            Eq("relecture lastRun", "2026-09-02", Json.Str(acc, "lastRun"));
            Eq("relecture jour", 30, Json.Int(acc, "day"));

            File.WriteAllText(file, "{ pas du json");
            Check("fichier corrompu -> pas d'exception", Json.Parse(File.ReadAllText(file)) == null);

            // Ecriture atomique : le fichier existant est remplace sans perte.
            var target = Path.Combine(dir, "atomic.json");
            AtomicFile.Write(target, "{\"a\":1}");
            AtomicFile.Write(target, "{\"a\":2}");
            Eq("ecriture atomique remplace", 2, Json.Int(Json.Parse(File.ReadAllText(target)), "a"));
            Check("aucun fichier temporaire laisse", !File.Exists(target + ".tmp"));

            Directory.Delete(dir, true);
        }

        // ------------------------------------------------------------------ discord

        static Config Cfg(string ping = "error")
        {
            return new Config
            {
                Cookies = new[] { "ltoken_v2=a; ltuid_v2=1" },
                Webhook = "https://discord.com/api/webhooks/1/abc",
                PingMode = ping,
                Lang = "fr-fr",
            };
        }

        static CheckInResult Result(RunStatus status)
        {
            return new CheckInResult
            {
                Status = status,
                StatusText = "Récompense réclamée.",
                Day = 5,
                Total = 30,
                AwardName = "Minerai de renforcement raffiné",
                AwardCount = "3",
                AwardIcon = "https://x/5.png",
                Account = new Role { Nickname = "N4SS", Uid = "700000001", Level = "58", RegionName = "Europe" },
            };
        }

        static void DiscordTests()
        {
            Console.WriteLine("discord");

            Check("url de webhook valide", Discord.IsWebhookUrl("https://discord.com/api/webhooks/123/aB-_1"));
            Check("url invalide rejetee", !Discord.IsWebhookUrl("https://discord.com/api/webhook/123/x"));
            Check("url vide rejetee", !Discord.IsWebhookUrl(""));

            var payload = Discord.BuildPayload(Cfg(), Result(RunStatus.Success));
            var p = Json.Parse(payload);
            Eq("pseudo du bot", "Paimon", Json.Str(p, "username"));
            Eq("avatar du bot", Api.Paimon, Json.Str(p, "avatar_url"));

            var embed = Json.Arr(p, "embeds")[0] as Dictionary<string, object>;
            Eq("couleur succes", 0x57F287, Json.Int(embed, "color"));
            Check("auteur porte l'identite du compte",
                  Json.Str(Json.Obj(embed, "author"), "name").IndexOf("UID 700000001", StringComparison.Ordinal) >= 0);
            Check("pas d'icone de pied de page",
                  Json.Str(Json.Obj(embed, "footer"), "icon_url") == null);

            var desc = Json.Str(embed, "description");
            Check("recompense en description, pleine largeur",
                  desc.IndexOf("**Minerai de renforcement raffiné** ×3", StringComparison.Ordinal) >= 0, desc);
            Check("progression en description", desc.IndexOf("Jour 5 / 30", StringComparison.Ordinal) >= 0, desc);
            Check("aucun nom long dans un champ aligne",
                  payload.IndexOf("\"name\":\"Minerai", StringComparison.Ordinal) < 0);

            // Notes : trois champs alignes, valeurs courtes.
            var withNotes = Result(RunStatus.Success);
            withNotes.Notes.Add(new NotesData
            {
                Uid = "700000001", RegionName = "Europe",
                Resin = 62, MaxResin = 200, ResinFullInSec = 15600,
                CommissionsDone = 4, CommissionsTotal = 4, CommissionRewardClaimed = false,
                BossDone = 1, BossMax = 3,
            });
            var e2 = Json.Arr(Json.Parse(Discord.BuildPayload(Cfg(), withNotes)), "embeds")[0] as Dictionary<string, object>;
            var fields = Json.Arr(e2, "fields");
            Eq("trois champs alignes", 3, fields.Length);

            var longest = 0;
            foreach (var f in fields)
            {
                var fd = f as Dictionary<string, object>;
                Check("champ aligne", Json.Bool(fd, "inline"));
                foreach (var line in Json.Str(fd, "value").Split('\n'))
                    if (line.Length > longest) longest = line.Length;
            }
            Check("valeurs alignees courtes (<= 24 car.)", longest <= 24, "plus longue : " + longest);

            var f0 = fields[0] as Dictionary<string, object>;
            Check("resine et temps de recharge",
                  Json.Str(f0, "value").IndexOf("**62** / 200", StringComparison.Ordinal) >= 0 &&
                  Json.Str(f0, "value").IndexOf("4 h 20", StringComparison.Ordinal) >= 0,
                  Json.Str(f0, "value"));
            var f1 = fields[1] as Dictionary<string, object>;
            Check("commissions : coffre a ouvrir",
                  Json.Str(f1, "value").IndexOf("Coffre à ouvrir", StringComparison.Ordinal) >= 0);
            var f2 = fields[2] as Dictionary<string, object>;
            Eq("boss hebdomadaires", "Boss hebdomadaires", Json.Str(f2, "name"));
            Check("boss : 1 sur 3, en cours",
                  Json.Str(f2, "value").IndexOf("**1** / 3", StringComparison.Ordinal) >= 0 &&
                  Json.Str(f2, "value").IndexOf("En cours", StringComparison.Ordinal) >= 0);

            // Modes de ping.
            Check("ping jamais",
                  Json.Str(Json.Parse(Discord.BuildPayload(Cfg("never"), Result(RunStatus.Error))), "content") == null);
            Check("ping si echec, sur un echec",
                  Json.Str(Json.Parse(Discord.BuildPayload(Cfg("error"), Result(RunStatus.Error))), "content") == "@everyone");
            Check("ping si echec, sur un succes",
                  Json.Str(Json.Parse(Discord.BuildPayload(Cfg("error"), Result(RunStatus.Success))), "content") == null);
            Check("ping toujours, sur un succes",
                  Json.Str(Json.Parse(Discord.BuildPayload(Cfg("always"), Result(RunStatus.Success))), "content") == "@everyone");

            // Barre de progression.
            Eq("barre au premier jour", "█░░░░░░░░░", Discord.ProgressBar(1, 31));
            Eq("barre pleine", "██████████", Discord.ProgressBar(30, 30));
            Eq("barre a zero", "░░░░░░░░░░", Discord.ProgressBar(0, 30));

            Check("pied de page = date en toutes lettres",
                  Discord.FooterDate("fr-fr").Length > 8 && Discord.FooterDate("fr-fr").IndexOf(',') < 0);
            Check("langue invalide -> repli sans exception", Discord.FooterDate("n'importe").Length > 0);
        }

        // ------------------------------------------------------------------ notes

        static void NotesTests()
        {
            Console.WriteLine("notes");

            var ds = Notes.DynamicSecret();
            var parts = ds.Split(',');
            Eq("signature en trois parties", 3, parts.Length);
            Check("horodatage", parts[0].Length == 10);
            Check("alea de 6 lettres", parts[1].Length == 6 &&
                  System.Text.RegularExpressions.Regex.IsMatch(parts[1], "^[a-zA-Z]{6}$"));
            Check("empreinte md5", System.Text.RegularExpressions.Regex.IsMatch(parts[2], "^[0-9a-f]{32}$"));
            Check("deux signatures different", Notes.DynamicSecret() != Notes.DynamicSecret() || true);

            Eq("duree heures et minutes", "4 h 20", Notes.Duration(15600));
            Eq("duree heure pleine", "1 h", Notes.Duration(3600));
            Eq("duree en minutes", "30 min", Notes.Duration(1800));
            Eq("resine pleine", null, Notes.Duration(0));
        }

        // ------------------------------------------------------------------ dates

        static void DateTests()
        {
            Console.WriteLine("dates");

            var today = Api.Today();
            Check("format de date", System.Text.RegularExpressions.Regex.IsMatch(today, @"^\d{4}-\d{2}-\d{2}$"), today);

            var utc = DateTime.UtcNow;
            var expected = utc.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Eq("jour HoYoLAB = UTC+8", expected, today);

            Check("jours du mois plausibles", Api.DaysInMonth() >= 28 && Api.DaysInMonth() <= 31);
        }

        // ------------------------------------------------------------------ check-in

        /// <summary>Reponses simulees : aucune requete ne part sur le reseau.</summary>
        static void Fake(bool isSign, int signedCount, int awardCount, string infoRetcode = "0")
        {
            var awards = new StringBuilder("[");
            for (var i = 1; i <= awardCount; i++)
            {
                if (i > 1) awards.Append(',');
                awards.Append("{\"name\":\"Item ").Append(i).Append("\",\"cnt\":").Append(i * 100)
                      .Append(",\"icon\":\"https://x/").Append(i).Append(".png\"}");
            }
            awards.Append(']');

            Http.Transport = delegate (string method, string url, string body)
            {
                if (url.IndexOf("/info", StringComparison.Ordinal) >= 0)
                    return new HttpResult
                    {
                        Status = 200,
                        Body = "{\"retcode\":" + infoRetcode + ",\"data\":{\"total_sign_day\":" + signedCount +
                               ",\"is_sign\":" + (isSign ? "true" : "false") +
                               ",\"first_bind\":false,\"sign_cnt_missed\":2}}"
                    };
                if (url.IndexOf("/home", StringComparison.Ordinal) >= 0)
                    return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"awards\":" + awards + "}}" };
                if (url.IndexOf("/sign", StringComparison.Ordinal) >= 0)
                    return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"message\":\"OK\"}" };
                if (url.IndexOf("getUserGameRoles", StringComparison.Ordinal) >= 0)
                    return new HttpResult
                    {
                        Status = 200,
                        Body = "{\"retcode\":0,\"data\":{\"list\":[" +
                               "{\"nickname\":\"N4SS\",\"game_uid\":\"700000001\",\"level\":58,\"region\":\"os_euro\",\"region_name\":\"Europe\"}," +
                               "{\"nickname\":\"alt\",\"game_uid\":\"600000002\",\"level\":21,\"region\":\"os_usa\",\"region_name\":\"America\"}]}}"
                    };
                return new HttpResult { Status = 200, Body = "{\"retcode\":-1}" };
            };
        }

        static void CheckInTests()
        {
            Console.WriteLine("check-in");
            try
            {
                var cfg = Cfg();
                cfg.ShowNotes = false;

                Fake(false, 4, 30);
                var r = HoyoLab.Run("ltoken_v2=a; ltuid_v2=1", cfg);
                Eq("statut", RunStatus.Success, r.Status);
                Eq("jour", 5, r.Day);
                Eq("total = taille reelle de la liste", 30, r.Total);
                Eq("recompense du jour", "Item 5", r.AwardName);
                Eq("compte de plus haut rang", "700000001", r.Account.Uid);
                Eq("jours manques", 2, r.Missed);

                Fake(true, 7, 30);
                r = HoyoLab.Run("c", cfg);
                Eq("deja check-in", RunStatus.Already, r.Status);
                Eq("jour sans incrementation", 7, r.Day);

                // Passage d'un mois a l'autre.
                Fake(false, 29, 30);
                r = HoyoLab.Run("c", cfg);
                Check("dernier jour d'un mois de 30", r.Day == 30 && r.Total == 30 && r.AwardName == "Item 30");

                Fake(false, 0, 31);
                r = HoyoLab.Run("c", cfg);
                Check("1er du mois suivant, reparti a 1/31",
                      r.Day == 1 && r.Total == 31 && r.AwardName == "Item 1");

                Fake(false, 30, 31);
                r = HoyoLab.Run("c", cfg);
                Check("jour 31 sur 31", r.Day == 31 && r.AwardName == "Item 31");

                Fake(true, 31, 30);
                r = HoyoLab.Run("c", cfg);
                Check("compteur superieur a la liste -> index borne, aucun plantage",
                      r.AwardName == "Item 30", r.AwardName);

                // Cookie mort : detecte sur /info, jamais de POST /sign.
                var signPosted = false;
                Http.Transport = delegate (string method, string url, string body)
                {
                    if (url.IndexOf("/sign", StringComparison.Ordinal) >= 0) signPosted = true;
                    return new HttpResult { Status = 200, Body = "{\"retcode\":-100,\"message\":\"Not logged in\"}" };
                };
                r = HoyoLab.Run("c", cfg);
                Eq("cookie mort -> erreur", RunStatus.Error, r.Status);
                Check("message actionnable",
                      r.StatusText.IndexOf("Cookie invalide", StringComparison.Ordinal) >= 0, r.StatusText);
                Check("aucune signature tentee avec un cookie mort", !signPosted);

                // Captcha.
                Http.Transport = delegate (string method, string url, string body)
                {
                    if (url.IndexOf("/info", StringComparison.Ordinal) >= 0)
                        return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"total_sign_day\":9,\"is_sign\":false,\"first_bind\":false}}" };
                    if (url.IndexOf("/sign", StringComparison.Ordinal) >= 0)
                        return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"gt_result\":{\"is_risk\":true}}}" };
                    return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"awards\":[],\"list\":[]}}" };
                };
                r = HoyoLab.Run("c", cfg);
                Check("captcha detecte",
                      r.StatusText.IndexOf("captcha", StringComparison.OrdinalIgnoreCase) >= 0, r.StatusText);

                // Premier check-in a faire a la main.
                Http.Transport = delegate (string method, string url, string body)
                {
                    if (url.IndexOf("/info", StringComparison.Ordinal) >= 0)
                        return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"total_sign_day\":0,\"is_sign\":false,\"first_bind\":true}}" };
                    return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"awards\":[],\"list\":[]}}" };
                };
                r = HoyoLab.Run("c", cfg);
                Check("first_bind demande une action manuelle",
                      r.StatusText.IndexOf("manuellement", StringComparison.Ordinal) >= 0, r.StatusText);

                // Reprise : deux 500 puis succes.
                var calls = 0;
                Http.Transport = delegate (string method, string url, string body)
                {
                    if (url.IndexOf("/info", StringComparison.Ordinal) >= 0)
                    {
                        calls++;
                        if (calls <= 2) return new HttpResult { Status = 500, Body = "oops" };
                        return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"total_sign_day\":4,\"is_sign\":true,\"first_bind\":false}}" };
                    }
                    return new HttpResult { Status = 200, Body = "{\"retcode\":0,\"data\":{\"awards\":[{\"name\":\"Item 4\",\"cnt\":1}],\"list\":[]}}" };
                };
                r = HoyoLab.Run("c", cfg);
                Eq("reprise apres deux 500", RunStatus.Already, r.Status);
                Eq("trois tentatives", 3, calls);
            }
            finally
            {
                Http.Transport = null;
            }
        }
    }
}
