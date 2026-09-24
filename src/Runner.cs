using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading;

namespace DailyCheckIn
{
    sealed class CachedInfo
    {
        public string Award;
        public int Day;
        public int Total;
    }

    sealed class RunOutcome
    {
        public readonly List<CheckInResult> Results = new List<CheckInResult>();
        public readonly List<CachedInfo> Cached = new List<CachedInfo>();
        public int Skipped;
        public string Error;
        public bool HasError { get { return Error != null; } }
    }

    static class Runner
    {
        public static RunOutcome Run(Config cfg, bool force, bool atStartup)
        {
            var outcome = new RunOutcome();
            var problems = cfg.Problems();
            if (problems.Count > 0)
            {
                outcome.Error = "Configuration incomplète : " + string.Join(" ", problems.ToArray());
                Log.Write(outcome.Error);
                return outcome;
            }

            var sw = Stopwatch.StartNew();
            var state = State.Load();
            var today = Api.Today();

            var pending = new List<int>();
            for (var i = 0; i < cfg.Cookies.Length; i++)
            {
                var acc = state.For(i);
                if (force || cfg.NotifyAlready || acc.LastRun != today) pending.Add(i);
                else
                {
                    outcome.Skipped++;
                    outcome.Cached.Add(new CachedInfo { Award = acc.Award, Day = acc.Day, Total = acc.Total });
                }
            }

            if (pending.Count == 0)
            {
                Log.Write("déjà validé aujourd'hui, rien à faire");
                return outcome;
            }

            if (atStartup)
            {
                if (cfg.StartupDelaySec > 0) Thread.Sleep(cfg.StartupDelaySec * 1000);
                if (!WaitForNetwork())
                {
                    outcome.Error = "Pas de connexion réseau, check-in abandonné.";
                    Log.Write(outcome.Error);
                    return outcome;
                }
            }

            foreach (var i in pending)
            {
                var result = RunOne(cfg, cfg.Cookies[i]);
                var acc = state.For(i);

                if (result.Status != RunStatus.Error)
                {
                    acc.LastRun = today;
                    acc.LastError = "";
                    if (result.AwardLabel != null)
                    {
                        acc.Award = result.AwardLabel;
                        acc.Day = result.Day;
                        acc.Total = result.Total;
                    }
                }

                var alreadyReported = result.Status == RunStatus.Error && acc.LastError == today && !force;
                if (result.Status == RunStatus.Error) acc.LastError = today;

                var silent = alreadyReported ||
                             (result.Status == RunStatus.Already && !cfg.NotifyAlready);

                if (!silent)
                {
                    try
                    {
                        Discord.Post(cfg, result);
                        result.Notified = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("envoi Discord", ex);
                    }
                }
                outcome.Results.Add(result);
            }

            state.SaveIfChanged();
            Log.Write(Summary(outcome, sw.ElapsedMilliseconds));
            return outcome;
        }

        static CheckInResult RunOne(Config cfg, string cookie)
        {
            CheckInResult result;
            try
            {
                result = HoyoLab.Run(cookie, cfg);
            }
            catch (Exception ex)
            {
                return new CheckInResult
                {
                    Status = RunStatus.Error,
                    StatusText = "Erreur inattendue : " + Log.Describe(ex),
                };
            }

            if (cfg.ShowNotes && result.Status != RunStatus.Error && result.Roles.Count > 0)
            {
                var targets = new List<Role>();
                if (cfg.AllServers) targets.AddRange(result.Roles);
                else targets.Add(result.Account ?? result.Roles[0]);

                targets.RemoveAll(r => r == null || string.IsNullOrEmpty(r.Server) || string.IsNullOrEmpty(r.Uid));
                if (targets.Count > 0) result.Notes = Notes.FetchAll(cookie, targets, cfg.Lang);
            }
            return result;
        }

        static string Summary(RunOutcome o, long ms)
        {
            if (o.Results.Count == 0) return "rien à faire";

            var r = o.Results[0];
            var sb = new System.Text.StringBuilder(96);
            switch (r.Status)
            {
                case RunStatus.Success: sb.Append("réclamé"); break;
                case RunStatus.Already: sb.Append("déjà réclamé"); break;
                case RunStatus.Error: sb.Append("ÉCHEC : ").Append(r.StatusText); break;
                default: sb.Append("réclamé (récompense inconnue)"); break;
            }
            if (r.AwardLabel != null) sb.Append(" · ").Append(r.AwardLabel);
            if (r.Day > 0) sb.Append(" · jour ").Append(r.Day).Append('/').Append(r.Total);
            if (r.Notified) sb.Append(" · embed envoyé");
            if (o.Results.Count > 1) sb.Append(" · ").Append(o.Results.Count).Append(" comptes");
            sb.Append(" · ").Append((ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s");
            return sb.ToString();
        }

        static bool WaitForNetwork()
        {
            var deadline = DateTime.UtcNow.AddMinutes(2);
            var wait = 500;
            while (true)
            {
                try
                {
                    Dns.GetHostEntry("sg-hk4e-api.hoyolab.com");
                    return true;
                }
                catch
                {
                    if (DateTime.UtcNow.AddMilliseconds(wait) >= deadline) return false;
                    Thread.Sleep(wait);
                    wait = Math.Min(wait * 2, 15000);
                }
            }
        }
    }
}
