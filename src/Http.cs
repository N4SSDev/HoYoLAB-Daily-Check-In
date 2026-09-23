using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace DailyCheckIn
{
    /// <summary>Reponse brute, sans exception sur les codes d'erreur HTTP.</summary>
    struct HttpResult
    {
        public int Status;
        public string Body;
        public bool Ok { get { return Status >= 200 && Status < 300; } }
    }

    /// <summary>
    /// Couche HTTP minimale, batie sur <see cref="HttpWebRequest"/> livre avec
    /// le framework. Pas de HttpClient : une seule instance a courte duree de
    /// vie n'en tirerait aucun benefice, et il embarque plus de machinerie.
    /// </summary>
    static class Http
    {
        const int TimeoutMs = 20000;
        const int Attempts = 3;

        static readonly Random Jitter = new Random();

        /// <summary>
        /// Point d'injection pour les tests : quand il est defini, aucune
        /// requete ne part sur le reseau. Place a l'interieur de la boucle de
        /// reprise, ce qui rend la politique de reprise elle-meme testable.
        /// </summary>
#pragma warning disable 649 // affecte uniquement par la suite de tests
        internal static Func<string, string, string, HttpResult> Transport;
#pragma warning restore 649

        static Http()
        {
            // Configure une fois pour tout le processus.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                                                 | (SecurityProtocolType)12288; // Tls13
            // Sans cela, .NET attend un « 100 Continue » avant d'envoyer le
            // corps d'un POST : un aller-retour perdu a chaque signature.
            ServicePointManager.Expect100Continue = false;
            // Nagle regroupe les petits paquets et ajoute jusqu'a 200 ms de
            // latence sur des requetes aussi courtes que les notres.
            ServicePointManager.UseNagleAlgorithm = false;
            // Trois requetes partent de front vers le meme hote.
            ServicePointManager.DefaultConnectionLimit = 8;
        }

        public static HttpResult Get(string url, string cookie, string lang = null, string ds = null)
        {
            return Send("GET", url, cookie, null, lang, ds);
        }

        public static HttpResult PostJson(string url, string cookie, string body, string lang = null)
        {
            return Send("POST", url, cookie, body, lang, null);
        }

        /// <summary>
        /// Envoie la requete, avec reprise sur les erreurs passageres.
        /// Les codes 4xx (hors 429) ne sont pas reessayes : c'est notre requete
        /// qui est en cause, pas le reseau.
        /// </summary>
        static HttpResult Send(string method, string url, string cookie, string body, string lang, string ds)
        {
            Exception last = null;

            for (var attempt = 1; attempt <= Attempts; attempt++)
            {
                try
                {
                    var result = Transport != null
                        ? Transport(method, url, body)
                        : Once(method, url, cookie, body, lang, ds);
                    if (result.Status == 429 || result.Status >= 500)
                    {
                        if (attempt == Attempts) return result;
                        Backoff(attempt);
                        continue;
                    }
                    return result;
                }
                catch (WebException ex)
                {
                    var res = ex.Response as HttpWebResponse;
                    if (res == null)
                    {
                        // Panne reseau, DNS, TLS : reessayable.
                        last = ex;
                        if (attempt == Attempts) break;
                        Backoff(attempt);
                        continue;
                    }

                    // Reponse d'erreur : on la lit plutot que de la jeter, le
                    // corps contient souvent le retcode explicatif.
                    HttpResult result;
                    try { result = new HttpResult { Status = (int)res.StatusCode, Body = ReadAll(res) }; }
                    finally { res.Close(); }

                    if ((result.Status == 429 || result.Status >= 500) && attempt < Attempts)
                    {
                        Backoff(attempt);
                        continue;
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempt == Attempts) break;
                    Backoff(attempt);
                }
            }
            throw new IOException("requete impossible vers " + Host(url) + " : " + Log.Describe(last), last);
        }

        static HttpResult Once(string method, string url, string cookie, string body, string lang, string ds)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = method;
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            req.KeepAlive = true;
            req.Proxy = null;                 // evite la detection automatique de proxy, lente
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.Accept = "application/json, text/plain, */*";
            req.UserAgent = Api.UserAgent;
            req.Referer = "https://act.hoyolab.com/";
            req.Headers.Add("Origin", "https://act.hoyolab.com");
            if (cookie != null) req.Headers.Add("Cookie", cookie);

            if (ds != null)
            {
                // En-tetes propres a l'API Battle Chronicle.
                req.Headers.Add("ds", ds);
                req.Headers.Add("x-rpc-app_version", "1.5.0");
                req.Headers.Add("x-rpc-client_type", "5");
                if (lang != null) { req.Headers.Add("x-rpc-language", lang); req.Headers.Add("x-rpc-lang", lang); }
            }
            else
            {
                req.Headers.Add("x-rpc-app_version", "2.34.1");
                req.Headers.Add("x-rpc-client_type", "4");
            }

            if (body != null)
            {
                req.ContentType = "application/json;charset=UTF-8";
                var bytes = Encoding.UTF8.GetBytes(body);
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            }

            using (var res = (HttpWebResponse)req.GetResponse())
            {
                return new HttpResult { Status = (int)res.StatusCode, Body = ReadAll(res) };
            }
        }

        static string ReadAll(HttpWebResponse res)
        {
            using (var stream = res.GetResponseStream())
            {
                if (stream == null) return "";
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        static void Backoff(int attempt)
        {
            // Exponentiel avec bruit : deux comptes ne repartent pas en choeur.
            var ms = 700 * (1 << (attempt - 1));
            lock (Jitter) ms += Jitter.Next(0, 300);
            Thread.Sleep(ms);
        }

        /// <summary>Hote seul, pour les messages : jamais la chaine de requete.</summary>
        public static string Host(string url)
        {
            try { return new Uri(url).Host; } catch { return "l'API"; }
        }
    }
}
