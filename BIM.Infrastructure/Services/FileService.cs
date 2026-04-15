using BIM.Application.Common.Configs;
using BIM.Application.Common.Interfaces;
using BIM.Infrastructure.Extensions;

namespace BIM.Infrastructure.Services
{
    public class FileService : IFileService
    {
        private readonly LabelStarSettings _labelStarSettings;
        private readonly FolderSettings _folderSettings;
        private readonly ICodeService _codeService;
        private readonly ILoggerService _loggerService;

        private HashSet<string> _printedCodes;

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public IEnumerable<string> FileText { get; set; } = Enumerable.Empty<string>();
        public string LastPrintedCode { get; set; } = string.Empty;

        public FileService(LabelStarSettings labelStar, FolderSettings folder,
            ILoggerService loggerService, ICodeService codeService)
        {
            _labelStarSettings = labelStar;
            _folderSettings = folder;
            _codeService = codeService;
            _loggerService = loggerService;

            _printedCodes = new();
        }

        public void CopyFileToLabelStarFolder()
        {
            if (string.IsNullOrWhiteSpace(FilePath) || string.IsNullOrWhiteSpace(_labelStarSettings.FileName))
            {
                _loggerService.LogError("CopyFileToLabelStarFolder: путь источника или имя файла назначения не задано.");
                return;
            }

            // Ensure destination directory exists
            if (!Directory.Exists(_folderSettings.Path))
            {
                Directory.CreateDirectory(_folderSettings.Path);
                _loggerService.LogInformation($"ℹ Создана папка 'Работа с кодами': {_folderSettings.Path}");
            }

            string labelStarFile = Path.Combine(_folderSettings.Path, _labelStarSettings.FileName);

            // If source and destination are the same file, do nothing to avoid deleting the source.
            if (string.Equals(FilePath, labelStarFile, StringComparison.OrdinalIgnoreCase))
            {
                _loggerService.LogInformation("CopyFileToLabelStarFolder: источник и назначение совпадают, копирование не требуется.");
                return;
            }

            if (!File.Exists(FilePath))
            {
                _loggerService.LogError($"CopyFileToLabelStarFolder: исходный файл не найден: {FilePath}");
                return;
            }

            File.Copy(FilePath, labelStarFile, true);
            _loggerService.LogInformation($"✓ Файл скопирован в LabelStar: {labelStarFile}");
        }

        /// <summary>
        /// Перемещает выбранный файл в папку "Работа с кодами" (первый этап)
        /// </summary>
        public void MoveFileToWorkFolder(string selectedFilePath)
        {
            try
            {
                // Убедиться, что папка "Работа с кодами" существует
                if (!Directory.Exists(_folderSettings.Path))
                {
                    Directory.CreateDirectory(_folderSettings.Path);
                    _loggerService.LogInformation($"ℹ Создана папка 'Работа с кодами': {_folderSettings.Path}");
                }

                // Убедиться, что папка временного хранения существует
                string temporaryStoragePath = Path.Combine(_folderSettings.Path, _folderSettings.TemporaryStorage);
                if (!Directory.Exists(temporaryStoragePath))
                {
                    Directory.CreateDirectory(temporaryStoragePath);
                    _loggerService.LogInformation($"ℹ Создана папка временного хранения: {temporaryStoragePath}");
                }

                string fileName = Path.GetFileName(selectedFilePath);
                string destinationPath = Path.Combine(temporaryStoragePath, fileName);

                // ПРОВЕРКА: Если файл уже находится в папке временного хранения
                if (Path.GetDirectoryName(selectedFilePath).Equals(temporaryStoragePath, StringComparison.OrdinalIgnoreCase))
                {
                    _loggerService.LogInformation($"ℹ Файл '{fileName}' уже в папке временного хранения, используется как есть");
                    
                    // Просто обновить пути в FileService
                    FilePath = destinationPath;
                    FileName = fileName;
                    return;
                }


                // Если файл с таким именем уже существует в папке работы, удалить его
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                // Перемещить файл
                File.Move(selectedFilePath, destinationPath, true);

                _loggerService.LogInformation($"✓ Файл '{fileName}' перемещен во временное хранение: {destinationPath}");

                // Обновить пути в FileService
                FilePath = destinationPath;
                FileName = fileName;
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Ошибка при перемещении файла в папку 'Работа с кодами': {ex.Message}");
                throw;
            }
        }

