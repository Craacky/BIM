using BIM_Control.Services;
using BIM.Application.Common.Configs;
using BIM.Application.Common.Interfaces;
using BIM.Application.Features.Reports;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Timer = System.Windows.Forms.Timer;
using System.Security.Principal;

namespace BIM_Control.Forms
{
    public partial class ControlForm : Form
    {
        private enum AppState
        {
            Initializing,
            Idle,
            FileLoaded,
            ReadyToPrint,
            Printing,
            Finished,
            Locked
        }

        private readonly ICultureSettingsService _cultureService;
        private readonly IFileService _fileService;
        private readonly IFolderService _folderService;
        private readonly ILoggerService _loggerService;
        private readonly ICurrentDbService _currentDbService;
        private readonly PrinterMonitorService _printerMonitor;
        private readonly CameraService _cameraService;
        private readonly FolderSettings _folderSettings;
        private readonly LabelStarSettings _labelStarSettings;
        private readonly StatisticsService _statisticsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly bool _offlinePrinterFlow;

        private PrinterStatusForm _printerStatusForm;
        private ConnectionLockForm _connectionLockForm;
        private bool _isHeadCurrentlyOpen = false;
        private Timer _periodicCheckTimer;
        private Timer _modalFocusTimer;

        private int _statsGoodCodes, _statsTotalCodes, _statsBadCodes, _statsHeadOpen;
        private AppState _currentState = AppState.Initializing;
        private bool _finishStatus;
        private DateTime? _jobStartTime;
        private bool _isPrinterConnected, _isCameraConnected, _isPrintingJobActive, _isCameraPausedByPrinterHead, _isCameraPausedByPrinterPause;
        private DateTime? _connectionLostAt;
        // PATCH-BEGIN: PrinterNoResponseDialog
        private DateTime? _lastPrinterNoResponseDialogUtc;
        private const int PrinterNoResponseDialogCooldownMs = 60000;
        private int _printerNoResponsePausePending = 0;
        // PATCH-END: PrinterNoResponseDialog
        private const int ConnectionLockDebounceMs = 1000;
        private readonly SemaphoreSlim _pauseResumeGate = new SemaphoreSlim(1, 1);
        // PATCH-BEGIN: UnifiedFinishWorkflow
        private readonly SemaphoreSlim _finishWorkflowGate = new SemaphoreSlim(1, 1);
        private int _finishWorkflowInProgress = 0;
        private int _finishHeadOpenSuppressLogged = 0;
        // PATCH-END: UnifiedFinishWorkflow
        private LoadingDialog _pauseResumeLoadingDialog;
        private bool _isPrinterPaused = false;
        private bool _isPausedByUser = false;
        private bool _isPausedByCamera = false;
        // PATCH-BEGIN: CameraDisconnectPause
        private int _cameraDisconnectPausePending = 0;
        private DateTime? _lastCameraDisconnectDialogUtc;
        private const int CameraDisconnectDialogCooldownMs = 60000;
        // PATCH-END: CameraDisconnectPause
        private Timer _cameraUiBatchTimer;
        private readonly object _cameraLogQueueLock = new object();
        private readonly Queue<(string Message, Color Color)> _cameraLogQueue = new Queue<(string Message, Color Color)>();
        private int _autoPauseRequestPending = 0;
        private const int CameraUiBatchIntervalMs = 200;
        private const int CameraUiBatchLogChunk = 40;


        public ControlForm(
            ICultureSettingsService cultureService, IFileService fileService, IFolderService folderService,
            ILoggerService loggerService, ICurrentDbService currentDbService, PrinterMonitorService printerMonitor,
            CameraService cameraService, FolderSettings folderSettings,
            LabelStarSettings labelStarSettings, StatisticsService statisticsService,
            ICurrentUserService currentUserService,
            IConfiguration configuration)
        {
            _cultureService = cultureService;
            _fileService = fileService;
            _folderService = folderService;
            _loggerService = loggerService;
            _currentDbService = currentDbService;
            _printerMonitor = printerMonitor;
            _cameraService = cameraService;
            _folderSettings = folderSettings;
            _labelStarSettings = labelStarSettings;
            _statisticsService = statisticsService
                ;
            _currentUserService = currentUserService;
            bool offlinePrinterFlow;
            if (!bool.TryParse(configuration["AppMode:OfflinePrinterFlow"], out offlinePrinterFlow))
            {
                offlinePrinterFlow = false;
            }
            _offlinePrinterFlow = offlinePrinterFlow;

            if (_offlinePrinterFlow)
            {
                _cameraService.SetModuleAvailability(false);
            }
            // PATCH-BEGIN: AdminModeLog
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                _loggerService?.LogInformation($"Режим запуска: {(isAdmin ? "Администратор" : "Пользователь")}");
            }
            catch (Exception ex)
            {
                _loggerService?.LogWarning($"Не удалось определить режим запуска (админ): {ex.Message}");
            }
            // PATCH-END: AdminModeLog
            InitializeComponent();
            ReduceFontsForHighDpi();
            TrySetAppIcon();
        }

        private void ReduceFontsForHighDpi()
        {
            using var g = this.CreateGraphics();
            var dpiScale = g.DpiX / 96f;
            if (dpiScale > 1.0f)
            {
                var mainFontScale = 0.9f; // чуть больше для основного текста
                var statsFontScale = 0.75f; // меньше для статистики
                ReduceFontsRecursive(this, mainFontScale);
                if (dgvStats != null)
                {
                    ScaleDataGridViewFonts(dgvStats, statsFontScale);
                }
            }
        }

        private void ScaleDataGridViewFonts(DataGridView dgv, float scale)
        {
            if (dgv.ColumnHeadersDefaultCellStyle.Font != null)
                dgv.ColumnHeadersDefaultCellStyle.Font = new Font(dgv.ColumnHeadersDefaultCellStyle.Font.Name, dgv.ColumnHeadersDefaultCellStyle.Font.Size * scale, dgv.ColumnHeadersDefaultCellStyle.Font.Style);
            if (dgv.DefaultCellStyle.Font != null)
                dgv.DefaultCellStyle.Font = new Font(dgv.DefaultCellStyle.Font.Name, dgv.DefaultCellStyle.Font.Size * scale, dgv.DefaultCellStyle.Font.Style);
            dgv.RowTemplate.Height = (int)(48 * scale);
            dgv.ColumnHeadersHeight = (int)(34 * scale);
        }

