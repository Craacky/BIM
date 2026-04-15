using BIM.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace BIM.Infrastructure.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly ILogger<LoggerService> _logger;
        private readonly string _appDataLogPath;

        public LoggerService(ILogger<LoggerService> logger)
        {
            _logger = logger;
            
            // Initialize AppData log path
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIM", "Logs");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            // Create log file with today's date
            string logFileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
            _appDataLogPath = Path.Combine(appDataPath, logFileName);
        }

        public void LogDebug(string message)
        {
            _logger.LogDebug(message);
            WriteToAppDataLog($"[DEBUG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public void LogError(string message)
        {
            _logger.LogError(message);
            WriteToAppDataLog($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public void LogWarning(string message)
        {
            _logger.LogWarning(message);
            WriteToAppDataLog($"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public void LogCritical(string message)
        {
            _logger.LogCritical(message);
            WriteToAppDataLog($"[CRITICAL] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public void LogInformation(string message)
        {
            _logger.LogInformation(message);
            WriteToAppDataLog($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }
        
        private void WriteToAppDataLog(string logEntry)
        {
            try
            {
                File.AppendAllText(_appDataLogPath, logEntry + Environment.NewLine);
            }
            catch
            {
                // If writing to AppData fails, we silently ignore to avoid recursive errors
                // The main logging system will still work
            }
        }
    }
}
