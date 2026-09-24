using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DailyCheckIn
{
    sealed class AccountState
    {
        public string LastRun = "";

        public string LastError = "";

        public string Award = "";
        public int Day;
        public int Total;
    }

    sealed class State
    {
        readonly Dictionary<string, AccountState> _accounts = new Dictionary<string, AccountState>(StringComparer.Ordinal);
        string _loadedJson;

        public AccountState For(int index)
        {
            var key = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            AccountState a;
            if (!_accounts.TryGetValue(key, out a)) { a = new AccountState(); _accounts[key] = a; }
            return a;
        }

        public static State Load()
        {
            var state = new State();
            try
            {
                if (!File.Exists(Paths.StateFile)) return state;

                var text = File.ReadAllText(Paths.StateFile, Encoding.UTF8);
                var root = Json.Parse(text);
                var accounts = Json.Obj(root, "accounts");
                if (accounts != null)
                {
                    foreach (var kv in accounts)
                    {
                        var d = kv.Value as Dictionary<string, object>;
                        if (d == null) continue;
                        state._accounts[kv.Key] = new AccountState
                        {
                            LastRun = Json.Str(d, "lastRun", ""),
                            LastError = Json.Str(d, "lastError", ""),
                            Award = Json.Str(d, "award", ""),
                            Day = Json.Int(d, "day"),
                            Total = Json.Int(d, "total"),
                        };
                    }
                }
                state._loadedJson = state.Serialize();
            }
            catch (Exception ex)
            {
                Log.Error("lecture de l'etat", ex);
                state._accounts.Clear();
            }
            return state;
        }

        public bool SaveIfChanged()
        {
            var json = Serialize();
            if (json == _loadedJson) return false;
            try
            {
                Paths.EnsureData();
                AtomicFile.Write(Paths.StateFile, json);
                _loadedJson = json;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("ecriture de l'etat", ex);
                return false;
            }
        }

        string Serialize()
        {
            var inner = new Json.Writer(2);
            foreach (var kv in _accounts)
            {
                var a = kv.Value;
                inner.AddRaw(kv.Key, new Json.Writer(3)
                    .Add("lastRun", a.LastRun)
                    .Add("lastError", a.LastError)
                    .Add("award", a.Award)
                    .Add("day", a.Day)
                    .Add("total", a.Total)
                    .ToString());
            }
            return new Json.Writer().AddRaw("accounts", inner.ToString()).ToString();
        }
    }
}
