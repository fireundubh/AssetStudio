using System;
using System.Drawing;
using System.Windows.Forms;

namespace AssetStudio.Components
{
    public class CustomProgressBar : ProgressBar
    {
        public new string Text { get; set; }

        public Brush FontColor { get; set; }

        public FontFamily FontFamily { get; set; }

        public float FontSize { get; set; }

        public CustomProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle rect = this.ClientRectangle;
            Graphics g = e.Graphics;

            ProgressBarRenderer.DrawHorizontalBar(g, rect);

            if (this.Value > 0)
            {
                var clip = new Rectangle(rect.X, rect.Y, (int) Math.Round((float) this.Value / this.Maximum * rect.Width), rect.Height);
                ProgressBarRenderer.DrawHorizontalChunks(g, clip);
            }

            if (string.IsNullOrWhiteSpace(this.Text))
            {
                return;
            }

            using (var f = new System.Drawing.Font(this.FontFamily ?? FontFamily.GenericMonospace, this.FontSize))
            {
                SizeF len = g.MeasureString(this.Text, f);

                var location = new Point(Convert.ToInt32(this.Width / 2 - len.Width / 2), Convert.ToInt32(this.Height / 2 - len.Height / 2));

                g.DrawString(this.Text, f, this.FontColor ?? Brushes.Red, location);
            }
        }
    }
}