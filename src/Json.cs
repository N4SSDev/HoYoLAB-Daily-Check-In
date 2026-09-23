using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace DailyCheckIn
{
    /// <summary>
    /// Acces JSON.
    ///
    /// Lecture : <see cref="JavaScriptSerializer"/>, livre avec le .NET
    /// Framework. Ecrire un analyseur a la main pour les reponses de HoYoLAB
    /// serait de la complexite sans gain — c'est exactement le genre
    /// d'optimisation artificielle a eviter.
    ///
    /// Ecriture : un petit constructeur maison. Nos fichiers ont une forme
    /// connue et doivent rester lisibles et modifiables a la main, ce que le
    /// serialiseur ne produit pas.
    /// </summary>
    static class Json
    {
        [ThreadStatic] static JavaScriptSerializer _reader;

        static JavaScriptSerializer Reader
        {
            get
            {
                if (_reader == null)
                {
                    // Les reponses de l'API tiennent largement dedans ; la borne
                    // evite qu'une reponse aberrante ne fasse exploser la memoire.
                    _reader = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                }
                return _reader;
            }
        }

        /// <summary>
        /// Analyse une reponse. Renvoie null si le texte n'est pas un objet
        /// JSON valide : une page anti-bot, une erreur de passerelle ou un
        /// fichier corrompu ne doivent pas remonter sous forme d'exception.
        /// </summary>
        public static Dictionary<string, object> Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try { return Reader.DeserializeObject(text) as Dictionary<string, object>; }
            catch { return null; }
        }

        // --- lecture defensive ------------------------------------------------
        // Les reponses distantes ne sont jamais tenues pour acquises : chaque
        // acces tolere l'absence de la cle ou un type inattendu.

        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static object[] Arr(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as object[];
            return null;
        }

        public static string Str(Dictionary<string, object> d, string key, string fallback = null)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v != null)
            {
                return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        public static int Int(Dictionary<string, object> d, string key, int fallback = 0)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is int) return (int)v;
            int parsed;
            return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                                NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                   ? parsed : fallback;
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool fallback = false)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            return fallback;
        }

        // --- ecriture ---------------------------------------------------------

        /// <summary>Constructeur d'objet JSON indente, pour nos propres fichiers.</summary>
        public sealed class Writer
        {
            readonly StringBuilder _sb = new StringBuilder(512);
            readonly int _indent;
            bool _first = true;

            public Writer(int indent = 1)
            {
                _indent = indent;
                _sb.Append('{');
            }

            void Key(string name)
            {
                _sb.Append(_first ? "\n" : ",\n");
                _first = false;
                _sb.Append(new string(' ', _indent * 2)).Append('"').Append(name).Append("\": ");
            }

            public Writer Add(string name, string value)
            {
                Key(name);
                if (value == null) _sb.Append("null"); else Escape(_sb, value);
                return this;
            }

            public Writer Add(string name, bool value)
            {
                Key(name); _sb.Append(value ? "true" : "false"); return this;
            }

            public Writer Add(string name, int value)
            {
                Key(name); _sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this;
            }

            public Writer Add(string name, IEnumerable<string> values)
            {
                Key(name);
                _sb.Append('[');
                var first = true;
                foreach (var v in values)
                {
                    if (!first) _sb.Append(',');
                    first = false;
                    _sb.Append('\n').Append(new string(' ', (_indent + 1) * 2));
                    Escape(_sb, v);
                }
                if (!first) _sb.Append('\n').Append(new string(' ', _indent * 2));
                _sb.Append(']');
                return this;
            }

            /// <summary>Insere un objet deja serialise (par un Writer imbrique).</summary>
            public Writer AddRaw(string name, string json)
            {
                Key(name); _sb.Append(json); return this;
            }

            public override string ToString()
            {
                return _sb.ToString() + (_first ? "}" : "\n" + new string(' ', (_indent - 1) * 2) + "}");
            }
        }

        /// <summary>Chaine JSON entre guillemets, echappee.</summary>
        public static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 8);
            Escape(sb, s);
            return sb.ToString();
        }

        static void Escape(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
