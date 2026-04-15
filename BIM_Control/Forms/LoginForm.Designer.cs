namespace BIM_Control
{
    partial class LoginForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginForm));
            btn_login = new Button();
            lb_login = new Label();
            lb_password = new Label();
            tb_login = new TextBox();
            tb_password = new TextBox();
            lb_pc = new Label();
            lb_pcName = new Label();
            SuspendLayout();
            // 
            // btn_login
            // 
            btn_login.Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point);
            btn_login.Location = new Point(74, 136);
            btn_login.Name = "btn_login";
            btn_login.Size = new Size(106, 41);
            btn_login.TabIndex = 0;
            btn_login.Text = "Войти";
            btn_login.UseVisualStyleBackColor = true;
            btn_login.Click += btn_login_Click;
            // 
            // lb_login
            // 
            lb_login.AutoSize = true;
            lb_login.Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point);
            lb_login.Location = new Point(37, 41);
            lb_login.Name = "lb_login";
            lb_login.Size = new Size(62, 25);
            lb_login.TabIndex = 1;
            lb_login.Text = "Логин";
            // 
            // lb_password
            // 
            lb_password.AutoSize = true;
            lb_password.Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point);
            lb_password.Location = new Point(25, 87);
            lb_password.Name = "lb_password";
            lb_password.Size = new Size(74, 25);
            lb_password.TabIndex = 2;
            lb_password.Text = "Пароль";
            // 
            // tb_login
            // 
            tb_login.Location = new Point(107, 44);
            tb_login.Name = "tb_login";
            tb_login.Size = new Size(120, 23);
            tb_login.TabIndex = 3;
            // 
            // tb_password
            // 
            tb_password.Location = new Point(107, 87);
            tb_password.Name = "tb_password";
            tb_password.Size = new Size(120, 23);
            tb_password.TabIndex = 4;
            tb_password.UseSystemPasswordChar = true;
            // 
            // lb_pc
            // 
            lb_pc.AutoSize = true;
            lb_pc.Font = new Font("Segoe UI", 11.25F, FontStyle.Underline, GraphicsUnit.Point);
            lb_pc.Location = new Point(12, 9);
            lb_pc.Name = "lb_pc";
            lb_pc.Size = new Size(36, 20);
            lb_pc.TabIndex = 5;
            lb_pc.Text = "ПК :";
            // 
            // lb_pcName
            // 
            lb_pcName.AutoSize = true;
            lb_pcName.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point);
            lb_pcName.Location = new Point(49, 9);
            lb_pcName.Name = "lb_pcName";
            lb_pcName.Size = new Size(0, 20);
            lb_pcName.TabIndex = 6;
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(268, 201);
            Controls.Add(lb_pcName);
            Controls.Add(lb_pc);
            Controls.Add(tb_password);
            Controls.Add(tb_login);
            Controls.Add(lb_password);
            Controls.Add(lb_login);
            Controls.Add(btn_login);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "LoginForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Авторизация";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btn_login;
        private Label lb_login;
        private Label lb_password;
        private TextBox tb_login;
        private TextBox tb_password;
        private Label lb_pc;
        private Label lb_pcName;
    }
}
