using BIM.Application.Features.Reports;
using BIM.Application.Common.Interfaces;
using BIM.Application.Common.Configs; // Added for FolderSettings
using Microsoft.Extensions.Configuration; // Still needed for fallback configuration access if any
using System;
using System.IO;
using System.Threading.Tasks;

namespace BIM_Control.Services
{
    public class StatisticsService
    {
        private readonly string _outputPath;
        private readonly ILoggerService _loggerService;

        // Modified constructor to accept FolderSettings
        public StatisticsService(FolderSettings folderSettings, ILoggerService loggerService)
        {
            _loggerService = loggerService;
            _loggerService.LogInformation("ℹ Инициализация сервиса статистики...");

            // Create the full path by combining the base path with the statistics subfolder
            if (string.IsNullOrWhiteSpace(folderSettings.StatisticsOutput))
            {
                _loggerService.LogWarning("⚠ FolderSettings.StatisticsOutput не настроен в appsettings.json. Используется путь по умолчанию.");
                _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Statistics");
            }
            else
            {
                // Combine the main path with the statistics subfolder
                _outputPath = Path.Combine(folderSettings.Path, folderSettings.StatisticsOutput);
            }

            // Ensure the directory exists
            try
            {
                if (!Directory.Exists(_outputPath))
                {
                    _loggerService.LogInformation($"ℹ Директория {_outputPath} не существует, создаю...");
                    Directory.CreateDirectory(_outputPath);
                    _loggerService.LogInformation($"✓ Директория для файлов статистики успешно создана: {_outputPath}");
                }
                else
                {
                    _loggerService.LogInformation($"ℹ Директория статистики уже существует: {_outputPath}");
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Не удалось создать директорию для файлов статистики '{_outputPath}': {ex.Message}");
                // Fallback to temp directory if creation fails
                _outputPath = Path.GetTempPath();
                _loggerService.LogInformation($"ℹ Используется временная директория для статистики: {_outputPath}");
            }
        }

        public async Task SaveStatisticsAsync(PrintJobStatistics stats)
        {
            try
            {
                _loggerService.LogInformation($"ℹ Начало сохранения статистики для файла: {stats.OriginalFileName}");

                // Create filename with proper sanitization
                string sanitizedFileName = SanitizeFileName(stats.OriginalFileName);
                _loggerService.LogInformation($"ℹ Санитизированное имя файла: {sanitizedFileName}");

                string fileName = $"Статистика_{sanitizedFileName}_{stats.Timestamp:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(_outputPath, fileName);

                _loggerService.LogInformation($"ℹ Путь сохранения: {filePath}");

                await File.WriteAllTextAsync(filePath, stats.ToString());
                _loggerService.LogInformation($"✓ Статистика успешно сохранена: {filePath}");
                
                // Also save to AppData/Local/Roaming/BIM/Statistics
                await SaveToAppDataAsync(stats, fileName);
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Не удалось сохранить статистику для '{stats.OriginalFileName}': {ex.Message}");
                _loggerService.LogError($"ℹ Деталь ошибки: {ex.StackTrace}");
            }
        }
        
        private async Task SaveToAppDataAsync(PrintJobStatistics stats, string fileName)
        {
            try
            {
                // Define the AppData path for BIM application
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIM", "Statistics");
                
                // Ensure the directory exists
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                    _loggerService.LogInformation($"ℹ Директория AppData создана: {appDataPath}");
                }
                
                string appDataFilePath = Path.Combine(appDataPath, fileName);
                await File.WriteAllTextAsync(appDataFilePath, stats.ToString());
                _loggerService.LogInformation($"✓ Статистика также сохранена в AppData: {appDataFilePath}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Не удалось сохранить статистику в AppData: {ex.Message}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Replace invalid characters in filename
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            // Also replace dots in the middle of filename to avoid extension confusion
            sanitized = sanitized.Replace(".", "_");
            return sanitized;
        }
    }
}
