using Microsoft.Extensions.Configuration;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Timer = System.Threading.Timer;
using BIM.Application.Common.Constants;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BIM_Control.Services
{
    public class PrinterMonitorService
    {
        private readonly string _printerIp;
        private readonly int _printerPort;
        private bool _isMonitoring;
        private PrinterState _previousValidatedState;
        private Timer _monitorTimer;
        private readonly object _lockObject = new object();
        private bool _isConnectionActive = true; 

        private MonitoringMode _currentMode = MonitoringMode.Background;
        private TcpClient _persistentClient;
        private NetworkStream _persistentStream;
        private TcpClient _controlClient;
        private NetworkStream _controlStream;
        private Timer _controlMaintenanceTimer;
        // PATCH-BEGIN: ControlReconnect
        private int _controlReconnectInFlight = 0;
        // PATCH-END: ControlReconnect
        // private CancellationTokenSource _activeModeCancellation; // Removed unused field
        private readonly SemaphoreSlim _ioGate = new SemaphoreSlim(1, 1);
        private volatile bool _isControlCommandInProgress = false;
        private volatile bool _isStatusMonitoringSuspended = false;
        private volatile bool _suppressControlMaintenance = false;
        private int _isControlMaintenanceRunning = 0;
        private int _monitorSuspendDepth = 0;
        private readonly object _controlStateLock = new object();
        private DateTime _lastAnyCommandTime = DateTime.MinValue;
        private int _totalCommandCounter = 0;
        private long _lastStatusIoUtcTicks = 0;
        private long _lastControlIoUtcTicks = 0;
        private const int StatusControlGapMs = 2500;
        private const int StatusIoTimeoutMs = 500;
        private const int RecoveryStatusWarmupTimeoutMs = 12000;
        private const int RecoveryStatusWarmupPollMs = 250;
        private const int PersistentConnectTimeoutMs = 1500;
        private const int PersistentReconnectBaseDelayMs = 1000;
        private const int PersistentReconnectMaxDelayMs = 10000;
        private const int ControlIoTimeoutMs = 12000;
        private const int ControlMaintenanceIntervalMs = 1500;
        private const int SpoolerStrictTimeoutMs = 30000;
        private const int SpoolerRetryDelayMs = 1200;
        private const int MinCommandGapMs = 3000;
        private const int StabilizationCommandThreshold = 5;
        private const int StabilizationPauseMs = 10000;
        private const int WaitProgressStepMs = 200;
        private const int StatusOutcomeUnknown = 0;
        private const int StatusOutcomeSuccess = 1;
        private const int StatusOutcomeSkipped = 2;
        private const int StatusOutcomeTransportError = 3;
        private readonly object _statusIoCtsLock = new object();
        private CancellationTokenSource _currentStatusIoCts;
        private int _lastStatusOutcome = StatusOutcomeUnknown;
        private int _persistentConnectFailCount = 0;
        private DateTime _nextPersistentConnectAttemptUtc = DateTime.MinValue;
        // PATCH-BEGIN: StatusErrorThreshold
        private int _consecutiveStatusTransportErrors = 0;
        private bool _statusTransportErrorNotified = false;
        // PATCH-END: StatusErrorThreshold

        public bool IsConnectionActive => _isConnectionActive;

        private int _consecutiveErrorCount = 0;
        private int _consecutiveNormalCount = 0;
        private const int MIN_CONSECUTIVE_READINGS_FOR_ERROR = 2;
        private const int MIN_CONSECUTIVE_READINGS_FOR_NORMAL = 3;
        
        private bool _wasPrintingBeforeError = false;
        private DateTime _lastPrintResumeTime = DateTime.MinValue;
        private const int GRACE_PERIOD_AFTER_RESUME_MS = 5000;

        public event EventHandler<PrinterStateChangedEventArgs> StateChanged;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> ControlCommandDispatched;
        public event EventHandler<MonitoringModeChangedEventArgs> ModeChanged;
        public event EventHandler<bool> ConnectionStatusChanged;
        // PATCH-BEGIN: StatusTransportErrorDialog
        public event EventHandler StatusTransportError;
        // PATCH-END: StatusTransportErrorDialog
        // PATCH-BEGIN: StatusErrorThreshold
        public event EventHandler StatusTransportErrorThreshold;
        // PATCH-END: StatusErrorThreshold

        public PrinterMonitorService(IConfiguration configuration)
        {
            _printerIp = configuration["PrinterSettings:IP"];
            if (!int.TryParse(configuration["PrinterSettings:Port"], out _printerPort))
            {
                _printerPort = AppConstants.Printer.DefaultPort;
            }
            _previousValidatedState = new PrinterState { Status = PrinterStatusType.Normal };
        }

        public async Task<bool> IsPrinterAvailableAsync(int timeoutMs = 2000)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(timeoutMs);
                
                #if NET5_0_OR_GREATER
                await client.ConnectAsync(_printerIp, _printerPort, cts.Token);
                #else
                var connectTask = client.ConnectAsync(_printerIp, _printerPort);
                var delayTask = Task.Delay(timeoutMs, cts.Token);
                if (await Task.WhenAny(connectTask, delayTask) != connectTask)
                {
                    return false;
                }
                await connectTask; 
                #endif
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region Public Methods

        public async Task StartMonitoringAsync(int intervalMs = AppConstants.Printer.BackgroundMonitorIntervalMs)
        {
            OnStatusChanged($"ℹ Инициализация мониторинга принтера {_printerIp}:{_printerPort} (интервал {intervalMs}ms)");

            if (_monitorTimer != null)
            {
                _monitorTimer.Dispose();
            }

            _isMonitoring = true;
            _currentMode = MonitoringMode.Background;
            _monitorTimer = new Timer(async (state) => await MonitorCallback(), null, 0, intervalMs);
            _controlMaintenanceTimer?.Dispose();
            _controlMaintenanceTimer = new Timer(async (state) => await MaintainControlConnectionCallback(), null, 0, ControlMaintenanceIntervalMs);

            OnStatusChanged("✓ Фоновый мониторинг принтера запущен");
            OnModeChanged(MonitoringMode.Background);

            // Pre-warm control channel to avoid first-click pause latency.
            _ = Task.Run(async () => await WarmupControlConnectionAsync());
        }

        public void StopMonitoring()
        {
            OnStatusChanged("⏸ Остановка мониторинга принтера...");
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;
            _controlMaintenanceTimer?.Dispose();
            _controlMaintenanceTimer = null;
            
            DisconnectPersistentConnection();
            // PATCH-BEGIN: ControlSafeStop
            // Do not tear down control channel if a control command is in progress.
            if (!_isControlCommandInProgress)
            {
                DisconnectControlConnection();
            }
            // PATCH-END: ControlSafeStop
            OnStatusChanged("✓ Мониторинг принтера остановлен");
        }

        public async Task<bool> SwitchToActiveModeAsync()
        {
            OnStatusChanged("ℹ Переключение в режим АКТИВНЫЙ (частый опрос)...");
            _currentMode = MonitoringMode.Active;
            
            if (_monitorTimer != null)
            {
                _monitorTimer.Change(0, AppConstants.Printer.ActiveMonitorIntervalMs);
            }

            OnStatusChanged($"✓ Режим АКТИВНЫЙ установлен (интервал {AppConstants.Printer.ActiveMonitorIntervalMs}ms)");
            OnModeChanged(MonitoringMode.Active);
            
            return await Task.FromResult(true);
        }

        public async Task SwitchToBackgroundModeAsync()
        {
            OnStatusChanged("ℹ Переключение в режим ФОНОВЫЙ (нормальный опрос)...");
            _currentMode = MonitoringMode.Background;

            if (_monitorTimer != null)
            {
                _monitorTimer.Change(0, AppConstants.Printer.BackgroundMonitorIntervalMs);
            }
                
            OnStatusChanged($"✓ Режим ФОНОВЫЙ установлен (интервал {AppConstants.Printer.BackgroundMonitorIntervalMs}ms)");
            OnModeChanged(MonitoringMode.Background);
            
            await Task.CompletedTask;
        }

        public async Task<PrinterStatus> GetStatusAsync()
        {
            // In active mode always use persistent path: it reconnects on demand and
            // avoids fallback to quick one-shot sockets after pause/resume commands.
            if (_currentMode == MonitoringMode.Active)
            {
                return await GetStatusFromPersistentConnectionAsync();
            }
            else
            {
                return await GetStatusWithQuickConnectionAsync();
            }
        }

        public async Task<bool> ClearPrinterQueueAsync()
        {
            OnStatusChanged("ℹ Инициирование жесткой перезагрузки принтера TSC...");
            
            bool lockTaken = false;
            try 
            {
                _isControlCommandInProgress = true;
                SuspendStatusMonitoringForControl();
                CancelInFlightStatusIo();
                lockTaken = await _ioGate.WaitAsync(AppConstants.Printer.ConnectionTimeoutMs);
                if (!lockTaken)
                {
                    OnErrorOccurred("✗ Очередь занята, команда перезагрузки не отправлена.");
                    return false;
                }
                // Release all active channels before maintenance command.
                DisconnectPersistentConnection();
                DisconnectControlConnection();

                bool sent = await ExecuteControlCommandAsync(
                    AppConstants.Printer.RebootCommand,
                    "ПЕРЕЗАГРУЗКА",
                    "ESC!C (0x1B 0x21 0x43)",
                    1000,
                    bypassSafetyWaits: true);
                if (!sent)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"✗ Ошибка при перезагрузке принтера: {ex.Message}");
                return false;
            }
            finally
            {
                // Reboot command must not keep control/persistent sockets alive.
                // TSC can reject further status sessions if previous control socket lingers.
                DisconnectControlConnection();
                DisconnectPersistentConnection();
                MarkControlIoNow();
                if (lockTaken) _ioGate.Release();
                _isControlCommandInProgress = false;
                ResumeStatusMonitoringAfterControl();
            }
        }

        public async Task<bool> PausePrinterAsync(bool bypassSafetyWaits = false)
        {
            OnStatusChanged("ℹ Отправка команды ПАУЗА принтеру TSC...");
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"[PrinterMonitor] Pause start {DateTime.Now:O}");
            
            bool lockTaken = false;
            try 
            {
                _isControlCommandInProgress = true;
                SuspendStatusMonitoringForControl();
                CancelInFlightStatusIo();
                await _ioGate.WaitAsync();
                lockTaken = true;
                byte[] pauseCmd = { 0x1B, 0x21, 0x50 }; // ESC ! P
                bool sent = await ExecuteControlCommandAsync(
                    pauseCmd,
                    "ПАУЗА",
                    "ESC!P (0x1B 0x21 0x50)",
                    500,
                    bypassSafetyWaits);
                if (!sent)
                {
                    return false;
                }
                Console.WriteLine($"[PrinterMonitor] Pause sent in {sw.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"✗ Ошибка при отправке команды ПАУЗА: {ex.Message}");
                Console.WriteLine($"[PrinterMonitor] Pause failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                return false;
            }
            finally
            {
                if (lockTaken) _ioGate.Release();
                MarkControlIoNow();
                _isControlCommandInProgress = false;
                ResumeStatusMonitoringAfterControl();
                Console.WriteLine($"[PrinterMonitor] Pause end {DateTime.Now:O}, elapsed={sw.ElapsedMilliseconds}ms");
            }
        }

        public async Task<bool> ResumePrinterAsync()
        {
            OnStatusChanged("ℹ Отправка команды ПРОДОЛЖИТЬ принтеру TSC...");
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"[PrinterMonitor] Resume start {DateTime.Now:O}");
            
            bool lockTaken = false;
            try 
            {
                _isControlCommandInProgress = true;
                SuspendStatusMonitoringForControl();
                CancelInFlightStatusIo();
                await _ioGate.WaitAsync();
                lockTaken = true;
                byte[] resumeCmd = { 0x1B, 0x21, 0x4F }; // ESC ! O
                bool sent = await ExecuteControlCommandAsync(
                    resumeCmd,
                    "ПРОДОЛЖИТЬ",
                    "ESC!O (0x1B 0x21 0x4F)",
                    1500);
                if (!sent)
                {
                    return false;
                }
                Console.WriteLine($"[PrinterMonitor] Resume sent in {sw.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"✗ Ошибка при отправке команды ПРОДОЛЖИТЬ: {ex.Message}");
                Console.WriteLine($"[PrinterMonitor] Resume failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                return false;
            }
            finally
            {
                if (lockTaken) _ioGate.Release();
                MarkControlIoNow();
                _isControlCommandInProgress = false;
                ResumeStatusMonitoringAfterControl();
                Console.WriteLine($"[PrinterMonitor] Resume end {DateTime.Now:O}, elapsed={sw.ElapsedMilliseconds}ms");
            }
        }

        private async Task<bool> ExecuteControlCommandAsync(byte[] command, string commandDisplayName, string wireDisplayName, int postSendDelayMs, bool bypassSafetyWaits = false)
        {
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    if (!bypassSafetyWaits)
                    {
                        await EnsureSafeCommandIntervalAsync(commandDisplayName);
                        await MaybeRunStabilizationPauseAsync(commandDisplayName);
                    }
                    DisconnectPersistentConnection();
                    await EnsureControlConnectionAsync(logAttempt: true, connectTimeoutMs: 2500);

                    OnStatusChanged($"ℹ Отправка {wireDisplayName} = {commandDisplayName}...");
                    await _controlStream.WriteAsync(command, 0, command.Length);
                    await _controlStream.FlushAsync();
                    OnControlCommandDispatched(commandDisplayName);
                    OnStatusChanged("ℹ Обработка команды принтером...");
                    await Task.Delay(postSendDelayMs);

                    int commandNumber = RegisterSuccessfulCommandSend();
                    OnStatusChanged($"✓ Команда {commandDisplayName} успешно отправлена. Счетчик команд: {commandNumber}");
                    return true;
                }
                catch (SocketException ex)
                {
                    DisconnectControlConnection();
                    if (attempt >= 2)
                    {
                        OnErrorOccurred($"✗ Ошибка сокета при отправке команды {commandDisplayName}: {ex.Message}. " +
                                       "Проверьте: 1) доступность IP/порта принтера 2) сетевой кабель/коммутатор 3) что принтер не в аварийном состоянии.");
                        return false;
                    }
                    OnStatusChanged($"⚠ Ошибка сокета, повторная попытка отправки {commandDisplayName}...");
                }
                catch (Exception ex)
                {
                    DisconnectControlConnection();
                    if (attempt >= 2)
                    {
                        OnErrorOccurred($"✗ Ошибка при отправке команды {commandDisplayName}: {ex.Message}");
                        return false;
                    }
                    OnStatusChanged($"⚠ Ошибка отправки, повторная попытка {commandDisplayName}...");
                }
            }

            return false;
        }

        private async Task EnsureControlConnectionAsync(bool logAttempt = true, int connectTimeoutMs = ControlIoTimeoutMs)
        {
            if (_controlClient != null && _controlClient.Connected && _controlStream != null)
            {
                // PATCH-BEGIN: ControlHealthCheck
                if (IsControlSocketUsable())
                {
                    return;
                }
                DisconnectControlConnection(scheduleReconnect: false);
                // PATCH-END: ControlHealthCheck
            }

            DisconnectControlConnection(scheduleReconnect: false);
            _controlClient = new TcpClient
            {
                ReceiveTimeout = ControlIoTimeoutMs,
                SendTimeout = ControlIoTimeoutMs
            };

            if (logAttempt)
            {
                OnStatusChanged($"ℹ Подключение control-канала к принтеру {_printerIp}:{_printerPort}...");
            }
            var connectTask = _controlClient.ConnectAsync(_printerIp, _printerPort);
            if (await Task.WhenAny(connectTask, Task.Delay(connectTimeoutMs)) != connectTask)
            {
                throw new SocketException((int)SocketError.TimedOut);
            }
            await connectTask;

            _controlStream = _controlClient.GetStream();
            _controlStream.ReadTimeout = ControlIoTimeoutMs;
            _controlStream.WriteTimeout = ControlIoTimeoutMs;
            if (logAttempt)
            {
                OnStatusChanged("✓ Control-канал подключен");
            }
        }

        private void DisconnectControlConnection(bool scheduleReconnect = true)
        {
            try
            {
                _controlStream?.Close();
                _controlClient?.Close();
            }
            catch { }
            finally
            {
                _controlStream = null;
                _controlClient = null;
            }

            // PATCH-BEGIN: ControlReconnect
            if (scheduleReconnect)
            {
                ScheduleControlReconnectIfNeeded();
            }
            // PATCH-END: ControlReconnect
        }

        // PATCH-BEGIN: ControlReconnect
        private void ScheduleControlReconnectIfNeeded()
        {
            if (!_isMonitoring) return;
            if (_suppressControlMaintenance) return;
            if (_isControlCommandInProgress) return;
            if (_isStatusMonitoringSuspended) return;
            if (_controlClient != null && _controlClient.Connected && _controlStream != null) return;
            if (Interlocked.Exchange(ref _controlReconnectInFlight, 1) == 1) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureControlConnectionAsync(logAttempt: false, connectTimeoutMs: 1200);
                }
                catch
                {
                    // swallow; maintenance timer or next command will retry
                }
                finally
                {
                    Interlocked.Exchange(ref _controlReconnectInFlight, 0);
                }
            });
        }
        // PATCH-END: ControlReconnect

        private async Task EnsureSafeCommandIntervalAsync(string commandDisplayName)
        {
            int waitMs = 0;
            lock (_controlStateLock)
            {
                if (_lastAnyCommandTime != DateTime.MinValue)
                {
                    int elapsedMs = (int)(DateTime.UtcNow - _lastAnyCommandTime).TotalMilliseconds;
                    waitMs = MinCommandGapMs - elapsedMs;
                }
            }

            if (waitMs <= 0)
            {
                return;
            }

            await WaitWithProgressAsync(waitMs,
                $"Ожидание безопасного интервала перед командой {commandDisplayName}...");
        }

        private async Task MaybeRunStabilizationPauseAsync(string commandDisplayName)
        {
            // Never apply 10s stabilization on PAUSE command.
            if (string.Equals(commandDisplayName, "ПАУЗА", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool shouldPause;
            lock (_controlStateLock)
            {
                shouldPause = _totalCommandCounter >= StabilizationCommandThreshold;
            }

            if (!shouldPause)
            {
                return;
            }

            OnStatusChanged($"⚠ Достигнуто {StabilizationCommandThreshold} команд. Запускаем стабилизационную паузу 10 секунд перед {commandDisplayName}.");
            await WaitWithProgressAsync(StabilizationPauseMs, "Стабилизация принтера, пожалуйста подождите...");
            lock (_controlStateLock)
            {
                _totalCommandCounter = 0;
            }
        }

        // PATCH-BEGIN: ControlHealthCheck
        private bool IsControlSocketUsable()
        {
            try
            {
                if (_controlClient == null || _controlStream == null) return false;
                if (!_controlClient.Connected) return false;
                var socket = _controlClient.Client;
                if (socket == null) return false;
                // If Poll indicates readable and no data available, socket is closed.
                bool readable = socket.Poll(0, SelectMode.SelectRead);
                if (readable && socket.Available == 0)
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        // PATCH-END: ControlHealthCheck

        private async Task WarmupControlConnectionAsync()
        {
            bool lockTaken = false;
            try
            {
                if (!_isMonitoring)
                {
                    return;
                }

                lockTaken = await _ioGate.WaitAsync(0);
                if (!lockTaken)
                {
                    return;
                }

                await EnsureControlConnectionAsync(logAttempt: false);
                OnStatusChanged("✓ Control-канал прогрет и готов к командам");
            }
            catch (Exception ex)
            {
                DisconnectControlConnection();
                OnStatusChanged($"⚠ Не удалось прогреть control-канал заранее: {ex.Message}");
            }
            finally
            {
                if (lockTaken)
                {
                    _ioGate.Release();
                }
            }
        }

        private async Task MaintainControlConnectionCallback()
        {
            if (!_isMonitoring) return;
            if (_isControlCommandInProgress) return;
            if (_isStatusMonitoringSuspended) return;
            if (_suppressControlMaintenance) return;
            if (_controlClient != null && _controlClient.Connected && _controlStream != null) return;
            if (Interlocked.Exchange(ref _isControlMaintenanceRunning, 1) == 1) return;

            bool lockTaken = false;
            try
            {
                lockTaken = await _ioGate.WaitAsync(0);
                if (!lockTaken) return;

                // Keep maintenance path lightweight but realistic for post-reboot reconnect.
                await EnsureControlConnectionAsync(logAttempt: false, connectTimeoutMs: 1200);
            }
            catch
            {
                // PATCH-BEGIN: ControlSafeStop
                // Avoid tearing down control channel on maintenance failure.
                // Next maintenance tick or next command will attempt reconnect.
                // PATCH-END: ControlSafeStop
            }
            finally
            {
                if (lockTaken)
                {
                    _ioGate.Release();
                }
                Interlocked.Exchange(ref _isControlMaintenanceRunning, 0);
            }
        }

        private int RegisterSuccessfulCommandSend()
        {
            lock (_controlStateLock)
            {
                _lastAnyCommandTime = DateTime.UtcNow;
                _totalCommandCounter++;
                return _totalCommandCounter;
            }
        }

        private async Task WaitWithProgressAsync(int totalMs, string text)
        {
            int elapsed = 0;
            while (elapsed < totalMs)
            {
                int percent = Math.Min(100, (int)Math.Round((double)elapsed * 100 / totalMs));
                OnStatusChanged($"PROGRESS|{percent}|{text} ({elapsed}/{totalMs} мс)");
                int delay = Math.Min(WaitProgressStepMs, totalMs - elapsed);
                await Task.Delay(delay);
                elapsed += delay;
            }
            OnStatusChanged($"PROGRESS|100|{text} ({totalMs}/{totalMs} мс)");
        }

        public async Task<bool> ClearWindowsSpoolerAsync()
        {
            OnStatusChanged("ℹ Жесткая очистка очереди печати Windows (все задания)...");
            try
            {
                // PATCH-BEGIN: Win7Spooler
                if (IsWindows7())
                {
                    OnStatusChanged("ℹ Режим Win7: используется WMI/WMIC + hard reset спулера");
                }
                // PATCH-END: Win7Spooler
                bool cleared = await ClearWindowsSpoolerStrictAsync(SpoolerStrictTimeoutMs);
                if (!cleared)
                {
                    OnStatusChanged("⚠ Очередь не очищена с первого раза, повторная попытка...");
                    cleared = await ClearWindowsSpoolerStrictAsync(SpoolerStrictTimeoutMs);
                }
                if (cleared)
                {
                    OnStatusChanged("✓ Очередь печати Windows полностью очищена");
                }
                else
                {
                    OnStatusChanged("✗ Очередь печати не очищена полностью после жестких попыток");
                }
                return cleared;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"✗ Ошибка жесткой очистки спулера Windows: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ClearWindowsSpoolerStrictAsync(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                // PATCH-BEGIN: UnifiedFinishWorkflow
                attempt++;
                OnStatusChanged($"ℹ Очистка очереди Windows: попытка {attempt}, прошло {sw.ElapsedMilliseconds} мс");
                // PATCH-END: UnifiedFinishWorkflow
                int before = await TryGetPrintQueueJobCountAsync();
                OnStatusChanged($"ℹ Заданий в очереди перед очисткой: {before}");
                if (before == 0)
                {
                    return true;
                }

                foreach (var method in GetSpoolerClearMethods())
                {
                    var (description, command) = method;
                    OnStatusChanged($"ℹ Попытка очистки очереди: {description}");
                    await RunPowerShellAsync(command);

                    // PATCH-BEGIN: SpoolerVerify
                    // If spooler was restarted, wait for it to be Running before verifying queue.
                    if (command.IndexOf("spooler", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        await WaitForSpoolerRunningAsync(5000, 200);
                    }
                    // PATCH-END: SpoolerVerify

                    // PATCH-BEGIN: SpoolerEarlyExit
                    // Re-check queue after each method and stop immediately when empty.
                    int afterMethod = await TryGetPrintQueueJobCountAsync();
                    OnStatusChanged($"ℹ Заданий после метода '{description}': {afterMethod}");
                    if (afterMethod == 0)
                    {
                        OnStatusChanged($"✓ Очередь очищена методом: {description}");
                        return true;
                    }
                    // PATCH-END: SpoolerEarlyExit
                }

                int after = await TryGetPrintQueueJobCountAsync();
                OnStatusChanged($"ℹ Заданий в очереди после цикла очистки: {after}");
                if (after == 0)
                {
                    return true;
                }
                // PATCH-BEGIN: SpoolerVerify
                // Double-check shortly after to avoid phantom job readings.
                if (after > 0)
                {
                    await Task.Delay(500);
                    int after2 = await TryGetPrintQueueJobCountAsync();
                    OnStatusChanged($"ℹ Повторная проверка очереди: {after2}");
                    if (after2 == 0)
                    {
                        return true;
                    }
                    if (after2 < 0)
                    {
                        await Task.Delay(SpoolerRetryDelayMs);
                        continue;
                    }
                }
                // PATCH-END: SpoolerVerify
                // PATCH-BEGIN: SpoolerVerify
                // Unknown count: retry until timeout instead of failing immediately.
                if (after < 0)
                {
                    await Task.Delay(SpoolerRetryDelayMs);
                    continue;
                }
                // PATCH-END: SpoolerVerify

                await Task.Delay(SpoolerRetryDelayMs);
            }

            // PATCH-BEGIN: UnifiedFinishWorkflow
            OnStatusChanged($"⚠ Очистка очереди Windows превысила таймаут {timeoutMs} мс");
            // PATCH-END: UnifiedFinishWorkflow
            return false;
        }

        private IEnumerable<(string Description, string Command)> GetSpoolerClearMethods()
        {
            // PATCH-BEGIN: Win7Spooler
            if (IsWindows7())
            {
                // Win7: avoid PrintManagement/CIM. Use WMI + WMIC + hard reset.
                string wmiAllWin7 = "Get-WmiObject Win32_PrintJob -ErrorAction SilentlyContinue | ForEach-Object { $_.Delete() | Out-Null }";
                string wmicAllWin7 = "wmic printjob delete";
                string spoolerResetWin7 = "Stop-Service Spooler -Force -ErrorAction SilentlyContinue; " +
                                      "Start-Sleep -Milliseconds 400; " +
                                      "Remove-Item -Path \"$env:SystemRoot\\System32\\spool\\PRINTERS\\*\" -Force -ErrorAction SilentlyContinue; " +
                                      "Start-Service Spooler -ErrorAction SilentlyContinue";
                string killResetWin7 = "Stop-Service Spooler -Force -ErrorAction SilentlyContinue; " +
                                   "Start-Sleep -Milliseconds 400; " +
                                   "taskkill /f /im spoolsv.exe; " +
                                   "Start-Sleep -Milliseconds 400; " +
                                   "Remove-Item -Path \"$env:SystemRoot\\System32\\spool\\PRINTERS\\*\" -Force -ErrorAction SilentlyContinue; " +
                                   "Start-Service Spooler -ErrorAction SilentlyContinue";

                yield return ("WMI (remove all Win32_PrintJob)", wmiAllWin7);
                yield return ("WMIC (remove all print jobs)", wmicAllWin7);
                yield return ("Spooler reset + purge spool files", spoolerResetWin7);
                yield return ("Spooler kill + purge + restart", killResetWin7);
                yield break;
            }
            // PATCH-END: Win7Spooler

            // Method 1: PrintManagement remove all jobs from all printer queues
            string printMgmtAll =
                "$printers = Get-Printer -ErrorAction SilentlyContinue; " +
                "foreach ($p in $printers) { " +
                "Get-PrintJob -PrinterName $p.Name -ErrorAction SilentlyContinue | " +
                "Remove-PrintJob -ErrorAction SilentlyContinue }";

            // Method 2: PrintManagement remove by explicit Job ID
            string printMgmtById =
                "$printers = Get-Printer -ErrorAction SilentlyContinue; " +
                "foreach ($p in $printers) { " +
                "$jobs = Get-PrintJob -PrinterName $p.Name -ErrorAction SilentlyContinue; " +
                "foreach ($j in $jobs) { " +
                "Remove-PrintJob -PrinterName $p.Name -ID $j.ID -ErrorAction SilentlyContinue } }";

            // Method 3: Legacy WMI remove all print jobs
            string wmiAll = "Get-WmiObject Win32_PrintJob -ErrorAction SilentlyContinue | ForEach-Object { $_.Delete() | Out-Null }";

            // Method 4: CIM remove all print jobs
            string cimAll = "Get-CimInstance Win32_PrintJob -ErrorAction SilentlyContinue | Remove-CimInstance -ErrorAction SilentlyContinue";

            // Method 5: Hard spooler reset + delete spool files
            string spoolerReset = "Stop-Service Spooler -Force -ErrorAction SilentlyContinue; " +
                                  "Start-Sleep -Milliseconds 400; " +
                                  "Remove-Item -Path \"$env:SystemRoot\\System32\\spool\\PRINTERS\\*\" -Force -ErrorAction SilentlyContinue; " +
                                  "Start-Service Spooler -ErrorAction SilentlyContinue";

            // Method 6: cmd fallback for legacy systems
            string cmdReset = "cmd /c \"net stop spooler /y & del /q /f %systemroot%\\System32\\spool\\PRINTERS\\*.* & net start spooler\"";

            yield return ("PrintManagement (remove all jobs)", printMgmtAll);
            yield return ("PrintManagement (remove by job ID)", printMgmtById);
            yield return ("WMI (remove all Win32_PrintJob)", wmiAll);
            yield return ("CIM (remove all Win32_PrintJob)", cimAll);
            yield return ("Spooler reset + purge spool files", spoolerReset);
            yield return ("CMD spooler reset fallback", cmdReset);
        }

        private async Task<int> TryGetPrintQueueJobCountAsync()
        {
            // PATCH-BEGIN: SpoolerVerify
            // Return -1 when count is unknown, so caller can retry.
            int unknown = -1;
            // PATCH-END: SpoolerVerify

            // PATCH-BEGIN: Win7Spooler
            if (IsWindows7())
            {
                // Win7: rely on WMI count only.
                string wmiCountWin7 =
                    "$jobs = Get-WmiObject Win32_PrintJob -ErrorAction SilentlyContinue; " +
                    "Write-Output (($jobs | Measure-Object).Count)";
                string count = await RunPowerShellForOutputAsync(wmiCountWin7);
                if (TryParseFirstInt(count, out int jobs))
                {
                    return jobs;
                }
                return unknown;
            }
            // PATCH-END: Win7Spooler

            // Win8+ / Win10/11 path
            string printMgmtCount =
                "$printers = Get-Printer -ErrorAction SilentlyContinue; " +
                "$jobs=@(); foreach($p in $printers) { $jobs += Get-PrintJob -PrinterName $p.Name -ErrorAction SilentlyContinue }; " +
                "Write-Output (($jobs | Measure-Object).Count)";
            string count1 = await RunPowerShellForOutputAsync(printMgmtCount);
            // PATCH-BEGIN: SpoolerVerify
            if (TryParseFirstInt(count1, out int jobs1))
            {
                return jobs1;
            }
            // PATCH-END: SpoolerVerify

            // Win7 fallback
            string wmiCount =
                "$jobs = Get-WmiObject Win32_PrintJob -ErrorAction SilentlyContinue; " +
                "Write-Output (($jobs | Measure-Object).Count)";
            string count2 = await RunPowerShellForOutputAsync(wmiCount);
            // PATCH-BEGIN: SpoolerVerify
            if (TryParseFirstInt(count2, out int jobs2))
            {
                return jobs2;
            }
            // PATCH-END: SpoolerVerify

            return unknown;
        }

        // PATCH-BEGIN: Win7Spooler
        private static bool IsWindows7()
        {
            // Windows 7 = 6.1
            var v = Environment.OSVersion.Version;
            return v.Major == 6 && v.Minor == 1;
        }
        // PATCH-END: Win7Spooler

        // PATCH-BEGIN: SpoolerVerify
        private async Task<bool> WaitForSpoolerRunningAsync(int timeoutMs, int pollMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                string status = await RunPowerShellForOutputAsync("Get-Service Spooler -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Status");
                if (string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                await Task.Delay(pollMs);
            }
            return false;
        }
        // PATCH-END: SpoolerVerify

        private async Task<bool> RunPowerShellAsync(string command)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            return await Task.Run(() =>
            {
                try
                {
                    using var process = Process.Start(startInfo);
                    if (process == null)
                        return false;

                    bool exited = process.WaitForExit(AppConstants.Printer.SpoolerCleanupTimeoutMs);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }

                    return process.ExitCode == 0;
                }
                catch
                {
                    return false;
                }
            });
        }

        private async Task<string> RunPowerShellForOutputAsync(string command)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            return await Task.Run(() =>
            {
                try
                {
                    using var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        return string.Empty;
                    }

                    bool exited = process.WaitForExit(AppConstants.Printer.SpoolerCleanupTimeoutMs);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        return string.Empty;
                    }

                    string stdout = process.StandardOutput.ReadToEnd().Trim();
                    string stderr = process.StandardError.ReadToEnd().Trim();
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        // PATCH-BEGIN: SpoolerVerify
                        string snippet = stderr.Length > 200 ? stderr.Substring(0, 200) : stderr;
                        OnStatusChanged($"⚠ PowerShell stderr: {snippet}");
                        // PATCH-END: SpoolerVerify
                    }

                    return stdout;
                }
                catch
                {
                    return string.Empty;
                }
            });
        }

        // PATCH-BEGIN: SpoolerVerify
        private static bool TryParseFirstInt(string input, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var match = Regex.Match(input, @"\d+");
            if (!match.Success) return false;
            return int.TryParse(match.Value, out value);
        }
        // PATCH-END: SpoolerVerify

        public async Task<bool> WaitPrinterBackOnlineAsync(int timeoutMs = 45000, int pollIntervalMs = 250)
        {
            OnStatusChanged("ℹ Ожидание восстановления ответа принтера после перезагрузки...");
            _suppressControlMaintenance = true;

            // Reset reconnect backoff to avoid long dead periods after reboot.
            _persistentConnectFailCount = 0;
            _nextPersistentConnectAttemptUtc = DateTime.MinValue;
            Interlocked.Exchange(ref _lastControlIoUtcTicks, 0);
            DisconnectPersistentConnection();
            DisconnectControlConnection();

            try
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    try
                    {
                        // Fast path: first confirmed TCP availability is enough to resume workflow.
                        // Full status recovery is primed asynchronously in the background.
                        bool tcpReady = await IsPrinterAvailableAsync(500);
                        if (tcpReady)
                        {
                            OnStatusChanged("✓ Принтер снова доступен по сети");
                            bool monitoringPrimed = await PrimeMonitoringAfterRecoveryAsync();
                            if (monitoringPrimed)
                            {
                                return true;
                            }
                        }

                        // Stronger path: status command returns payload.
                        var status = await GetStatusWithQuickConnectionAsync();
                        if (status != null)
                        {
                            OnStatusChanged("✓ Принтер снова отвечает на запросы статуса");
                            return true;
                        }
                    }
                    catch
                    {
                        // ignore and continue retries within timeout
                    }

                    await Task.Delay(pollIntervalMs);
                }
            }
            finally
            {
                _suppressControlMaintenance = false;
            }

            OnErrorOccurred("✗ Принтер не вышел в онлайн после перезагрузки в отведенное время.");
            return false;
        }

        private async Task<bool> PrimeMonitoringAfterRecoveryAsync()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await Task.Delay(200);

                int attempt = 0;
                while (sw.ElapsedMilliseconds < RecoveryStatusWarmupTimeoutMs)
                {
                    attempt++;

                    // Ensure we don't stay on stale sockets from pre-reboot session.
                    DisconnectControlConnection();
                    DisconnectPersistentConnection();

                    // Restore persistent channel quickly if printer already accepts connections.
                    await ConnectPersistentConnectionAsync(1000);

                    // First successful status read means monitoring can continue normally.
                    var status = await GetStatusWithQuickConnectionAsync();
                    if (status != null)
                    {
                        await PrimeControlChannelAfterRecoveryAsync();
                        OnStatusChanged($"✓ Мониторинг статуса восстановлен после перезагрузки (попытка {attempt})");
                        return true;
                    }

                    await Task.Delay(RecoveryStatusWarmupPollMs);
                }
            }
            catch (Exception ex)
            {
                OnStatusChanged($"⚠ Фоновый прогрев мониторинга после восстановления не завершен: {ex.Message}");
            }

            OnStatusChanged("⚠ Принтер доступен по сети, но ответы статуса пока не восстановлены");
            return false;
        }

        private async Task PrimeControlChannelAfterRecoveryAsync()
        {
            bool lockTaken = false;
            try
            {
                for (int attempt = 1; attempt <= 8; attempt++)
                {
                    CancelInFlightStatusIo();
                    lockTaken = await _ioGate.WaitAsync(600);
                    if (!lockTaken)
                    {
                        await Task.Delay(150);
                        continue;
                    }

                    try
                    {
                        await EnsureControlConnectionAsync(logAttempt: false, connectTimeoutMs: 1500);
                        OnStatusChanged($"✓ Control-канал восстановлен после перезагрузки (попытка {attempt})");
                        return;
                    }
                    finally
                    {
                        _ioGate.Release();
                        lockTaken = false;
                    }
                }
            }
            catch (Exception ex)
            {
                DisconnectControlConnection();
                OnStatusChanged($"⚠ Не удалось заранее восстановить control-канал после перезагрузки: {ex.Message}");
            }
            finally
            {
                if (lockTaken) _ioGate.Release();
            }
        }

        private async Task<PrinterStatus> GetStatusWithQuickConnectionAsync()
        {
            var sw = Stopwatch.StartNew();
            TcpClient client = null;
            NetworkStream stream = null;
            bool usesControlConnection = false;
            bool lockTaken = false;
            bool statusAttempted = false;
            var statusIoTimeoutMs = StatusIoTimeoutMs;

            try
            {
                lockTaken = await _ioGate.WaitAsync(0);
                if (!lockTaken)
                {
                    MarkStatusOutcome(StatusOutcomeSkipped);
                    return null; // skip if another command is in progress
                }
                // Give priority to control commands (pause/resume) if they were requested.
                if (_isControlCommandInProgress)
                {
                    MarkStatusOutcome(StatusOutcomeSkipped);
                    return null;
                }
                if (IsWithinControlToStatusGap())
                {
                    MarkStatusOutcome(StatusOutcomeSkipped);
                    return null;
                }
                statusAttempted = true;
                if (_controlClient != null && _controlClient.Connected && _controlStream != null && IsControlSocketUsable())
                {
                    usesControlConnection = true;
                    stream = _controlStream;
                    stream.ReadTimeout = statusIoTimeoutMs;
                    stream.WriteTimeout = statusIoTimeoutMs;
                }
                else
                {
                    client = new TcpClient();
                    client.ReceiveTimeout = statusIoTimeoutMs;
                    client.SendTimeout = statusIoTimeoutMs;

                    var connectTask = client.ConnectAsync(_printerIp, _printerPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(statusIoTimeoutMs)) != connectTask)
                    {
                        throw new SocketException((int)SocketError.TimedOut);
                    }
                    await connectTask;

                    stream = client.GetStream();
                    stream.ReadTimeout = statusIoTimeoutMs;
                    stream.WriteTimeout = statusIoTimeoutMs;
                }
                if (_isControlCommandInProgress)
                {
                    return null;
                }

                byte[] statusCommand = AppConstants.Printer.StatusCommand;
                using (var ioCts = new CancellationTokenSource(statusIoTimeoutMs))
                {
                    SetCurrentStatusIoCts(ioCts);
                    await stream.WriteAsync(statusCommand, 0, statusCommand.Length, ioCts.Token);
                    await stream.FlushAsync(ioCts.Token);

                    byte[] response = new byte[8];
                    int bytesRead = await stream.ReadAsync(response, 0, response.Length, ioCts.Token);

                    if (bytesRead > 0)
                    {
                        Console.WriteLine($"[PrinterMonitor] Status(quick) success in {sw.ElapsedMilliseconds}ms");
                        MarkStatusOutcome(StatusOutcomeSuccess);
                        return ParsePrinterStatus(response[0]);
                    }
                }
                MarkStatusOutcome(StatusOutcomeTransportError);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[PrinterMonitor] Status(quick) I/O timeout after {sw.ElapsedMilliseconds}ms");
                MarkStatusOutcome(StatusOutcomeTransportError);
                if (usesControlConnection)
                {
                    // PATCH-BEGIN: ControlHealthCheck
                    // Do not drop control channel on status timeout; allow commands to use it.
                    // PATCH-END: ControlHealthCheck
                }
                else
                {
                    try { stream?.Close(); } catch { }
                    try { client?.Close(); } catch { }
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                // Expected timeout, ignore
                Console.WriteLine($"[PrinterMonitor] Status(quick) socket timeout after {sw.ElapsedMilliseconds}ms");
                MarkStatusOutcome(StatusOutcomeTransportError);
                if (usesControlConnection)
                {
                    // PATCH-BEGIN: ControlHealthCheck
                    // Do not drop control channel on status timeout; allow commands to use it.
                    // PATCH-END: ControlHealthCheck
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Background status check error: {ex.Message}");
                Console.WriteLine($"[PrinterMonitor] Status(quick) failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                MarkStatusOutcome(StatusOutcomeTransportError);
                if (usesControlConnection)
                {
                    // PATCH-BEGIN: ControlHealthCheck
                    // Avoid tearing down control channel on status failure.
                    // PATCH-END: ControlHealthCheck
                }
            }
            finally
            {
                if (!usesControlConnection)
                {
                    stream?.Close();
                    client?.Close();
                }
                ClearCurrentStatusIoCts();
                if (statusAttempted) MarkStatusIoNow();
                if (lockTaken) _ioGate.Release();
            }

            return null;
        }

        private bool _isChecking = false;

        private async Task MonitorCallback()
        {
            if (!_isMonitoring) return;
            if (_isStatusMonitoringSuspended) return;
            if (_isControlCommandInProgress) return;
            if (_isChecking) return; 

            _isChecking = true;

            try
            {
                var status = await GetStatusAsync();

                if (status != null)
                {
                    // PATCH-BEGIN: StatusErrorThreshold
                    _consecutiveStatusTransportErrors = 0;
                    _statusTransportErrorNotified = false;
                    // PATCH-END: StatusErrorThreshold
                    var validatedState = ValidatePrinterState(status.State);

                    if (!_isConnectionActive)
                    {
                        _isConnectionActive = true;
                        ConnectionStatusChanged?.Invoke(this, true);
                        
                        if (_monitorTimer != null)
                        {
                            _monitorTimer.Change(0, _currentMode == MonitoringMode.Active ? AppConstants.Printer.ActiveMonitorIntervalMs : AppConstants.Printer.BackgroundMonitorIntervalMs);
                        }
                    }

                    if (ShouldRaiseStateChangeEvent(validatedState))
                    {
                        _previousValidatedState = validatedState;

                        OnStateChanged(new PrinterStateChangedEventArgs
                        {
                            State = validatedState,
                            Timestamp = DateTime.Now,
                            FullStatus = status
                        });
                    }
                }
                else
                {
                    if (ReadLastStatusOutcome() == StatusOutcomeTransportError && _isConnectionActive)
                    {
                        _isConnectionActive = false;
                        ConnectionStatusChanged?.Invoke(this, false);
                        // PATCH-BEGIN: StatusTransportErrorDialog
                        StatusTransportError?.Invoke(this, EventArgs.Empty);
                        // PATCH-END: StatusTransportErrorDialog
                        
                        if (_monitorTimer != null)
                        {
                            _monitorTimer.Change(0, AppConstants.Printer.BackgroundMonitorIntervalMs);
                        }
                    }

                    // PATCH-BEGIN: StatusErrorThreshold
                    if (ReadLastStatusOutcome() == StatusOutcomeTransportError)
                    {
                        _consecutiveStatusTransportErrors++;
                        if (!_statusTransportErrorNotified && _consecutiveStatusTransportErrors >= 30)
                        {
                            _statusTransportErrorNotified = true;
                            StatusTransportErrorThreshold?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    // PATCH-END: StatusErrorThreshold

                    _consecutiveErrorCount = 0;
                    _consecutiveNormalCount = 0;
                }
            }
            catch (Exception ex)
            {
                _consecutiveErrorCount = 0;
                _consecutiveNormalCount = 0;
                OnErrorOccurred($"Background monitoring error: {ex.Message}");
            }
            finally
            {
                _isChecking = false;
            }
        }

        private void SuspendStatusMonitoringForControl()
        {
            lock (_lockObject)
            {
                _monitorSuspendDepth++;
                if (_monitorSuspendDepth > 1)
                {
                    return;
                }

                _isStatusMonitoringSuspended = true;
                try
                {
                    _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch
                {
                    // ignore timer race; monitor guard flag remains active
                }
            }
        }

        private void ResumeStatusMonitoringAfterControl()
        {
            lock (_lockObject)
            {
                if (_monitorSuspendDepth == 0)
                {
                    return;
                }

                _monitorSuspendDepth--;
                if (_monitorSuspendDepth > 0)
                {
                    return;
                }

                _isStatusMonitoringSuspended = false;
                if (!_isMonitoring)
                {
                    return;
                }

                int interval = _currentMode == MonitoringMode.Active
                    ? AppConstants.Printer.ActiveMonitorIntervalMs
                    : AppConstants.Printer.BackgroundMonitorIntervalMs;
                try
                {
                    _monitorTimer?.Change(0, interval);
                }
                catch
                {
                    // Timer may be disposed/raced during control command lifecycle.
                    // Recreate it so monitoring always resumes without requiring manual restart.
                    _monitorTimer?.Dispose();
                    _monitorTimer = new Timer(async (state) => await MonitorCallback(), null, 0, interval);
                }
            }
        }

        #endregion

        #region Active Mode (Blocking with Persistent Connection)

        private async Task<bool> ConnectPersistentConnectionAsync(int connectTimeoutMs = AppConstants.Printer.ConnectionTimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            Console.WriteLine($"[PrinterMonitor] Persistent connect start {DateTime.Now:O}");
            try
            {
                if (DateTime.UtcNow < _nextPersistentConnectAttemptUtc)
                {
                    return false;
                }

                DisconnectPersistentConnection(); 

                _persistentClient = new TcpClient();
                _persistentClient.ReceiveTimeout = AppConstants.Printer.PersistentConnectionTimeoutMs;
                _persistentClient.SendTimeout = AppConstants.Printer.PersistentConnectionTimeoutMs;

                var connectTask = _persistentClient.ConnectAsync(_printerIp, _printerPort);
                if (await Task.WhenAny(connectTask, Task.Delay(connectTimeoutMs)) != connectTask)
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }
                await connectTask;

                _persistentStream = _persistentClient.GetStream();
                _persistentStream.ReadTimeout = AppConstants.Printer.PersistentConnectionTimeoutMs;
                _persistentStream.WriteTimeout = AppConstants.Printer.PersistentConnectionTimeoutMs;

                OnStatusChanged("Persistent connection established");
                _persistentConnectFailCount = 0;
                _nextPersistentConnectAttemptUtc = DateTime.MinValue;
                Console.WriteLine($"[PrinterMonitor] Persistent connect success in {sw.ElapsedMilliseconds}ms");
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to establish persistent connection: {ex.Message}");
                _persistentConnectFailCount++;
                int delayMs = GetPersistentReconnectDelayMs(_persistentConnectFailCount);
                _nextPersistentConnectAttemptUtc = DateTime.UtcNow.AddMilliseconds(delayMs);
                Console.WriteLine($"[PrinterMonitor] Persistent connect failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                DisconnectPersistentConnection();
                return false;
            }
        }

        // PATCH-BEGIN: UnifiedPrinterRecoveryWorkflow
        public async Task<PrinterRecoveryResult> RunQueueClearAndRebootWorkflowAsync(
            string caller,
            int onlineTimeoutMs = 45000,
            int onlinePollIntervalMs = 400,
            bool continueWhenSpoolerNotConfirmed = true)
        {
            string callerName = string.IsNullOrWhiteSpace(caller) ? "UnknownCaller" : caller.Trim();
            OnStatusChanged($"ℹ [{callerName}] Запуск общего сценария: очистка очереди -> перезагрузка -> проверка online");
            var totalSw = Stopwatch.StartNew();

            var result = new PrinterRecoveryResult
            {
                Caller = callerName
            };

            var stageSw = Stopwatch.StartNew();
            OnStatusChanged($"ℹ [{callerName}] Этап 1/3: очистка очереди Windows...");
            result.SpoolerCleared = await ClearWindowsSpoolerAsync();
            OnStatusChanged($"ℹ [{callerName}] Этап 1/3 завершен за {stageSw.ElapsedMilliseconds} мс. Результат: {(result.SpoolerCleared ? "OK" : "NOT_CONFIRMED")}");
            if (!result.SpoolerCleared)
            {
                OnStatusChanged($"⚠ [{callerName}] Очистка очереди не подтверждена");
                if (!continueWhenSpoolerNotConfirmed)
                {
                    result.IsSuccess = false;
                    result.FailureStage = "SpoolerClear";
                    result.ErrorMessage = "Очистка очереди Windows не подтверждена";
                    OnStatusChanged($"✗ [{callerName}] Сценарий завершен с ошибкой на этапе {result.FailureStage}. Общее время: {totalSw.ElapsedMilliseconds} мс");
                    return result;
                }
            }

            stageSw.Restart();
            OnStatusChanged($"ℹ [{callerName}] Этап 2/3: отправка команды перезагрузки...");
            result.RebootCommandSent = await ClearPrinterQueueAsync();
            OnStatusChanged($"ℹ [{callerName}] Этап 2/3 завершен за {stageSw.ElapsedMilliseconds} мс. Результат: {(result.RebootCommandSent ? "OK" : "FAIL")}");
            if (!result.RebootCommandSent)
            {
                result.IsSuccess = false;
                result.FailureStage = "RebootCommand";
                result.ErrorMessage = "Не удалось отправить команду перезагрузки принтера";
                OnStatusChanged($"✗ [{callerName}] {result.ErrorMessage}");
                OnStatusChanged($"✗ [{callerName}] Сценарий завершен с ошибкой на этапе {result.FailureStage}. Общее время: {totalSw.ElapsedMilliseconds} мс");
                return result;
            }

            stageSw.Restart();
            OnStatusChanged($"ℹ [{callerName}] Этап 3/3: ожидание восстановления связи с принтером...");
            result.PrinterBackOnline = await WaitPrinterBackOnlineAsync(onlineTimeoutMs, onlinePollIntervalMs);
            OnStatusChanged($"ℹ [{callerName}] Этап 3/3 завершен за {stageSw.ElapsedMilliseconds} мс. Результат: {(result.PrinterBackOnline ? "OK" : "FAIL")}");
            if (!result.PrinterBackOnline)
            {
                result.IsSuccess = false;
                result.FailureStage = "WaitBackOnline";
                result.ErrorMessage = "Принтер не вернулся online после перезагрузки";
                OnStatusChanged($"✗ [{callerName}] {result.ErrorMessage}");
                OnStatusChanged($"✗ [{callerName}] Сценарий завершен с ошибкой на этапе {result.FailureStage}. Общее время: {totalSw.ElapsedMilliseconds} мс");
                return result;
            }

            result.IsSuccess = true;
            OnStatusChanged($"✓ [{callerName}] Сценарий очистки/перезагрузки успешно завершен за {totalSw.ElapsedMilliseconds} мс");
            return result;
        }
        // PATCH-END: UnifiedPrinterRecoveryWorkflow

        private void DisconnectPersistentConnection()
        {
            try
            {
                _persistentStream?.Close();
                _persistentClient?.Close();
            }
            catch { }
            finally
            {
                _persistentStream = null;
                _persistentClient = null;
            }
        }

        private async Task<PrinterStatus> GetStatusFromPersistentConnectionAsync()
        {
            var sw = Stopwatch.StartNew();
            var statusIoTimeoutMs = StatusIoTimeoutMs;
            bool lockTaken = await _ioGate.WaitAsync(0);
            bool statusAttempted = false;
            if (!lockTaken)
            {
                MarkStatusOutcome(StatusOutcomeSkipped);
                return null; // skip if another command is in progress
            }
            // Give priority to control commands (pause/resume) if they were requested.
            if (_isControlCommandInProgress)
            {
                MarkStatusOutcome(StatusOutcomeSkipped);
                _ioGate.Release();
                return null;
            }
            if (IsWithinControlToStatusGap())
            {
                MarkStatusOutcome(StatusOutcomeSkipped);
                _ioGate.Release();
                return null;
            }
            if (_persistentStream == null || _persistentClient == null || !_persistentClient.Connected)
            {
                if (!await ConnectPersistentConnectionAsync(PersistentConnectTimeoutMs))
                {
                    MarkStatusOutcome(StatusOutcomeTransportError);
                    if (lockTaken)
                    {
                        _ioGate.Release();
                        lockTaken = false;
                    }
                    return null;
                }
            }

            try
            {
                statusAttempted = true;
                byte[] statusCommand = AppConstants.Printer.StatusCommand;
                using (var ioCts = new CancellationTokenSource(statusIoTimeoutMs))
                {
                    SetCurrentStatusIoCts(ioCts);
                    await _persistentStream.WriteAsync(statusCommand, 0, statusCommand.Length, ioCts.Token);
                    await _persistentStream.FlushAsync(ioCts.Token);

                    byte[] response = new byte[8];
                    int bytesRead = await _persistentStream.ReadAsync(response, 0, response.Length, ioCts.Token);

                    if (bytesRead > 0)
                    {
                        Console.WriteLine($"[PrinterMonitor] Status(persistent) success in {sw.ElapsedMilliseconds}ms");
                        MarkStatusOutcome(StatusOutcomeSuccess);
                        return ParsePrinterStatus(response[0]);
                    }
                }
                MarkStatusOutcome(StatusOutcomeTransportError);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[PrinterMonitor] Status(persistent) I/O timeout after {sw.ElapsedMilliseconds}ms");
                MarkStatusOutcome(StatusOutcomeTransportError);
                DisconnectPersistentConnection();
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Active mode status check error: {ex.Message}");
                Console.WriteLine($"[PrinterMonitor] Status(persistent) failed after {sw.ElapsedMilliseconds}ms: {ex.Message}");
                MarkStatusOutcome(StatusOutcomeTransportError);
                DisconnectPersistentConnection();
                
                if (await ConnectPersistentConnectionAsync(PersistentConnectTimeoutMs))
                {
                    if (lockTaken)
                    {
                        _ioGate.Release();
                        lockTaken = false;
                    }
                    return await GetStatusFromPersistentConnectionAsync();
                }
            }
            finally
            {
                ClearCurrentStatusIoCts();
                if (statusAttempted) MarkStatusIoNow();
                if (lockTaken) _ioGate.Release();
            }

            return null;
        }

        private bool IsWithinControlToStatusGap()
        {
            long lastControlTicks = Interlocked.Read(ref _lastControlIoUtcTicks);
            if (lastControlTicks <= 0) return false;
            return RemainingGapMs(lastControlTicks) > 0;
        }

        private static int RemainingGapMs(long sinceUtcTicks)
        {
            var since = new DateTime(sinceUtcTicks, DateTimeKind.Utc);
            int elapsedMs = (int)(DateTime.UtcNow - since).TotalMilliseconds;
            int remaining = StatusControlGapMs - elapsedMs;
            return remaining > 0 ? remaining : 0;
        }

        private void MarkStatusIoNow()
        {
            Interlocked.Exchange(ref _lastStatusIoUtcTicks, DateTime.UtcNow.Ticks);
        }

        private void MarkStatusOutcome(int outcome)
        {
            Interlocked.Exchange(ref _lastStatusOutcome, outcome);
        }

        private int ReadLastStatusOutcome()
        {
            return Volatile.Read(ref _lastStatusOutcome);
        }

        private void MarkControlIoNow()
        {
            Interlocked.Exchange(ref _lastControlIoUtcTicks, DateTime.UtcNow.Ticks);
        }

        private void SetCurrentStatusIoCts(CancellationTokenSource cts)
        {
            lock (_statusIoCtsLock)
            {
                _currentStatusIoCts = cts;
            }
        }

        private void ClearCurrentStatusIoCts()
        {
            lock (_statusIoCtsLock)
            {
                _currentStatusIoCts = null;
            }
        }

        private void CancelInFlightStatusIo()
        {
            CancellationTokenSource cts;
            lock (_statusIoCtsLock)
            {
                cts = _currentStatusIoCts;
            }
            try
            {
                cts?.Cancel();
            }
            catch
            {
                // ignore cancellation race with disposal
            }
        }

        private static int GetPersistentReconnectDelayMs(int failCount)
        {
            int step = failCount <= 1 ? 1 : 1 << Math.Min(failCount - 1, 4); // 1,2,4,8,16
            int delayMs = PersistentReconnectBaseDelayMs * step;
            return delayMs > PersistentReconnectMaxDelayMs ? PersistentReconnectMaxDelayMs : delayMs;
        }

        private async Task ActiveMonitoringLoopAsync(CancellationToken cancellationToken)
        {
            OnStatusChanged("Active monitoring loop started");

            while (!cancellationToken.IsCancellationRequested && _currentMode == MonitoringMode.Active)
            {
                try
                {
                    var status = await GetStatusAsync();

                    if (status != null)
                    {
                        var validatedState = ValidatePrinterState(status.State);

                        if (ShouldRaiseStateChangeEvent(validatedState))
                        {
                            _previousValidatedState = validatedState;

                            OnStateChanged(new PrinterStateChangedEventArgs
                            {
                                State = validatedState,
                                Timestamp = DateTime.Now,
                                FullStatus = status
                            });
                        }
                    }

                    await Task.Delay(AppConstants.Printer.ActiveMonitorIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break; 
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Active monitoring loop error: {ex.Message}");
                    DisconnectPersistentConnection(); 
                    await Task.Delay(1000, cancellationToken); 
                }
            }

            OnStatusChanged("Active monitoring loop stopped");
        }

        #endregion

        #region Status Parsing and Validation

        private PrinterStatus ParsePrinterStatus(byte statusByte)
        {
            var status = new PrinterStatus
            {
                RawStatusByte = statusByte,
                Method = _currentMode == MonitoringMode.Active ? "ESC!? (Active)" : "ESC!? (Background)"
            };

            status.State = new PrinterState();

            if (statusByte == 0x00)
            {
                status.State.Status = PrinterStatusType.Normal;
                status.State.IsHeadOpen = false;
                status.State.IsPaused = false;
                status.State.IsPrinting = false;
                return status;
            }

            bool headOpen = (statusByte & 0x01) != 0;
            bool paperJam = (statusByte & 0x02) != 0;
            bool paperOut = (statusByte & 0x04) != 0;
            bool ribbonOut = (statusByte & 0x08) != 0;
            bool paused = (statusByte & 0x10) != 0;
            bool printing = (statusByte & 0x20) != 0;
            bool coverOpen = (statusByte & 0x40) != 0;

            status.State.IsHeadOpen = headOpen || coverOpen;
            status.State.IsPaused = paused;
            status.State.IsPrinting = printing;

            if (headOpen || coverOpen)
            {
                status.State.Status = PrinterStatusType.HeadOpen;
                status.State.ErrorMessage = "Открыта печатающая головка";
            }
            else if (paperJam)
            {
                status.State.Status = PrinterStatusType.PaperJam;
                status.State.ErrorMessage = "Замятие бумаги";
            }
            else if (paperOut)
            {
                status.State.Status = PrinterStatusType.PaperOut;
                status.State.ErrorMessage = "Закончилась бумага";
            }
            else if (ribbonOut)
            {
                status.State.Status = PrinterStatusType.RibbonOut;
                status.State.ErrorMessage = "Закончилась красящая лента";
            }
            else if (paused && !printing)
            {
                status.State.Status = PrinterStatusType.Paused;
                status.State.ErrorMessage = "Принтер на паузе";
            }
            else if (printing)
            {
                status.State.Status = PrinterStatusType.Printing;
                status.State.ErrorMessage = null;
            }
            else
            {
                status.State.Status = PrinterStatusType.Unknown;
                status.State.ErrorMessage = $"Неизвестное состояние (код: 0x{statusByte:X2})";
            }

            return status;
        }

        private PrinterState ValidatePrinterState(PrinterState currentState)
        {
            bool isError = currentState.Status != PrinterStatusType.Normal && 
                          currentState.Status != PrinterStatusType.Printing;

            int errorThreshold = _currentMode == MonitoringMode.Active ? 1 : MIN_CONSECUTIVE_READINGS_FOR_ERROR;
            int normalThreshold = _currentMode == MonitoringMode.Active ? 2 : MIN_CONSECUTIVE_READINGS_FOR_NORMAL;

            if (isError)
            {
                _consecutiveErrorCount++;
                _consecutiveNormalCount = 0;
                _wasPrintingBeforeError = _previousValidatedState.IsPrinting || 
                                         _previousValidatedState.Status == PrinterStatusType.Printing;

                if (_consecutiveErrorCount >= errorThreshold)
                {
                    return currentState;
                }
                else
                {
                    return _previousValidatedState;
                }
            }
            else
            {
                _consecutiveNormalCount++;
                _consecutiveErrorCount = 0;

                if (_previousValidatedState.Status != PrinterStatusType.Normal &&
                    _previousValidatedState.Status != PrinterStatusType.Printing)
                {
                    if (_wasPrintingBeforeError && currentState.Status == PrinterStatusType.Printing)
                    {
                        _lastPrintResumeTime = DateTime.Now;
                        _wasPrintingBeforeError = false;
                    }
                }

                if (_consecutiveNormalCount >= normalThreshold)
                {
                    return currentState;
                }
                else
                {
                    return _previousValidatedState;
                }
            }
        }

        private bool ShouldRaiseStateChangeEvent(PrinterState newState)
        {
            if (newState.Status == _previousValidatedState.Status)
            {
                return false;
            }

            if ((DateTime.Now - _lastPrintResumeTime).TotalMilliseconds < GRACE_PERIOD_AFTER_RESUME_MS)
            {
                if (newState.Status == PrinterStatusType.Normal || 
                    newState.Status == PrinterStatusType.Printing)
                {
                    return false;
                }
            }

            if (_previousValidatedState.Status == PrinterStatusType.Printing && 
                newState.Status == PrinterStatusType.Normal)
            {
                return false;
            }

            if (_previousValidatedState.Status == PrinterStatusType.Normal && 
                newState.Status == PrinterStatusType.Printing)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Event Handlers

        protected virtual void OnStateChanged(PrinterStateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(string errorMessage)
        {
            ErrorOccurred?.Invoke(this, errorMessage);
        }

        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        protected virtual void OnControlCommandDispatched(string commandDisplayName)
        {
            ControlCommandDispatched?.Invoke(this, commandDisplayName);
        }

        protected virtual void OnModeChanged(MonitoringMode newMode)
        {
            ModeChanged?.Invoke(this, new MonitoringModeChangedEventArgs 
            { 
                NewMode = newMode,
                Timestamp = DateTime.Now
            });
        }

        #endregion
    }

    #region Supporting Classes and Enums

    public enum MonitoringMode
    {
        Background,  
        Active       
    }

    public enum PrinterStatusType
    {
        Normal,
        Printing,
        HeadOpen,
        PaperJam,
        PaperOut,
        RibbonOut,
        Paused,
        Unknown
    }

    public class PrinterState
    {
        public PrinterStatusType Status { get; set; }
        public bool IsHeadOpen { get; set; }
        public bool IsPaused { get; set; }
        public bool IsPrinting { get; set; }
        public string ErrorMessage { get; set; }

        public bool IsError => Status != PrinterStatusType.Normal && 
                               Status != PrinterStatusType.Printing;
    }

    public class PrinterStatus
    {
        public PrinterState State { get; set; }
        public byte RawStatusByte { get; set; }
        public string Method { get; set; }

        public override string ToString()
        {
            return $"[{Method}] Status: {State.Status}, Error: {State.ErrorMessage ?? "None"}";
        }
    }

    public class PrinterStateChangedEventArgs : EventArgs
    {
        public PrinterState State { get; set; }
        public DateTime Timestamp { get; set; }
        public PrinterStatus FullStatus { get; set; }
    }

    public class MonitoringModeChangedEventArgs : EventArgs
    {
        public MonitoringMode NewMode { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // PATCH-BEGIN: UnifiedPrinterRecoveryWorkflow
    public class PrinterRecoveryResult
    {
        public string Caller { get; set; }
        public bool SpoolerCleared { get; set; }
        public bool RebootCommandSent { get; set; }
        public bool PrinterBackOnline { get; set; }
        public bool IsSuccess { get; set; }
        public string FailureStage { get; set; }
        public string ErrorMessage { get; set; }
    }
    // PATCH-END: UnifiedPrinterRecoveryWorkflow

    #endregion
}   
