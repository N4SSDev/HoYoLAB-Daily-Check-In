using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DailyCheckIn
{
    /// <summary>Resine, commissions et boss hebdomadaires d'un compte de jeu.</summary>
    sealed class NotesData
    {
        public string Uid = "";
        public string RegionName = "";
        public int Resin, MaxResin, ResinFullInSec;
        public int CommissionsDone, CommissionsTotal;
        public bool CommissionRewardClaimed;
        public int BossDone, BossMax;
    }

    /// <summary>
    /// API « Notes en temps reel » du Battle Chronicle.
    ///
    /// Contrairement au check-in, elle exige une signature `ds` regeneree a
    /// chaque appel, et que l'utilisateur ait rendu ses notes publiques. Tout
    /// echec ici est avale : c'est un supplement d'information, jamais une
    /// raison de compromettre le check-in.
    /// </summary>
    static class Notes
    {
        const string Salt = "6s25p5ox5y14umn1p61aqyyvbvvl3lrt";
        const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        static readonly Dictionary<string, string> Retcodes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "-100",  "cookie invalide" },
            { "10001", "cookie invalide" },
            { "10102", "Notes en temps réel désactivées (HoYoLAB > Paramètres > Confidentialité)" },
            { "10103", "ce compte de jeu n'est pas lié à HoYoLAB" },
            { "1034",  "HoYoLAB demande une vérification" },
        };

        /// <summary>
        /// Signature dynamique attendue par l'API : md5("salt=..&t=..&r=..").
        /// </summary>
        public static string DynamicSecret()
        {
            var t = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            var r = new char[6];
            var bytes = new byte[6];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            for (var i = 0; i < 6; i++) r[i] = Letters[bytes[i] % Letters.Length];
            var rs = new string(r);

            var payload = "salt=" + Salt + "&t=" + t.ToString(CultureInfo.InvariantCulture) + "&r=" + rs;
            string hash;
            using (var md5 = MD5.Create())
            {
                var digest = md5.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(32);
                foreach (var b in digest) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                hash = sb.ToString();
            }
            return t.ToString(CultureInfo.InvariantCulture) + "," + rs + "," + hash;
        }

        /// <summary>
        /// Notes de plusieurs comptes, en parallele : les serveurs sont
        /// independants, les enchainer paierait la latence autant de fois.
        /// </summary>
        public static List<NotesData> FetchAll(string cookie, List<Role> roles, string lang)
        {
            var tasks = new List<Task<NotesData>>(roles.Count);
            foreach (var role in roles)
            {
                var r = role;
                tasks.Add(Task.Run(() => Fetch(cookie, r, lang)));
            }

            var result = new List<NotesData>(roles.Count);
            foreach (var t in tasks)
            {
                NotesData n = null;
                try { n = t.Result; } catch { }
                if (n != null) result.Add(n);
            }
            return result;
        }

        static NotesData Fetch(string cookie, Role role, string lang)
        {
            try
            {
                var url = Api.NotesUrl + "?server=" + Uri.EscapeDataString(role.Server)
                        + "&role_id=" + Uri.EscapeDataString(role.Uid);
                var res = Http.Get(url, cookie, lang, DynamicSecret());
                var root = Json.Parse(res.Body);
                var code = Json.Str(root, "retcode", "?");

                if (code != "0")
                {
                    string why;
                    if (!Retcodes.TryGetValue(code, out why)) why = "retcode " + code;
                    Log.Write("résine indisponible (UID " + role.Uid + ") : " + why);
                    return null;
                }

                var d = Json.Obj(root, "data");
                if (d == null) return null;

                // L'API compte les remises de resine *restantes* sur les boss
                // hebdomadaires. Ce qui interesse, c'est combien sont faits.
                var bossMax = Json.Int(d, "resin_discount_num_limit");
                var bossLeft = Json.Int(d, "remain_resin_discount_num");

                return new NotesData
                {
                    Uid = role.Uid,
                    RegionName = role.RegionName,
                    Resin = Json.Int(d, "current_resin"),
                    MaxResin = Json.Int(d, "max_resin"),
                    ResinFullInSec = Json.Int(d, "resin_recovery_time"),
                    CommissionsDone = Json.Int(d, "finished_task_num"),
                    CommissionsTotal = Json.Int(d, "total_task_num"),
                    CommissionRewardClaimed = Json.Bool(d, "is_extra_task_reward_received"),
                    BossDone = Math.Max(0, bossMax - bossLeft),
                    BossMax = bossMax,
                };
            }
            catch (Exception ex)
            {
                Log.Write("résine indisponible (UID " + role.Uid + ") : " + Log.Describe(ex));
                return null;
            }
        }

        /// <summary>« 4 h 20 », « 38 min », ou null si deja plein.</summary>
        public static string Duration(int seconds)
        {
            if (seconds <= 0) return null;
            var h = seconds / 3600;
            var m = (int)Math.Round((seconds % 3600) / 60.0);
            if (h == 0) return m + " min";
            return m == 0 ? h + " h" : h + " h " + m.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
