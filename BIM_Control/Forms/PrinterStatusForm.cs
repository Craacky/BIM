using System;
using System.Drawing;
using System.Windows.Forms;
using BIM_Control.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using BIM.Application.Common.Interfaces; // Added for ILoggerService
using System.Text;
using System.IO;

namespace BIM_Control.Forms
{
    public partial class PrinterStatusForm : Form
    {
        private readonly Services.PrinterMonitorService _printerMonitor;
        private readonly BIM.Application.Common.Configs.FolderSettings _folderSettings;
        private readonly BIM.Application.Common.Configs.LabelStarSettings _labelStarSettings;
        private readonly CameraService _cameraService; // New field
        private readonly ILoggerService _loggerService; // New field
        private System.Windows.Forms.Timer _checkerTimer; // New field for the timer
        private bool _fileGenerationCompleted = false; // Flag to track if file generation is completed
        private DateTime _fileGenerationCompletedAtUtc = DateTime.MinValue;
        private Services.PrinterStatusType _lastKnownPrinterStatus = Services.PrinterStatusType.Unknown;
        private DateTime _lastKnownPrinterStatusAtUtc = DateTime.MinValue;
        private bool _suppressAutoActivate = false;
        private bool _newFileActionStarted = false;

        public PrinterStatusForm(
            Services.PrinterMonitorService printerMonitor,
            BIM.Application.Common.Configs.FolderSettings folderSettings,
            BIM.Application.Common.Configs.LabelStarSettings labelStarSettings,
            CameraService cameraService, // Inject CameraService
            ILoggerService loggerService) // Inject ILoggerService
        {
            _printerMonitor = printerMonitor;
            _folderSettings = folderSettings;
            _labelStarSettings = labelStarSettings;
            _cameraService = cameraService; // Assign
            _loggerService = loggerService; // Assign
            InitializeComponent();
            TrySetAppIcon();
            btnContinue.Enabled = false; // Initially disable the continue button

            SetupCheckerTimer(); // Setup the timer
        }

        private void TrySetAppIcon()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "BIMv2.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
        }

