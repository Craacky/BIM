using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIM_Control.Services
{
    public class PrintJobService
    {
        private readonly CameraCodesDatabase _database;

        public PrintJobService(CameraCodesDatabase database)
        {
            _database = database;
        }

        public void ProcessNewJob(List<string> scannedCodes, string sourceFilePath, string newFilePath)
        {
            if (scannedCodes == null || scannedCodes.Count != 4)
            {
                throw new ArgumentException("Требуется ровно 4 отсканированных кода.");
            }

            try
            {
                // --- ШАГ 1: РАБОТА С ФАЙЛОМ ---
                var allCodes = File.ReadAllLines(sourceFilePath).ToList();

                // Находим индекс последнего из 4-х кодов
                int lastScannedIndex = -1;
                foreach (var code in scannedCodes)
                {
                    int currentIndex = allCodes.IndexOf(code);
                    if (currentIndex == -1)
                    {
                        throw new InvalidOperationException($"Отсканированный код '{code}' не найден в исходном файле.");
                    }
                    if (currentIndex > lastScannedIndex)
                    {
                        lastScannedIndex = currentIndex;
                    }
                }
                
                // Формируем список оставшихся кодов
                var remainingCodes = allCodes.Skip(lastScannedIndex + 1).ToList();

                // Сохраняем новый файл
                File.WriteAllLines(newFilePath, remainingCodes);
                
                // --- ШАГ 2: РАБОТА С БАЗОЙ ДАННЫХ (только после успеха с файлом) ---
                _database.DeleteCodesAfter(scannedCodes);

            }
            catch (Exception ex)
            {
                // Логируем ошибку и пробрасываем ее выше для UI
                throw new InvalidOperationException($"Не удалось обработать задание: {ex.Message}", ex);
            }
        }
    }
}