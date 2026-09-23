// Outil de developpement : rend la fenetre de configuration dans un PNG, pour
// pouvoir la relire sans avoir a la regarder a l'ecran. Jamais compile dans
// l'application livree.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DailyCheckIn
{
    static class Shot
    {
        static System.Collections.Generic.IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in AllControls(c)) yield return d;
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            var target = args.Length > 0 ? args[0] : "form.png";
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var form = new ConfigForm())
            {
                // On affiche hors ecran : les controles doivent etre realises
                // pour que le rendu ne soit pas vide.
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-4000, -4000);
                form.Show();
                for (var i = 0; i < 60; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(25); }

                using (var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.ClientSize));
                    bmp.Save(target, ImageFormat.Png);
                }
                Console.WriteLine("rendu " + form.ClientSize.Width + "x" + form.ClientSize.Height + " -> " + target);

                // Etat reel des controles, pour ne pas juger sur une image.
                var cfg = Config.Load();
                Console.WriteLine("config lue : notifyAlready=" + cfg.NotifyAlready
                    + " showNotes=" + cfg.ShowNotes + " allServers=" + cfg.AllServers
                    + " toastSuccess=" + cfg.ToastOnSuccess + " toastAlready=" + cfg.ToastOnAlready
                    + " toastError=" + cfg.ToastOnError + " ping=" + cfg.PingMode
                    + " cookies=" + cfg.Cookies.Length + " webhookOk=" + Discord.IsWebhookUrl(cfg.Webhook));
                foreach (Control c in AllControls(form))
                {
                    var cb = c as CheckBox;
                    if (cb != null) Console.WriteLine("  case « " + cb.Text + " » = " + cb.Checked);
                    var combo = c as ComboBox;
                    if (combo != null) Console.WriteLine("  liste = " + combo.Text);
                }
                form.Close();
            }
            return 0;
        }
    }
}