        private void InitializeComponent()
        {
            // Form properties
            this.Text = "Статус принтера";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Create main layout - increased rows to accommodate multiple status labels
            TableLayoutPanel mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6, // 6 rows to include status labels
                Padding = new Padding(10)
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // Checkbox panel with datamatrix images
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // Status label for code matching
            mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Scanner values (takes remaining space)
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F)); // Generate file, Reset, and Clear buttons
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // Printer status (adjusted for 3 status labels)
            mainTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F)); // Continue button

            // Create a main panel to hold the centered content
            Panel checkboxMainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // Create checkbox panel with horizontal layout - centered inside the main panel
            FlowLayoutPanel checkboxPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0),
                // Center the flow panel in the parent
                Location = new Point(0, 0),
                // Align items in the center
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            // Center the flow panel horizontally in the main panel
            checkboxMainPanel.Resize += (s, e) =>
            {
                checkboxPanel.Location = new Point(
                    Math.Max(0, (checkboxMainPanel.Width - checkboxPanel.Width) / 2),
                    0
                );
            };

            // Create datamatrix picture boxes - make them read-only so user can't click them
            PictureBox picBox1 = new PictureBox
            {
                Width = 80,
                Height = 80,
                TabStop = false,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = Image.FromFile("Images/Datamatrix.svg.png"),
                BackColor = Color.Red
            };

            PictureBox picBox2 = new PictureBox
            {
                Width = 80,
                Height = 80,
                TabStop = false,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = Image.FromFile("Images/Datamatrix.svg.png"),
                BackColor = Color.Red
            };

            PictureBox picBox3 = new PictureBox
            {
                Width = 80,
                Height = 80,
                TabStop = false,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = Image.FromFile("Images/Datamatrix.svg.png"),
                BackColor = Color.Red
            };

            PictureBox picBox4 = new PictureBox
            {
                Width = 80,
                Height = 80,
                TabStop = false,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = Image.FromFile("Images/Datamatrix.svg.png"),
                BackColor = Color.Red
            };

            // Create reset codes button
            Button btnResetCodes = new Button
            {
                Text = "Сбросить коды",
                Size = new Size(220, 35),
                Margin = new Padding(5),
                FlatStyle = FlatStyle.Standard
            };

            // Create panels to hold each image with its label
            Panel panel1 = new Panel
            {
                Width = 100,
                Height = 120,
                Margin = new Padding(10)
            };
            Panel panel2 = new Panel
            {
                Width = 100,
                Height = 120,
                Margin = new Padding(10)
            };
            Panel panel3 = new Panel
            {
                Width = 100,
                Height = 120,
                Margin = new Padding(10)
            };
            Panel panel4 = new Panel
            {
                Width = 100,
                Height = 120,
                Margin = new Padding(10)
            };

            // Position pictures and labels within their panels - center the image
            picBox1.Location = new Point((panel1.Width - picBox1.Width) / 2, 5);
            Label lblCode1 = new Label
            {
                Text = "Код N",
                Location = new Point((panel1.Width - 60) / 2, 90),
                Size = new Size(60, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            panel1.Controls.AddRange(new Control[]
            {
                picBox1, lblCode1
            });

            picBox2.Location = new Point((panel2.Width - picBox2.Width) / 2, 5);
            Label lblCode2 = new Label
            {
                Text = "Код N",
                Location = new Point((panel2.Width - 60) / 2, 90),
                Size = new Size(60, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            panel2.Controls.AddRange(new Control[]
            {
                picBox2, lblCode2
            });

            picBox3.Location = new Point((panel3.Width - picBox3.Width) / 2, 5);
            Label lblCode3 = new Label
            {
                Text = "Код N",
                Location = new Point((panel3.Width - 60) / 2, 90),
                Size = new Size(60, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            panel3.Controls.AddRange(new Control[]
            {
                picBox3, lblCode3
            });

            picBox4.Location = new Point((panel4.Width - picBox4.Width) / 2, 5);
            Label lblCode4 = new Label
            {
                Text = "Код N",
                Location = new Point((panel4.Width - 60) / 2, 90),
                Size = new Size(60, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            panel4.Controls.AddRange(new Control[]
            {
                picBox4, lblCode4
            });

            checkboxPanel.Controls.AddRange(new Control[]
            {
                panel1, panel2, panel3, panel4
            });
            checkboxMainPanel.Controls.Add(checkboxPanel); // Add the flow panel to the main panel

            // Calculate the total width of all panels to center them properly
            int totalPanelsWidth = panel1.Width + panel2.Width + panel3.Width + panel4.Width + 60; // Add spacing between panels
            checkboxPanel.Width = totalPanelsWidth;
            checkboxPanel.Height = Math.Max(Math.Max(panel1.Height, panel2.Height), Math.Max(panel3.Height, panel4.Height));

            // Initially center the checkbox panel
            checkboxPanel.Location = new Point(Math.Max(0, (checkboxMainPanel.Width - checkboxPanel.Width) / 2), 0);

            // Create status label for code matching
            Label lblCodeStatus = new Label
            {
                Text = "Статус: Коды не найдены в файле",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.Red,
                Height = 30,
                Left = 2,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Panel statusLabelPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5)
            };
            statusLabelPanel.Controls.Add(lblCodeStatus);

            // Create textbox for scanner values
            TextBox txtScannerValues = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true, // Set to true as requested
                Dock = DockStyle.Fill,
                Text = "",
                Font = new Font("Consolas", 10F),
                TabStop = false // Don't allow tab focus to interfere
            };

            // Setup scanner timer
            _scanTimer = new System.Windows.Forms.Timer();
            _scanTimer.Interval = 200; // Wait 200ms after last keystroke
            _scanTimer.Tick += (s, e) =>
            {
                _scanTimer.Stop();
                // Get buffer content and remove all spaces (including the ones we added for Enter)
                string code = _inputBuffer.ToString().Replace(" ", "").Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    AddScannedCode(code);
                }
                _inputBuffer.Clear();
            };

            // Enable form-level key listening
            this.KeyPreview = true;
            this.KeyPress += (s, e) =>
            {
                // Reset the timer on every key press
                _scanTimer.Stop();
                _scanTimer.Start();

                if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Return)
                {
                    // Treat newline as a space separator to prevent splitting, allowing the buffer to accumulate
                    _inputBuffer.Append(' '); 
                    e.Handled = true;
                }
                else if (!char.IsControl(e.KeyChar) || e.KeyChar == (char)29)
                {
                    _inputBuffer.Append(e.KeyChar);
                }
            };

            // Create generate file button and reset codes button - side by side in panel
            Button btnGenerateFile = new Button
            {
                Text = "Сформировать новый файл",
                Size = new Size(220, 35),
                BackColor = SystemColors.ButtonFace,
                FlatStyle = FlatStyle.Standard,
                Anchor = AnchorStyles.None,
                Enabled = false // Initially disabled until all 4 codes are scanned
            };

            // Create a flow panel to hold both buttons side by side
            FlowLayoutPanel buttonFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // Center the buttons in the flow panel
            Panel leftSpacer = new Panel
            {
                Width = (buttonFlowPanel.Width - (220 * 2 + 10)) / 2,
                Dock = DockStyle.Left
            };
            Panel rightSpacer = new Panel
            {
                Width = Math.Max(0, (buttonFlowPanel.Width - (220 * 3 + 20)) / 2),
                Dock = DockStyle.Right
            };

            // Handle panel resize to keep buttons centered
            buttonFlowPanel.Resize += (s, e) =>
            {
                leftSpacer.Width = Math.Max(0, (buttonFlowPanel.Width - (220 * 2 + 10)) / 2);
                rightSpacer.Width = Math.Max(0, (buttonFlowPanel.Width - (220 * 2 + 10)) / 2);
            };

            buttonFlowPanel.Controls.AddRange(new Control[]
            {
                leftSpacer, btnGenerateFile, btnResetCodes, rightSpacer
            });

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5)
            };
            buttonPanel.Controls.Add(buttonFlowPanel);

            // Create a TableLayoutPanel for the status panel to properly arrange multiple status labels
            TableLayoutPanel statusPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3, // 3 status labels (removed ribbon status)
                Margin = new Padding(0, 5, 0, 5)
            };

            // Set row styles to evenly distribute space among the 3 status labels
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F)); // ~33% for each label
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));

            // Create main printer status label (head status)
            Label lblPrinterStatus = new Label
            {
                Name = "lblPrinterStatus",
                Text = "Статус головы принтера: Неизвестно",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.Black,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Create additional status labels in the same style
            Label lblPrinterState = new Label
            {
                Name = "lblPrinterState",
                Text = "Состояние принтера: Неизвестно",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.Black,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblPaperStatus = new Label
            {
                Name = "lblPaperStatus",
                Text = "Статус бумаги: Неизвестно",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.Black,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Add labels to the table layout panel in order
            statusPanel.Controls.Add(lblPrinterStatus, 0, 0); // Row 0
            statusPanel.Controls.Add(lblPrinterState, 0, 1); // Row 1
            statusPanel.Controls.Add(lblPaperStatus, 0, 2); // Row 2

            // Create continue button - stretched across the full width with margins
            Button btnContinue = new Button
            {
                Text = "Продолжить работу",
                Height = 45, // Fixed height
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnContinue.FlatAppearance.BorderSize = 0;
            btnContinue.FlatStyle = FlatStyle.Flat;
            btnContinue.FlatAppearance.BorderColor = Color.Green;

            Panel continueButtonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };

            // Stretch the button across the full width with margins
            continueButtonPanel.Resize += (s, e) =>
            {
                btnContinue.Width = continueButtonPanel.Width - 20; // Full width minus margins
                btnContinue.Location = new Point(10, (continueButtonPanel.Height - btnContinue.Height) / 2); // Positioned with left margin, centered vertically
            };

            continueButtonPanel.Controls.Add(btnContinue);

            // Add controls to table
            mainTable.Controls.Add(checkboxMainPanel, 0, 0);
            mainTable.Controls.Add(statusLabelPanel, 0, 1);
            mainTable.Controls.Add(txtScannerValues, 0, 2);
            mainTable.Controls.Add(buttonPanel, 0, 3);
            mainTable.Controls.Add(statusPanel, 0, 4);
            mainTable.Controls.Add(continueButtonPanel, 0, 5);

            // Add main table to form
            this.Controls.Add(mainTable);

            // Store references to controls for later use
            this.picBox1 = picBox1;
            this.picBox2 = picBox2;
            this.picBox3 = picBox3;
            this.picBox4 = picBox4;
            this.lblCode1 = lblCode1;
            this.lblCode2 = lblCode2;
            this.lblCode3 = lblCode3;
            this.lblCode4 = lblCode4;
            this.txtScannerValues = txtScannerValues;
            this.btnGenerateFile = btnGenerateFile;
            this.lblPrinterStatus = lblPrinterStatus;
            this.lblPrinterState = lblPrinterState;
            this.lblPaperStatus = lblPaperStatus;
            this.btnContinue = btnContinue;
            this.lblCodeStatus = lblCodeStatus;
            this.btnResetCodes = btnResetCodes;

            // The event handler for btnContinue is set in the parent form (ControlForm)
            // So we don't set it here to avoid conflicts

            // Set up the reset button event
            btnResetCodes.Click += (s, e) =>
            {
                _loggerService?.LogInformation($"PrinterStatusForm: ResetCodes click (enabled={btnResetCodes.Enabled}, visible={btnResetCodes.Visible}, focused={this.Focused})");
                ResetCodes();
            };

            // Set up the generate file button event
            btnGenerateFile.Click += async (s, e) => await GenerateNewFileAsync();

            // Prevent form from closing when user clicks X - only allow closing via Continue button
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true; // Cancel the close if user clicked X
                }
                else
                {
                    // When form is closed programmatically (e.g., via Dispose), enable parent if it exists
                    Form parent = this.Owner;
                    if (parent != null && !parent.IsDisposed)
                    {
                        parent.Enabled = true;
                    }
                    // Reset the file generation flag when form is closing
                    _fileGenerationCompleted = false;
                    _fileGenerationCompletedAtUtc = DateTime.MinValue;
                }
            };

            // Keep the form on top when it loses focus (for modal behavior)
            this.Deactivate += (s, e) =>
            {
                if (_suppressAutoActivate || this.Disposing || this.IsDisposed || !this.Visible)
                {
                    return;
                }

                if (this.Owner != null && !this.Owner.IsDisposed && !this.Owner.Focused)
                {
                    this.Activate();
                }
            };

            // No longer need to focus the textbox since we use KeyPreview
            this.Load += PrinterStatusForm_Load; // Add Load event handler
        }

        private void SetupCheckerTimer()
        {
            _checkerTimer = new System.Windows.Forms.Timer();
            _checkerTimer.Interval = 2000; // Check every 2000ms to reduce UI load
            _checkerTimer.Tick += _checkerTimer_Tick;
        }

        private void PrinterStatusForm_Load(object sender, EventArgs e)
        {
            _checkerTimer.Start(); // Start the timer when the form loads
        }

        private async void _checkerTimer_Tick(object sender, EventArgs e)
        {
            await UpdateContinueButtonStateAsync();
        }

        private async Task UpdateContinueButtonStateAsync()
        {
            // Only check if we have 4 codes and need to validate them
            if (_scannedCodes.Count == 4 && lblCodeStatus.Text.Contains("Коды в файле последовательны и образуют полную четверку") && lblCodeStatus.ForeColor == Color.Green)
            {
                // Check for duplicates in the 4 scanned codes
                var duplicatesInScannedCodes = _cameraService.GetDatabase().CheckDuplicatesInList(_scannedCodes);
                if (duplicatesInScannedCodes.Count > 0)
                {
                    lblCodeStatus.Text = $"Статус: ОШИБКА - Дубликаты в 4 кодах: {string.Join(", ", duplicatesInScannedCodes)}";
                    lblCodeStatus.ForeColor = Color.Red;
                    btnContinue.Enabled = false;
                    return;
                }
            }

            // Condition: printer must be strictly "Ready" (Normal state)
            bool printerIsReady = false;
            bool liveStatusReceived = false;
            try
            {
                var printerStatus = await _printerMonitor.GetStatusAsync();
                if (printerStatus != null && printerStatus.State.Status == Services.PrinterStatusType.Normal)
                {
                    liveStatusReceived = true;
                    printerIsReady = true;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Ошибка при проверке статуса принтера в PrinterStatusForm: {ex.Message}");
            }

            // Fallback after successful file generation:
            // 1) use last known status from UI updates (fresh window),
            // 2) after timeout do not block operator forever.
            if (!liveStatusReceived && _fileGenerationCompleted)
            {
                bool hasFreshCachedStatus = _lastKnownPrinterStatusAtUtc != DateTime.MinValue &&
                                            (DateTime.UtcNow - _lastKnownPrinterStatusAtUtc).TotalSeconds <= 90;
                if (hasFreshCachedStatus &&
                    _lastKnownPrinterStatus == Services.PrinterStatusType.Normal)
                {
                    printerIsReady = true;
                }
            }

            // Enable/disable the continue button based on all required conditions
            // Now the button should be enabled only after file generation is completed AND printer is ready
            bool shouldEnable = _fileGenerationCompleted && printerIsReady;
            btnContinue.Enabled = shouldEnable;
        }

        // Public properties to access controls from outside
        public PictureBox picBox1 { get; private set; }
        public PictureBox picBox2 { get; private set; }
        public PictureBox picBox3 { get; private set; }
        public PictureBox picBox4 { get; private set; }
        public Label lblCode1 { get; private set; }
        public Label lblCode2 { get; private set; }
        public Label lblCode3 { get; private set; }
        public Label lblCode4 { get; private set; }
        public TextBox txtScannerValues { get; private set; }
        public Button btnGenerateFile { get; private set; }
        public Label lblPrinterStatus { get; private set; }
        public Label lblPrinterState { get; private set; } // Printer state status
        public Label lblPaperStatus { get; private set; } // Paper status
        public Label lblCodeStatus { get; private set; }
        public Button btnContinue { get; private set; }
        public Button btnResetCodes { get; private set; }

        public void PrepareForProgrammaticClose()
        {
            _suppressAutoActivate = true;
        }




        // Method to update printer status from external sources
        public void UpdatePrinterStatus(string statusText, Color textColor)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdatePrinterStatus(statusText, textColor)));
            }
            else
            {
                lblPrinterStatus.Text = statusText;
                lblPrinterStatus.ForeColor = textColor;
            }
        }

        // Method to update all printer status information
        public void UpdateDetailedStatus(Services.PrinterStatus printerStatus)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateDetailedStatus(printerStatus)));
            }
            else
            {
                // Update the printer state status
                string stateText = "";
                Color stateColor = Color.Black;

                switch (printerStatus.State.Status)
                {
                    case Services.PrinterStatusType.Normal:
                        stateText = "Состояние принтера: Готов";
                        stateColor = Color.Green;
                        break;
                    case Services.PrinterStatusType.Printing:
                        stateText = "Состояние принтера: Печать";
                        stateColor = Color.Blue;
                        _loggerService?.LogInformation("PrinterStatusForm: статус принтера = Печать");
                        // PATCH-BEGIN: PrintWhileOpenLog
                        if (!_codesResetSinceOpen)
                        {
                            _loggerService?.LogWarning("PrinterStatusForm: принтер печатает, форма статуса открыта, сброс кодов не выполнен");
                        }
                        // PATCH-END: PrintWhileOpenLog
                        break;
                    case Services.PrinterStatusType.HeadOpen:
                        stateText = "Состояние принтера: Голова открыта";
                        stateColor = Color.Red;
                        break;
                    case Services.PrinterStatusType.PaperJam:
                        stateText = "Состояние принтера: Замятие бумаги";
                        stateColor = Color.Red;
                        break;
                    case Services.PrinterStatusType.PaperOut:
                        stateText = "Состояние принтера: Нет бумаги";
                        stateColor = Color.Red;
                        break;
                    case Services.PrinterStatusType.RibbonOut:
                        stateText = "Состояние принтера: Нет ленты";
                        stateColor = Color.Red;
                        break;
                    case Services.PrinterStatusType.Paused:
                        stateText = "Состояние принтера: На паузе";
                        stateColor = Color.Orange;
                        break;
                    default:
                        stateText = "Состояние принтера: Неизвестно";
                        stateColor = Color.Gray;
                        break;
                }

                this.lblPrinterState.Text = stateText;
                this.lblPrinterState.ForeColor = stateColor;

                // Update paper status
                string paperText = "";
                Color paperColor = Color.Black;

                if (printerStatus.State.Status == Services.PrinterStatusType.PaperOut)
                {
                    paperText = "Статус бумаги: Нет бумаги";
                    paperColor = Color.Red;
                }
                else if (printerStatus.State.Status == Services.PrinterStatusType.PaperJam)
                {
                    paperText = "Статус бумаги: Замятие";
                    paperColor = Color.Red;
                }
                else
                {
                    paperText = "Статус бумаги: В наличии";
                    paperColor = Color.Green;
                }

                this.lblPaperStatus.Text = paperText;
                this.lblPaperStatus.ForeColor = paperColor;

                _lastKnownPrinterStatus = printerStatus.State.Status;
                _lastKnownPrinterStatusAtUtc = DateTime.UtcNow;
            }
        }

        // Properties to store expected codes and scanned codes
        private string[] _expectedCodes = new string[4];
        private List<string> _scannedCodes = new List<string>();
        private System.Text.StringBuilder _inputBuffer = new System.Text.StringBuilder();
        private System.Windows.Forms.Timer _scanTimer; // Timer for scanner accumulation
        private int _minFoundIndex = -1;
        // PATCH-BEGIN: PrintWhileOpenLog
        private bool _codesResetSinceOpen = false;
        // PATCH-END: PrintWhileOpenLog
        // PATCH-BEGIN: LastCodesEndOfFile
        private bool _lastCodesEndOfFileHandled = false;
        // PATCH-END: LastCodesEndOfFile

        // PATCH-BEGIN: RequestContinueEvent
        public event EventHandler RequestContinue;
        // PATCH-END: RequestContinueEvent
        // PATCH-BEGIN: AsyncValidate
        private CancellationTokenSource _validateCodesCts = null;
        private int _validateCodesVersion = 0;
        // PATCH-END: AsyncValidate

        // Method to add a scanned code to the display
        public void AddScannedCode(string code)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AddScannedCode(code)));
                return;
            }

            if (_scannedCodes.Count >= 4)
            {
                return;
            }

            string trimmedCode = code.Trim();

            // Check if the code is the same as any already scanned code
            if (_scannedCodes.Contains(trimmedCode))
            {
                return;
            }

            _scannedCodes.Add(trimmedCode);
            _loggerService?.LogDebug($"PrinterStatusForm: отсканирован код, всего {_scannedCodes.Count}/4");

            // Refresh the display with formatted lines
            UpdateTextBoxDisplay();

            // Update corresponding picture box color
            UpdateDatamatrixImageColors();

            // Update status label
            if (_scannedCodes.Count == 4)
            {
                _ = ValidateCodesInFileAsync();
            }
            else
            {
                lblCodeStatus.Text = $"Статус: Ожидание кодов ({_scannedCodes.Count}/4)";
                lblCodeStatus.ForeColor = Color.Orange;
            }
        }

        private async Task ValidateCodesInFileAsync()
        {
            // PATCH-BEGIN: AsyncValidate
            CancelValidateCodes();
            var cts = new CancellationTokenSource();
            _validateCodesCts = cts;
            int version = ++_validateCodesVersion;
            // PATCH-END: AsyncValidate

            try
            {
                _loggerService?.LogInformation("PrinterStatusForm: начало проверки 4 кодов в файле LabelStar");

                string filePath = System.IO.Path.Combine(_folderSettings.Path, _labelStarSettings.FileName);

                if (!System.IO.File.Exists(filePath))
                {
                    _loggerService?.LogWarning($"PrinterStatusForm: файл LabelStar не найден: {filePath}");
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = $"Статус: Файл {_labelStarSettings.FileName} не найден";
                            lblCodeStatus.ForeColor = Color.Red;
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = $"Статус: Файл {_labelStarSettings.FileName} не найден";
                        lblCodeStatus.ForeColor = Color.Red;
                    }
                    return;
                }


                // Stream file to avoid loading entire content into memory
                var targets = _scannedCodes
                    .Select(code => new { Original = code, Normalized = CleanBarcode(code) })
                    .ToList();

                // PATCH-BEGIN: AsyncValidate
                var result = await Task.Run(() =>
                {
                    var remaining = new HashSet<string>(targets.Select(t => t.Normalized), StringComparer.OrdinalIgnoreCase);
                    var foundIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    int lineNumber = 0;
                    using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            cts.Token.ThrowIfCancellationRequested();
                            lineNumber++;
                            if (remaining.Count > 0)
                            {
                                var normalizedLine = CleanBarcode(line);
                                if (remaining.Contains(normalizedLine))
                                {
                                    foundIndices[normalizedLine] = lineNumber;
                                    remaining.Remove(normalizedLine);
                                }
                            }
                        }
                    }

                    return (remaining, foundIndices, totalLines: lineNumber);
                }, cts.Token);

                if (cts.IsCancellationRequested || version != _validateCodesVersion)
                {
                    return;
                }

                var remaining = result.remaining;
                var foundIndices = result.foundIndices;
                int totalLines = result.totalLines;
                // PATCH-END: AsyncValidate

                if (remaining.Count > 0)
                {
                    var missing = targets.First(t => remaining.Contains(t.Normalized)).Original;
                    _loggerService?.LogWarning("PrinterStatusForm: один или несколько кодов не найдены в файле");
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = "Статус: Коды не принадлежат текущему файлу";
                            lblCodeStatus.ForeColor = Color.Red;
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = "Статус: Коды не принадлежат текущему файлу";
                        lblCodeStatus.ForeColor = Color.Red;
                    }
                    return;
                }

                var results = new System.Collections.Generic.List<(string code, int index)>();
                foreach (var target in targets)
                {
                    results.Add((target.Original, foundIndices[target.Normalized]));
                }

                // Sort results by index
                results = results.OrderBy(r => r.index).ToList();

                int minIdx = results[0].index;
                int maxIdx = results[3].index;
                _loggerService?.LogDebug($"PrinterStatusForm: индексы найденных кодов {minIdx}-{maxIdx}");

                // PATCH-BEGIN: LastCodesEndOfFile
                if (!_lastCodesEndOfFileHandled && maxIdx >= totalLines)
                {
                    _lastCodesEndOfFileHandled = true;
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = "Статус: Последние коды файла";
                            lblCodeStatus.ForeColor = Color.DarkOrange;
                            btnGenerateFile.Enabled = false;
                            MessageBox.Show(
                                this,
                                "Отсканированы последние коды файла.\n\n" +
                                "Закройте голову принтера, затем нажмите ОК и завершите задание.",
                                "Последние коды файла",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            RequestContinue?.Invoke(this, EventArgs.Empty);
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = "Статус: Последние коды файла";
                        lblCodeStatus.ForeColor = Color.DarkOrange;
                        btnGenerateFile.Enabled = false;
                        MessageBox.Show(
                            this,
                            "Отсканированы последние коды файла.\n\n" +
                            "Закройте голову принтера, затем нажмите ОК и завершите задание.",
                            "Последние коды файла",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        RequestContinue?.Invoke(this, EventArgs.Empty);
                    }
                    return;
                }
                // PATCH-END: LastCodesEndOfFile


                // Update the textbox with sorted results immediately (even if not sequential)
                List<string> displayLines = new List<string>();
                for (int i = 0; i < results.Count; i++)
                {
                    displayLines.Add($"{results[i].code} - Номер {results[i].index}");
                }
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        txtScannerValues.Lines = displayLines.ToArray();
                    }));
                }
                else
                {
                    txtScannerValues.Lines = displayLines.ToArray();
                }

                if (maxIdx - minIdx == 3) // 4 consecutive lines
                {
                    // Additional check: verify that the 4 codes form a complete group (indices divisible by 4)
                    // For example: codes at positions 1,2,3,4 (0-based: 0,1,2,3) or 5,6,7,8 (0-based: 4,5,6,7) are valid
                    // But codes at positions 2,3,4,5 (0-based: 1,2,3,4) are invalid (half of one group + half of another)

                    // Convert to 0-based indexing for validation
                    int minIdxZeroBased = minIdx - 1; // Convert to 0-based index

                    if (minIdxZeroBased % 4 == 0) // Valid starting position for a complete group
                    {
                        _loggerService?.LogInformation("PrinterStatusForm: коды последовательны и образуют полную четверку");
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                lblCodeStatus.Text = "Статус: Коды в файле последовательны и образуют полную четверку";
                                lblCodeStatus.ForeColor = Color.Green;

                                // Update labels under DataMatrix sequentially left to right with file indices
                                lblCode1.Text = $"Код {results[0].index}";
                                lblCode2.Text = $"Код {results[1].index}";
                                lblCode3.Text = $"Код {results[2].index}";
                                lblCode4.Text = $"Код {results[3].index}";

                                _minFoundIndex = minIdx;
                                btnGenerateFile.Enabled = !_newFileActionStarted;

                                // Scroll to the end
                                txtScannerValues.SelectionStart = txtScannerValues.Text.Length;
                                txtScannerValues.ScrollToCaret();
                            }));
                        }
                        else
                        {
                            lblCodeStatus.Text = "Статус: Коды в файле последовательны и образуют полную четверку";
                            lblCodeStatus.ForeColor = Color.Green;

                            // Update labels under DataMatrix sequentially left to right with file indices
                            lblCode1.Text = $"Код {results[0].index}";
                            lblCode2.Text = $"Код {results[1].index}";
                            lblCode3.Text = $"Код {results[2].index}";
                            lblCode4.Text = $"Код {results[3].index}";

                            _minFoundIndex = minIdx;
                            btnGenerateFile.Enabled = !_newFileActionStarted;

                            // Scroll to the end
                            txtScannerValues.SelectionStart = txtScannerValues.Text.Length;
                            txtScannerValues.ScrollToCaret();
                        }
                    }
                    else
                    {
                        _loggerService?.LogWarning("PrinterStatusForm: коды последовательны, но не образуют полную четверку");
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                lblCodeStatus.Text = $"Статус: Коды последовательны, но не образуют полную четверку (начало на строке {minIdx})";
                                lblCodeStatus.ForeColor = Color.Orange; // Warning color
                                btnGenerateFile.Enabled = false;
                            }));
                        }
                        else
                        {
                            lblCodeStatus.Text = $"Статус: Коды последовательны, но не образуют полную четверку (начало на строке {minIdx})";
                            lblCodeStatus.ForeColor = Color.Orange; // Warning color
                            btnGenerateFile.Enabled = false;
                        }
                    }
                }
                else
                {
                    _loggerService?.LogWarning("PrinterStatusForm: коды не последовательны");
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = $"Статус: Коды не последовательны (строки {minIdx}-{maxIdx})";
                            lblCodeStatus.ForeColor = Color.Red;
                            btnGenerateFile.Enabled = false;
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = $"Статус: Коды не последовательны (строки {minIdx}-{maxIdx})";
                        lblCodeStatus.ForeColor = Color.Red;
                        btnGenerateFile.Enabled = false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore cancellation
            }
            catch (Exception ex)
            {
                _loggerService?.LogError($"PrinterStatusForm: ошибка проверки файла LabelStar: {ex.Message}");
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblCodeStatus.Text = "Ошибка при проверке файла: " + ex.Message;
                        lblCodeStatus.ForeColor = Color.Red;
                    }));
                }
                else
                {
                    lblCodeStatus.Text = "Ошибка при проверке файла: " + ex.Message;
                    lblCodeStatus.ForeColor = Color.Red;
                }
            }
        }

        // PATCH-BEGIN: AsyncValidate
        private void CancelValidateCodes()
        {
            try
            {
                _validateCodesCts?.Cancel();
            }
            catch
            {
                // ignore cancellation race
            }
        }
        // PATCH-END: AsyncValidate

        private async Task GenerateNewFileAsync()
        {
            if (_minFoundIndex < 1) return;

            // Prevent multiple clicks by disabling action buttons immediately
            if (!btnGenerateFile.Enabled || _newFileActionStarted) return; // Already processing

            _newFileActionStarted = true;
            btnGenerateFile.Enabled = false;
            btnResetCodes.Enabled = false;

            LoadingDialog loadingDialog = null;
            DateTime lastUiUpdateAt = DateTime.MinValue;
            void ThrottledUiUpdate(Action uiAction)
            {
                var now = DateTime.UtcNow;
                if ((now - lastUiUpdateAt).TotalMilliseconds < 750)
                {
                    return;
                }
                lastUiUpdateAt = now;
                if (this.InvokeRequired)
                {
                    this.Invoke(uiAction);
                }
                else
                {
                    uiAction();
                }
            }

            try
            {
                _loggerService?.LogInformation("PrinterStatusForm: начало формирования нового файла LabelStar");

                loadingDialog = new LoadingDialog("Подготовка к формированию нового файла...");
                loadingDialog.TopMost = true;
                loadingDialog.ShowInTaskbar = false;
                loadingDialog.StartPosition = FormStartPosition.Manual;

                Rectangle parentBounds = this.Bounds;
                int x = parentBounds.X + (parentBounds.Width - loadingDialog.Width) / 2;
                int y = parentBounds.Y + (parentBounds.Height - loadingDialog.Height) / 2;
                loadingDialog.Location = new Point(x, y);
                loadingDialog.Show(this);
                loadingDialog.BringToFront();
                Application.DoEvents();

                string originalFilePath = System.IO.Path.Combine(_folderSettings.Path, _labelStarSettings.FileName);

            if (!System.IO.File.Exists(originalFilePath))
            {
                _loggerService?.LogError($"PrinterStatusForm: исходный файл не найден: {originalFilePath}");

                    // Close loading dialog before showing error
                    if (loadingDialog != null && !loadingDialog.IsDisposed)
                    {
                        loadingDialog.Close();
                    }

                MessageBox.Show(this, "Исходный файл не найден!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGenerateFile.Enabled = false;
                return;
            }

                // Update loading dialog message
                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    ThrottledUiUpdate(() => loadingDialog.UpdateMessage("Чтение исходного файла..."));
                }

                // Create backup name
                string fileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(originalFilePath);
                string extension = System.IO.Path.GetExtension(originalFilePath);
                string backupPath = System.IO.Path.Combine(_folderSettings.Path, $"{fileNameNoExt}_backup_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
                _loggerService?.LogDebug($"PrinterStatusForm: путь бэкапа: {backupPath}");


                // Update loading dialog message
                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    ThrottledUiUpdate(() => loadingDialog.UpdateMessage("Создание резервной копии и формирование нового файла..."));
                }

                // Perform file operations asynchronously where possible
                // Truncate: skip lines up to the LAST found code (maxIdx).
                int codesToSkip = _minFoundIndex + 3; // Skips the 4 codes found (idx, idx+1, idx+2, idx+3)

                var fileStats = await Task.Run(() =>
                {
                    // Write to a temp file first to avoid losing the original on failure
                    string tempPath = System.IO.Path.Combine(_folderSettings.Path, $"{fileNameNoExt}_temp_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");

                    // Stream read/write to avoid loading the entire file into memory
                    int totalLines = 0;
                    int writtenLines = 0;

                    using (var reader = new StreamReader(originalFilePath, Encoding.UTF8, true))
                    using (var writer = new StreamWriter(tempPath, false, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            totalLines++;
                            if (totalLines <= codesToSkip) continue;
                            writer.WriteLine(line);
                            writtenLines++;
                        }
                    }

                    // Move original to backup only after temp file is created successfully
                    System.IO.File.Move(originalFilePath, backupPath);

                    try
                    {
                        // Promote temp to original file path
                        System.IO.File.Move(tempPath, originalFilePath);
                    }
                    catch
                    {
                        // Attempt to restore original if temp promotion fails
                        try
                        {
                            if (System.IO.File.Exists(backupPath))
                            {
                                System.IO.File.Move(backupPath, originalFilePath);
                            }
                        }
                        catch
                        {
                            // Swallow to preserve original exception
                        }
                        throw;
                    }
                    finally
                    {
                        // Clean up temp if it still exists
                        try
                        {
                            if (System.IO.File.Exists(tempPath))
                            {
                                System.IO.File.Delete(tempPath);
                            }
                        }
                        catch
                        {
                            // Best-effort cleanup
                        }
                    }

                    return (totalLines, writtenLines);
                });
                _loggerService?.LogInformation($"PrinterStatusForm: новый файл сформирован (строк всего={fileStats.totalLines}, записано={fileStats.writtenLines})");


                // Update loading dialog message
                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    ThrottledUiUpdate(() => loadingDialog.UpdateMessage("Очистка базы данных камеры..."));
                }

                // ============================================
                // ОЧИСТКА БАЗЫ ДАННЫХ КАМЕРЫ (удаление кодов после 4 отсканированных)
                // ============================================
                try
                {
                    _loggerService?.LogInformation("PrinterStatusForm: очистка БД камеры после формирования файла");
                    int anchorSequence = _minFoundIndex + 3; // keep 4 validated codes, remove everything after them
                    int beforeCount = _cameraService.GetDatabase().GetCodesCount();
                    int deleted = 0;
                    await Task.Run(() => deleted = _cameraService.GetDatabase().DeleteCodesAfterSequence(anchorSequence));
                    int afterCount = _cameraService.GetDatabase().GetCodesCount();
                    _loggerService?.LogInformation($"PrinterStatusForm: очистка БД камеры выполнена. anchorSequence={anchorSequence}, before={beforeCount}, deleted={deleted}, after={afterCount}");
                }
                catch (Exception ex)
                {
                    _loggerService?.LogError($"PrinterStatusForm: ошибка очистки БД камеры: {ex.Message}");
                    _loggerService.LogError($"Ошибка при очистке базы данных камеры: {ex.Message}");
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = "Статус: Ошибка очистки БД камеры. Перезагрузка принтера отменена.";
                            lblCodeStatus.ForeColor = Color.DarkRed;
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = "Статус: Ошибка очистки БД камеры. Перезагрузка принтера отменена.";
                        lblCodeStatus.ForeColor = Color.DarkRed;
                    }
                    return;
                }

                // Update loading dialog message
                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    ThrottledUiUpdate(() => loadingDialog.UpdateMessage("Подготовка очистки очереди и перезагрузки принтера..."));
                }

                // Update UI on UI thread
                ThrottledUiUpdate(() =>
                {
                    lblCodeStatus.Text = "Статус: Новый файл сформирован. Выполняется очистка очереди и перезагрузка принтера...";
                    lblCodeStatus.ForeColor = Color.DarkGreen;
                });

                // ============================================
                // ПЕРЕЗАГРУЗКА ПРИНТЕРА (Только после успеха)
                // ============================================

                // 1. Режим мониторинга больше не переключаем. Освобождение порта выполняется внутри сервиса.

                // Update loading dialog message
                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    ThrottledUiUpdate(() => loadingDialog.UpdateMessage("Очистка очереди принтера..."));
                }

                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    ThrottledUiUpdate(() => loadingDialog.UpdateMessage("Очистка очереди и перезагрузка принтера..."));
                }

                // PATCH-BEGIN: UnifiedPrinterRecoveryWorkflow
                var recoveryResult = await _printerMonitor.RunQueueClearAndRebootWorkflowAsync(
                    caller: "PrinterStatusForm.GenerateNewFile",
                    onlineTimeoutMs: 45000,
                    onlinePollIntervalMs: 400,
                    continueWhenSpoolerNotConfirmed: true);

                if (!recoveryResult.SpoolerCleared)
                {
                    _loggerService?.LogWarning("PrinterStatusForm: очередь печати Windows не подтверждена как очищенная, продолжаем по сценарию.");
                }

                if (!recoveryResult.RebootCommandSent)
                {
                    _loggerService?.LogError("PrinterStatusForm: не удалось отправить команду перезагрузки принтера");
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = "Статус: Ошибка перезагрузки принтера. Проверьте подключение и попробуйте снова.";
                            lblCodeStatus.ForeColor = Color.DarkRed;
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = "Статус: Ошибка перезагрузки принтера. Проверьте подключение и попробуйте снова.";
                        lblCodeStatus.ForeColor = Color.DarkRed;
                    }
                    return;
                }

                if (!recoveryResult.PrinterBackOnline)
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            lblCodeStatus.Text = "Статус: Принтер не отвечает после перезагрузки.";
                            lblCodeStatus.ForeColor = Color.DarkRed;
                        }));
                    }
                    else
                    {
                        lblCodeStatus.Text = "Статус: Принтер не отвечает после перезагрузки.";
                        lblCodeStatus.ForeColor = Color.DarkRed;
                    }
                    btnGenerateFile.Enabled = false;
                    return;
                }
                // PATCH-END: UnifiedPrinterRecoveryWorkflow

                // 5. Режим уже активный и сохраняется до завершения приложения.

                _loggerService?.LogInformation("PrinterStatusForm: формирование файла и перезагрузка принтера завершены");


                // Set the flag to indicate that file generation is completed
                _fileGenerationCompleted = true;
                _fileGenerationCompletedAtUtc = DateTime.UtcNow;

                // Keep the button locked after successful generation
                btnGenerateFile.Enabled = false;
            }
            catch (Exception ex)
            {
                _loggerService?.LogError($"PrinterStatusForm: ошибка при формировании файла: {ex.Message}");

                // Show error message on UI thread
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show(this, $"Ошибка при формировании файла: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        btnGenerateFile.Enabled = false;
                    }));
                }
                else
                {
                    MessageBox.Show(this, $"Ошибка при формировании файла: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnGenerateFile.Enabled = false;
                }
            }
            finally
            {
                if (loadingDialog != null && !loadingDialog.IsDisposed)
                {
                    try
                    {
                        loadingDialog.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Helper to remove invisible/control characters (like GS separator) from barcode strings
        private string CleanBarcode(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Remove control characters (characters with ASCII code < 32, which includes GS (29), CR (13), LF (10))
            return new string(input.Where(c => c >= 32).ToArray()).Trim();
        }

        private void UpdateTextBoxDisplay()
        {
            List<string> displayLines = new List<string>();
            for (int i = 0; i < _scannedCodes.Count; i++)
            {
                displayLines.Add($"{_scannedCodes[i]} - Номер {i + 1}");
            }
            txtScannerValues.Lines = displayLines.ToArray();

            // Scroll to the end
            txtScannerValues.SelectionStart = txtScannerValues.Text.Length;
            txtScannerValues.ScrollToCaret();
        }

        // Method to set the expected codes
        public void SetExpectedCodes(params string[] codes)
        {
            if (codes.Length > 4) Array.Resize(ref codes, 4);

            for (int i = 0; i < codes.Length; i++)
            {
                _expectedCodes[i] = codes[i];
            }
        }

        // Method to check if scanned codes match expected codes
        private void CheckForMatches()
        {
            // Note: This method is kept for compatibility but the main logic is now in AddScannedCode
            UpdateDatamatrixImageColors();
        }

        // Method to update datamatrix image colors based on matches
        private void UpdateDatamatrixImageColors()
        {
            // Update colors based on the number of scanned codes
            picBox1.BackColor = _scannedCodes.Count >= 1 ? Color.Green : Color.Red;
            picBox2.BackColor = _scannedCodes.Count >= 2 ? Color.Green : Color.Red;
            picBox3.BackColor = _scannedCodes.Count >= 3 ? Color.Green : Color.Red;
            picBox4.BackColor = _scannedCodes.Count >= 4 ? Color.Green : Color.Red;
        }

        // Method to reset all codes and clear the scanner values
        public void ResetCodes()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ResetCodes()));
                return;
            }
            _loggerService?.LogInformation("PrinterStatusForm: ResetCodes start");
            // PATCH-BEGIN: PrintWhileOpenLog
            _codesResetSinceOpen = true;
            // PATCH-END: PrintWhileOpenLog
            // PATCH-BEGIN: LastCodesEndOfFile
            _lastCodesEndOfFileHandled = false;
            // PATCH-END: LastCodesEndOfFile
            // PATCH-BEGIN: AsyncValidate
            CancelValidateCodes();
            _validateCodesVersion++;
            // PATCH-END: AsyncValidate
            _loggerService?.LogInformation("PrinterStatusForm: сброс отсканированных кодов");

            // Reset all datamatrix images to red
            picBox1.BackColor = Color.Red;
            picBox2.BackColor = Color.Red;
            picBox3.BackColor = Color.Red;
            picBox4.BackColor = Color.Red;

            // Clear the scanned codes list
            _scannedCodes.Clear();
            _inputBuffer.Clear();
            _minFoundIndex = -1;

            // Reset the scanner values text box to its initial state
            txtScannerValues.Clear();

            // Reset labels
            lblCode1.Text = "Код N";
            lblCode2.Text = "Код N";
            lblCode3.Text = "Код N";
            lblCode4.Text = "Код N";

            // Reset the file generation flag
            _fileGenerationCompleted = false;
            _fileGenerationCompletedAtUtc = DateTime.MinValue;

            // Update the status label and disable the generate file button
            lblCodeStatus.Text = "Статус: Ожидание кодов (0/4)";
            lblCodeStatus.ForeColor = Color.Red;
            btnGenerateFile.Enabled = false;
            _loggerService?.LogInformation("PrinterStatusForm: ResetCodes done");
        }

        // Methods to dynamically update the code labels
        public void UpdateCodeLabel(int index, string text)
        {
            if (index < 1 || index > 4) return; // Only allow indices 1-4

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateCodeLabel(index, text)));
                return;
            }

            switch (index)
            {
                case 1:
                    lblCode1.Text = text;
                    break;
                case 2:
                    lblCode2.Text = text;
                    break;
                case 3:
                    lblCode3.Text = text;
                    break;
                case 4:
                    lblCode4.Text = text;
                    break;
            }
        }

        // Method to update multiple code labels at once
        public void UpdateCodeLabels(params string[] texts)
        {
            if (texts.Length > 4) Array.Resize(ref texts, 4);

            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateCodeLabels(texts)));
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                switch (i + 1)
                {
                    case 1:
                        lblCode1.Text = texts[i];
                        break;
                    case 2:
                        lblCode2.Text = texts[i];
                        break;
                    case 3:
                        lblCode3.Text = texts[i];
                        break;
                    case 4:
                        lblCode4.Text = texts[i];
                        break;
                }
            }
        }
    }
}

