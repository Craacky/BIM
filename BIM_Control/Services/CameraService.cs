
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using BIM.Application.Common.Constants;

namespace BIM_Control.Services
{
    public class CameraService
    
    {
        private readonly string _ip;
        private readonly int _port;
        private readonly bool _moduleAvailableDefault; // Default from config
        private bool _moduleAvailableRuntime; // Runtime state that can be changed
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isConnected;
        private readonly StringBuilder _buffer = new StringBuilder();
        private int _sequenceNumber = 0;
        private volatile bool _isProcessingData = false; // New flag to control data processing independently of connection - now starts as false
        private DateTime _lastActivityTime = DateTime.UtcNow; // Track last successful communication

        // Fields for special processing mode
        private bool _specialProcessingMode = false;
        private const int SpecialIgnoreCount = 8;
        private int _specialIgnoreRemaining = 0;
        private List<string> _firstSequence = new List<string>();
        private List<string> _secondSequence = new List<string>();
        private int _sequenceCounter = 0;
        private bool _firstFailIgnored = false;
        private int _firstSequenceFailCount = 0; // Count of fails received instead of first sequence
        private System.Timers.Timer _specialModeTimeoutTimer; // Timer to auto-exit special mode
        private bool _specialModeHasActivity = false;

        public event EventHandler<string> LogMessage;
        public event EventHandler<string> GoodCodeReceived;
        public event EventHandler<string> BadCodeReceived;
        public event EventHandler<bool> ConnectionStatusChanged;
        public event EventHandler<bool> ModuleAvailabilityChanged; // New event

        public bool IsConnected => _isConnected;
        public bool ModuleAvailable => _moduleAvailableRuntime;

        public CameraService(IConfiguration config)
        {
            _ip = config["CameraSettings:IP"];
            if (!int.TryParse(config["CameraSettings:Port"], out _port))
            {
                _port = AppConstants.Camera.DefaultPort;
            }
            if (!bool.TryParse(config["CameraSettings:ModuleAvailable"], out _moduleAvailableDefault))
            {
                _moduleAvailableDefault = false;
            }
            // Initialize runtime state from config
            _moduleAvailableRuntime = _moduleAvailableDefault;
        }

        /// <summary>
        /// Динамически включает или отключает модуль камеры во время выполнения
        /// </summary>
        public void SetModuleAvailability(bool isAvailable)
        {
            if (_moduleAvailableRuntime != isAvailable)
            {
                _moduleAvailableRuntime = isAvailable;

                // Вызвать событие для уведомления подписчиков
                ModuleAvailabilityChanged?.Invoke(this, isAvailable);
            }
        }

        private CameraCodesDatabase _database;

        public void InitializeDatabase(string dbPath)
        {
            _database = new CameraCodesDatabase(dbPath);
        }

        public void Start()
        {
            // Проверить что модуль включен перед запуском
            if (!_moduleAvailableRuntime)
            {
                return;
            }

            if (_cts != null) return;

            if (_database == null)
            {
                // Store DB in a writable location for non-admin users
                string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dbDir = System.IO.Path.Combine(commonAppData, "BIMv2");
                string dbPath = System.IO.Path.Combine(dbDir, "camera_codes.db");
                InitializeDatabase(dbPath);
            }

            _cts = new CancellationTokenSource();
            _lastActivityTime = DateTime.UtcNow; // Initialize activity time
            Task.Run(() => ConnectionLoop(_cts.Token));
            
            // Start health check task
            Task.Run(() => HealthCheckLoop(_cts.Token));

            LogMessage?.Invoke(this, $"ℹ Попытка подключения к камере {_ip}:{_port}...");
        }

