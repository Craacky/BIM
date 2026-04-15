using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BIM.Application.Common.Constants;

namespace BIM_Control.Forms
{
    public partial class ControlForm
    {


        private void SetupSidebarLayout()
        {
            bool isCameraModuleAvailable = !_offlinePrinterFlow && (_cameraService?.ModuleAvailable ?? false);

            // ===== НОВОЕ: Добавляем чекбокс для управления модулем камеры =====
            chkCameraModuleEnabled = new CheckBox
            {
                Text = "Модуль камеры включен",
                AutoSize = true,
                Location = new Point(10, 10),
                Checked = isCameraModuleAvailable,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Visible = !_offlinePrinterFlow,
                Enabled = !_offlinePrinterFlow
            };
            chkCameraModuleEnabled.CheckedChanged += ChkCameraModuleEnabled_CheckedChanged;
            this.splitContainer.Panel1.Controls.Add(chkCameraModuleEnabled);

            // ===== ВСЕГДА создаем mainLayout и sidebar, независимо от статуса модуля =====
            int width;
            if (_offlinePrinterFlow)
                width = 750; // offline printer flow - маленькая форма
            else if (isCameraModuleAvailable)
                width = 1150; // принтер + камера
            else
                width = 750; // только принтер, камера отключена
            
            this.Size = new Size(width, 760);
            this.splitContainer.SplitterDistance = 400;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            var originalSplit = this.splitContainer;
            this.Controls.Remove(originalSplit);

            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(5),
                Visible = true // ВСЕГДА видна! Скрываем только sidebar
            };
            mainLayout.SuspendLayout();
            mainLayout.ColumnStyles.Clear(); // Clear existing styles
            if (isCameraModuleAvailable)
            {
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            }
            else
            {
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0F));
            }
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            sidebar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10, 0, 0, 0),
                Visible = true // Sidebar is always logically visible; its physical display will be controlled by mainLayout.ColumnStyles width
            };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 226F));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            lblStatsTitle = new Label
            {
                Text = "ОБЛАСТЬ СТАТИСТИКИ",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGray,
                Margin = new Padding(0)
            };

            dgvStats = new DataGridView
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Control,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ScrollBars = ScrollBars.None,
                AllowUserToResizeColumns = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                Margin = new Padding(0)
            };
            dgvStats.RowTemplate.Height = 48;
            dgvStats.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            dgvStats.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 12, FontStyle.Bold);
            dgvStats.DefaultCellStyle.Font = new Font("Arial", 14);

            dgvStats.DefaultCellStyle.SelectionBackColor = dgvStats.DefaultCellStyle.BackColor;
            dgvStats.DefaultCellStyle.SelectionForeColor = dgvStats.DefaultCellStyle.ForeColor;
            dgvStats.SelectionChanged += (s, e) => dgvStats.ClearSelection();

            dgvStats.Columns[0].Name = "Показатель";
            dgvStats.Columns[0].SortMode = DataGridViewColumnSortMode.Programmatic;
            dgvStats.Columns[0].FillWeight = 72;

            dgvStats.Columns[1].Name = "Кол-во";
            dgvStats.Columns[1].SortMode = DataGridViewColumnSortMode.Programmatic;
            dgvStats.Columns[1].FillWeight = 28;
            dgvStats.Columns[1].DefaultCellStyle.Font = new Font("Arial", 16, FontStyle.Bold);
            dgvStats.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            dgvStats.ColumnHeaderMouseClick += (s, e) =>
            {
                var direction = dgvStats.SortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

                dgvStats.Rows.Cast<DataGridViewRow>()
                    .OrderBy(r => (string)r.Tag, direction == SortOrder.Ascending ? System.StringComparer.Ordinal : System.StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    .ForEach(r =>
                    {
                        var values = new object[dgvStats.ColumnCount];
                        var tag = r.Tag;
                        var color = r.Cells[1].Style.ForeColor;
                        for (int i = 0; i < dgvStats.ColumnCount; i++) values[i] = r.Cells[i].Value;

                        dgvStats.Rows.Remove(r);
                        int newIdx = dgvStats.Rows.Add(values);
                        dgvStats.Rows[newIdx].Tag = tag;
                        dgvStats.Rows[newIdx].Cells[1].Style.ForeColor = color;
                    });
            };

            int rowIdx;

            rowIdx = dgvStats.Rows.Add("Кол-во прочитанных кодов", "0");
            dgvStats.Rows[rowIdx].Cells[1].Style.ForeColor = Color.Green;
            dgvStats.Rows[rowIdx].Tag = "A_Good";

            rowIdx = dgvStats.Rows.Add("Всего прочитанных кодов", "0");
            dgvStats.Rows[rowIdx].Cells[1].Style.ForeColor = Color.Green;
            dgvStats.Rows[rowIdx].Tag = "A_Good";

            rowIdx = dgvStats.Rows.Add("Кол-во непрочитанных кодов", "0");
            dgvStats.Rows[rowIdx].Cells[1].Style.ForeColor = Color.Red;
            dgvStats.Rows[rowIdx].Tag = "B_Bad";

            rowIdx = dgvStats.Rows.Add("Кол-во открытий головы", "0");
            dgvStats.Rows[rowIdx].Cells[1].Style.ForeColor = Color.Red;
            dgvStats.Rows[rowIdx].Tag = "B_Bad";

            // Initialize the new checkbox
            chkPauseOnFail = new CheckBox
            {
                Text = "Пауза принтера при ошибке",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            gbCamera = new GroupBox
            {
                Text = "Данные с камеры",
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 0)
            };
            rtbCameraLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 14F),
                Text = "Ожидание данных..."
            };
            gbCamera.Controls.Add(rtbCameraLogs);

            sidebar.Controls.Add(lblStatsTitle, 0, 0);
            sidebar.Controls.Add(dgvStats, 0, 1);
            sidebar.Controls.Add(chkPauseOnFail, 0, 2);
            sidebar.Controls.Add(gbCamera, 0, 3);

            mainLayout.Controls.Add(originalSplit, 0, 0);
            mainLayout.Controls.Add(sidebar, 1, 0);

            this.Controls.Clear();
            this.Controls.Add(mainLayout);
            mainLayout.ResumeLayout(true); // Add this
            this.Invalidate(true); // Add this
        }
    }
}
