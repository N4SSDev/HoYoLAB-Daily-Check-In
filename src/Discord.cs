using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DailyCheckIn
{
    /// <summary>
    /// Envoi de l'embed sur un webhook Discord.
    ///
    /// Mise en page : Discord place trois champs `inline` par ligne, soit un
    /// tiers de largeur chacun. Tout ce qui peut etre long — nom de recompense,
    /// message d'erreur, liste de serveurs — va donc en description ou en champ
    /// pleine largeur ; les champs alignes ne recoivent que des valeurs courtes
    /// et calibrees.
    /// </summary>
    static class Discord
    {
        const int ColorSuccess = 0x57F287;
        const int ColorAlready = 0x5865F2;
        const int ColorWarning = 0xFEE75C;
        const int ColorError = 0xED4245;

        static readonly Regex WebhookRe = new Regex(
            @"^https://(discord|discordapp)\.com/api/webhooks/\d+/[\w-]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsWebhookUrl(string url)
        {
            return !string.IsNullOrEmpty(url) && WebhookRe.IsMatch(url.Trim());
        }

        /// <summary>Poste l'embed correspondant a un resultat de check-in.</summary>
        public static void Post(Config cfg, CheckInResult r)
        {
            Send(cfg.Webhook, BuildPayload(cfg, r));
        }

        /// <summary>
        /// Embed de test. Il applique le meme reglage de ping que les vrais
        /// messages : c'est le seul moyen de verifier que le webhook a le droit
        /// de mentionner @everyone.
        /// </summary>
        public static void SendTest(string webhook, string pingMode, string lang)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"username\":\"Paimon\",\"avatar_url\":").Append(Json.Quote(Api.Paimon)).Append(',');
            sb.Append(Mention(pingMode, false)).Append(',');
            sb.Append("\"embeds\":[{");
            sb.Append("\"author\":{\"name\":\"Paimon\",\"icon_url\":").Append(Json.Quote(Api.Paimon)).Append("},");
            sb.Append("\"title\":").Append(Json.Quote(Api.GameName + " — test du webhook")).Append(',');
            sb.Append("\"description\":").Append(Json.Quote("Si tu vois ce message, le webhook est bon.")).Append(',');
            sb.Append("\"color\":").Append(ColorSuccess).Append(',');
            sb.Append("\"footer\":{\"text\":").Append(Json.Quote(FooterDate(lang))).Append("},");
            sb.Append("\"timestamp\":").Append(Json.Quote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
            sb.Append("}]}");
            Send(webhook, sb.ToString());
        }

        // ------------------------------------------------------------------ payload

        public static string BuildPayload(Config cfg, CheckInResult r)
        {
            var sb = new StringBuilder(1024);
            sb.Append("{\"username\":\"Paimon\",\"avatar_url\":").Append(Json.Quote(Api.Paimon)).Append(',');
            sb.Append(Mention(cfg.PingMode, r.Status == RunStatus.Error)).Append(',');
            sb.Append("\"embeds\":[{");

            // Auteur : l'identite du compte, la ou elle ne casse aucune colonne.
            var author = r.Account == null
                ? "Paimon"
                : r.Account.Nickname + " · UID " + r.Account.Uid + " · AR " + r.Account.Level;
            sb.Append("\"author\":{\"name\":").Append(Json.Quote(author))
              .Append(",\"icon_url\":").Append(Json.Quote(Api.Paimon)).Append("},");

            sb.Append("\"title\":").Append(Json.Quote(Api.GameName + " — Check-in quotidien")).Append(',');
            sb.Append("\"url\":").Append(Json.Quote(Api.SignInPage)).Append(',');
            sb.Append("\"description\":").Append(Json.Quote(Description(r))).Append(',');
            sb.Append("\"color\":").Append(Color(r.Status)).Append(',');
            sb.Append("\"fields\":").Append(Fields(r)).Append(',');
            sb.Append("\"footer\":{\"text\":").Append(Json.Quote(FooterDate(cfg.Lang))).Append("},");

            if (!string.IsNullOrEmpty(r.AwardIcon))
                sb.Append("\"thumbnail\":{\"url\":").Append(Json.Quote(r.AwardIcon)).Append("},");

            sb.Append("\"timestamp\":").Append(Json.Quote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
            sb.Append("}]}");
            return sb.ToString();
        }

        static string Description(CheckInResult r)
        {
            var sb = new StringBuilder(256);
            sb.Append(r.StatusText);
            if (r.AwardName != null)
            {
                sb.Append("\n\n**").Append(r.AwardName).Append("** ×").Append(r.AwardCount);
            }
            if (r.Day > 0 && r.Total > 0)
            {
                sb.Append("\nJour ").Append(r.Day).Append(" / ").Append(r.Total)
                  .Append("  `").Append(ProgressBar(r.Day, r.Total)).Append('`');
            }
            if (r.Missed > 0)
            {
                sb.Append('\n').Append(r.Missed).Append(r.Missed > 1 ? " jours manqués" : " jour manqué")
                  .Append(" ce mois-ci.");
            }
            return sb.ToString();
        }

        static string Fields(CheckInResult r)
        {
            if (r.Notes.Count == 0) return "[]";

            var sb = new StringBuilder(512);
            sb.Append('[');
            var main = r.Notes[0];

            var full = Notes.Duration(main.ResinFullInSec);
            Field(sb, "Résine",
                "**" + main.Resin + "** / " + main.MaxResin + "\n" +
                (full != null ? "Plein dans " + full : "Au maximum"),
                true, true);

            Field(sb, "Commissions",
                "**" + main.CommissionsDone + "** / " + main.CommissionsTotal + "\n" + CommissionState(main),
                true, false);

            if (main.BossMax == 0) Field(sb, "Boss hebdo.", "Indisponible", true, false);
            else Field(sb, "Boss hebdomadaires",
                "**" + main.BossDone + "** / " + main.BossMax + "\n" + BossState(main), true, false);

            // Serveurs supplementaires : une ligne chacun, pleine largeur.
            if (r.Notes.Count > 1)
            {
                var extra = new StringBuilder(256);
                for (var i = 1; i < r.Notes.Count; i++)
                {
                    var n = r.Notes[i];
                    if (extra.Length > 0) extra.Append('\n');
                    extra.Append("**").Append(n.RegionName).Append("** · UID ").Append(n.Uid)
                         .Append(" — Résine ").Append(n.Resin).Append('/').Append(n.MaxResin)
                         .Append(" · Commissions ").Append(n.CommissionsDone).Append('/').Append(n.CommissionsTotal)
                         .Append(" · Boss ").Append(n.BossDone).Append('/').Append(n.BossMax);
                }
                Field(sb, "Autres serveurs", extra.ToString(), false, false);
            }

            sb.Append(']');
            return sb.ToString();
        }

        static void Field(StringBuilder sb, string name, string value, bool inline, bool first)
        {
            if (!first) sb.Append(',');
            sb.Append("{\"name\":").Append(Json.Quote(name))
              .Append(",\"value\":").Append(Json.Quote(value))
              .Append(",\"inline\":").Append(inline ? "true" : "false").Append('}');
        }

        static string CommissionState(NotesData n)
        {
            if (n.CommissionsTotal == 0) return "Indisponible";
            if (n.CommissionsDone < n.CommissionsTotal) return "À terminer";
            return n.CommissionRewardClaimed ? "Terminées ✓" : "Coffre à ouvrir";
        }

        static string BossState(NotesData n)
        {
            if (n.BossDone == 0) return "Aucun fait";
            return n.BossDone < n.BossMax ? "En cours" : "Terminés ✓";
        }

        /// <summary>Barre de progression. Un jour reclame vaut au moins un segment.</summary>
        public static string ProgressBar(int current, int total)
        {
            const int Slots = 10;
            var filled = (int)Math.Round((double)current / total * Slots);
            if (current > 0 && filled < 1) filled = 1;
            if (filled < 0) filled = 0;
            if (filled > Slots) filled = Slots;
            return new string('█', filled) + new string('░', Slots - filled);
        }

        /// <summary>
        /// Discord n'affiche que « Aujourd'hui à 06:33 » a cote de l'horodatage :
        /// la date du calendrier n'apparait nulle part. On la met donc ici.
        /// </summary>
        public static string FooterDate(string lang)
        {
            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(lang); }
            catch { culture = CultureInfo.GetCultureInfo("fr-FR"); }
            return DateTime.Now.ToString("D", culture);
        }

        static string Mention(string pingMode, bool isError)
        {
            var ping = pingMode == "always" || (pingMode == "error" && isError);
            // Discord n'interprete @everyone que si allowed_mentions l'autorise.
            // Sans ping on passe une liste vide : un nom de recompense contenant
            // « @everyone » ne peut alors rien declencher.
            return ping
                ? "\"content\":\"@everyone\",\"allowed_mentions\":{\"parse\":[\"everyone\"]}"
                : "\"allowed_mentions\":{\"parse\":[]}";
        }

        static int Color(RunStatus s)
        {
            switch (s)
            {
                case RunStatus.Success: return ColorSuccess;
                case RunStatus.Already: return ColorAlready;
                case RunStatus.Error: return ColorError;
                default: return ColorWarning;
            }
        }

        // ------------------------------------------------------------------ envoi

        static void Send(string webhook, string payload)
        {
            var res = Http.PostJson(webhook, null, payload);
            if (res.Ok) return;

            if (res.Status == 429)
            {
                // Rate limit : une seule reprise, apres le delai demande.
                var wait = RetryAfterMs(res.Body);
                System.Threading.Thread.Sleep(Math.Min(wait, 15000));
                res = Http.PostJson(webhook, null, payload);
                if (res.Ok) return;
            }

            var body = res.Body ?? "";
            throw new InvalidOperationException(
                "Discord HTTP " + res.Status + " : " + body.Substring(0, Math.Min(160, body.Length)));
        }

        static int RetryAfterMs(string body)
        {
            try
            {
                var v = Json.Parse(body);
                object raw;
                if (v != null && v.TryGetValue("retry_after", out raw))
                {
                    var seconds = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                    // L'API renvoie des secondes ; certains proxys des ms.
                    if (seconds > 0) return (int)(seconds < 100 ? seconds * 1000 : seconds) + 250;
                }
            }
            catch { }
            return 2000;
        }
    }
}
