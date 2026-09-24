using System;
using System.Drawing;
using System.Windows.Forms;

namespace DailyCheckIn
{
    static class Notifier
    {
        const int RegisterMs = 600;

        const int ShowTimeoutMs = 5000;

        const int AckTimeoutMs = 120000;

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

                _timer.Interval = ShowTimeoutMs;
                _timer.Tick += OnTimeout;
                _timer.Start();
            }

            void OnShown(object sender, EventArgs e)
            {
                WasShown = true;
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
