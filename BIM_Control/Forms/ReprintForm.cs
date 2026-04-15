using BIM.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Drawing;

namespace BIM_Control.Forms
{
    public partial class ReprintForm : Form
    {
        //di
        private readonly IFileService _fileService;
        private readonly ILoggerService _loggerService;
        //private ICodeService codeService { get; set; } = default!;

        public ReprintForm(IFileService fileService, ILoggerService loggerService)
        {
            InitializeComponent();
            TrySetAppIcon();

            _fileService = fileService;
            _loggerService = loggerService;
            //codeService = Program.ServiceProvider.GetRequiredService<ICodeService>();
        }

        private void TrySetAppIcon()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "BIMv2.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
        }

        //private void btn_createFile_Click(object sender, EventArgs e)
        //{
        //    fileService.TakeCodesForReprint(fileService.LastPrintedCode);
        //    DialogResult = DialogResult.OK;
        //    logger.LogInformation("Файл с новыми кодами для LabelStar успешно пересоздан!");
        //    this.Close();
        //}

        //private void rbtn_continuePrint_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (rbtn_continuePrint.Checked)
        //    {
        //        gb_reprint.Enabled = true;
        //        logger.LogInformation("Выбран вариант продолжить печать файла с последнего кода после сбоя");
        //    }
        //    else gb_reprint.Enabled = false;
        //}

        //private void rbtn_resetPrint_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (rbtn_resetPrint.Checked)
        //    {
        //        logger.LogInformation("Выбран вариант печати файла сначала после сбоя");
        //        MessageBox.Show("Обратитесь к администратору!", "Внимание",
        //            MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        //    }
        //}

        private void btn_confirmChoice_Click(object sender, EventArgs e)
        {
            //if (rbtn_continuePrint.Checked)
            //{
            //    logger.LogInformation("Выбран вариант продолжить печать файла с последнего кода после сбоя");
            //    fileService.TakeCodesForReprint(fileService.LastPrintedCode);
            //    DialogResult = DialogResult.OK;
            //    MessageBox.Show("Файл с новыми кодами для LabelStar успешно пересоздан!", "Успешно",
            //        MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    this.Close();
            //}
            //else if (rbtn_resetPrint.Checked)
            //{
            //    logger.LogInformation("Выбран вариант печати файла сначала после сбоя");
            //    DialogResult = DialogResult.Cancel;
            //    this.Close();
            //}
        }
    }
}