        //public void CopyLogsFromLabelStar()
        //{
        //    var directory = new DirectoryInfo(labelStarSettings.LogPath);
        //    var file = directory.GetFiles()
        //                .OrderByDescending(q => q.LastWriteTime)
        //                .FirstOrDefault();
        //    if (file is not null)
        //        File.Copy(file.FullName,
        //            Path.Combine(folderSettings.Path, folderSettings.Logs, file.Name));
        //    else logger.LogError("Нет файла с логами принтера!");
        //}

        /// <summary>
        /// Удаляет все бэкап файлы (NewTest_backup_*.txt) из папки "Работа с кодами"
        /// Вызывается при завершении печати для очистки временных файлов
        /// </summary>
        public void DeleteBackupFiles()
        {
            try
            {

                // Получить все файлы в папке "Работа с кодами"
                if (!Directory.Exists(_folderSettings.Path))
                {
                    return;
                }

                // Найти все файлы с паттерном *_backup_*.txt
                var backupFiles = Directory.GetFiles(_folderSettings.Path, "*_backup_*.txt");

                if (backupFiles.Length == 0)
                {
                    _loggerService.LogInformation("ℹ Бэкап файлов для удаления не найдено");
                    return;
                }


                int deletedCount = 0;
                foreach (var backupFile in backupFiles)
                {
                    try
                    {
                        string fileName = System.IO.Path.GetFileName(backupFile);
                        File.Delete(backupFile);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _loggerService.LogWarning($"⚠ Не удалось удалить бэкап файл {System.IO.Path.GetFileName(backupFile)}: {ex.Message}");
                    }
                }

                _loggerService.LogInformation($"✓ Удалено {deletedCount} бэкап файлов из папки 'Работа с кодами'");
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Ошибка при удалении бэкап файлов: {ex.Message}");
            }
        }

