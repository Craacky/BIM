using System;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace BIM_Control.Forms
{
    public class ConnectionLockForm : Form
    {
        private bool _allowedToClose = false;
        private Label _statusLabel;
        private Label _titleLabel;
        private Label _subLabel;
        private Button _disableCameraModuleButton;
        private Timer _dotTimer;
        private int _dotCount = 0;

        // Track connection status for both devices
        private bool _printerConnected = false;
        private bool _cameraConnected = false;
        private bool _cameraModuleAvailable = false;

        // Callback for disabling camera module
        public event Action OnDisableCameraModuleRequested;

        public ConnectionLockForm(bool printerConnected, bool cameraConnected, bool cameraModuleAvailable)
        {
            _printerConnected = printerConnected;
            _cameraConnected = cameraConnected;
            _cameraModuleAvailable = cameraModuleAvailable;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Size = new Size(500, 250);
            this.BackColor = Color.DarkRed;
            this.Padding = new Padding(5);

            Panel innerPanel = new Panel();
            innerPanel.Dock = DockStyle.Fill;
            innerPanel.BackColor = Color.White;
            this.Controls.Add(innerPanel);

            _titleLabel = new Label();
            _titleLabel.Text = GetTitleText();
            _titleLabel.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            _titleLabel.ForeColor = Color.DarkRed;
            _titleLabel.Dock = DockStyle.Top;
            _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            _titleLabel.Height = 60;
            innerPanel.Controls.Add(_titleLabel);

            _statusLabel = new Label();
            _statusLabel.Text = GetStatusText();
            _statusLabel.Font = new Font("Segoe UI", 12);
            _statusLabel.ForeColor = Color.Black;
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            innerPanel.Controls.Add(_statusLabel);

            _subLabel = new Label();
            _subLabel.Text = "Работа приложения заблокирована.\nОкно закроется автоматически при восстановлении связи.";
            _subLabel.Font = new Font("Segoe UI", 10, FontStyle.Italic);
            _subLabel.ForeColor = Color.Gray;
            _subLabel.Dock = DockStyle.Bottom;
            _subLabel.TextAlign = ContentAlignment.MiddleCenter;
            _subLabel.Height = 60;
            innerPanel.Controls.Add(_subLabel);

            // Кнопка для отключения модуля камеры (видна только если модуль доступен и прибор не подключен)
            _disableCameraModuleButton = new Button();
            _disableCameraModuleButton.Text = "Отключить модуль камеры";
            _disableCameraModuleButton.Font = new Font("Segoe UI", 10);
            _disableCameraModuleButton.BackColor = Color.Orange;
            _disableCameraModuleButton.ForeColor = Color.Black;
            _disableCameraModuleButton.Dock = DockStyle.Bottom;
            _disableCameraModuleButton.Height = 40;
            _disableCameraModuleButton.Visible = false; // Будет видна только в определённых условиях
            _disableCameraModuleButton.Click += (s, e) =>
            {
                OnDisableCameraModuleRequested?.Invoke();
            };
            innerPanel.Controls.Add(_disableCameraModuleButton);
            UpdateDisableCameraButtonVisibility(); // Add this line

            _dotTimer = new Timer();
            _dotTimer.Interval = 500;
            _dotTimer.Tick += (s, e) =>
            {
                if (!_allowedToClose)
                {
                    _dotCount = (_dotCount + 1) % 4;
                    string dots = new string('.', _dotCount);
                    _statusLabel.Text = GetStatusText() + dots;
                }
            };
            _dotTimer.Start();

        }

        private string GetTitleText()
        {
            if (!_printerConnected && !_cameraConnected && _cameraModuleAvailable)
            {
                return "СВЯЗЬ С ПРИНТЕРОМ И КАМЕРОЙ ПОТЕРЯНА";
            }
            else if (!_printerConnected)
            {
                return "СВЯЗЬ С ПРИНТЕРОМ ПОТЕРЯНА";
            }
            else if (!_cameraConnected && _cameraModuleAvailable)
            {
                return "СВЯЗЬ С КАМЕРОЙ ПОТЕРЯНА";
            }
            return "СВЯЗЬ ПОТЕРЯНА";
        }

        private string GetStatusText()
        {
            if (!_printerConnected && !_cameraConnected && _cameraModuleAvailable)
            {
                return "Ожидание подключения принтера и камеры...";
            }
            else if (!_printerConnected && !_cameraConnected && !_cameraModuleAvailable)
            {
                return "Ожидание подключения принтера...";
            }
            else if (!_printerConnected)
            {
                return "Ожидание подключения принтера...";
            }
            else if (!_cameraConnected && _cameraModuleAvailable)
            {
                return "Ожидание подключения камеры...";
            }
            return "Ожидание подключения...";
        }

        public void UpdateDeviceStatus(bool printerConnected, bool cameraConnected)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateDeviceStatus(printerConnected, cameraConnected)));
                return;
            }

            _printerConnected = printerConnected;
            _cameraConnected = cameraConnected;

            // Update UI based on new status
            _titleLabel.Text = GetTitleText();
            _statusLabel.Text = GetStatusText();

            // Обновить видимость кнопки отключения модуля
            UpdateDisableCameraButtonVisibility();

            // If both devices are connected, close the form
            if (_printerConnected && (_cameraConnected || !_cameraModuleAvailable))
            {
                StartAutoCloseSequence();
            }
        }

        public void UpdateCameraStatus(bool cameraConnected)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateCameraStatus(cameraConnected)));
                return;
            }

            _cameraConnected = cameraConnected;

            // Update UI based on new status
            _titleLabel.Text = GetTitleText();
            _statusLabel.Text = GetStatusText();

            // Обновить видимость кнопки отключения модуля
            UpdateDisableCameraButtonVisibility();

            // If both devices are connected, close the form
            if (_printerConnected && (_cameraConnected || !_cameraModuleAvailable))
            {
                StartAutoCloseSequence();
            }
        }

        /// <summary>
        /// Динамически обновляет состояние модуля камеры
        /// Используется при включении/отключении модуля пользователем
        /// </summary>
        public void UpdateModuleAvailability(bool moduleAvailable)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateModuleAvailability(moduleAvailable)));
                return;
            }

            _cameraModuleAvailable = moduleAvailable;

            // Update UI based on new status
            _titleLabel.Text = GetTitleText();
            _statusLabel.Text = GetStatusText();

            // Показать кнопку отключения модуля если нужно
            UpdateDisableCameraButtonVisibility();

            // If printer connected and (camera connected OR camera module disabled), close the form
            if (_printerConnected && (_cameraConnected || !_cameraModuleAvailable))
            {
                StartAutoCloseSequence();
            }
        }

        /// <summary>
        /// Обновляет видимость кнопки отключения модуля камеры
        /// </summary>
        private void UpdateDisableCameraButtonVisibility()
        {
            // Показываем кнопку если:
            // - Модуль камеры включен И
            // - Камера отключена (независимо от принтера)
            bool shouldShow = _cameraModuleAvailable && !_cameraConnected;
            
            if (_disableCameraModuleButton != null)
            {
                _disableCameraModuleButton.Visible = shouldShow;
            }
        }

        public void ShowConnectionRestored()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ShowConnectionRestored));
                return;
            }

            _dotTimer.Stop();

            this.BackColor = Color.ForestGreen;
            _titleLabel.ForeColor = Color.ForestGreen;
            _titleLabel.Text = "СОЕДИНЕНИЕ ВОССТАНОВЛЕНО";

            _statusLabel.Text = "Связь с устройствами успешно восстановлена.\nВозврат к работе...";
            _statusLabel.ForeColor = Color.DarkGreen;

            _subLabel.Text = "Окно закрывается...";

            this.Refresh();
        }

        public void StartAutoCloseSequence()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(StartAutoCloseSequence));
                return;
            }

            ShowConnectionRestored();
            SafeClose();
        }

        public void SafeClose()
        {
            _allowedToClose = true;
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowedToClose)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                }
            }
            base.OnFormClosing(e);
        }
    }
}

