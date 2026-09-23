using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DailyCheckIn
{
    sealed class CookieParseResult
    {
        public List<string> Cookies = new List<string>();
        public List<string> Dropped = new List<string>();
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Extraction des deux seuls cookies utiles depuis un collage brut.
    ///
    /// Le check-in n'a besoin que de ltoken_v2 (le jeton) et ltuid_v2
    /// (l'identifiant). Tout le reste — cookie_token_v2, account_mid_v2,
    /// DEVICEFP, consentement, langue — est du bruit qu'on jette : on ne
    /// conserve sur disque que le strict minimum.
    ///
    /// Formats acceptes : tableau DevTools colle tel quel (tabulations),
    /// en-tete « Cookie: … », sortie de document.cookie, lignes cle=valeur.
    /// </summary>
    static class CookieParser
    {
        static readonly Regex Pair = new Regex(
            @"(?:^|[\s;,])(ltoken_v2|ltuid_v2)[=\t: ]+([^\s;,]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        static readonly Regex AnyName = new Regex(
            @"(?:^|[\n;])\s*([A-Za-z_][\w.\-]*)\s*[=\t]",
            RegexOptions.Compiled | RegexOptions.Multiline);

        static readonly Regex Uid = new Regex(@"^\d{4,}$", RegexOptions.Compiled);

        public static CookieParseResult Extract(string raw)
        {
            var result = new CookieParseResult();
            var text = (raw ?? "").Replace("\r\n", "\n").Replace('\r', '\n');

            // Un compte par ligne : c'est le seul appariement fiable, et le cas
            // d'un en-tete Cookie ou de plusieurs comptes colles a la suite.
            var accounts = new List<KeyValuePair<string, string>>();
            var seenUids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var line in text.Split('\n'))
            {
                string token, uid;
                PairsIn(line, out token, out uid);
                if (token != null && uid != null && seenUids.Add(uid))
                    accounts.Add(new KeyValuePair<string, string>(uid, token));
            }

            // Sinon, tableau DevTools : une cle par ligne, on regroupe sur le tout.
            if (accounts.Count == 0)
            {
                var tokens = new List<string>();
                var uids = new List<string>();
                foreach (Match m in Pair.Matches(text))
                {
                    var key = m.Groups[1].Value.ToLowerInvariant();
                    var value = m.Groups[2].Value;
                    var bucket = key == "ltoken_v2" ? tokens : uids;
                    if (!bucket.Contains(value)) bucket.Add(value);
                }

                var usable = uids.FindAll(u => Uid.IsMatch(u));
                if (usable.Count != uids.Count)
                    result.Warnings.Add("Une valeur ltuid_v2 non numérique a été ignorée.");

                if (tokens.Count > 1 && usable.Count > 1)
                {
                    result.Warnings.Add(tokens.Count + " jetons et " + usable.Count
                        + " identifiants trouvés : ils ont été appariés dans l'ordre d'apparition. "
                        + "Vérifie le résultat, ou colle un compte à la fois.");
                }

                var n = Math.Min(tokens.Count, usable.Count);
                for (var i = 0; i < n; i++)
                    accounts.Add(new KeyValuePair<string, string>(usable[i], tokens[i]));

                if (tokens.Count > 0 && usable.Count == 0)
                    result.Warnings.Add("ltuid_v2 introuvable dans le collage.");
                else if (tokens.Count == 0 && usable.Count > 0)
                    result.Warnings.Add("ltoken_v2 introuvable dans le collage.");
                else if (tokens.Count == 0 && usable.Count == 0)
                    result.Warnings.Add("Ni ltoken_v2 ni ltuid_v2 dans ce collage. "
                        + "Vérifie que tu copies bien les cookies de hoyolab.com.");
            }

            foreach (var a in accounts)
                result.Cookies.Add("ltoken_v2=" + a.Value + "; ltuid_v2=" + a.Key);

            // Inventaire de ce qui est jete, pour pouvoir le dire.
            var dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in AnyName.Matches(text)) Keep(dropped, m.Groups[1].Value);
            foreach (var line in text.Split('\n'))
            {
                var first = line.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (first.Length > 0 && Regex.IsMatch(first[0], @"^[A-Za-z_][\w.\-]*$")) Keep(dropped, first[0]);
            }
            result.Dropped.AddRange(dropped);
            return result;
        }

        static void Keep(HashSet<string> set, string name)
        {
            if (!name.Equals("ltoken_v2", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("ltuid_v2", StringComparison.OrdinalIgnoreCase))
                set.Add(name);
        }

        static void PairsIn(string text, out string token, out string uid)
        {
            token = null; uid = null;
            foreach (Match m in Pair.Matches(text))
            {
                var key = m.Groups[1].Value.ToLowerInvariant();
                if (key == "ltoken_v2") { if (token == null) token = m.Groups[2].Value; }
                else if (uid == null) uid = m.Groups[2].Value;
            }
        }
    }
}
