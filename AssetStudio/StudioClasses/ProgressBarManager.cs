using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using AssetStudio.Extensions;

namespace AssetStudio.StudioClasses
{
    public class ProgressBarManager
    {
        public readonly CustomProgressBar progressBar;

        private int stepDelta;
        private Stopwatch stepTimer;

        private static readonly TimeSpan Fps = TimeSpan.FromMilliseconds(1000 / 30f); // 30fps target

        public ProgressBarManager()
        {
            this.progressBar = InitializeComponent();
        }

        private static CustomProgressBar InitializeComponent()
        {
            var component = new CustomProgressBar
            {
                Dock = DockStyle.Bottom,
                Location = new Point(1, 2),
                Name = "progressBar",
                Size = new Size(416, 17),
                Step = 1,
                TabIndex = 1,
                FontColor = Brushes.Black,
                FontFamily = FontFamily.GenericSansSerif,
                FontSize = 8
            };

            return component;
        }

        public void ClearText()
        {
            this.progressBar.AsyncInvokeIfRequired(delegate
            {
                this.progressBar.Text = "0 / 0";
            });
        }

        public void IncrementMaximum(int value)
        {
            this.progressBar.AsyncInvokeIfRequired(delegate
            {
                this.progressBar.Maximum += value;
            });
        }

        public void IncrementValue(int value)
        {
            this.progressBar.AsyncInvokeIfRequired(delegate
            {
                this.progressBar.Value += value;
            });
        }

        public void PerformStep()
        {
            Interlocked.Increment(ref this.stepDelta);

            // timers aren't "real" - minimal alloc here
            Stopwatch sw = Interlocked.CompareExchange(ref this.stepTimer, Stopwatch.StartNew(), null);

            if (sw == null || sw.Elapsed <= Fps)
            {
                return;
            }

            // don't double-reset timer
            Interlocked.CompareExchange(ref this.stepTimer, Stopwatch.StartNew(), sw);

            this.IncrementValue(Interlocked.Exchange(ref this.stepDelta, 0));

            this.SetText(string.Format("{0} / {1}", this.progressBar.Value, this.progressBar.Maximum));
        }

        public void Reset(int max = 0)
        {
            this.progressBar.AsyncInvokeIfRequired(delegate
            {
                this.progressBar.Value = 0;
                this.progressBar.Maximum = max;
                this.progressBar.Text = "0 / 0";
                this.stepDelta = 0;
            });
        }

        public void SetMaximum(int value)
        {
            this.progressBar.AsyncInvokeIfRequired(() => this.progressBar.Maximum = value);
        }

        public void SetText(string text)
        {
            this.progressBar.AsyncInvokeIfRequired(() => this.progressBar.Text = text);
        }

        public void SetValue(int value)
        {
            this.progressBar.AsyncInvokeIfRequired(() => this.progressBar.Value = value);
        }
    }
}
