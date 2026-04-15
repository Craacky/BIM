namespace BIM_Control.Forms
{
    partial class ControlForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ControlForm));
            splitContainer = new SplitContainer();
            label4 = new Label();
            label3 = new Label();
            btn_st1_verifyDB = new Button();
            gb_productVerify = new GroupBox();
            btn_resetProduct = new Button();
            btn_verifyProduct = new Button();
            rb_productInfo = new RichTextBox();
            gb_loadDb = new GroupBox();
            lb_firstCode = new Label();
            lb_fileName = new Label();
            tb_fileName = new TextBox();
            btn_loadDB = new Button();
            tb_productCode = new TextBox();
            tb_capsLockMode = new TextBox();
            tb_currentLang = new TextBox();
            pb_stage1 = new PictureBox();
            label1 = new Label();
            btn_reprint = new Button();
            btn_startPrint = new Button();
            btn_finishPrint = new Button();
            gb_printerControls = new GroupBox();
            btn_pausePrint = new Button();
            btn_resumePrint = new Button();
            pb_stage2 = new PictureBox();
            gb_labelStar = new GroupBox();
            btn_st2_verifyDB = new Button();
            tb_labelStarCode = new TextBox();
            label2 = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            gb_productVerify.SuspendLayout();
            gb_loadDb.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pb_stage1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pb_stage2).BeginInit();
            gb_labelStar.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 0);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(label4);
            splitContainer.Panel1.Controls.Add(label3);
            splitContainer.Panel1.Controls.Add(btn_st1_verifyDB);
            splitContainer.Panel1.Controls.Add(gb_productVerify);
            splitContainer.Panel1.Controls.Add(gb_loadDb);
            splitContainer.Panel1.Controls.Add(tb_capsLockMode);
            splitContainer.Panel1.Controls.Add(tb_currentLang);
            splitContainer.Panel1.Controls.Add(pb_stage1);
            splitContainer.Panel1.Controls.Add(label1);
            splitContainer.Panel1MinSize = 40;
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(btn_reprint);
            splitContainer.Panel2.Controls.Add(btn_startPrint);
            splitContainer.Panel2.Controls.Add(btn_finishPrint);
            splitContainer.Panel2.Controls.Add(gb_printerControls);
            splitContainer.Panel2.Controls.Add(pb_stage2);
            splitContainer.Panel2.Controls.Add(gb_labelStar);
            splitContainer.Panel2.Controls.Add(label2);
            splitContainer.Size = new Size(717, 760);
            splitContainer.SplitterDistance = 400;
            splitContainer.TabIndex = 0;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(590, 51);
            label4.Name = "label4";
            label4.Size = new Size(70, 15);
            label4.TabIndex = 17;
            label4.Text = "Язык(англ.)";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(598, 21);
            label3.Name = "label3";
            label3.Size = new Size(61, 15);
            label3.TabIndex = 16;
            label3.Text = "Caps Lock";
            // 
            // btn_st1_verifyDB
            // 
            btn_st1_verifyDB.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            btn_st1_verifyDB.Location = new Point(573, 250);
            btn_st1_verifyDB.Name = "btn_st1_verifyDB";
            btn_st1_verifyDB.Size = new Size(132, 64);
            btn_st1_verifyDB.TabIndex = 15;
            btn_st1_verifyDB.Text = "3. Проверка";
            btn_st1_verifyDB.UseVisualStyleBackColor = true;
            btn_st1_verifyDB.Click += btn_st1_verifyDB_Click;
            // 
            // gb_productVerify
            // 
            gb_productVerify.Controls.Add(btn_resetProduct);
            gb_productVerify.Controls.Add(btn_verifyProduct);
            gb_productVerify.Controls.Add(rb_productInfo);
            gb_productVerify.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            gb_productVerify.Location = new Point(12, 212);
            gb_productVerify.Name = "gb_productVerify";
            gb_productVerify.Size = new Size(555, 133);
            gb_productVerify.TabIndex = 14;
            gb_productVerify.TabStop = false;
            gb_productVerify.Text = "2. Проверка наименования продукта";
            // 
            // btn_resetProduct
            // 
            btn_resetProduct.Location = new Point(430, 84);
            btn_resetProduct.Name = "btn_resetProduct";
            btn_resetProduct.Size = new Size(113, 43);
            btn_resetProduct.TabIndex = 2;
            btn_resetProduct.Text = "Сброс";
            btn_resetProduct.UseVisualStyleBackColor = true;
            btn_resetProduct.Click += btn_resetProduct_Click;
            // 
            // btn_verifyProduct
            // 
            btn_verifyProduct.Location = new Point(431, 25);
            btn_verifyProduct.Name = "btn_verifyProduct";
            btn_verifyProduct.Size = new Size(113, 43);
            btn_verifyProduct.TabIndex = 1;
            btn_verifyProduct.Text = "Сверить";
            btn_verifyProduct.UseVisualStyleBackColor = true;
            btn_verifyProduct.Click += btn_verifyProduct_Click;
            // 
            // rb_productInfo
            // 
            rb_productInfo.Font = new Font("Times New Roman", 14.25F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point);
            rb_productInfo.Location = new Point(6, 25);
            rb_productInfo.Name = "rb_productInfo";
            rb_productInfo.ReadOnly = true;
            rb_productInfo.Size = new Size(412, 102);
            rb_productInfo.TabIndex = 0;
            rb_productInfo.Text = "";
            // 
            // gb_loadDb
            // 
            gb_loadDb.Controls.Add(lb_firstCode);
            gb_loadDb.Controls.Add(lb_fileName);
            gb_loadDb.Controls.Add(tb_fileName);
            gb_loadDb.Controls.Add(btn_loadDB);
            gb_loadDb.Controls.Add(tb_productCode);
            gb_loadDb.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            gb_loadDb.Location = new Point(12, 69);
            gb_loadDb.Name = "gb_loadDb";
            gb_loadDb.Size = new Size(693, 133);
            gb_loadDb.TabIndex = 13;
            gb_loadDb.TabStop = false;
            gb_loadDb.Text = "1. Загрузка базы данных";
            // 
            // lb_firstCode
            // 
            lb_firstCode.AutoSize = true;
            lb_firstCode.Location = new Point(20, 84);
            lb_firstCode.Name = "lb_firstCode";
            lb_firstCode.Size = new Size(102, 19);
            lb_firstCode.TabIndex = 9;
            lb_firstCode.Text = "Первый код:";
            // 
            // lb_fileName
            // 
            lb_fileName.AutoSize = true;
            lb_fileName.Location = new Point(20, 36);
            lb_fileName.Name = "lb_fileName";
            lb_fileName.Size = new Size(54, 19);
            lb_fileName.TabIndex = 8;
            lb_fileName.Text = "Файл:";
            // 
            // tb_fileName
            // 
            tb_fileName.Location = new Point(80, 33);
            tb_fileName.Name = "tb_fileName";
            tb_fileName.ReadOnly = true;
            tb_fileName.Size = new Size(480, 26);
            tb_fileName.TabIndex = 7;
            // 
            // btn_loadDB
            // 
            btn_loadDB.Location = new Point(578, 25);
            btn_loadDB.Name = "btn_loadDB";
            btn_loadDB.Size = new Size(107, 47);
            btn_loadDB.TabIndex = 6;
            btn_loadDB.Text = "Загрузить";
            btn_loadDB.UseVisualStyleBackColor = true;
            btn_loadDB.Click += btn_loadDB_Click;
            // 
            // tb_productCode
            // 
            tb_productCode.Location = new Point(128, 81);
            tb_productCode.Name = "tb_productCode";
            tb_productCode.ReadOnly = true;
            tb_productCode.Size = new Size(480, 26);
            tb_productCode.TabIndex = 5;
            // 
            // tb_capsLockMode
            // 
            tb_capsLockMode.ForeColor = SystemColors.Window;
            tb_capsLockMode.Location = new Point(663, 17);
            tb_capsLockMode.Name = "tb_capsLockMode";
            tb_capsLockMode.ReadOnly = true;
            tb_capsLockMode.Size = new Size(36, 23);
            tb_capsLockMode.TabIndex = 12;
            // 
            // tb_currentLang
            // 
            tb_currentLang.ForeColor = SystemColors.Window;
            tb_currentLang.Location = new Point(663, 46);
            tb_currentLang.Name = "tb_currentLang";
            tb_currentLang.ReadOnly = true;
            tb_currentLang.Size = new Size(36, 23);
            tb_currentLang.TabIndex = 11;
            // 
            // pb_stage1
            // 
            pb_stage1.Image = (Image)resources.GetObject("pb_stage1.Image");
            pb_stage1.Location = new Point(408, 11);
            pb_stage1.Name = "pb_stage1";
            pb_stage1.Size = new Size(50, 50);
            pb_stage1.SizeMode = PictureBoxSizeMode.StretchImage;
            pb_stage1.TabIndex = 10;
            pb_stage1.TabStop = false;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Arial", 14.25F, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Point);
            label1.Location = new Point(331, 24);
            label1.Name = "label1";
            label1.Size = new Size(71, 22);
            label1.TabIndex = 9;
            label1.Text = "Этап 1";
            // 
            // btn_startPrint
            // 
            btn_startPrint.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            btn_startPrint.Location = new Point(18, 172);
            btn_startPrint.Name = "btn_startPrint";
            btn_startPrint.Size = new Size(179, 57);
            btn_startPrint.TabIndex = 15;
            btn_startPrint.Text = "Начать печать";
            btn_startPrint.UseVisualStyleBackColor = true;
            btn_startPrint.Click += btn_startPrint_Click;
            // 
            // btn_reprint
            // 
            btn_reprint.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            btn_reprint.Location = new Point(277, 172);
            btn_reprint.Name = "btn_reprint";
            btn_reprint.Size = new Size(179, 57);
            btn_reprint.TabIndex = 18;
            btn_reprint.Text = "Сбой";
            btn_reprint.UseVisualStyleBackColor = true;
            btn_reprint.Click += btn_reprint_Click;
            // 
            // btn_finishPrint
            // 
            btn_finishPrint.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            btn_finishPrint.Location = new Point(520, 172);
            btn_finishPrint.Name = "btn_finishPrint";
            btn_finishPrint.Size = new Size(179, 57);
            btn_finishPrint.TabIndex = 14;
            btn_finishPrint.Text = "Завершить печать";
            btn_finishPrint.UseVisualStyleBackColor = true;
            btn_finishPrint.Click += btn_finishPrint_Click;
            // 
            // gb_printerControls
            // 
            gb_printerControls.Controls.Add(btn_pausePrint);
            gb_printerControls.Controls.Add(btn_resumePrint);
            gb_printerControls.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            gb_printerControls.Location = new Point(18, 240);
            gb_printerControls.Name = "gb_printerControls";
            gb_printerControls.Size = new Size(681, 57);
            gb_printerControls.TabIndex = 19;
            gb_printerControls.TabStop = false;
            gb_printerControls.Text = "Управление принтером";
            // 
            // btn_pausePrint
            // 
            btn_pausePrint.Location = new Point(6, 22);
            btn_pausePrint.Name = "btn_pausePrint";
            btn_pausePrint.Size = new Size(330, 30);
            btn_pausePrint.TabIndex = 0;
            btn_pausePrint.Text = "Пауза";
            btn_pausePrint.UseVisualStyleBackColor = true;
            btn_pausePrint.Click += btn_pausePrint_Click;
            // 
            // btn_resumePrint
            // 
            btn_resumePrint.Location = new Point(345, 22);
            btn_resumePrint.Name = "btn_resumePrint";
            btn_resumePrint.Size = new Size(330, 30);
            btn_resumePrint.TabIndex = 1;
            btn_resumePrint.Text = "Снять с паузы";
            btn_resumePrint.UseVisualStyleBackColor = true;
            btn_resumePrint.Click += btn_resumePrint_Click;
            // 
            // pb_stage2
            // 
            pb_stage2.Image = (Image)resources.GetObject("pb_stage2.Image");
            pb_stage2.Location = new Point(406, 8);
            pb_stage2.Name = "pb_stage2";
            pb_stage2.Size = new Size(50, 50);
            pb_stage2.SizeMode = PictureBoxSizeMode.StretchImage;
            pb_stage2.TabIndex = 12;
            pb_stage2.TabStop = false;
            // 
            // gb_labelStar
            // 
            gb_labelStar.Controls.Add(btn_st2_verifyDB);
            gb_labelStar.Controls.Add(tb_labelStarCode);
            gb_labelStar.Font = new Font("Times New Roman", 12F, FontStyle.Bold, GraphicsUnit.Point);
            gb_labelStar.Location = new Point(18, 55);
            gb_labelStar.Name = "gb_labelStar";
            gb_labelStar.Size = new Size(679, 101);
            gb_labelStar.TabIndex = 11;
            gb_labelStar.TabStop = false;
            gb_labelStar.Text = "1. Считывание кода из LabelStar";
            // 
            // btn_st2_verifyDB
            // 
            btn_st2_verifyDB.Location = new Point(483, 25);
            btn_st2_verifyDB.Name = "btn_st2_verifyDB";
            btn_st2_verifyDB.Size = new Size(179, 57);
            btn_st2_verifyDB.TabIndex = 1;
            btn_st2_verifyDB.Text = "Проверка базы данных";
            btn_st2_verifyDB.UseVisualStyleBackColor = true;
            btn_st2_verifyDB.Click += btn_st2_verifyDB_Click;
            // 
            // tb_labelStarCode
            // 
            tb_labelStarCode.Location = new Point(14, 36);
            tb_labelStarCode.Name = "tb_labelStarCode";
            tb_labelStarCode.Size = new Size(444, 26);
            tb_labelStarCode.TabIndex = 0;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Arial", 14.25F, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Point);
            label2.Location = new Point(331, 21);
            label2.Name = "label2";
            label2.Size = new Size(71, 22);
            label2.TabIndex = 10;
            label2.Text = "Этап 2";
            // 
            // ControlForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(900, 980);
            Controls.Add(splitContainer);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ControlForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Проверка кодов";
            FormClosing += ControlForm_FormClosing;
            Load += ControlForm_Load;
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel1.PerformLayout();
            splitContainer.Panel2.ResumeLayout(false);
            splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            gb_productVerify.ResumeLayout(false);
            gb_loadDb.ResumeLayout(false);
            gb_loadDb.PerformLayout();
            gb_printerControls.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pb_stage1).EndInit();
            ((System.ComponentModel.ISupportInitialize)pb_stage2).EndInit();
            gb_labelStar.ResumeLayout(false);
            gb_labelStar.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer;
        private Button btn_st1_verifyDB;
        private GroupBox gb_productVerify;
        private Button btn_resetProduct;
        private Button btn_verifyProduct;
        private RichTextBox rb_productInfo;
        private GroupBox gb_loadDb;
        private TextBox tb_fileName;
        private Button btn_loadDB;
        private TextBox tb_productCode;
        private TextBox tb_capsLockMode;
        private TextBox tb_currentLang;
        private PictureBox pb_stage1;
        private Label label1;
        private Label label2;
        private PictureBox pb_stage2;
        private GroupBox gb_labelStar;
        private Button btn_st2_verifyDB;
        private TextBox tb_labelStarCode;
        private Label label4;
        private Label label3;
        private Button btn_finishPrint;
        private Button btn_startPrint;
        private Button btn_reprint;
        private GroupBox gb_printerControls;
        private Button btn_pausePrint;
        private Button btn_resumePrint;
        private Label lb_fileName;
        private Label lb_firstCode;
        // New controls added here
        private System.Windows.Forms.CheckBox chkPauseOnFail;
        private System.Windows.Forms.CheckBox chkCameraModuleEnabled;
        private System.Windows.Forms.DataGridView dgvStats;
        private System.Windows.Forms.RichTextBox rtbCameraLogs;
        private System.Windows.Forms.GroupBox gbCamera;
        private System.Windows.Forms.Label lblStatsTitle;
        private System.Windows.Forms.TableLayoutPanel sidebar;
        private System.Windows.Forms.TableLayoutPanel mainLayout;
    }
}