        public void MoveFileToArchive()
        {
            try
            {
                // Если файл уже был перемещен, просто выйти
                if (!File.Exists(FilePath))
                {
                    _loggerService.LogInformation($"MoveFileToArchive: Файл по пути '{FilePath}' не существует. Предполагается, что он уже был перемещен или обработан.");
                    return;
                }

                // Убедиться, что папка архива существует (поддержка относительного, локального абсолютного и UNC пути)
                string archivePath = ResolveTargetFolderPath(_folderSettings.Archive);
                if (!Directory.Exists(archivePath))
                {
                    Directory.CreateDirectory(archivePath);
                    _loggerService.LogInformation($"ℹ Создана папка архива: {archivePath}");
                }


                // Удалить файл LabelStar из папки работы, если это не тот же файл, что и архивируемый
                string labelStarFile = Path.Combine(_folderSettings.Path, _labelStarSettings.FileName);
                if (File.Exists(labelStarFile) && !string.Equals(FilePath, labelStarFile, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(labelStarFile);
                }

                // Перемещить исходный файл в архив
                string archiveFilePath = Path.Combine(archivePath, FileName);

                _loggerService.LogInformation($"MoveFileToArchive: Preparing to move file. " +
                                              $"File.Exists(FilePath) = {File.Exists(FilePath)}. " +
                                              $"FilePath = '{FilePath}'. " +
                                              $"FileName = '{FileName}'. " +
                                              $"archiveFilePath = '{archiveFilePath}'.");

                File.Move(FilePath, archiveFilePath, true);

                _loggerService.LogInformation($"✓ Файл '{FileName}' успешно перемещен в архив: {archiveFilePath}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Ошибка при перемещении файла в архив: {ex.Message}");

                string archivePath = ResolveTargetFolderPath(_folderSettings.Archive);
                if (IsUncPath(archivePath) && TryMoveToReserveFolder("Архив резервная"))
                {
                    return;
                }

                throw;
            }
        }

        public void MoveFileToDuplicates()
        {
            try
            {
                // Если файл уже был перемещен, просто выйти
                if (!File.Exists(FilePath))
                {
                    _loggerService.LogInformation($"MoveFileToDuplicates: Файл по пути '{FilePath}' не существует. Предполагается, что он уже был перемещен или обработан.");
                    return;
                }

                // Убедиться, что папка дубликатов существует (поддержка относительного, локального абсолютного и UNC пути)
                string duplicatesPath = ResolveTargetFolderPath(_folderSettings.Duplicates);
                if (!Directory.Exists(duplicatesPath))
                {
                    Directory.CreateDirectory(duplicatesPath);
                    _loggerService.LogInformation($"ℹ Создана папка дубликатов: {duplicatesPath}");
                }


                // Удалить файл LabelStar из папки работы, если это не тот же файл, что и перемещаемый
                string labelStarFile = Path.Combine(_folderSettings.Path, _labelStarSettings.FileName);
                if (File.Exists(labelStarFile) && !string.Equals(FilePath, labelStarFile, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(labelStarFile);
                }

                // Переместить исходный файл в папку дубликатов
                string duplicatesFilePath = Path.Combine(duplicatesPath, FileName);

                _loggerService.LogInformation($"MoveFileToDuplicates: Preparing to move file. " +
                                              $"File.Exists(FilePath) = {File.Exists(FilePath)}. " +
                                              $"FilePath = '{FilePath}'. " +
                                              $"FileName = '{FileName}'. " +
                                              $"duplicatesFilePath = '{duplicatesFilePath}'.");

                File.Move(FilePath, duplicatesFilePath, true);

                _loggerService.LogInformation($"✓ Файл '{FileName}' успешно перемещен в папку дубликатов: {duplicatesFilePath}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"✗ Ошибка при перемещении файла в папку дубликатов: {ex.Message}");

                string duplicatesPath = ResolveTargetFolderPath(_folderSettings.Duplicates);
                if (IsUncPath(duplicatesPath) && TryMoveToReserveFolder("Дубликаты резервная"))
                {
                    return;
                }

                throw;
            }
        }

        private string ResolveTargetFolderPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return _folderSettings.Path;
            }

            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(_folderSettings.Path, configuredPath);
        }

        private bool IsUncPath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal);

        private bool TryMoveToReserveFolder(string reserveFolderName)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    _loggerService.LogWarning($"Резервное перемещение пропущено: файл '{FilePath}' не найден.");
                    return false;
                }

                string workParentPath = Directory.GetParent(_folderSettings.Path)?.FullName ?? _folderSettings.Path;
                string reservePath = Path.Combine(workParentPath, reserveFolderName);
                if (!Directory.Exists(reservePath))
                {
                    Directory.CreateDirectory(reservePath);
                    _loggerService.LogInformation($"ℹ Создана резервная папка: {reservePath}");
                }

