using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DailyCheckIn
{
    static class Api
    {
        public const string ActId = "e202102251931481";
        public const string SolBase = "https://sg-hk4e-api.hoyolab.com/event/sol";
        public const string RolesUrl = "https://sg-public-api.hoyolab.com/binding/api/getUserGameRolesByCookie";
        public const string NotesUrl = "https://sg-public-api.hoyolab.com/event/game_record/genshin/api/dailyNote";
        public const string SignInPage = "https://act.hoyolab.com/ys/event/signin-sea-v3/index.html?act_id=" + ActId;
        public const string GameBiz = "hk4e_global";
        public const string GameName = "Genshin Impact";

        /// <summary>Avatar Paimon : icone de l'app et image du bot Discord.</summary>
        public const string Paimon = "https://img-os-static.hoyolab.com/communityWeb/upload/1d7dd8f33c5ccdfdeac86e1e86ddd652.png";

        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/126.0.0.0 Safari/537.36";

        /// <summary>Date du jour cote HoYoLAB : la remise a zero a lieu a 00h00 UTC+8.</summary>
        public static string Today()
        {
            return DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Repli si /home ne repond pas : nombre de jours du mois en cours.</summary>
        public static int DaysInMonth()
        {
            var d = DateTime.UtcNow.AddHours(8);
            return DateTime.DaysInMonth(d.Year, d.Month);
        }
    }

    enum RunStatus { Success, Already, Warning, Error }

    /// <summary>Un compte de jeu lie au cookie.</summary>
    sealed class Role
    {
        public string Nickname = "?";
        public string Uid = "?";
        public string Level = "?";
        public string Server = "";
        public string RegionName = "?";
    }

    sealed class CheckInResult
    {
        public RunStatus Status = RunStatus.Error;
        public string StatusText = "";
        public int Day;                 // 0 = inconnu
        public int Total;
        public string AwardName;
        public string AwardCount;
        public string AwardIcon;
        public Role Account;
        public List<Role> Roles = new List<Role>();
        public int Missed;
        public List<NotesData> Notes = new List<NotesData>();

        /// <summary>Vrai si l'embed a ete envoye pour ce resultat.</summary>
        public bool Notified;

        public string AwardLabel
        {
            get { return AwardName == null ? null : AwardName + " ×" + AwardCount; }
        }
    }

    static class HoyoLab
    {
        /// <summary>retcode -> (succes ?, deja fait ?, message).</summary>
        static readonly Dictionary<string, Tuple<bool, bool, string>> Retcodes =
            new Dictionary<string, Tuple<bool, bool, string>>(StringComparer.Ordinal)
        {
            { "0",       Tuple.Create(true,  false, "Récompense réclamée.") },
            { "-5003",   Tuple.Create(true,  true,  "Déjà check-in aujourd'hui.") },
            { "-100",    Tuple.Create(false, false, "Cookie invalide ou expiré (non connecté). Regénère le cookie HoYoLAB.") },
            { "10001",   Tuple.Create(false, false, "Cookie invalide ou expiré. Regénère le cookie HoYoLAB.") },
            { "-10002",  Tuple.Create(false, false, "Aucun compte Genshin lié à ce compte HoYoLAB.") },
            { "-500012", Tuple.Create(false, false, "Requête refusée par HoYoLAB (anti-bot). Réessaie plus tard.") },
            { "-999",    Tuple.Create(false, false, "Captcha requis. Fais un check-in manuel sur le site.") },
        };

        /// <summary>Fait le check-in d'un compte HoYoLAB.</summary>
        public static CheckInResult Run(string cookie, Config cfg)
        {
            var qs = "?lang=" + Uri.EscapeDataString(cfg.Lang) + "&act_id=" + Api.ActId;

            // Les trois lectures sont independantes : les enchainer paierait
            // trois fois la latence. Lancees ensemble, on n'en paie qu'une.
            var rolesTask = Task.Run(() => FetchRoles(cookie, cfg.Lang));
            var infoTask = Task.Run(() => Http.Get(Api.SolBase + "/info" + qs, cookie));
            var homeTask = Task.Run(() => Http.Get(Api.SolBase + "/home" + qs, cookie));

            var result = new CheckInResult();
            result.Roles = Safe(rolesTask);
            result.Account = Primary(result.Roles);

            Dictionary<string, object> info;
            try
            {
                info = Json.Parse(infoTask.Result.Body);
            }
            catch (Exception ex)
            {
                return Fail(result, "Lecture du statut impossible : " + Log.Describe(ex));
            }

            var retcode = Json.Str(info, "retcode", "?");
            if (retcode != "0")
            {
                // /info sert de sonde : un cookie mort est vu avant tout POST.
                return Fail(result, Explain(retcode, Json.Str(info, "message")));
            }

            var data = Json.Obj(info, "data") ?? new Dictionary<string, object>();
            var awards = AwardsOf(homeTask);
            result.Total = awards.Length > 0 ? awards.Length : Api.DaysInMonth();
            result.Missed = Json.Int(data, "sign_cnt_missed");

            if (Json.Bool(data, "first_bind"))
            {
                return Fail(result, "Premier check-in à faire manuellement sur act.hoyolab.com, "
                                  + "ensuite l'app prend le relais.");
            }

            var signed = Json.Int(data, "total_sign_day");

            if (Json.Bool(data, "is_sign"))
            {
                result.Status = RunStatus.Already;
                result.StatusText = "Déjà check-in aujourd'hui, récompense déjà reçue.";
            }
            else
            {
                var body = "{\"act_id\":\"" + Api.ActId + "\",\"lang\":\"" + cfg.Lang + "\"}";
                var res = Http.PostJson(Api.SolBase + "/sign" + qs, cookie, body);
                var signJson = Json.Parse(res.Body) ?? new Dictionary<string, object>();

                var risk = Json.Obj(signJson, "data");
                var gt = risk == null ? null : Json.Obj(risk, "gt_result");
                if (gt != null && Json.Bool(gt, "is_risk"))
                {
                    return Fail(result, "HoYoLAB demande un captcha. Fais un check-in manuel sur le site puis relance.");
                }

                var code = Json.Str(signJson, "retcode", "?");
                Tuple<bool, bool, string> known;
                if (!Retcodes.TryGetValue(code, out known) || !known.Item1)
                {
                    return Fail(result, Explain(code, Json.Str(signJson, "message")));
                }

                result.Status = known.Item2 ? RunStatus.Already : RunStatus.Success;
                result.StatusText = known.Item3;
                // /info datait d'avant la signature : on incremente nous-memes.
                if (!known.Item2) signed++;
            }

            result.Day = signed;
            if (signed > 0 && awards.Length > 0)
            {
                var idx = Math.Min(Math.Max(signed - 1, 0), awards.Length - 1);
                var a = awards[idx] as Dictionary<string, object>;
                if (a != null)
                {
                    result.AwardName = Json.Str(a, "name", "Récompense inconnue");
                    result.AwardCount = Json.Str(a, "cnt", "?");
                    result.AwardIcon = Json.Str(a, "icon", "");
                }
            }
            if (result.AwardName == null) result.Status = RunStatus.Warning;
            return result;
        }

        static object[] AwardsOf(Task<HttpResult> home)
        {
            try
            {
                var d = Json.Obj(Json.Parse(home.Result.Body), "data");
                return Json.Arr(d, "awards") ?? new object[0];
            }
            catch
            {
                // La liste des recompenses n'est que cosmetique : son absence
                // ne doit pas empecher la reclamation.
                return new object[0];
            }
        }

        static List<Role> Safe(Task<List<Role>> t)
        {
            try { return t.Result; } catch { return new List<Role>(); }
        }

        /// <summary>Comptes de jeu lies au cookie. Purement decoratif.</summary>
        static List<Role> FetchRoles(string cookie, string lang)
        {
            var list = new List<Role>();
            try
            {
                var url = Api.RolesUrl + "?game_biz=" + Api.GameBiz + "&lang=" + Uri.EscapeDataString(lang);
                var arr = Json.Arr(Json.Obj(Json.Parse(Http.Get(url, cookie).Body), "data"), "list");
                if (arr == null) return list;

                foreach (var o in arr)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    list.Add(new Role
                    {
                        Nickname = Json.Str(d, "nickname", "?"),
                        Uid = Json.Str(d, "game_uid", "?"),
                        Level = Json.Str(d, "level", "?"),
                        Server = Json.Str(d, "region", ""),
                        RegionName = Json.Str(d, "region_name", null) ?? Json.Str(d, "region", "?"),
                    });
                }
            }
            catch
            {
                // Sans consequence : l'embed s'affichera sans l'identite du compte.
            }
            return list;
        }

        /// <summary>Le compte principal : celui de plus haut rang d'aventure.</summary>
        public static Role Primary(List<Role> roles)
        {
            Role best = null;
            var bestLevel = -1;
            foreach (var r in roles)
            {
                int lvl;
                if (!int.TryParse(r.Level, NumberStyles.Integer, CultureInfo.InvariantCulture, out lvl)) lvl = 0;
                if (lvl > bestLevel) { bestLevel = lvl; best = r; }
            }
            return best;
        }

        static string Explain(string retcode, string message)
        {
            Tuple<bool, bool, string> known;
            var text = Retcodes.TryGetValue(retcode ?? "", out known) && !known.Item1
                ? known.Item3
                : "HoYoLAB a refusé la requête";
            var detail = string.IsNullOrEmpty(message) ? "" : " - " + message;
            return text + " (retcode " + retcode + detail + ")";
        }

        static CheckInResult Fail(CheckInResult r, string text)
        {
            r.Status = RunStatus.Error;
            r.StatusText = text;
            r.AwardName = null;
            r.Day = 0;
            return r;
        }
    }
}
