using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIM.Application.Features.Reports
{
    public class PrintJobStatistics
    {
        public class DuplicateCodeDetail
        {
            public string Code { get; set; } = string.Empty;
            public List<int> LineNumbers { get; set; } = new List<int>();
        }

        public DateTime Timestamp { get; set; }
        public string UserName { get; set; }
        public string OriginalFileName { get; set; }
        public int GoodCodesCount { get; set; }
        public int BadCodesCount { get; set; }
        public int TotalCodesReceived { get; set; }
        public int HeadOpenCount { get; set; }
        public int DuplicateCount { get; set; } // Changed from bool to int to show actual count
        public int CameraDatabaseCodesCount { get; set; }
        public List<string> DuplicateCodes { get; set; } = new List<string>(); // Store actual duplicate codes if needed
        public List<DuplicateCodeDetail> DuplicateCodeDetails { get; set; } = new List<DuplicateCodeDetail>();
        public TimeSpan JobDuration { get; set; } // Optional: Duration of the print job
        public bool IsCameraMode { get; set; } // Flag indicating if camera was used

        // Constructor for easy initialization
        public PrintJobStatistics(
            DateTime timestamp,
            string userName,
            string originalFileName,
            int goodCodesCount,
            int badCodesCount,
            int totalCodesReceived,
            int headOpenCount,
            int duplicateCount, // Changed parameter from bool to int
            int cameraDatabaseCodesCount,
            bool isCameraMode,
            List<string> duplicateCodes = null,
            List<DuplicateCodeDetail> duplicateCodeDetails = null,
            TimeSpan? jobDuration = null)
        {
            Timestamp = timestamp;
            UserName = userName;
            OriginalFileName = originalFileName;
            GoodCodesCount = goodCodesCount;
            BadCodesCount = badCodesCount;
            TotalCodesReceived = totalCodesReceived;
            HeadOpenCount = headOpenCount;
            DuplicateCount = duplicateCount; // Changed assignment
            CameraDatabaseCodesCount = cameraDatabaseCodesCount;
            IsCameraMode = isCameraMode;
            DuplicateCodes = duplicateCodes ?? new List<string>();
            DuplicateCodeDetails = duplicateCodeDetails ?? new List<DuplicateCodeDetail>();
            JobDuration = jobDuration ?? TimeSpan.Zero;
        }

        private string BuildDuplicateDetailsSection()
        {
            var builder = new StringBuilder();
            builder.AppendLine("----------------------");
            builder.AppendLine("Повторяющиеся строки:");

            if (DuplicateCodeDetails == null || DuplicateCodeDetails.Count == 0)
            {
                builder.Append("Нет");
                return builder.ToString();
            }

            var orderedDetails = DuplicateCodeDetails
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Code))
                .OrderBy(d => d.LineNumbers != null && d.LineNumbers.Count > 0 ? d.LineNumbers.Min() : int.MaxValue)
                .ThenBy(d => d.Code, StringComparer.Ordinal)
                .ToList();

            if (orderedDetails.Count == 0)
            {
                builder.Append("Нет");
                return builder.ToString();
            }

            foreach (var detail in orderedDetails)
            {
                var lines = detail.LineNumbers == null
                    ? "не определены"
                    : string.Join(", ", detail.LineNumbers.OrderBy(n => n));

                builder.AppendLine($"Код: {detail.Code}");
                builder.AppendLine($"Строки: {lines}");
            }

            return builder.ToString().TrimEnd('\r', '\n');
        }

        public override string ToString()
        {
            if (IsCameraMode)
            {
                // Detailed statistics for camera mode
                return $"--- Статистика задания печати ({Timestamp}) ---\n" +
                       $"Пользователь: {UserName}\n" +
                       $"Исходный файл: {OriginalFileName}\n" +
                       $"Количество открытий головы принтера: {HeadOpenCount}\n" +
                       $"Получено кодов всего: {TotalCodesReceived}\n" +
                       $"Кодов в БД камеры: {CameraDatabaseCodesCount}\n" +
                       $"Хороших кодов: {GoodCodesCount}\n" +
                       $"Плохих кодов: {BadCodesCount}\n" +
                       $"Найдено повторяющихся кодов: {(DuplicateCount > 0 ? DuplicateCount.ToString() : "Нет")}\n" +
                       (DuplicateCount > 0 ? $"  Уникальных дублирующихся кодов: {DuplicateCodes.Count}, общее количество повторений: {DuplicateCount}\n" : "  Уникальных дублирующихся кодов: 0\n") +
                       $"Время работы с файлом: {JobDuration:hh\\:mm\\:ss}\n" +
                       "Режим: С использованием камеры\n" +
                       "------------------------------------------\n" +
                       BuildDuplicateDetailsSection();
            }
            else
            {
                // Simplified statistics for no-camera mode (without camera-specific metrics)
                return $"--- Статистика задания печати ({Timestamp}) ---\n" +
                       $"Пользователь: {UserName}\n" +
                       $"Исходный файл: {OriginalFileName}\n" +
                       $"Количество открытий головы принтера: {HeadOpenCount}\n" +
                       $"Время работы с файлом: {JobDuration:hh\\:mm\\:ss}\n" +
                       "Режим: Без использования камеры\n" +
                       "------------------------------------------\n" +
                       "----------------------\n" +
                       "Повторяющиеся строки:\n" +
                       "Недоступно без модуля камеры";
            }
        }
    }
}
