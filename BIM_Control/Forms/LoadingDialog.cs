using System;
using System.Drawing;
using System.Windows.Forms;

namespace BIM_Control.Forms
{
    public partial class LoadingDialog : Form
    {
        private Label lblMessage;
        private ProgressBar progressBar;
        private bool _isDeterminate;

        public LoadingDialog(string message = "Обработка...")
        {
            InitializeComponent(message);
        }

        private void InitializeComponent(string message)
        {
            // Form properties
            this.Text = "Загрузка";
            this.Size = new Size(400, 120); // Increased size to accommodate longer messages
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false; // Remove close button
            this.ShowInTaskbar = false; // Don't show in taskbar

            // Message label
            lblMessage = new Label
            {
                Text = message,
                AutoSize = false,
                Size = new Size(380, 40), // Allow multiline if needed
                Location = new Point(10, 10),
                Font = new Font("Arial", 10, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Progress bar
            progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee, // Continuous animation
                Location = new Point(10, 60),
                Size = new Size(380, 23),
                MarqueeAnimationSpeed = 30 // Speed of marquee animation
            };

            // Add controls to form
            this.Controls.Add(lblMessage);
            this.Controls.Add(progressBar);

            // Set form background color
            this.BackColor = SystemColors.Window;
        }

        // Method to update the message
        public void UpdateMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateMessage(message)));
            }
            else
            {
                lblMessage.Text = message;
            }
        }

        public void SetDeterminateMode(bool enabled)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetDeterminateMode(enabled)));
                return;
            }

            _isDeterminate = enabled;
            progressBar.Style = enabled ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            if (enabled)
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 100;
                progressBar.Value = 0;
            }
            else
            {
                progressBar.MarqueeAnimationSpeed = 30;
            }
        }

        public void SetProgress(int percent)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetProgress(percent)));
                return;
            }

            if (!_isDeterminate)
            {
                SetDeterminateMode(true);
            }

            int safe = Math.Max(0, Math.Min(100, percent));
            progressBar.Value = safe;
        }
    }
}