        /// <summary>
        /// Controls whether data processing is active, independent of connection status
        /// </summary>
        public void SetDataProcessing(bool processing)
        {
            if (!processing && _isProcessingData)
            {
                // Stop processing and clear any pending special-mode sequences
                ExitSpecialProcessingMode();
            }
            _isProcessingData = processing;
        }

        
        private async Task HealthCheckLoop(CancellationToken token)
        {
            const int healthCheckInterval = 30000; // 30 seconds
            const int maxInactivityMinutes = 2; // Max inactivity before considering connection stale
            
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(healthCheckInterval, token);
                    
                    // Check if we've had activity recently
                    var timeSinceLastActivity = DateTime.UtcNow - _lastActivityTime;
                    
                    if (timeSinceLastActivity.TotalMinutes > maxInactivityMinutes && _isConnected)
                    {
                        // Connection might be stale, try a quick check
                        LogMessage?.Invoke(this, $"Проверка активности соединения с камерой...");
                        
                        // Check if the socket is still connected by checking socket properties
                        if (_client?.Client != null)
                        {
                            var socket = _client.Client;
                            
                            // Check if the socket is still connected using Poll
                            // Check Connected property first, then use Poll as additional check
                            bool isConnected = socket.Connected;
                            
                            if (!isConnected)
                            {
                                LogMessage?.Invoke(this, "Обнаружено неактивное соединение с камерой, инициируется переподключение...");
                                // Force disconnection to trigger reconnection
                                Disconnect();
                                break; // Exit to allow reconnection in main loop
                            }
                            else
                            {
                                // Connection is still good, update the activity time
                                _lastActivityTime = DateTime.UtcNow;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, exit the loop
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Ошибка проверки состояния соединения: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
            
            // Stop and dispose the timeout timer
            if (_specialModeTimeoutTimer != null)
            {
                _specialModeTimeoutTimer.Stop();
                _specialModeTimeoutTimer.Dispose();
                _specialModeTimeoutTimer = null;
            }
            
            Disconnect();
        }

        private void Disconnect()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
            finally
            {
                _stream = null;
                _client = null;
                if (_isConnected)
                {
                    _isConnected = false;
                    ConnectionStatusChanged?.Invoke(this, false);
                    if (_moduleAvailableRuntime) // Only log if module is available
                    {
                        LogMessage?.Invoke(this, "Камера отключена.");
                    }
                }
            }
        }

        private async Task ConnectionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_client == null || !_client.Connected)
                    {
                        if (_moduleAvailableRuntime)
                        {
                            LogMessage?.Invoke(this, $"ℹ Попытка подключения к камере {_ip}:{_port}...");
                        }

                        _client = new TcpClient();
                        _client.ReceiveTimeout = 5000; // 5 seconds receive timeout
                        _client.SendTimeout = 5000;     // 5 seconds send timeout
                        
                        // Use a timeout for connection attempt
                        var connectTask = _client.ConnectAsync(_ip, _port);
                        var timeoutTask = Task.Delay(10000, token); // 10 second timeout for connection
                        
                        var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                        
                        if (completedTask == timeoutTask)
                        {
                            _client?.Close();
                            throw new TimeoutException($"Timeout connecting to camera at {_ip}:{_port}");
                        }
                        
                        // Wait for the connection task to complete (or throw)
                        await connectTask;

                        _stream = _client.GetStream();
                        _isConnected = true;
                        ConnectionStatusChanged?.Invoke(this, true);

                        if (_moduleAvailableRuntime)
                        {
                            LogMessage?.Invoke(this, $"✓ Камера успешно подключена к {_ip}:{_port}");
                        }

                        await ReadLoop(token);

                        // Read loop exited: treat this as connection loss and force a clean reconnect cycle.
                        Disconnect();
                        if (!token.IsCancellationRequested)
                        {
                            await Task.Delay(AppConstants.Camera.ReconnectDelayMs, token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_moduleAvailableRuntime)
                    {
                        if (_isConnected)
                        {
                            LogMessage?.Invoke(this, $"✗ Подключение потеряно: {ex.Message}");
                        }
                        else
                        {
                            LogMessage?.Invoke(this, $"✗ Ошибка подключения: {ex.Message}");
                        }
                    }

                    Disconnect();
                    await Task.Delay(AppConstants.Camera.ReconnectDelayMs, token);
                }
            }
        }

