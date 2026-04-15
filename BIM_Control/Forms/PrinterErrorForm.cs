using System;
using System.Drawing;
using System.Windows.Forms;

namespace BIM_Control.Forms
{
    public class PrinterErrorForm : Form
    {
        private Label _messageLabel;

        public PrinterErrorForm(string initialMessage)
        {
            InitializeComponent(initialMessage);
        }

        private void InitializeComponent(string initialMessage)
        {
            _messageLabel = new Label();

            SuspendLayout();

            // 
            // _messageLabel
            // 
            _messageLabel.AutoSize = true;
            _messageLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            _messageLabel.ForeColor = Color.White;
            _messageLabel.Location = new Point(40, 40);
            _messageLabel.MaximumSize = new Size(320, 0);
            _messageLabel.Name = "_messageLabel";
            _messageLabel.Text = initialMessage;
            _messageLabel.TextAlign = ContentAlignment.MiddleCenter;
            
            // 
            // PrinterErrorForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(220, 53, 69); // A shade of red
            ClientSize = new Size(400, 150);
            Controls.Add(_messageLabel);
            FormBorderStyle = FormBorderStyle.None;
            Name = "PrinterErrorForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Printer Error";
            TopMost = true;

            ResumeLayout(false);
            PerformLayout();
        }

        public void UpdateMessage(string newMessage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _messageLabel.Text = newMessage));
            }
            else
            {
                _messageLabel.Text = newMessage;
            }
        }
    }
}