                string reserveFilePath = Path.Combine(reservePath, FileName);
                File.Move(FilePath, reserveFilePath, true);
                _loggerService.LogWarning($"Файл '{FileName}' перемещен в резервную папку: {reserveFilePath}");
                return true;
            }
            catch (Exception reserveEx)
            {
                _loggerService.LogError($"✗ Ошибка резервного перемещения файла: {reserveEx.Message}");
                return false;
            }
        }

        public bool IsFileContainsDupes(IEnumerable<string> lines)
        {
            var dupes = lines
                .GroupBy(l => l)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .Where(g => g.Count > 1);
            return dupes.Any();
        }

        public void SaveValidatedFileToGoodCodesFolder()
        {
            if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(FileName))
            {
                _loggerService.LogError("Не удается сохранить файл в папку 'Хорошие коды': путь или имя файла отсутствуют.");
                return;
            }

            try
            {
                // Ensure the "Good codes" directory exists
                string goodCodesPath = Path.Combine(_folderSettings.Path, _folderSettings.Good);
                if (!Directory.Exists(goodCodesPath))
                {
                    Directory.CreateDirectory(goodCodesPath);
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Ошибка при сохранении файла в папку 'Хорошие коды': {ex.Message}");
            }
        }

        private int FindLastPrintedCodePosition(string code) => FileText.IndexOf(code);
        public bool IsLastCodeFounded(string code) => FindLastPrintedCodePosition(code) != -1;
        //private IEnumerable<string> GetCodesFromFile(string path) => File.ReadLines(path);

        //public void TakeCodesForReprint(string code)
        //{
        //    var lastCodePos = FindLastPrintedCodePosition(code);
        //    if(lastCodePos != -1)
        //    {
        //        FileText = FileText.Skip(lastCodePos);
        //        string filePath = Path.Combine(folderSettings.Path, folderSettings.LabelStar, labelStarSettings.FileName);
        //        File.Delete(filePath);
        //        File.WriteAllLines(filePath, FileText);
        //        logger.LogInformation("Файл с новыми кодами для LabelStar успешно пересоздан!");
        //    }
        //}

        //проверка после печати
        //public IEnumerable<string> GetInfoFromLastLogFile()
        //{
        //    var directory = new DirectoryInfo(Path.Combine(folderSettings.Path, folderSettings.Logs));
        //    var file = directory.GetFiles()
        //                .OrderByDescending(q => q.LastWriteTime)
        //                .FirstOrDefault();
        //    return file is null ? Enumerable.Empty<string>() : GetCodesFromFile(file.FullName);
        //}

        //private int VerifyLabelStarPrintedFile(IEnumerable<string> codes, string verifyCode)
        //{
        //    string firstCode = codes.FirstOrDefault()!;
        //    //string formattedFirstCode = codeService.FormatCodesForReport(firstCode, firstCode.Length);
        //    //проверяем сначала на совпадение
        //    if (firstCode.Equals(verifyCode))
        //    {
        //        //потом на дубли
        //        if (!IsFileContainsDupes(codes))
        //        {
        //            logger.LogInformation("Отпечатанный файл не содержит дубликатов");
        //            return 0;
        //        }
        //        logger.LogError("Отпечатанный файл содержит дубликаты");
        //        return 2;
        //    }
        //    logger.LogError("Первые коды в отпечатанном и входном файлах не совпадают");
        //    return 1;
        //}

        //создаем отчёт
        //public (bool, int) WriteReportFile(string verifyCode)
        //{
        //    string pathToSave = string.Empty;
        //    //HashSet<string> formattedCodes = new();
        //    //IEnumerable<string> codes = GetInfoFromLastLogFile();
        //    string firstCode = printedCodes.FirstOrDefault()!;
        //    //записываем коды в файл отчета
        //    //foreach (var code in codes)
        //    //    formattedCodes.Add(codeService.FormatCodesForReport(code, firstCode.Length));

        //    if (printedCodes.Count() > 0)
        //    {
        //        int verifyResult = VerifyLabelStarPrintedFile(printedCodes, verifyCode);
        //        switch (verifyResult)
        //        {
        //            case 0:
        //                pathToSave = Path.Combine(folderSettings.Path, folderSettings.Reports,
        //                    DateTime.Now.ToString("dd.MM.yyyy"),
        //                    $"{firstCode.Substring(2, 14)}_{DateTime.Now.ToString("HH.mm.ss")}.txt");
        //                File.WriteAllLines(pathToSave, printedCodes);
        //                return (true, 0);
        //            case 1:
        //                return (false, 1);
        //            case 2:
        //                pathToSave = Path.Combine(folderSettings.Path, folderSettings.Reports,
        //                    DateTime.Now.ToString("dd.MM.yyyy"),
        //                    $"{firstCode.Substring(2, 14)}_{DateTime.Now.ToString("HH.mm.ss")}_дубль.txt");
        //                File.WriteAllLines(pathToSave, printedCodes);
        //                return (false, 2);
        //        }
        //    }
        //    else
        //    {
        //        logger.LogWarning("Ни один код не был отпечатан!");
        //        return (false, 3);
        //    }
        //    return (false,-1);
        //}
    }
}
