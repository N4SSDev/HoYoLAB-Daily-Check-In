using System;
using System.Drawing;
using System.Windows.Forms;

namespace DailyCheckIn
{
    /// <summary>
    /// Notification Windows en bas a droite.
    ///
    /// Deux pieges, tous deux mesures :
    ///
    /// 1. Demander la bulle juste apres avoir rendu l'icone visible la fait
    ///    disparaitre en silence — Windows ne l'a pas encore enregistree dans
    ///    la zone de notification. On lui laisse donc le temps.
    ///
    /// 2. `ShowBalloonTip` ne fait que confier la bulle au shell. Quitter
    ///    aussitot tue le processus avant l'affichage : c'est ce qui empechait
    ///    toute notification d'apparaitre. On attend `BalloonTipShown`, qui est
    ///    une preuve et non une supposition, puis l'acquittement.
    ///
    /// L'attente est entierement evenementielle : la boucle de messages dort
    /// tant que rien n'arrive, plutot que de scruter un etat. Sur les quelques
    /// secondes d'affichage, cela represente des centaines de reveils evites.
    /// </summary>
    static class Notifier
    {
        /// <summary>Delai laisse a Windows pour enregistrer l'icone.</summary>
        const int RegisterMs = 600;

        /// <summary>Au-dela, on considere que Windows n'affichera rien.</summary>
        const int ShowTimeoutMs = 5000;

        /// <summary>Attente maximale de l'acquittement, pour ne jamais rester en memoire.</summary>
        const int AckTimeoutMs = 120000;

        /// <summary>
        /// Affiche la notification et rend la main une fois qu'elle est
        /// acquittee : clic, fermeture, ou bascule au centre de notifications.
        /// </summary>
        /// <returns>Vrai si Windows l'a effectivement affichee.</returns>
        public static bool Show(string title, string body)
        {
            try
            {
                using (var ctx = new BalloonContext(title, body))
                {
                    Application.Run(ctx);
                    return ctx.WasShown;
                }
            }
            catch (Exception ex)
            {
                Log.Error("notification", ex);
                return false;
            }
        }

        /// <summary>
        /// Cycle de vie de la bulle, pilote par les evenements de Windows et un
        /// unique minuteur qui change de role selon la phase.
        /// </summary>
        sealed class BalloonContext : ApplicationContext
        {
            readonly NotifyIcon _icon = new NotifyIcon();
            readonly Timer _timer = new Timer();
            readonly Icon _bitmap;
            readonly string _title, _body;
            bool _finished;

            public bool WasShown { get; private set; }

            public BalloonContext(string title, string body)
            {
                _title = title;
                _body = body;
                _bitmap = AppIcon();

                _icon.Icon = _bitmap;
                _icon.Text = Paths.AppName;
                _icon.BalloonTipShown += OnShown;
                _icon.BalloonTipClosed += OnAcknowledged;
                _icon.BalloonTipClicked += OnAcknowledged;
                _icon.Visible = true;

                // Phase 1 : laisser Windows enregistrer l'icone.
                _timer.Interval = RegisterMs;
                _timer.Tick += OnRegistered;
                _timer.Start();
            }

            void OnRegistered(object sender, EventArgs e)
            {
                _timer.Stop();
                _timer.Tick -= OnRegistered;

                _icon.BalloonTipTitle = _title;
                _icon.BalloonTipText = _body;
                _icon.ShowBalloonTip(10000);

                // Phase 2 : Windows va-t-il l'afficher ?
                _timer.Interval = ShowTimeoutMs;
                _timer.Tick += OnTimeout;
                _timer.Start();
            }

            void OnShown(object sender, EventArgs e)
            {
                WasShown = true;
                // Phase 3 : affichee, on attend l'acquittement.
                _timer.Stop();
                _timer.Interval = AckTimeoutMs;
                _timer.Start();
            }

            void OnTimeout(object sender, EventArgs e) { Finish(); }
            void OnAcknowledged(object sender, EventArgs e) { Finish(); }

            void Finish()
            {
                if (_finished) return;
                _finished = true;
                _timer.Stop();
                _icon.Visible = false;
                ExitThread();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _timer.Dispose();
                    _icon.Dispose();
                    if (_bitmap != null) _bitmap.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>Icone Paimon embarquee dans l'executable, sinon celle du systeme.</summary>
        static Icon AppIcon()
        {
            try
            {
                var own = Icon.ExtractAssociatedIcon(Paths.CurrentExe);
                if (own != null) return own;
            }
            catch { }
            return (Icon)SystemIcons.Information.Clone();
        }
    }
}
