using System;
using System.Drawing;
using System.Windows.Forms;

namespace AssetStudio.Extensions
{
    public class CustomProgressBar : ProgressBar
    {
        public new string Text { get; set; }

        public Brush FontColor { get; set; }

        public FontFamily FontFamily { get; set; }

        public float FontSize { get; set; }

        public CustomProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rect = ClientRectangle;
            Graphics g = e.Graphics;

            ProgressBarRenderer.DrawHorizontalBar(g, rect);

            if (Value > 0)
            {
                var clip = new Rectangle(rect.X, rect.Y, (int) Math.Round((float) this.Value / this.Maximum * rect.Width), rect.Height);
                ProgressBarRenderer.DrawHorizontalChunks(g, clip);
            }

            if (string.IsNullOrWhiteSpace(Text))
            {
                return;
            }

            using (var f = new System.Drawing.Font(FontFamily ?? FontFamily.GenericMonospace, FontSize))
            {
                SizeF len = g.MeasureString(Text, f);

                var location = new Point(Convert.ToInt32(this.Width / 2 - len.Width / 2), Convert.ToInt32(this.Height / 2 - len.Height / 2));

                g.DrawString(Text, f, FontColor ?? Brushes.Red, location);
            }
        }
    }
}