        private void ReduceFontsRecursive(Control parent, float scale)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (ctrl.Font.Size > 6)
                {
                    ctrl.Font = new Font(ctrl.Font.Name, ctrl.Font.Size * scale, ctrl.Font.Style);
                }
                if (ctrl.HasChildren)
                {
                    ReduceFontsRecursive(ctrl, scale);
                }
            }
        }

        private void TrySetAppIcon()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "BIMv2.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
        }

        #region State and UI Management

        private void SetAppState(AppState newState)
        {
            _loggerService?.LogInformation($"Смена состояния приложения: {_currentState} -> {newState}");
            _currentState = newState;
            UpdateUIState();
        }

        private void UpdateUIState()
        {
            ThreadExtension.SafeInvoke(this, () =>
            {
                // Check if the control is available before accessing it
                if (chkCameraModuleEnabled == null) return;

                if (btn_pausePrint != null) btn_pausePrint.Visible = !_offlinePrinterFlow;
                if (btn_resumePrint != null) btn_resumePrint.Visible = !_offlinePrinterFlow;
                if (gb_printerControls != null) gb_printerControls.Visible = !_offlinePrinterFlow;

                var cameraModule = chkCameraModuleEnabled; // Declared once here
                switch (_currentState)
                {
                    case AppState.Initializing:
                        if (splitContainer != null)
                        {
                            splitContainer.Panel1.Enabled = false;
                            splitContainer.Panel2.Enabled = false;
                        }
                        if (gb_loadDb != null) gb_loadDb.Enabled = false;
                        if (gb_productVerify != null) gb_productVerify.Enabled = false;
                        if (gb_labelStar != null) gb_labelStar.Enabled = false;
                        if (btn_startPrint != null) btn_startPrint.Enabled = false;
                        if (btn_finishPrint != null) btn_finishPrint.Enabled = false;
                        if (btn_reprint != null) btn_reprint.Enabled = false;
                        if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
                        if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
                        if (cameraModule != null) cameraModule.Enabled = false;
                        break;
                    case AppState.Idle:
                        ResetStage1();
                        ResetStage2();
                        ResetStatistics();
                        if (splitContainer != null)
                        {
                            splitContainer.Panel1.Enabled = true;
                            splitContainer.Panel2.Enabled = false;
                        }
                        if (gb_loadDb != null) gb_loadDb.Enabled = true;
                        if (gb_labelStar != null) gb_labelStar.Enabled = false;
                        if (btn_loadDB != null) btn_loadDB.Enabled = true;
                        if (btn_startPrint != null) btn_startPrint.Enabled = false;
                        if (btn_finishPrint != null) btn_finishPrint.Enabled = false;
                        if (btn_reprint != null) btn_reprint.Enabled = false;
                        if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
                        if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
                        if (cameraModule != null) cameraModule.Enabled = true;
                        break;
                    case AppState.FileLoaded:
                        if (gb_productVerify != null) gb_productVerify.Enabled = true;
                        if (btn_loadDB != null) btn_loadDB.Enabled = false;
                        if (btn_st1_verifyDB != null) btn_st1_verifyDB.Enabled = true;
                        if (splitContainer != null) splitContainer.Panel2.Enabled = false;
                        if (gb_labelStar != null) gb_labelStar.Enabled = false;
                        if (btn_startPrint != null) btn_startPrint.Enabled = false;
                        if (btn_finishPrint != null) btn_finishPrint.Enabled = false;
                        if (btn_reprint != null) btn_reprint.Enabled = false;
                        if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
                        if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
                        if (cameraModule != null) cameraModule.Enabled = false;
                        break;
                    case AppState.ReadyToPrint:
                        if (splitContainer != null)
                        {
                            splitContainer.Panel1.Enabled = false;
                            splitContainer.Panel2.Enabled = true;
                        }
                        if (gb_labelStar != null) gb_labelStar.Enabled = true;
                        if (btn_startPrint != null) btn_startPrint.Enabled = true;
                        if (btn_finishPrint != null) btn_finishPrint.Enabled = false;
                        if (btn_reprint != null) btn_reprint.Enabled = false;
                        if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
                        if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
                        if (cameraModule != null) cameraModule.Enabled = false;
                        break;
                    case AppState.Printing:
                        if (btn_finishPrint != null) btn_finishPrint.Enabled = true;
                        if (btn_reprint != null) btn_reprint.Enabled = true;
                        if (btn_startPrint != null) btn_startPrint.Enabled = false;
                        if (gb_labelStar != null) gb_labelStar.Enabled = false;
                        if (cameraModule != null) cameraModule.Enabled = false;
                        UpdatePauseResumeButtons();
                        break;
                    // PATCH-BEGIN: UnifiedFinishWorkflow
                    case AppState.Finished:
                        if (btn_finishPrint != null) btn_finishPrint.Enabled = false;
                        if (btn_reprint != null) btn_reprint.Enabled = false;
                        if (btn_startPrint != null) btn_startPrint.Enabled = false;
                        if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
                        if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
                        if (gb_labelStar != null) gb_labelStar.Enabled = false;
                        if (cameraModule != null) cameraModule.Enabled = false;
                        break;
                    // PATCH-END: UnifiedFinishWorkflow
                    case AppState.Locked:
                        this.Enabled = false;
                        break;
                }
            }, false);
        }

        private void ResetStage1()
        {
            ThreadExtension.SafeInvoke(this, () =>
            {
                // Check if controls are available before accessing them
                if (pb_stage1 == null || gb_loadDb == null || tb_productCode == null ||
                    tb_fileName == null || rb_productInfo == null || btn_loadDB == null ||
                    gb_productVerify == null || btn_verifyProduct == null ||
                    btn_resetProduct == null || btn_st1_verifyDB == null) return;

                try
                {
                    pb_stage1.Image = Image.FromFile("Images/delete-button.png");
                }
                catch (Exception ex)
                {
                    _loggerService?.LogWarning($"Error setting pb_stage1 image: {ex.Message}");
                }
                
                if (gb_loadDb != null) gb_loadDb.Enabled = true;
                if (tb_productCode != null) tb_productCode.Clear();
                if (tb_fileName != null) tb_fileName.Clear();
                if (rb_productInfo != null) rb_productInfo.Clear();
                if (btn_loadDB != null) btn_loadDB.Enabled = true;
                if (gb_productVerify != null) gb_productVerify.Enabled = false;
                if (btn_verifyProduct != null) btn_verifyProduct.Enabled = true;
                if (btn_resetProduct != null) btn_resetProduct.Enabled = true;
                if (btn_st1_verifyDB != null) btn_st1_verifyDB.Enabled = false;
            }, false);
        }

        private void ResetStage2()
        {
            ThreadExtension.SafeInvoke(this, () =>
            {
                // Check if controls are available before accessing them
                if (pb_stage2 == null || gb_labelStar == null || tb_labelStarCode == null ||
                    btn_st2_verifyDB == null || btn_finishPrint == null ||
                    btn_reprint == null || btn_startPrint == null) return;

                try
                {
                    if (pb_stage2 != null) pb_stage2.Image = Image.FromFile("Images/delete-button.png");
                }
                catch (Exception ex)
                {
                    _loggerService?.LogWarning($"Error setting pb_stage2 image: {ex.Message}");
                }
                
                if (gb_labelStar != null) gb_labelStar.Enabled = true;
                if (tb_labelStarCode != null) tb_labelStarCode.Clear();
                if (btn_st2_verifyDB != null) btn_st2_verifyDB.Enabled = false;
                if (btn_finishPrint != null) btn_finishPrint.Enabled = false;
                if (btn_reprint != null) btn_reprint.Enabled = false;
                if (btn_startPrint != null) btn_startPrint.Enabled = false;
                if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
                if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
            }, false);
        }

        private void ResetAppState()
        {
            _isPrintingJobActive = false;
            HidePrinterStatusForm();

            if (_cameraService?.ModuleAvailable ?? false)
            {
                _cameraService.FlushPendingSpecialSequence();
                _cameraService.SetDataProcessing(false);
            }

            SetAppState(AppState.Idle);
            ResetPrinterControls();
        }

        private void ResetStatistics()
        {
            _statsGoodCodes = 0;
            _statsTotalCodes = 0;
            _statsBadCodes = 0;
            _statsHeadOpen = 0;
            UpdateStatsUI();
        }

        #endregion

        #region Form and Service Setup

        private async void ControlForm_Load(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Инициализация ControlForm");
            _finishStatus = false;
            SetAppState(AppState.Initializing);
            _folderService.VerifyAllFolders();
            SetupSidebarLayout();
            ReduceFontsForHighDpi();
            SetupCameraService();
            SetupPrinterMonitoring();
            SetupPeriodicCheckTimer();
            SetupCameraUiBatchTimer();
            SetAppState(AppState.Idle);

            _ = Task.Run(async () =>
            {
                while (!this.IsDisposed)
                {
                    _cultureService.HandleCurrentLanguage();
                    _cultureService.HandleCapsLock();
                    ThreadExtension.SafeInvoke(this, () =>
                    {
                        tb_currentLang.Text = _cultureService.CurrentLanguage;
                        tb_currentLang.BackColor = _cultureService.CurrentLanguage == "On" ? Color.Green : Color.Red;
                        tb_capsLockMode.Text = _cultureService.CapsLock;
                        tb_capsLockMode.BackColor = _cultureService.CapsLock == "Off" ? Color.Green : Color.Red;
                    }, false);
                    await Task.Delay(500);
                }
            });

            await Task.Delay(100);

            if (_offlinePrinterFlow)
            {
                _isPrinterConnected = true;
                _isCameraConnected = true;
                _loggerService?.LogInformation("Режим OfflinePrinterFlow активен: сетевые подключения принтера и камеры отключены.");
            }
            else
            {
                _isPrinterConnected = await _printerMonitor.IsPrinterAvailableAsync(500);
                _isCameraConnected = !_cameraService.ModuleAvailable || _cameraService.IsConnected;
                _loggerService?.LogDebug($"Стартовые соединения: принтер={_isPrinterConnected}, камера={_isCameraConnected}, модуль камеры={_cameraService.ModuleAvailable}");
                HandleConnectionStatusCombined();
            }
        }

        private void ControlForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_currentState == AppState.Printing && !_finishStatus)
            {
                if (MessageBox.Show("Задание не завершено. Вы уверены, что хотите закрыть программу?", "Закрытие программы", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _loggerService.LogInformation("Программа закрыта без сохранения текущего задания");
            }
            try
            {
                _cameraService?.ClearCodes();
            }
            catch (Exception ex)
            {
                _loggerService?.LogError($"Ошибка при очистке БД камеры при закрытии: {ex.Message}");
            }
            _printerMonitor?.StopMonitoring();
            _periodicCheckTimer?.Stop();
            _periodicCheckTimer?.Dispose();
            _cameraUiBatchTimer?.Stop();
            _cameraUiBatchTimer?.Dispose();
            _modalFocusTimer?.Stop();
            _modalFocusTimer?.Dispose();
            e.Cancel = false;
        }

        private void SetupPeriodicCheckTimer()
        {
            _periodicCheckTimer = new Timer
            {
                Interval = 500
            };
            _periodicCheckTimer.Tick += (sender, e) =>
            {
                if (!_offlinePrinterFlow &&
                    _currentState == AppState.Printing &&
                    _isHeadCurrentlyOpen &&
                    (_printerStatusForm == null || _printerStatusForm.IsDisposed))
                {
                    ShowPrinterStatusForm();
                }
            };
            _periodicCheckTimer.Start();
        }

        private void SetupCameraUiBatchTimer()
        {
            _cameraUiBatchTimer = new Timer
            {
                Interval = CameraUiBatchIntervalMs
            };
            _cameraUiBatchTimer.Tick += (s, e) => FlushCameraUiBatch();
            _cameraUiBatchTimer.Start();
        }

        private void SetupCameraService()
        {
            if (_cameraService != null)
            {
                _cameraService.ModuleAvailabilityChanged += OnModuleAvailabilityChanged;
                if (_offlinePrinterFlow)
                {
                    _cameraService.SetModuleAvailability(false);
                    _isCameraConnected = true;
                    if (rtbCameraLogs != null) AppendToCameraLogs("OfflinePrinterFlow: модуль камеры принудительно отключен", Color.Gray);
                    return;
                }

                if (_cameraService.ModuleAvailable)
                {
                    SubscribeToCameraEvents();
                    if (rtbCameraLogs != null) AppendToCameraLogs("Модуль камеры включен", Color.Lime);
                    _cameraService.Start();
                    try
                    {
                        _cameraService.ClearCodes();
                        _loggerService?.LogInformation("Локальная БД камеры очищена при запуске программы");
                    }
                    catch (Exception ex)
                    {
                        _loggerService?.LogError($"Ошибка очистки БД камеры при запуске: {ex.Message}");
                    }
                    
                    // Initially set data processing based on current state - should be disabled initially
                    _cameraService.SetDataProcessing(false);
                }
                else
                {
                    if (rtbCameraLogs != null) AppendToCameraLogs("Модуль камеры отключен", Color.Gray);
                    _isCameraConnected = true;
                }
            }
        }

        private async void SetupPrinterMonitoring()
        {
            if (_offlinePrinterFlow)
            {
                _isPrinterConnected = true;
                _loggerService?.LogInformation("OfflinePrinterFlow: мониторинг принтера и сетевые команды отключены.");
                return;
            }

            _printerMonitor.StateChanged += OnPrinterStateChanged;
            _printerMonitor.ErrorOccurred += OnPrinterErrorOccurred;
            _printerMonitor.StatusChanged += OnPrinterStatusChanged;
            _printerMonitor.ControlCommandDispatched += OnPrinterControlCommandDispatched;
            // PATCH-BEGIN: PrinterNoResponseDialog
            _printerMonitor.StatusTransportErrorThreshold += (s, e) =>
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    HandleAutoPauseByPrinterNoResponseAsync();
                });
            };
            // PATCH-END: PrinterNoResponseDialog
            _printerMonitor.ConnectionStatusChanged += (s, isConnected) =>
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    _isPrinterConnected = isConnected;
                    HandleConnectionStatusCombined();
                });
            };
            await _printerMonitor.StartMonitoringAsync(1000);
            // Keep background monitoring by default to reduce I/O pressure during pause/resume cycles.
        }

        #endregion

        #region Connection and Modal Form Handling

        private void HandleConnectionStatusCombined()
        {
            if (_offlinePrinterFlow)
            {
                _connectionLostAt = null;
                _connectionLockForm?.StartAutoCloseSequence();
                return;
            }

            bool shouldBeLocked = !_isPrinterConnected || (_cameraService.ModuleAvailable && !_isCameraConnected);
            if (shouldBeLocked)
            {
                if (_connectionLostAt == null)
                {
                    _connectionLostAt = DateTime.UtcNow;
                    return;
                }

                var elapsedMs = (DateTime.UtcNow - _connectionLostAt.Value).TotalMilliseconds;
                if (elapsedMs < ConnectionLockDebounceMs && (_connectionLockForm == null || _connectionLockForm.IsDisposed))
                {
                    return;
                }

                if (_connectionLockForm == null || _connectionLockForm.IsDisposed)
                {
                    _loggerService.LogWarning("Потеряна связь с устройством. Блокировка интерфейса.");
                    DisableAllForms();
                    _connectionLockForm = new ConnectionLockForm(_isPrinterConnected, _isCameraConnected, _cameraService.ModuleAvailable)
                    {
                        TopMost = true
                    };
                    _connectionLockForm.OnDisableCameraModuleRequested += () =>
                    {
                        if (chkCameraModuleEnabled != null && _currentState == AppState.Idle) this.Invoke((MethodInvoker)(() => chkCameraModuleEnabled.Checked = false));
                        else MessageBox.Show("Невозможно отключить модуль камеры в текущем состоянии.", "Операция недоступна", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    };
                    _connectionLockForm.FormClosed += (s, a) =>
                    {
                        EnableAllForms();
                        _loggerService.LogInformation("Связь с устройствами восстановлена.");
                    };
                    _connectionLockForm.ShowDialog(this);
                    _connectionLockForm = null;
                }
                else _connectionLockForm.UpdateDeviceStatus(_isPrinterConnected, _isCameraConnected);
            }
            else
            {
                _connectionLostAt = null;
                _connectionLockForm?.StartAutoCloseSequence();
            }
        }

        private void DisableAllForms()
        {
            foreach (Form form in Application.OpenForms)
                if (form != this && form.Visible)
                    form.Enabled = false;
            this.Enabled = false;
        }

        private void EnableAllForms()
        {
            foreach (Form form in Application.OpenForms) form.Enabled = true;
            this.Enabled = true;
            this.BringToFront();
            _printerStatusForm?.BringToFront();
        }

        private void ShowPrinterStatusForm()
        {
            // PATCH-BEGIN: UnifiedFinishWorkflow
            if (Interlocked.CompareExchange(ref _finishWorkflowInProgress, 0, 0) == 1)
            {
                _loggerService?.LogInformation("ShowPrinterStatusForm suppressed: выполняется завершение печати.");
                return;
            }
            // PATCH-END: UnifiedFinishWorkflow
            if (_offlinePrinterFlow)
            {
                return;
            }

            if (_currentState != AppState.Printing)
            {
                _loggerService.LogInformation("Попытка открыть форму статуса принтера вне активного задания печати - заблокирована.");
                return;
            }

            // Disable camera processing right before opening printer status workflow.
            // This replaces disabling by raw HeadOpen signal.
            if (_cameraService.ModuleAvailable && !_isCameraPausedByPrinterHead)
            {
                _cameraService.SetDataProcessing(false);
                _isCameraPausedByPrinterHead = true;
            }

            if (_printerStatusForm == null || _printerStatusForm.IsDisposed)
            {
                _printerStatusForm = new PrinterStatusForm(_printerMonitor, _folderSettings, _labelStarSettings, _cameraService, _loggerService);
                _printerStatusForm.Disposed += OnPrinterStatusFormDisposed;
                _printerStatusForm.btnContinue.Click += async (s, e) => await ContinueAfterPrinterStatusFormAsync();
                _printerStatusForm.RequestContinue += async (s, e) => await ContinueAfterPrinterStatusFormAsync();
                _printerStatusForm.lblPrinterStatus.Text = "Статус головы принтера: Открыта";
                _printerStatusForm.lblPrinterStatus.ForeColor = Color.Red;
                _printerStatusForm.btnGenerateFile.Enabled = false;
                _printerStatusForm.UpdateDetailedStatus(new PrinterStatus
                {
                    State = new PrinterState
                    {
                        IsHeadOpen = true,
                        Status = PrinterStatusType.HeadOpen
                    },
                    Method = "Manual Trigger",
                    RawStatusByte = 0xFF
                });
                this.Enabled = false;
                _printerStatusForm.Show(this);
                _printerStatusForm.BringToFront();
                _modalFocusTimer = new Timer
                {
                    Interval = 100
                };
                _modalFocusTimer.Tick += (s, e) =>
                {
                    if (_printerStatusForm != null && !_printerStatusForm.IsDisposed && !_printerStatusForm.Focused)
                    {
                        _printerStatusForm.BringToFront();
                        _printerStatusForm.Focus();
                    }
                };
                _modalFocusTimer.Start();
            }
            else
            {
                _printerStatusForm.BringToFront();
                _printerStatusForm.Focus();
                _printerStatusForm.UpdatePrinterStatus("Статус головы принтера: Открыта", Color.Red);
                _printerStatusForm.UpdateDetailedStatus(new PrinterStatus
                {
                    State = new PrinterState
                    {
                        IsHeadOpen = true,
                        Status = PrinterStatusType.HeadOpen
                    },
                    Method = "UI Refresh",
                    RawStatusByte = 0xFE
                });
            }
        }

        // PATCH-BEGIN: PrinterNoResponseDialog
        private void ShowPrinterNoResponseDialogIfNeeded()
        {
            if (_offlinePrinterFlow) return;
            // PATCH-BEGIN: UnifiedFinishWorkflow
            if (Interlocked.CompareExchange(ref _finishWorkflowInProgress, 0, 0) == 1) return;
            // PATCH-END: UnifiedFinishWorkflow
            if (_currentState != AppState.Printing) return;
            if (_printerStatusForm != null && !_printerStatusForm.IsDisposed) return;
            if (this.IsDisposed || !this.Visible) return;

            var nowUtc = DateTime.UtcNow;
            if (_lastPrinterNoResponseDialogUtc.HasValue &&
                (nowUtc - _lastPrinterNoResponseDialogUtc.Value).TotalMilliseconds < PrinterNoResponseDialogCooldownMs)
            {
                return;
            }

            _lastPrinterNoResponseDialogUtc = nowUtc;
            MessageBox.Show(
                "Принтер физически подключен, но не отвечает на запросы.\n\n" +
                "1. Остановите принтер вручную.\n" +
                "2. Завершите задание.\n" +
                "3. Сбросьте очередь принтера вручную и перезагрузите принтер.\n" +
                "4. Обратитесь к администратору для старта задания заново.",
                "Принтер не отвечает",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        // PATCH-END: PrinterNoResponseDialog

        // PATCH-BEGIN: PrinterNoResponseDialog
        private async void HandleAutoPauseByPrinterNoResponseAsync()
        {
            if (_offlinePrinterFlow) return;
            // PATCH-BEGIN: UnifiedFinishWorkflow
            if (Interlocked.CompareExchange(ref _finishWorkflowInProgress, 0, 0) == 1) return;
            // PATCH-END: UnifiedFinishWorkflow
            if (_currentState != AppState.Printing) return;
            if (_printerStatusForm != null && !_printerStatusForm.IsDisposed) return;
            if (Interlocked.Exchange(ref _printerNoResponsePausePending, 1) == 1) return;

            try
            {
                var nowUtc = DateTime.UtcNow;
                if (!_lastPrinterNoResponseDialogUtc.HasValue ||
                    (nowUtc - _lastPrinterNoResponseDialogUtc.Value).TotalMilliseconds >= PrinterNoResponseDialogCooldownMs)
                {
                    _lastPrinterNoResponseDialogUtc = nowUtc;
                    MessageBox.Show(
                        "Принтер физически подключен, но не отвечает на запросы.\n\n" +
                        "1. Остановите принтер вручную.\n" +
                        "2. Завершите задание.\n" +
                        "3. Сбросьте очередь принтера вручную и перезагрузите принтер.\n" +
                        "4. Обратитесь к администратору для старта задания заново.",
                        "Принтер не отвечает",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _printerNoResponsePausePending, 0);
            }
        }
        // PATCH-END: PrinterNoResponseDialog

        private void HidePrinterStatusForm()
        {
            if (_printerStatusForm != null && !_printerStatusForm.IsDisposed)
            {
                // Stop the modal focus timer
                _modalFocusTimer?.Stop();
                _modalFocusTimer?.Dispose();
                _modalFocusTimer = null;

                // Unsubscribe from events to prevent memory leaks
                _printerStatusForm.Disposed -= OnPrinterStatusFormDisposed;
                _printerStatusForm.PrepareForProgrammaticClose();

                // Close the form and dispose of resources
                _printerStatusForm.Close();

                // Explicitly dispose the form
                _printerStatusForm.Dispose();
                _printerStatusForm = null;

                // Re-enable the parent form
                this.Enabled = true;

                // Keep printer monitor in active mode for the full app lifetime.
            }
        }

        private async Task ContinueAfterPrinterStatusFormAsync()
        {
            if (_currentState != AppState.Printing)
            {
                HidePrinterStatusForm();
                return;
            }

            HidePrinterStatusForm();

            _isPausedByUser = false;
            _isPausedByCamera = false;
            _isPrinterPaused = false;

            if (_cameraService.ModuleAvailable)
            {
                _cameraService.SetDataProcessing(true);
                _cameraService.EnterSpecialProcessingMode();
                _isCameraPausedByPrinterHead = false;
                _isCameraPausedByPrinterPause = false;
            }
            UpdatePauseResumeButtons();
        }
        
        private void OnPrinterStatusFormDisposed(object sender, EventArgs e)
        {
            // Clean up resources when the form is disposed
            _modalFocusTimer?.Stop();
            _modalFocusTimer?.Dispose();
            _modalFocusTimer = null;
            this.Enabled = true;
        }

        #endregion

        #region Service Event Handlers

        private void SubscribeToCameraEvents()
        {
            if (_cameraService == null) return;
            _cameraService.GoodCodeReceived += OnGoodCodeReceived;
            _cameraService.BadCodeReceived += OnBadCodeReceived;
            _cameraService.LogMessage += OnCameraLogMessage;
            _cameraService.ConnectionStatusChanged += OnCameraConnectionStatusChanged;
        }

        private void UnsubscribeFromCameraEvents()
        {
            if (_cameraService == null) return;
            _cameraService.GoodCodeReceived -= OnGoodCodeReceived;
            _cameraService.BadCodeReceived -= OnBadCodeReceived;
            _cameraService.LogMessage -= OnCameraLogMessage;
            _cameraService.ConnectionStatusChanged -= OnCameraConnectionStatusChanged;
        }

        private async void OnModuleAvailabilityChanged(object sender, bool isAvailable)
        {
            if (_cameraService == null) return;
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnModuleAvailabilityChanged(sender, isAvailable)));
                return;
            }
            if (_offlinePrinterFlow && isAvailable)
            {
                _cameraService.SetModuleAvailability(false);
                return;
            }

            if (isAvailable)
            {
                SubscribeToCameraEvents();
                _cameraService.Start();
                
                // Set data processing based on current state - only enable during printing
                if (_currentState == AppState.Printing)
                {
                    _cameraService.SetDataProcessing(true);
                }
                else
                {
                    _cameraService.SetDataProcessing(false);
                }
            }
            else
            {
                UnsubscribeFromCameraEvents();
            }
            _isCameraConnected = _cameraService.IsConnected;
            if (isAvailable)
            {
                await Task.Delay(2000);
                this.Refresh();
            }
            HandleConnectionStatusCombined();
            _connectionLockForm?.UpdateModuleAvailability(isAvailable);
        }

        private void OnPrinterStateChanged(object sender, PrinterStateChangedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnPrinterStateChanged(sender, e)));
                return;
            }
            // PATCH-BEGIN: UnifiedFinishWorkflow
            if (Interlocked.CompareExchange(ref _finishWorkflowInProgress, 0, 0) == 1)
            {
                if (e.State.IsHeadOpen && Interlocked.Exchange(ref _finishHeadOpenSuppressLogged, 1) == 0)
                {
                    _loggerService?.LogInformation("FinishWorkflow: head-open event suppressed (PrinterStatusForm will not be shown during finish).");
                }
                _isHeadCurrentlyOpen = e.State.IsHeadOpen;
                return;
            }
            // PATCH-END: UnifiedFinishWorkflow
            if (_currentState == AppState.Printing)
            {
                if (e.State.IsHeadOpen)
                {
                    // Camera processing is paused when PrinterStatusForm is shown.
                }
                else if (_isCameraPausedByPrinterHead)
                {
                    // Do not auto-resume by head-close status.
                    // Camera processing resumes only via explicit workflow actions
                    // (e.g., Continue in PrinterStatusForm).
                }
                // No implicit camera auto-resume by head status.
                // Resume is handled by explicit user workflows only.

                // Pause/resume synchronization by printer status is intentionally disabled.
                // Camera processing for pause scenarios is controlled only by explicit BIMv2 actions
                // (Pause/Resume buttons, Continue workflow, auto-pause by bad code).

                // Do not bind pause/resume UI to printer monitor state
            }
            else
            {
                // If we're not in printing state, ensure camera data processing is disabled
                if (_cameraService.ModuleAvailable && _cameraService.IsDataProcessingEnabled())
                {
                    _cameraService.SetDataProcessing(false);
                }
            }
            if (e.State.IsHeadOpen && !_isHeadCurrentlyOpen)
            {
                _statsHeadOpen++;
                UpdateStatsUI();
            }
            _isHeadCurrentlyOpen = e.State.IsHeadOpen;
            if (e.State.IsHeadOpen) ShowPrinterStatusForm();
            if (_printerStatusForm != null && !_printerStatusForm.IsDisposed)
            {
                _printerStatusForm.UpdatePrinterStatus(e.State.IsHeadOpen ? "Статус головы принтера: Открыта" : "Статус головы принтера: Закрыта", e.State.IsHeadOpen ? Color.Red : Color.Green);
                if (e.FullStatus != null) _printerStatusForm.UpdateDetailedStatus(e.FullStatus);
            }
        }

        private void OnPrinterErrorOccurred(object sender, string errorMessage)
        {
            _loggerService.LogError($"Printer monitor error: {errorMessage}");
        }
        private void OnPrinterStatusChanged(object sender, string status)
        {
            _loggerService.LogInformation($"Printer monitor: {status}");
            UpdatePauseResumeLoadingStatus(status);
        }

        private void OnPrinterControlCommandDispatched(object sender, string commandDisplayName)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnPrinterControlCommandDispatched(sender, commandDisplayName)));
                return;
            }
            if (this.IsDisposed || !this.Visible) return;

            if (string.Equals(commandDisplayName, "ПРОДОЛЖИТЬ", StringComparison.OrdinalIgnoreCase))
            {
                _isPausedByUser = false;
                _isPausedByCamera = false;
                _isPrinterPaused = false;

                if (_currentState == AppState.Printing && _cameraService.ModuleAvailable)
                {
                    _cameraService.SetDataProcessing(true);
                    _isCameraPausedByPrinterPause = false;
                }
                UpdatePauseResumeButtons();
            }
        }

        private void UpdatePauseResumeLoadingStatus(string status)
        {
            if (_pauseResumeLoadingDialog == null || _pauseResumeLoadingDialog.IsDisposed)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            const string progressPrefix = "PROGRESS|";
            if (status.StartsWith(progressPrefix, StringComparison.Ordinal))
            {
                var parts = status.Split('|', 3);
                if (parts.Length == 3 && int.TryParse(parts[1], out int percent))
                {
                    _pauseResumeLoadingDialog.SetDeterminateMode(true);
                    _pauseResumeLoadingDialog.SetProgress(percent);
                    _pauseResumeLoadingDialog.UpdateMessage(parts[2]);
                    return;
                }
            }

            _pauseResumeLoadingDialog.SetDeterminateMode(false);
            _pauseResumeLoadingDialog.UpdateMessage(status);
        }

        private void ShowPauseResumeLoadingDialog(string message)
        {
            ClosePauseResumeLoadingDialog();
            _pauseResumeLoadingDialog = new LoadingDialog(message)
            {
                TopMost = true,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual
            };

            Rectangle parentBounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;
            int x = parentBounds.X + (parentBounds.Width - _pauseResumeLoadingDialog.Width) / 2;
            int y = parentBounds.Y + (parentBounds.Height - _pauseResumeLoadingDialog.Height) / 2;
            _pauseResumeLoadingDialog.Location = new Point(Math.Max(0, x), Math.Max(0, y));

            _pauseResumeLoadingDialog.SetDeterminateMode(false);
            _pauseResumeLoadingDialog.Show(this);
            _pauseResumeLoadingDialog.BringToFront();
        }

        private void ClosePauseResumeLoadingDialog()
        {
            if (_pauseResumeLoadingDialog == null)
            {
                return;
            }
            try
            {
                if (!_pauseResumeLoadingDialog.IsDisposed)
                {
                    _pauseResumeLoadingDialog.Close();
                    _pauseResumeLoadingDialog.Dispose();
                }
            }
            catch
            {
                // ignore close race during form shutdown
            }
            finally
            {
                _pauseResumeLoadingDialog = null;
            }
        }

        private void OnGoodCodeReceived(object sender, string code)
        {
            if (this.IsDisposed) return;

            int good = Interlocked.Increment(ref _statsGoodCodes);
            int total = Interlocked.Increment(ref _statsTotalCodes);

            if (total % 1000 == 0)
            {
                _loggerService?.LogInformation($"Камера: принято кодов good={good}, bad={_statsBadCodes}, total={total}");
            }
        }

        private async void OnBadCodeReceived(object sender, string code)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnBadCodeReceived(sender, code)));
                return;
            }
            if (this.IsDisposed) return;

            Interlocked.Increment(ref _statsBadCodes);
            Interlocked.Increment(ref _statsTotalCodes);

            if (_cameraService != null && _cameraService.ModuleAvailable)
            {
                EnqueueCameraLogLine($"✗ {code}", Color.Red);
                _loggerService?.LogWarning($"Камера: ОШИБКА! Получен некорректный код: {code}");
            }

            // Don't trigger pause if in special processing mode
            if (!_cameraService.IsInSpecialProcessingMode() && chkPauseOnFail != null && chkPauseOnFail.Checked)
            {
                if (Interlocked.Exchange(ref _autoPauseRequestPending, 1) == 1)
                {
                    return;
                }

                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            await HandleAutoPauseByBadCodeAsync();
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _autoPauseRequestPending, 0);
                        }
                    }));
                    return;
                }

                if (!await _pauseResumeGate.WaitAsync(0))
                {
                    _loggerService?.LogDebug("Автопауза: команда уже выполняется, пропуск");
                    Interlocked.Exchange(ref _autoPauseRequestPending, 0);
                    return;
                }
                // Pause the printer and disable camera processing
                try
                {
                    bool pauseOk = await _printerMonitor.PausePrinterAsync(bypassSafetyWaits: true);
                    if (!pauseOk)
                    {
                        _loggerService?.LogWarning("Автопауза: не удалось отправить команду паузы принтеру.");
                        return;
                    }
                    if (_cameraService.ModuleAvailable)
                    {
                        _cameraService.SetDataProcessing(false);
                        _isCameraPausedByPrinterPause = true;
                    }
                    _isPausedByCamera = true;
                    _isPausedByUser = false;
                    _isPrinterPaused = true;
                    UpdatePauseResumeButtons();
                    MessageBox.Show("Открыть голову принтера", "Принтер на паузе", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    Interlocked.Exchange(ref _autoPauseRequestPending, 0);
                    _pauseResumeGate.Release();
                }
            }
        }

        private void OnCameraLogMessage(object sender, string message)
        {
            if (this.IsDisposed) return;
            if (_currentState != AppState.Printing)
            {
                return;
            }
            if (_cameraService != null &&
                _cameraService.ModuleAvailable &&
                !message.Contains("Подключение") &&
                !message.Contains("Камера") &&
                !message.Contains("Специальный режим") &&
                !message.Contains("ОШИБКА КОД"))
            {
                EnqueueCameraLogLine(message, Color.Yellow);
            }
        }
        

        private void OnCameraConnectionStatusChanged(object sender, bool isConnected)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnCameraConnectionStatusChanged(sender, isConnected)));
                return;
            }
            if (this.IsDisposed || !this.Visible) return;
            if (_offlinePrinterFlow)
            {
                _isCameraConnected = true;
                return;
            }
            _isCameraConnected = isConnected;
            if (_cameraService != null && _cameraService.ModuleAvailable && isConnected && rtbCameraLogs != null)
            {
                lock (_cameraLogQueueLock)
                {
                    _cameraLogQueue.Clear();
                }
                rtbCameraLogs.Clear();
                AppendToCameraLogs("Ожидание данных...", Color.Lime);
            }
            _connectionLockForm?.UpdateCameraStatus(isConnected);
            HandleConnectionStatusCombined();
            // PATCH-BEGIN: CameraDisconnectPause
            if (!isConnected && _currentState == AppState.Printing)
            {
                HandleAutoPauseByCameraDisconnectAsync();
            }
            // PATCH-END: CameraDisconnectPause
        }

        // PATCH-BEGIN: CameraDisconnectPause
        private async void HandleAutoPauseByCameraDisconnectAsync()
        {
            if (_offlinePrinterFlow) return;
            if (_currentState != AppState.Printing) return;
            if (Interlocked.Exchange(ref _cameraDisconnectPausePending, 1) == 1) return;

            if (!await _pauseResumeGate.WaitAsync(0))
            {
                Interlocked.Exchange(ref _cameraDisconnectPausePending, 0);
                return;
            }

            try
            {
                bool pauseOk = await _printerMonitor.PausePrinterAsync(bypassSafetyWaits: true);
                if (!pauseOk)
                {
                    _loggerService?.LogWarning("Автопауза (камера): не удалось отправить команду паузы принтеру.");
                    return;
                }
                if (_cameraService.ModuleAvailable)
                {
                    await Task.Delay(500);
                    _cameraService.SetDataProcessing(false);
                    _isCameraPausedByPrinterPause = true;
                }
                _isPausedByCamera = true;
                _isPausedByUser = false;
                _isPrinterPaused = true;
                UpdatePauseResumeButtons();

                if (_printerStatusForm == null || _printerStatusForm.IsDisposed)
                {
                    var nowUtc = DateTime.UtcNow;
                    if (!_lastCameraDisconnectDialogUtc.HasValue ||
                        (nowUtc - _lastCameraDisconnectDialogUtc.Value).TotalMilliseconds >= CameraDisconnectDialogCooldownMs)
                    {
                        _lastCameraDisconnectDialogUtc = nowUtc;
                        MessageBox.Show(
                            "Потеряна связь с камерой. Принтер поставлен на паузу.\n\n" +
                            "1. Проверьте камеру и кабели.\n" +
                            "2. После восстановления нажмите \"Продолжить\".\n" +
                            "3. Если восстановить нельзя — завершите задание.",
                            "Камера отключена",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _cameraDisconnectPausePending, 0);
                _pauseResumeGate.Release();
            }
        }
        // PATCH-END: CameraDisconnectPause

        #endregion

        #region UI Helpers

        private void AppendToCameraLogs(string message, Color color)
        {
            if (rtbCameraLogs != null)
            {
                rtbCameraLogs.SelectionColor = color;
                rtbCameraLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                rtbCameraLogs.ScrollToCaret();
            }
        }

        private void EnqueueCameraLogLine(string message, Color color)
        {
            lock (_cameraLogQueueLock)
            {
                _cameraLogQueue.Enqueue((message, color));
            }
        }

        private async Task HandleAutoPauseByBadCodeAsync()
        {
            if (!await _pauseResumeGate.WaitAsync(0))
            {
                _loggerService?.LogDebug("Автопауза: команда уже выполняется, пропуск");
                return;
            }

            try
            {
                bool pauseOk = await _printerMonitor.PausePrinterAsync(bypassSafetyWaits: true);
                if (!pauseOk)
                {
                    _loggerService?.LogWarning("Автопауза: не удалось отправить команду паузы принтеру.");
                    return;
                }
                if (_cameraService.ModuleAvailable)
                {
                    await Task.Delay(500);
                    _cameraService.SetDataProcessing(false);
                    _isCameraPausedByPrinterPause = true;
                }
                _isPausedByCamera = true;
                _isPausedByUser = false;
                _isPrinterPaused = true;
                UpdatePauseResumeButtons();
                MessageBox.Show("Открыть голову принтера", "Принтер на паузе", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _pauseResumeGate.Release();
            }
        }

        private void FlushCameraUiBatch()
        {
            if (this.IsDisposed || !this.Visible) return;

            UpdateStatsUI();

            if (rtbCameraLogs == null) return;

            List<(string Message, Color Color)> chunk = null;
            lock (_cameraLogQueueLock)
            {
                if (_cameraLogQueue.Count == 0)
                {
                    return;
                }

                int take = Math.Min(CameraUiBatchLogChunk, _cameraLogQueue.Count);
                chunk = new List<(string Message, Color Color)>(take);
                for (int i = 0; i < take; i++)
                {
                    chunk.Add(_cameraLogQueue.Dequeue());
                }
            }

            foreach (var line in chunk)
            {
                AppendToCameraLogs(line.Message, line.Color);
            }
        }

        private void UpdateStatsUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateStatsUI));
                return;
            }
            if (dgvStats != null && dgvStats.Rows != null && dgvStats.Rows.Count >= 4)
            {
                try
                {
                    dgvStats.Rows[0].Cells[1].Value = _statsGoodCodes.ToString();
                    dgvStats.Rows[1].Cells[1].Value = _statsTotalCodes.ToString();
                    dgvStats.Rows[2].Cells[1].Value = _statsBadCodes.ToString();
                    dgvStats.Rows[3].Cells[1].Value = _statsHeadOpen.ToString();
                }
                catch (Exception ex)
                {
                    _loggerService?.LogWarning($"Error updating statistics UI: {ex.Message}");
                }
            }
        }

        private void ClearCameraLogs()
        {
            lock (_cameraLogQueueLock)
            {
                _cameraLogQueue.Clear();
            }
            if (rtbCameraLogs != null)
            {
                rtbCameraLogs.Clear();
                if (_cameraService != null && _cameraService.ModuleAvailable) AppendToCameraLogs("Ожидание данных...", Color.Lime);
            }
        }

        #endregion

        #region Button Events

        private void btn_startPrint_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Начать печать");
            _finishStatus = false;
            _currentDbService.StartPrint();
            _jobStartTime = DateTime.Now;
            _isPrintingJobActive = true;
            _isCameraPausedByPrinterHead = false;
            _isCameraPausedByPrinterPause = false;
            if (!_offlinePrinterFlow && chkCameraModuleEnabled != null && chkCameraModuleEnabled.Checked && !_cameraService.ModuleAvailable) _cameraService.SetModuleAvailability(true);

            // Enable camera data processing when starting print job
            if (_cameraService.ModuleAvailable)
            {
                _cameraService.SetDataProcessing(true);
                _cameraService.EnterSpecialProcessingMode();
                // Reset any pause flags when starting print
                _isCameraPausedByPrinterHead = false;
                _isCameraPausedByPrinterPause = false;
            }

            SetAppState(AppState.Printing);
            if (!_offlinePrinterFlow && _isHeadCurrentlyOpen)
            {
                ShowPrinterStatusForm();
            }
        }

        private async void btn_finishPrint_Click(object sender, EventArgs e)
        {
            // PATCH-BEGIN: UnifiedFinishWorkflow
            if (!await _finishWorkflowGate.WaitAsync(0))
            {
                _loggerService?.LogWarning("Завершение печати уже выполняется, повторный запуск пропущен.");
                return;
            }
            if (Interlocked.Exchange(ref _finishWorkflowInProgress, 1) == 1)
            {
                _finishWorkflowGate.Release();
                _loggerService?.LogWarning("Завершение печати уже выполняется, повторный запуск пропущен.");
                return;
            }
            // PATCH-END: UnifiedFinishWorkflow
            _loggerService?.LogInformation("Операция: Завершить печать");
            // PATCH-BEGIN: UnifiedFinishWorkflow
            _loggerService?.LogInformation("FinishWorkflow: gate acquired, start protected finalization flow.");
            Interlocked.Exchange(ref _finishHeadOpenSuppressLogged, 0);
            // PATCH-END: UnifiedFinishWorkflow
            _isPrintingJobActive = false;
            _finishStatus = true;
            DateTime finishTimestamp = DateTime.Now;
            bool shouldClearCameraCodes = true;
            bool shouldExitApplication = true;
            try
            {
                _currentDbService.FinishPrint();
                SetAppState(AppState.Finished);
                HidePrinterStatusForm();
            
            // Disable camera processing after job finishes
            _cameraService.FlushPendingSpecialSequence();
            _cameraService.SetDataProcessing(false);
            ClearCameraLogs();
            if (_cameraService?.ModuleAvailable ?? false)
            {
                AppendToCameraLogs("Сканирование камеры остановлено", Color.Gray);
            }

            // Reset pause flags
            _isCameraPausedByPrinterHead = false;
            _isCameraPausedByPrinterPause = false;
            if (_fileService.FileName != null && _statisticsService != null)
            {
                TimeSpan jobDuration = _jobStartTime.HasValue ? finishTimestamp - _jobStartTime.Value : TimeSpan.Zero;
                LoadingDialog finishDrainDialog = null;
                try
                {
                    if (_cameraService?.ModuleAvailable ?? false)
                    {
                        int initialPending = _cameraService.GetPendingDatabaseWritesCount();
                        if (initialPending > 0)
                        {
                            finishDrainDialog = new LoadingDialog("Завершение печати: ожидание записи кодов в БД...")
                            {
                                TopMost = true,
                                ShowInTaskbar = false
                            };
                            finishDrainDialog.SetDeterminateMode(false);
                            finishDrainDialog.Show(this);
                            finishDrainDialog.BringToFront();

                            Task drainTask = Task.Run(() => _cameraService.WaitForDatabaseDrain(TimeSpan.FromMinutes(5)));
                            while (!drainTask.IsCompleted)
                            {
                                int pendingNow = _cameraService.GetPendingDatabaseWritesCount();
                                finishDrainDialog.UpdateMessage($"Завершение печати: дожим очереди камеры в БД... Осталось: {pendingNow}");
                                await Task.Delay(100);
                            }
                            await drainTask;
                        }

                        _cameraService.FlushDatabaseWrites();

                        finishDrainDialog ??= new LoadingDialog("Завершение печати: экспорт кодов камеры...")
                        {
                            TopMost = true,
                            ShowInTaskbar = false
                        };
                        finishDrainDialog.SetDeterminateMode(false);
                        if (!finishDrainDialog.Visible)
                        {
                            finishDrainDialog.Show(this);
                            finishDrainDialog.BringToFront();
                        }
                        finishDrainDialog.UpdateMessage("Завершение печати: экспорт кодов камеры в файл...");

                        string cameraCodesFilePath = BuildCameraStatisticsFilePath(finishTimestamp, _fileService.FileName);
                        int exportedCodes = await Task.Run(() => _cameraService.ExportAllCodesToFile(cameraCodesFilePath));
                        _loggerService?.LogInformation($"Коды камеры экспортированы: {cameraCodesFilePath}, строк={exportedCodes}");
                    }
                }
                catch (Exception ex)
                {
                    shouldClearCameraCodes = false;
                    _loggerService?.LogError($"Ошибка завершения камеры (дожим/экспорт): {ex.Message}");
                    MessageBox.Show("Не удалось завершить выгрузку кодов камеры в файл. База камеры не очищена. Проверьте лог и повторите завершение.", "Ошибка завершения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    if (finishDrainDialog != null)
                    {
                        try
                        {
                            if (!finishDrainDialog.IsDisposed)
                            {
                                finishDrainDialog.Close();
                                finishDrainDialog.Dispose();
                            }
                        }
                        catch
                        {
                            // ignore close race
                        }
                    }
                }

                // PATCH-BEGIN: UnifiedFinishWorkflow
                if (!_offlinePrinterFlow)
                {
                    _loggerService?.LogInformation("FinishWorkflow: reboot-only mode started (without Windows spooler cleanup).");
                    bool rebootSent = await _printerMonitor.ClearPrinterQueueAsync();
                    if (!rebootSent)
                    {
                        shouldExitApplication = false;
                        _loggerService?.LogError("ControlForm: не удалось отправить команду перезагрузки принтера при завершении печати.");
                        MessageBox.Show(
                            "Не удалось отправить команду перезагрузки принтера.\nПроверьте подключение и повторите завершение задания.",
                            "Ошибка перезагрузки принтера",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    else
                    {
                        bool printerBackOnline = await _printerMonitor.WaitPrinterBackOnlineAsync(45000, 400);
                        if (!printerBackOnline)
                        {
                            shouldExitApplication = false;
                            _loggerService?.LogError("ControlForm: принтер не вернулся online после команды перезагрузки при завершении печати.");
                            MessageBox.Show(
                                "Принтер не вернулся в сеть после перезагрузки.\nПроверьте состояние принтера и повторите завершение задания.",
                                "Ошибка восстановления принтера",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                        else
                        {
                            _loggerService?.LogInformation("FinishWorkflow: reboot-only mode completed successfully.");
                        }
                    }
                }
                // PATCH-END: UnifiedFinishWorkflow

                List<string> duplicateCodesSnapshot = new List<string>();
                List<PrintJobStatistics.DuplicateCodeDetail> duplicateCodeDetailsSnapshot = new List<PrintJobStatistics.DuplicateCodeDetail>();
                int totalDuplicateCountSnapshot = 0;
                int cameraDatabaseCodesCountSnapshot = 0;
                if (_cameraService?.ModuleAvailable ?? false)
                {
                    duplicateCodesSnapshot = _cameraService.GetDuplicateCodes();
                    duplicateCodeDetailsSnapshot = _cameraService
                        .GetDuplicateCodesWithSequenceNumbers()
                        .Select(pair => new PrintJobStatistics.DuplicateCodeDetail
                        {
                            Code = pair.Key,
                            LineNumbers = pair.Value?.OrderBy(n => n).ToList() ?? new List<int>()
                        })
                        .ToList();
                    totalDuplicateCountSnapshot = _cameraService.GetTotalDuplicateCount();
                    cameraDatabaseCodesCountSnapshot = _cameraService.GetCodesCount();
                }

                var stats = new PrintJobStatistics(finishTimestamp, _currentUserService.UserName, _fileService.FileName, _statsGoodCodes, _statsBadCodes, _statsTotalCodes, _statsHeadOpen, totalDuplicateCountSnapshot, cameraDatabaseCodesCountSnapshot, _cameraService.ModuleAvailable, duplicateCodesSnapshot, duplicateCodeDetailsSnapshot, jobDuration);
                await _statisticsService.SaveStatisticsAsync(stats);
                _loggerService?.LogInformation($"Статистика сохранена: файл={_fileService.FileName}, good={_statsGoodCodes}, bad={_statsBadCodes}, total={_statsTotalCodes}, cameraDbCount={cameraDatabaseCodesCountSnapshot}, headOpen={_statsHeadOpen}, dupTotal={totalDuplicateCountSnapshot}");
                if (_cameraService?.ModuleAvailable ?? false)
                {
                    _loggerService?.LogInformation($"Проверка дубликатов: уникальных={duplicateCodesSnapshot.Count}, всего повторений={totalDuplicateCountSnapshot}");
                    MessageBox.Show(duplicateCodesSnapshot.Any() ? $"Найдено {duplicateCodesSnapshot.Count} уникальных дублирующихся кодов, всего повторений: {totalDuplicateCountSnapshot}." : "Дубликаты не обнаружены.", "Проверка дубликатов", MessageBoxButtons.OK, duplicateCodesSnapshot.Any() ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }

                if (_fileService.FileName != null && _statisticsService != null)
                {
                    try
                    {
                        _fileService.DeleteBackupFiles();
                    }
                    catch (Exception ex)
                    {
                        _loggerService.LogWarning($"Ошибка при удалении бэкап файлов: {ex.Message}");
                    }
                    try
                    {
                        if (_offlinePrinterFlow)
                        {
                            _fileService.MoveFileToArchive();
                            _loggerService?.LogInformation("OfflinePrinterFlow: файл перемещен в архив.");
                            MessageBox.Show("Файл перемещен в архив.", "Перемещение файла", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            // Check camera DB for duplicates and move file accordingly
                            bool hasDuplicates = duplicateCodesSnapshot.Any() || totalDuplicateCountSnapshot > 0;

                            if (hasDuplicates)
                            {
                                // Move file to duplicates folder if duplicates were found
                                _fileService.MoveFileToDuplicates();
                                _loggerService?.LogInformation("Файл перемещен в папку 'Дубликаты' по результатам проверки БД камеры.");
                                MessageBox.Show("Файл перемещен в папку 'Дубликаты'.", "Перемещение файла", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            else
                            {
                                // Move file to archive if no duplicates were found
                                _fileService.MoveFileToArchive();
                                _loggerService?.LogInformation("Файл перемещен в архив по результатам проверки БД камеры.");
                                MessageBox.Show("Файл перемещен в архив.", "Перемещение файла", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggerService.LogError($"Ошибка при перемещении файла: {ex.Message}");
                    }
                }
            }
                if ((_cameraService?.ModuleAvailable ?? false) && !shouldClearCameraCodes)
                {
                    _loggerService?.LogWarning("Очистка БД камеры пропущена: экспорт кодов завершился с ошибкой.");
                }
                else
                {
                    _cameraService?.ClearCodes();
                }
                if (MessageBox.Show("Задание завершено. Выполнить еще одно?", "Завершение печати", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ResetPrinterControls();
                    SetAppState(AppState.Idle);
                    ClearCameraLogs();
                }
                else if (shouldExitApplication)
                {
                    Application.Exit();
                }
                else
                {
                    // PATCH-BEGIN: UnifiedFinishWorkflow
                    // Keep application open when printer recovery failed so operator can recover manually.
                    SetAppState(AppState.Idle);
                    // PATCH-END: UnifiedFinishWorkflow
                }
            }
            catch (Exception ex)
            {
                // PATCH-BEGIN: UnifiedFinishWorkflow
                _loggerService?.LogError($"Критическая ошибка сценария завершения печати: {ex.Message}");
                MessageBox.Show("Произошла ошибка при завершении печати. Проверьте лог и повторите операцию.", "Ошибка завершения", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetAppState(AppState.Idle);
                // PATCH-END: UnifiedFinishWorkflow
            }
            finally
            {
                // PATCH-BEGIN: UnifiedFinishWorkflow
                Interlocked.Exchange(ref _finishWorkflowInProgress, 0);
                _finishWorkflowGate.Release();
                _loggerService?.LogInformation("FinishWorkflow: gate released.");
                // PATCH-END: UnifiedFinishWorkflow
            }
        }

        private void ResetPrinterControls()
        {
            ThreadExtension.SafeInvoke(this, () =>
            {
                _isPausedByUser = false;
                _isPausedByCamera = false;
                _isPrinterPaused = false;
                _isCameraPausedByPrinterPause = false;
                UpdatePauseResumeButtons();
            }, false);
        }

        private void btn_reprint_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Сбой (перепечать)");
            if (MessageBox.Show("Вы уверены, что хотите выполнить сброс печати?", "Внимание", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk) == DialogResult.OK)
            {
                _finishStatus = true;
                _currentDbService.RePrint();
                SetAppState(AppState.Idle);
                MessageBox.Show("Для перепечатывания файла обратитесь к администратору!", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private async void btn_pausePrint_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Пауза печати");
            if (_offlinePrinterFlow)
            {
                return;
            }
            // Allow clicks regardless of printer state, but serialize commands to avoid overlaps
            if (!await _pauseResumeGate.WaitAsync(0))
            {
                _loggerService?.LogDebug("Пауза: команда уже выполняется, пропуск");
                return;
            }
            if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
            if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
            try
            {
                ShowPauseResumeLoadingDialog("Подготовка к отправке команды ПАУЗА...");
                bool pauseOk = await _printerMonitor.PausePrinterAsync();
                if (!pauseOk)
                {
                    MessageBox.Show("Не удалось отправить команду паузы принтеру.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Stop camera processing first; local pause flags are switched last.
                if (_currentState == AppState.Printing && _cameraService.ModuleAvailable)
                {
                    await Task.Delay(500);
                    _cameraService.SetDataProcessing(false);
                    _isCameraPausedByPrinterPause = true;
                }

                _isPausedByUser = true;
                _isPausedByCamera = false;
                _isPrinterPaused = true;
                UpdatePauseResumeButtons();
            }
            finally
            {
                ClosePauseResumeLoadingDialog();
                UpdatePauseResumeButtons();
                _pauseResumeGate.Release();
            }
        }

        private async void btn_resumePrint_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Снять с паузы");
            if (_offlinePrinterFlow)
            {
                return;
            }
            // Allow clicks regardless of printer state, but serialize commands to avoid overlaps
            if (!await _pauseResumeGate.WaitAsync(0))
            {
                _loggerService?.LogDebug("Снять с паузы: команда уже выполняется, пропуск");
                return;
            }
            if (btn_pausePrint != null) btn_pausePrint.Enabled = false;
            if (btn_resumePrint != null) btn_resumePrint.Enabled = false;
            bool preResumedCamera = false;
            try
            {
                // User requirement: enable camera reading immediately on Resume click,
                // before sending resume command to printer.
                if (_currentState == AppState.Printing && _cameraService.ModuleAvailable)
                {
                    if (_isCameraPausedByPrinterPause || !_cameraService.IsDataProcessingEnabled())
                    {
                        _cameraService.SetDataProcessing(true);
                    }
                    _cameraService.EnterSpecialProcessingMode();
                    _isCameraPausedByPrinterPause = false;
                    preResumedCamera = true;
                }

                _isPausedByUser = false;
                _isPausedByCamera = false;
                _isPrinterPaused = false;

                ShowPauseResumeLoadingDialog("Подготовка к отправке команды ПРОДОЛЖИТЬ...");
                bool resumeOk = await _printerMonitor.ResumePrinterAsync();
                if (!resumeOk)
                {
                    // Roll back local state if printer command failed.
                    _isPausedByUser = true;
                    _isPrinterPaused = true;
                    if (preResumedCamera && _cameraService.ModuleAvailable)
                    {
                        _cameraService.SetDataProcessing(false);
                        _isCameraPausedByPrinterPause = true;
                    }
                    MessageBox.Show("Не удалось отправить команду продолжить принтеру.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                UpdatePauseResumeButtons();
            }
            finally
            {
                ClosePauseResumeLoadingDialog();
                UpdatePauseResumeButtons();
                _pauseResumeGate.Release();
            }
        }

        private void UpdatePauseResumeButtons()
        {
            ThreadExtension.SafeInvoke(this, () =>
            {
                if (btn_pausePrint == null || btn_resumePrint == null) return;
                if (_offlinePrinterFlow)
                {
                    btn_pausePrint.Enabled = false;
                    btn_resumePrint.Enabled = false;
                    return;
                }
                if (_currentState != AppState.Printing)
                {
                    btn_pausePrint.Enabled = false;
                    btn_resumePrint.Enabled = false;
                    return;
                }

                bool isPaused = _isPrinterPaused || _isPausedByUser || _isPausedByCamera;
                if (isPaused)
                {
                    btn_pausePrint.Enabled = false;
                    btn_resumePrint.Enabled = true;
                }
                else
                {
                    btn_pausePrint.Enabled = true;
                    btn_resumePrint.Enabled = false;
                }
            }, false);
        }

        private void btn_st2_verifyDB_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Этап 2: Проверка базы данных");
            var (item1, item2) = _currentDbService.VerifyStage2Db(tb_labelStarCode.Text);
            _loggerService?.LogDebug($"Этап 2: результат проверки = {item1}");
            if (item1 == 0)
            {
                try
                {
                    if (pb_stage2 != null) pb_stage2.Image = Image.FromFile("Images/ResultTrue.ico");
                }
                catch (Exception ex)
                {
                    _loggerService?.LogWarning($"Error setting pb_stage2 image: {ex.Message}");
                }
                SetAppState(AppState.ReadyToPrint);
            }
            else
            {
                MessageBox.Show(item2, "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (btn_startPrint != null) btn_startPrint.Enabled = false;
            }
        }

        private async void btn_st1_verifyDB_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Этап 1: Проверка базы данных");
            if (_cameraService.ModuleAvailable && _fileService.IsFileContainsDupes(_fileService.FileText))
            {
                MessageBox.Show($"Файл {_fileService.FileName} содержит дубликаты.", "Ошибка!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            int verifyResult = await _currentDbService.VerifyStage1Db();
            _loggerService?.LogDebug($"Этап 1: результат проверки = {verifyResult}");
            switch (verifyResult)
            {
                case 0:
                case 3:
                    ThreadExtension.SafeInvoke(this, () =>
                    {
                        if (splitContainer != null)
                        {
                            splitContainer.Panel1.Enabled = false;
                            splitContainer.Panel2.Enabled = true;
                        }
                        if (gb_labelStar != null) gb_labelStar.Enabled = true;
                        if (btn_st2_verifyDB != null) btn_st2_verifyDB.Enabled = true;
                        try
                        {
                            if (pb_stage1 != null) pb_stage1.Image = Image.FromFile("Images/ResultTrue.ico");
                        }
                        catch (Exception ex)
                        {
                            _loggerService?.LogWarning($"Error setting pb_stage1 image: {ex.Message}");
                        }
                    }, false); break;
                case 1: MessageBox.Show("Первый код уже существует в базе.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Stop); break;
                case 2: MessageBox.Show("Файл с этим кодом уже был запущен.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Error); break;
                case 4: MessageBox.Show("Печать файла после сбоя! Обратитесь к администратору!", "Ожидание", MessageBoxButtons.OK, MessageBoxIcon.Error); break;
                case 5: MessageBox.Show("Файл с этим кодом уже в архиве.", "Ожидание", MessageBoxButtons.OK, MessageBoxIcon.Error); break;
                default: MessageBox.Show("Ошибка при добавлении базы!", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Error); break;
            }
        }

        private void btn_resetProduct_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Сброс продукта/этапа");
            ResetAppState();
        }

        private async void btn_verifyProduct_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Проверка продукта");
            var (item1, item2) = await _currentDbService.VerifyProduct();
            if (item1)
            {
                _loggerService?.LogInformation("Проверка продукта: успешно");
                ThreadExtension.SafeInvoke(this, () =>
                {
                    if (rb_productInfo != null) rb_productInfo.Text = item2;
                    SetAppState(AppState.FileLoaded);
                }, true);
            }
            else
            {
                _loggerService?.LogWarning("Проверка продукта: продукт не найден");
                MessageBox.Show("Продукт не найден.", "Продукт не найден", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btn_loadDB_Click(object sender, EventArgs e)
        {
            _loggerService?.LogInformation("Операция: Загрузка файла");
            _folderService.VerifyAllFolders();
            using (OpenFileDialog ofd = new()
                   {
                       Filter = "All files (*.*)|*.*"
                   })
            {
                if (ofd.ShowDialog() == DialogResult.Cancel)
                {
                    _loggerService?.LogDebug("Загрузка файла отменена пользователем");
                    return;
                }
                _loggerService?.LogDebug($"Выбран файл: {ofd.FileName}");
                _fileService.MoveFileToWorkFolder(ofd.FileName);
            }
            try
            {
                _cameraService?.ClearCodes();
                _loggerService?.LogInformation("Локальная БД камеры очищена при загрузке файла");
            }
            catch (Exception ex)
            {
                _loggerService?.LogError($"Ошибка очистки БД камеры при загрузке файла: {ex.Message}");
            }
            var fileLines = File.ReadLines(_fileService.FilePath).ToList();
            _fileService.FileText = fileLines;
            if (tb_fileName != null) tb_fileName.Text = _fileService.FileName;
            if (tb_productCode != null) tb_productCode.Text = fileLines.FirstOrDefault();
            _currentDbService.AddNewDb(out bool isAdded);
            if (isAdded)
            {
                _loggerService?.LogInformation($"Файл успешно загружен: {_fileService.FileName}");
                ResetStatistics();
                if (gb_productVerify != null) gb_productVerify.Enabled = true;
                if (btn_loadDB != null) btn_loadDB.Enabled = false;
            }
            else
            {
                _loggerService?.LogWarning($"Файл не добавлен в БД: {_fileService.FileName}");
                if (gb_productVerify != null) gb_productVerify.Enabled = false;
                if (btn_st1_verifyDB != null) btn_st1_verifyDB.Enabled = false;
                if (btn_loadDB != null) btn_loadDB.Enabled = true;
            }
        }

        #endregion

        #region Checkbox Events

        private void ChkCameraModuleEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (chkCameraModuleEnabled == null) return;
            if (_offlinePrinterFlow)
            {
                chkCameraModuleEnabled.CheckedChanged -= ChkCameraModuleEnabled_CheckedChanged;
                chkCameraModuleEnabled.Checked = false;
                chkCameraModuleEnabled.CheckedChanged += ChkCameraModuleEnabled_CheckedChanged;
                return;
            }

            bool isEnabled = chkCameraModuleEnabled.Checked;
            if (_currentState != AppState.Idle)
            {
                chkCameraModuleEnabled.CheckedChanged -= ChkCameraModuleEnabled_CheckedChanged;
                chkCameraModuleEnabled.Checked = !isEnabled;
                chkCameraModuleEnabled.CheckedChanged += ChkCameraModuleEnabled_CheckedChanged;
                MessageBox.Show("Невозможно изменить модуль камеры, пока есть активное задание.", "Операция недоступна", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _cameraService.SetModuleAvailability(isEnabled);
                if (mainLayout != null)
                {
                    mainLayout.SuspendLayout();
                    mainLayout.ColumnStyles.Clear();
                    if (isEnabled)
                    {
                        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
                        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
                    }
                    else
                    {
                        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0F));
                    }
                    mainLayout.ResumeLayout(true);
                }
                this.Size = isEnabled ? new Size(1150, 760) : new Size(800, 760);
                ResetStatistics();
                ClearCameraLogs();
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Ошибка при переключении модуля камеры: {ex}");
                chkCameraModuleEnabled.CheckedChanged -= ChkCameraModuleEnabled_CheckedChanged;
                chkCameraModuleEnabled.Checked = !isEnabled;
                chkCameraModuleEnabled.CheckedChanged += ChkCameraModuleEnabled_CheckedChanged;
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        private string BuildCameraStatisticsFilePath(DateTime finishedAt, string sourceFileName)
        {
            string folderName = string.IsNullOrWhiteSpace(_folderSettings.CameraStatisticsOutput)
                ? "Статистика Камеры"
                : _folderSettings.CameraStatisticsOutput;
            string outputDirectory = Path.Combine(_folderSettings.Path, folderName);
            Directory.CreateDirectory(outputDirectory);

            string normalizedSourceName = string.IsNullOrWhiteSpace(sourceFileName) ? "unknown_file" : sourceFileName;
            string sanitizedSourceName = SanitizeFileNamePart(normalizedSourceName);
            string outputFileName = $"КодыКамеры_{sanitizedSourceName}_{finishedAt:yyyyMMdd_HHmmss}.txt";
            return Path.Combine(outputDirectory, outputFileName);
        }

        private static string SanitizeFileNamePart(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            return sanitized.Replace(".", "_");
        }

        private List<string> CheckForDuplicateCodesInternal() => _cameraService?.GetDuplicateCodes() ?? new List<string>();
    }
}
