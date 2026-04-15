namespace BIM_Control.Forms
{
    partial class ReprintForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ReprintForm));
            rbtn_continuePrint = new RadioButton();
            rbtn_resetPrint = new RadioButton();
            label1 = new Label();
            btn_confirmChoice = new Button();
            SuspendLayout();
            // 
            // rbtn_continuePrint
            // 
            rbtn_continuePrint.AutoSize = true;
            rbtn_continuePrint.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point);
            rbtn_continuePrint.Location = new Point(18, 40);
            rbtn_continuePrint.Name = "rbtn_continuePrint";
            rbtn_continuePrint.Size = new Size(166, 24);
            rbtn_continuePrint.TabIndex = 0;
            rbtn_continuePrint.TabStop = true;
            rbtn_continuePrint.Text = "Продолжить печать";
            rbtn_continuePrint.UseVisualStyleBackColor = true;
            // 
            // rbtn_resetPrint
            // 
            rbtn_resetPrint.AutoSize = true;
            rbtn_resetPrint.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            rbtn_resetPrint.Location = new Point(220, 40);
            rbtn_resetPrint.Name = "rbtn_resetPrint";
            rbtn_resetPrint.Size = new Size(136, 24);
            rbtn_resetPrint.TabIndex = 1;
            rbtn_resetPrint.TabStop = true;
            rbtn_resetPrint.Text = "Печать сначала";
            rbtn_resetPrint.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(157, 19);
            label1.TabIndex = 2;
            label1.Text = "Выберите действие:";
            // 
            // btn_confirmChoice
            // 
            btn_confirmChoice.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            btn_confirmChoice.Location = new Point(98, 72);
            btn_confirmChoice.Name = "btn_confirmChoice";
            btn_confirmChoice.Size = new Size(182, 32);
            btn_confirmChoice.TabIndex = 4;
            btn_confirmChoice.Text = "Подтвердить";
            btn_confirmChoice.UseVisualStyleBackColor = true;
            btn_confirmChoice.Click += btn_confirmChoice_Click;
            // 
            // ReprintForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(377, 116);
            Controls.Add(btn_confirmChoice);
            Controls.Add(label1);
            Controls.Add(rbtn_resetPrint);
            Controls.Add(rbtn_continuePrint);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ReprintForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Допечатать коды";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RadioButton rbtn_continuePrint;
        private RadioButton rbtn_resetPrint;
        private Label label1;
        private Button btn_confirmChoice;
    }
}