        private async Task ReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[1024];
            while (!token.IsCancellationRequested && _client != null && _client.Connected)
            {
                try
                {
                    // Read with cancellation token only - no artificial timeout
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) 
                    {
                        // Connection closed gracefully
                        LogMessage?.Invoke(this, "Camera connection closed gracefully, triggering reconnect...");
                        break;
                    }

                    string chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    ProcessData(chunk);
                }
                catch (IOException ioEx)
                {
                    LogMessage?.Invoke(this, $"IO Exception reading from camera: {ioEx.Message}");
                    break; // Exit read loop to allow reconnection
                }
                catch (ObjectDisposedException)
                {
                    // Stream was closed, exit read loop
                    break;
                }
                catch (TaskCanceledException)
                {
                    // Operation was cancelled, exit normally
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Unexpected error reading from camera: {ex.Message}");
                    break; // Exit read loop to allow reconnection
                }   
            }
        }

        private void ProcessData(string chunk)
        {
            _buffer.Append(chunk);
            int startIndex = IndexOf(_buffer, AppConstants.Camera.TagStart, 0);
            int stopIndex = IndexOf(_buffer, AppConstants.Camera.TagStop, 0);

            while (startIndex != -1 && stopIndex != -1 && stopIndex > startIndex)
            {
                int innerStart = startIndex + AppConstants.Camera.TagStart.Length;
                int innerLength = stopIndex - innerStart;

                if (innerLength > 0)
                {
                    string innerData = _buffer.ToString(innerStart, innerLength);
                    ParseInnerData(innerData);
                    // Update last activity time when we successfully parse data
                    _lastActivityTime = DateTime.UtcNow;
                }

                _buffer.Remove(0, stopIndex + AppConstants.Camera.TagStop.Length);
                startIndex = IndexOf(_buffer, AppConstants.Camera.TagStart, 0);
                stopIndex = IndexOf(_buffer, AppConstants.Camera.TagStop, 0);
            }

            if (_buffer.Length > AppConstants.Camera.MaxBufferLength && startIndex == -1)
            {
                _buffer.Clear();
            }
        }

        private static int IndexOf(StringBuilder source, string value, int startIndex)
        {
            if (source == null || value == null) return -1;
            if (value.Length == 0) return startIndex < source.Length ? startIndex : -1;
            if (startIndex < 0) startIndex = 0;
            if (source.Length - startIndex < value.Length) return -1;

            for (int i = startIndex; i <= source.Length - value.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < value.Length; j++)
                {
                    if (source[i + j] != value[j])
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched) return i;
            }
            return -1;
        }

        private void ParseInnerData(string data)
        {
            if (!_isProcessingData)
            {
                return;
            }
            // Treat as fail only when the value is exactly "fail" in the same case.
            if (IsFailCode(data))
            {
                // In special processing mode, each incoming value (including fail) counts as one ignored item.
                if (_specialProcessingMode)
                {
                    ProcessInSpecialMode(AppConstants.Camera.FailMarker);
                }
                // In normal processing mode, treat as single bad code
                else if (!_specialProcessingMode)
                {
                    BadCodeReceived?.Invoke(this, data);
                    if (_moduleAvailableRuntime)
                    {
                        LogMessage?.Invoke(this, $"✗ ОШИБКА КОД: {data}");
                    }
                }
                return; // Exit early since we've processed the entire data
            }

            string[] parts = data.Split(new[] { AppConstants.Camera.TagNext }, StringSplitOptions.None);

            foreach (var part in parts)
            {
                if (!_isProcessingData)
                {
                    break;
                }

                string cleanPart = part.Trim();
                if (string.IsNullOrEmpty(cleanPart)) continue;

                if (_specialProcessingMode)
                {
                    ProcessInSpecialMode(cleanPart);
                }
                else
                {
                    if (IsFailCode(cleanPart))
                    {
                        // Only raise the event if we're processing data (normal case)
                        BadCodeReceived?.Invoke(this, cleanPart);
                        if (_moduleAvailableRuntime)
                        {
                            LogMessage?.Invoke(this, $"✗ ОШИБКА КОД: {cleanPart}");
                        }
                    }
                    else
                    {
                        int sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
                        _database?.AddCode(cleanPart, sequenceNumber);
                        GoodCodeReceived?.Invoke(this, cleanPart);
                    }
                }
            }

        }
        
        private void ProcessInSpecialMode(string cleanPart)
        {
            // Special mode simplified:
            // ignore first 8 elements (2x4), then immediately switch back to normal mode.
            if (!_specialProcessingMode)
            {
                return;
            }

            if (_specialIgnoreRemaining > 0)
            {
                _specialIgnoreRemaining--;
            }

            if (_specialIgnoreRemaining <= 0)
            {
                ExitSpecialProcessingMode();
            }
        }
        
        private bool AreSequencesIdentical(List<string> seq1, List<string> seq2)
        {
            if (seq1.Count != seq2.Count) return false;
            
            for (int i = 0; i < seq1.Count; i++)
            {
                if (!string.Equals(seq1[i], seq2[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private void ProcessSequence(List<string> sequence)
        {
            foreach (string code in sequence)
            {
                if (_isProcessingData)
                {
                    int sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
                    _database?.AddCode(code, sequenceNumber);
                    // Don't invoke event to prevent updating stats during special processing
                    // GoodCodeReceived?.Invoke(this, code);
                }
            }
        }
        
        private void ProcessSequenceWithEvents(List<string> sequence)
        {
            foreach (string code in sequence)
            {
                if (_isProcessingData)
                {
                    int sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
                    _database?.AddCode(code, sequenceNumber);
                    GoodCodeReceived?.Invoke(this, code);
                }
            }
        }
        
        public void EnterSpecialProcessingMode()
        {
            // Do not re-arm special ignore window if already active.
            // Repeated calls can happen around pause/resume/head transitions.
            if (_specialProcessingMode)
            {
                return;
            }

            _specialProcessingMode = true;
            _specialIgnoreRemaining = SpecialIgnoreCount;
            _firstSequence.Clear();
            _secondSequence.Clear();
            _sequenceCounter = 0;
            _firstFailIgnored = false;
            _firstSequenceFailCount = 0;
            _specialModeHasActivity = false;
            
            if (_moduleAvailableRuntime)
            {
                LogMessage?.Invoke(this, "Специальный режим камеры активирован");
            }
        }

        private void SetupSpecialModeTimeout()
        {
            // Stop any existing timer
            if (_specialModeTimeoutTimer != null)
            {
                _specialModeTimeoutTimer.Stop();
                _specialModeTimeoutTimer.Dispose();
            }
            
            // Create new timer with 10 second timeout
            _specialModeTimeoutTimer = new System.Timers.Timer(10000); // 10 seconds
            _specialModeTimeoutTimer.AutoReset = false; // Run only once
            _specialModeTimeoutTimer.Elapsed += (sender, e) =>
            {
                // Switch to normal processing mode if timeout occurs
                HandleSpecialModeTimeout();
            };
            _specialModeTimeoutTimer.Start();
        }

        private void HandleSpecialModeTimeout()
        {
            if (!_specialProcessingMode)
            {
                return;
            }

            // On timeout, if only the first sequence is complete, process its codes (if any).
            if (_firstSequence.Count == 4 && _secondSequence.Count == 0)
            {
                if (SequenceHasCodes(_firstSequence))
                {
                    ProcessSequenceCodesOnly(_firstSequence);
                }
            }
            ExitSpecialProcessingMode();
        }

        private void ExitSpecialProcessingMode()
        {
            _specialProcessingMode = false;
            _specialIgnoreRemaining = 0;
            _firstSequence.Clear();
            _secondSequence.Clear();
            _sequenceCounter = 0;
            _firstFailIgnored = false;
            _firstSequenceFailCount = 0;
            _specialModeHasActivity = false;
            
            // Stop and dispose the timeout timer
            if (_specialModeTimeoutTimer != null)
            {
                _specialModeTimeoutTimer.Stop();
                _specialModeTimeoutTimer.Dispose();
                _specialModeTimeoutTimer = null;
            }
            
            if (_moduleAvailableRuntime)
            {
                LogMessage?.Invoke(this, "Специальный режим камеры завершен");
            }
        }

        private void TouchSpecialModeTimeout()
        {
            _specialModeHasActivity = true;
            SetupSpecialModeTimeout();
        }

        public List<string> GetDuplicateCodes()
        {
            return _database?.GetDuplicateCodes() ?? new List<string>();
        }

        public Dictionary<string, List<int>> GetDuplicateCodesWithSequenceNumbers()
        {
            return _database?.GetDuplicateCodesWithSequenceNumbers()
                ?? new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Возвращает доступ к БД камеры для специальных операций
        /// Используется PrinterStatusForm для проверки дубликатов в 4 кодах
        /// </summary>
        public CameraCodesDatabase GetDatabase()
        {
            return _database;
        }

        public void FlushDatabaseWrites()
        {
            _database?.FlushPendingWritesPublic();
        }

        public void WaitForDatabaseDrain(TimeSpan timeout)
        {
            _database?.WaitForWritesToDrain(timeout);
        }

        public int GetPendingDatabaseWritesCount()
        {
            return _database?.GetPendingWriteCount() ?? 0;
        }

        public int ExportAllCodesToFile(string filePath)
        {
            if (_database == null)
            {
                return 0;
            }

            return _database.ExportAllCodesToFile(filePath);
        }

        public void ClearCodes()
        {
            _database?.ClearAllCodes();
            Interlocked.Exchange(ref _sequenceNumber, 0);
        }
        
        public int GetTotalDuplicateCount()
        {
            return _database?.GetTotalDuplicateCount() ?? 0;
        }

        public int GetCodesCount()
        {
            return _database?.GetCodesCount() ?? 0;
        }
        
        public bool IsInSpecialProcessingMode()
        {
            return _specialProcessingMode;
        }
        
        public bool IsDataProcessingEnabled()
        {
            return _isProcessingData;
        }

        public void FlushPendingSpecialSequence()
        {
            if (!_specialProcessingMode)
            {
                return;
            }
            // In simplified special mode there is no pending sequence to flush.
            ExitSpecialProcessingMode();
        }

        private static bool IsFailCode(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return string.Equals(
                value.Trim(),
                AppConstants.Camera.FailMarker,
                StringComparison.Ordinal);
        }

        private static bool SequenceHasFail(List<string> sequence)
        {
            foreach (var value in sequence)
            {
                if (IsFailCode(value))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool SequenceHasCodes(List<string> sequence)
        {
            foreach (var value in sequence)
            {
                if (!IsFailCode(value))
                {
                    return true;
                }
            }
            return false;
        }

        private void ProcessSequenceCodesOnly(List<string> sequence)
        {
            foreach (string code in sequence)
            {
                if (!_isProcessingData) continue;
                if (IsFailCode(code)) continue;
                int sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
                _database?.AddCode(code, sequenceNumber);
                GoodCodeReceived?.Invoke(this, code);
            }
        }
    }
}
