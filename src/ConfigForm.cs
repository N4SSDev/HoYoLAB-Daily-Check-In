using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DailyCheckIn
{
    /// <summary>Fenetre de configuration. Construite en code : pas de designer, pas de ressources.</summary>
    sealed class ConfigForm : Form
    {
        static readonly Color Bg = ColorTranslator.FromHtml("#12131A");
        static readonly Color Card = ColorTranslator.FromHtml("#1A1C26");
        static readonly Color Input = ColorTranslator.FromHtml("#12141C");
        static readonly Color Border = ColorTranslator.FromHtml("#3A3E50");
        static readonly Color Fg = ColorTranslator.FromHtml("#E6E8EF");
        static readonly Color Dim = ColorTranslator.FromHtml("#9AA0B4");
        static readonly Color Gold = ColorTranslator.FromHtml("#D3BC8E");
        static readonly Color Green = ColorTranslator.FromHtml("#57F287");
        static readonly Color Red = ColorTranslator.FromHtml("#ED4245");

        readonly TextBox _cookies = new TextBox();
        readonly TextBox _webhook = new TextBox();
        readonly ComboBox _ping = new ComboBox();
        readonly Label _tidyInfo = new Label();
        readonly Label _status = new Label();
        readonly Label _banner = new Label();
        readonly Button _save = new Button();
        readonly Button _run = new Button();
        readonly Button _test = new Button();
        readonly CheckBox _notifyAlready = new CheckBox();
        readonly CheckBox _showNotes = new CheckBox();
        readonly CheckBox _allServers = new CheckBox();
        readonly CheckBox _toastSuccess = new CheckBox();
        readonly CheckBox _toastAlready = new CheckBox();
        readonly CheckBox _toastError = new CheckBox();
        readonly CheckBox _autoStart = new CheckBox();
        readonly Label _autoStartHint = new Label();

        Config _cfg;
        bool _dirty;
        bool _loading;

        public ConfigForm()
        {
            Text = Paths.AppName;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 620);
            try { Icon = Icon.ExtractAssociatedIcon(Paths.CurrentExe); } catch { }

            Build();
            Load += delegate { LoadConfig(); DarkTitleBar(); };
        }

        // ------------------------------------------------------------------ mise en page

        void Build()
        {
            var y = 12;

            // En-tete
            var title = Label("Daily Check-In", 16, y, Fg, FontStyle.Bold, 11f);
            _status.SetBounds(380, y + 2, 244, 20);
            _status.TextAlign = ContentAlignment.MiddleRight;
            _status.ForeColor = Dim;
            Controls.Add(title); Controls.Add(_status);
            y += 32;

            // 1 - Compte HoYoLAB
            y = Section("1 · COMPTE HOYOLAB", y, 158, delegate (Panel p)
            {
                _cookies.Multiline = true;
                _cookies.ScrollBars = ScrollBars.Vertical;
                _cookies.SetBounds(14, 30, 580, 48);
                Style(_cookies);
                _cookies.Font = new Font("Consolas", 8.5f);
                _cookies.TextChanged += delegate { MarkDirty(); };
                p.Controls.Add(_cookies);

                var tidy = Ghost("Trier le collage", 14, 86, 120);
                tidy.Click += delegate { Tidy(true); };
                p.Controls.Add(tidy);

                _tidyInfo.SetBounds(142, 90, 452, 18);
                _tidyInfo.ForeColor = Dim;
                p.Controls.Add(_tidyInfo);

                p.Controls.Add(Hint(
                    "F12 sur hoyolab.com → Application → Cookies → colle tout le tableau.\r\n" +
                    "Seuls ltoken_v2 et ltuid_v2 sont conservés, le reste est jeté.", 14, 112, 580));
            });

            // 2 - Discord
            y = Section("2 · DISCORD", y, 132, delegate (Panel p)
            {
                _webhook.SetBounds(14, 30, 470, 24);
                Style(_webhook);
                _webhook.TextChanged += delegate { MarkDirty(); };
                p.Controls.Add(_webhook);

                _test.Text = "Tester";
                _test.SetBounds(494, 29, 100, 26);
                StyleGhost(_test);
                _test.Click += delegate { TestWebhook(); };
                p.Controls.Add(_test);

                p.Controls.Add(Label("Ping @everyone", 14, 68, Dim, FontStyle.Regular, 9f));
                _ping.DropDownStyle = ComboBoxStyle.DropDownList;
                _ping.Items.AddRange(new object[] { "Jamais", "Seulement si échec", "À chaque check-in" });
                _ping.SetBounds(150, 64, 180, 24);
                _ping.FlatStyle = FlatStyle.Flat;
                _ping.BackColor = Input; _ping.ForeColor = Fg;
                _ping.SelectedIndexChanged += delegate { MarkDirty(); };
                p.Controls.Add(_ping);

                Check(_notifyAlready, "Envoyer l'embed même si le check-in était déjà fait", 14, 96, 580);
                p.Controls.Add(_notifyAlready);
            });

            // 3 - Options
            y = Section("3 · OPTIONS", y, 116, delegate (Panel p)
            {
                p.Controls.Add(Label("Dans l'embed", 14, 28, Dim, FontStyle.Regular, 8.5f));
                Check(_showNotes, "Résine, commissions && boss hebdomadaires", 14, 46, 300);
                Check(_allServers, "Tous mes serveurs", 330, 46, 260);
                p.Controls.Add(_showNotes); p.Controls.Add(_allServers);

                p.Controls.Add(Label("Notifications Windows", 14, 72, Dim, FontStyle.Regular, 8.5f));
                Check(_toastSuccess, "Réclamée", 14, 90, 110);
                Check(_toastAlready, "Déjà récupérée", 134, 90, 140);
                Check(_toastError, "Échec", 284, 90, 100);
                p.Controls.Add(_toastSuccess); p.Controls.Add(_toastAlready); p.Controls.Add(_toastError);
            });

            // 4 - Demarrage
            y = Section("4 · DÉMARRAGE DE WINDOWS", y, 84, delegate (Panel p)
            {
                Check(_autoStart, "Lancer au démarrage, en arrière-plan", 14, 30, 400);
                _autoStart.Font = new Font(Font, FontStyle.Bold);
                _autoStart.Click += delegate { ToggleAutoStart(); };
                p.Controls.Add(_autoStart);

                _autoStartHint.SetBounds(32, 52, 562, 18);
                _autoStartHint.ForeColor = Dim;
                p.Controls.Add(_autoStartHint);
            });

            // Actions
            _save.Text = "Enregistrer";
            _save.SetBounds(16, y, 130, 32);
            StylePrimary(_save);
            _save.Click += delegate { Save(); };
            Controls.Add(_save);

            _run.Text = "Lancer le check-in maintenant";
            _run.SetBounds(154, y, 230, 32);
            StyleGhost(_run);
            _run.Click += delegate { RunNow(); };
            Controls.Add(_run);
            y += 42;

            _banner.SetBounds(16, y, 608, 44);
            _banner.TextAlign = ContentAlignment.MiddleLeft;
            _banner.Visible = false;
            Controls.Add(_banner);

            ClientSize = new Size(640, y + 56);
        }

        int Section(string caption, int y, int height, Action<Panel> fill)
        {
            var panel = new Panel();
            panel.SetBounds(16, y, 608, height);
            panel.BackColor = Card;
            panel.Controls.Add(Label(caption, 14, 10, Gold, FontStyle.Bold, 8f));
            fill(panel);
            Controls.Add(panel);
            return y + height + 10;
        }

        Label Label(string text, int x, int y, Color color, FontStyle style, float size)
        {
            var l = new Label
            {
                Text = text,
                ForeColor = color,
                AutoSize = true,
                Font = new Font("Segoe UI", size, style),
            };
            l.Location = new Point(x, y);
            return l;
        }

        Label Hint(string text, int x, int y, int width)
        {
            var l = new Label
            {
                Text = text,
                ForeColor = Dim,
                Font = new Font("Segoe UI", 8f),
            };
            l.SetBounds(x, y, width, 34);
            return l;
        }

        void Check(CheckBox c, string text, int x, int y, int width)
        {
            c.Text = text;
            c.ForeColor = Fg;
            c.FlatStyle = FlatStyle.Flat;
            c.SetBounds(x, y, width, 22);
            if (c != _autoStart) c.CheckedChanged += delegate { MarkDirty(); };
        }

        void Style(TextBox t)
        {
            t.BackColor = Input; t.ForeColor = Fg; t.BorderStyle = BorderStyle.FixedSingle;
        }

        void StylePrimary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Gold; b.ForeColor = Color.FromArgb(26, 23, 16);
            b.FlatAppearance.BorderSize = 0;
            b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        void StyleGhost(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Card; b.ForeColor = Fg;
            b.FlatAppearance.BorderColor = Border;
        }

        Button Ghost(string text, int x, int y, int width)
        {
            var b = new Button { Text = text };
            b.SetBounds(x, y, width, 26);
            StyleGhost(b);
            return b;
        }

        /// <summary>Barre de titre sombre sur Windows 10/11. Sans effet ailleurs.</summary>
        void DarkTitleBar()
        {
            try
            {
                var on = 1;
                DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
            }
            catch { }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // ------------------------------------------------------------------ donnees

        void LoadConfig()
        {
            _loading = true;
            _cfg = Config.Load();
            _cookies.Text = string.Join("\r\n", _cfg.Cookies);
            _webhook.Text = _cfg.Webhook;
            _ping.SelectedIndex = Array.IndexOf(Config.PingModes, _cfg.PingMode);
            if (_ping.SelectedIndex < 0) _ping.SelectedIndex = 1;
            _notifyAlready.Checked = _cfg.NotifyAlready;
            _showNotes.Checked = _cfg.ShowNotes;
            _allServers.Checked = _cfg.AllServers;
            _toastSuccess.Checked = _cfg.ToastOnSuccess;
            _toastAlready.Checked = _cfg.ToastOnAlready;
            _toastError.Checked = _cfg.ToastOnError;
            _autoStart.Checked = AutoStart.IsEnabled;
            _loading = false;
            _dirty = false;

            // Reparation opportuniste : c'est la seule occasion de le faire,
            // puisqu'une tache cassee empeche justement le demarrage de tourner.
            var healed = AutoStart.Heal(Paths.CurrentExe, "--background");
            if (healed != null)
            {
                _autoStart.Checked = AutoStart.IsEnabled;
                Banner("Le lancement au démarrage visait un exécutable disparu.\r\n"
                     + "Il a été réparé et pointe maintenant vers cette version.", Green);
            }
            Refresh_();
        }

        void ReadInto(Config c)
        {
            var lines = _cookies.Text.Replace("\r\n", "\n").Split('\n');
            var list = new List<string>();
            foreach (var l in lines) { var t = l.Trim(); if (t.Length > 0) list.Add(t); }
            c.Cookies = list.ToArray();
            c.Webhook = _webhook.Text.Trim();
            c.PingMode = Config.PingModes[Math.Max(0, _ping.SelectedIndex)];
            c.NotifyAlready = _notifyAlready.Checked;
            c.ShowNotes = _showNotes.Checked;
            c.AllServers = _allServers.Checked;
            c.ToastOnSuccess = _toastSuccess.Checked;
            c.ToastOnAlready = _toastAlready.Checked;
            c.ToastOnError = _toastError.Checked;
            c.Normalize();
        }

        void MarkDirty()
        {
            if (_loading) return;
            _dirty = true;
            _banner.Visible = false;
            _tidyInfo.Text = "";
            Refresh_();
        }

        void Refresh_()
        {
            var probe = new Config { Lang = _cfg.Lang };
            ReadInto(probe);
            var valid = probe.IsUsable;

            _run.Enabled = valid && !_dirty;
            _autoStart.Enabled = (valid && !_dirty) || _autoStart.Checked;

            if (_dirty) { _status.Text = "Non enregistré"; _status.ForeColor = Gold; _autoStartHint.Text = "Enregistre d'abord tes modifications."; }
            else if (!valid) { _status.Text = "Non configuré"; _status.ForeColor = Red; _autoStartHint.Text = "Renseigne un cookie et un webhook valides."; }
            else if (_autoStart.Checked) { _status.Text = "Actif au démarrage"; _status.ForeColor = Green; _autoStartHint.Text = "Se lancera sans fenêtre, fera le check-in, puis se fermera."; }
            else { _status.Text = "Démarrage désactivé"; _status.ForeColor = Gold; _autoStartHint.Text = "Coche pour automatiser le check-in à chaque démarrage."; }
        }

        void Banner(string text, Color color)
        {
            _banner.Text = text;
            _banner.ForeColor = color;
            _banner.Visible = true;
        }

        // ------------------------------------------------------------------ actions

        void Tidy(bool manual)
        {
            var raw = _cookies.Text;
            if (raw.Trim().Length == 0) { if (manual) Info("Le champ est vide.", Red); return; }

            var res = CookieParser.Extract(raw);
            if (res.Cookies.Count == 0)
            {
                Info(res.Warnings.Count > 0 ? res.Warnings[0] : "Aucun cookie exploitable trouvé.", Red);
                return;
            }

            var cleaned = string.Join("\r\n", res.Cookies.ToArray());
            var changed = cleaned != raw.Trim();
            if (changed) { _loading = true; _cookies.Text = cleaned; _loading = false; _dirty = true; _banner.Visible = false; Refresh_(); }

            var n = res.Cookies.Count;
            var compte = n + (n > 1 ? " comptes" : " compte");
            if (res.Warnings.Count > 0) Info(string.Join(" ", res.Warnings.ToArray()), Gold);
            else if (changed) Info("Trié : " + compte + " · " + res.Dropped.Count + " autres jetés.", Green);
            else if (manual) Info("Déjà propre : " + compte + ".", Green);
        }

        void Info(string text, Color color) { _tidyInfo.Text = text; _tidyInfo.ForeColor = color; }

        void Save()
        {
            var c = new Config { Lang = _cfg.Lang, StartupDelaySec = _cfg.StartupDelaySec };
            ReadInto(c);
            var problems = c.Problems();
            if (problems.Count > 0) { Banner(string.Join("\r\n", problems.ToArray()), Red); return; }

            c.Save();
            _cfg = c;
            _dirty = false;
            Banner("Configuration enregistrée.", Green);
            Refresh_();
        }

        void TestWebhook()
        {
            var url = _webhook.Text.Trim();
            if (!Discord.IsWebhookUrl(url)) { Banner("L'URL du webhook n'a pas le format attendu.", Red); return; }

            var mode = Config.PingModes[Math.Max(0, _ping.SelectedIndex)];
            Busy(_test, "Envoi…", delegate
            {
                try { Discord.SendTest(url, mode, _cfg.Lang); return null; }
                catch (Exception ex) { return Log.Describe(ex); }
            },
            delegate (string error)
            {
                Banner(error == null ? "Embed de test envoyé, va voir ton salon." : "Échec : " + error,
                       error == null ? Green : Red);
            });
        }

        void RunNow()
        {
            RunOutcome outcome = null;
            Busy(_run, "Check-in en cours…", delegate
            {
                try { outcome = Runner.Run(_cfg, true, false); return null; }
                catch (Exception ex) { return Log.Describe(ex); }
            },
            delegate (string error)
            {
                if (error != null) { Banner("Échec : " + error, Red); return; }
                if (outcome.HasError) { Banner("Échec : " + outcome.Error, Red); return; }
                if (outcome.Results.Count == 0) { Banner("Rien à faire : le check-in du jour est déjà validé.", Green); return; }

                var r = outcome.Results[0];
                var parts = new List<string> { r.StatusText };
                var detail = new List<string>();
                if (r.AwardLabel != null) detail.Add(r.AwardLabel + " — jour " + r.Day + "/" + r.Total);
                if (r.Notes.Count > 0)
                {
                    var n = r.Notes[0];
                    detail.Add("Résine " + n.Resin + "/" + n.MaxResin);
                    detail.Add("Commissions " + n.CommissionsDone + "/" + n.CommissionsTotal);
                    if (n.BossMax > 0) detail.Add("Boss " + n.BossDone + "/" + n.BossMax);
                }
                if (detail.Count > 0) parts.Add(string.Join(" · ", detail.ToArray()));
                Banner(string.Join("\r\n", parts.ToArray()), r.Status == RunStatus.Error ? Red : Green);
            });
        }

        void ToggleAutoStart()
        {
            var wanted = _autoStart.Checked;
            if (wanted)
            {
                var c = new Config { Lang = _cfg.Lang };
                ReadInto(c);
                if (!c.IsUsable || _dirty)
                {
                    _autoStart.Checked = false;
                    Banner("Enregistre d'abord une configuration valide.", Red);
                    Refresh_();
                    return;
                }
            }

            var error = wanted ? AutoStart.Enable(Paths.CurrentExe, "--background") : AutoStart.Disable();
            _autoStart.Checked = AutoStart.IsEnabled;
            if (error != null) Banner("Le lancement au démarrage a échoué :\r\n" + error, Red);
            else if (_autoStart.Checked) Banner("Lancement au démarrage activé.", Green);
            else _banner.Visible = false;
            Refresh_();
        }

        /// <summary>
        /// Execute un travail reseau hors du fil d'interface, pour que la
        /// fenetre ne se fige jamais, puis revient afficher le resultat.
        /// </summary>
        void Busy(Button button, string busyText, Func<string> work, Action<string> done)
        {
            var label = button.Text;
            button.Text = busyText;
            button.Enabled = false;
            _banner.Visible = false;

            ThreadPool.QueueUserWorkItem(delegate
            {
                var error = work();
                BeginInvoke((MethodInvoker)delegate
                {
                    button.Text = label;
                    button.Enabled = true;
                    done(error);
                    Refresh_();
                });
            });
        }
    }
}
