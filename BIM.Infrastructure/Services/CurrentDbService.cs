using BIM.Application.Common.Interfaces;
using BIM.Application.Common.Validator;
using BIM.Application.Features.Databases.Commands;
using BIM.Application.Features.Databases.DTO;
using BIM.Application.Features.Databases.Queries;
using BIM.Application.Features.Products.DTO;
using BIM.Application.Features.Products.Queries;
using BIM.Application.Models;
using BIM.Domain.Enums;
using MediatR;

namespace BIM.Infrastructure.Services
{
    public class CurrentDbService : ICurrentDbService
    {
        //di
        private readonly LabelStarCodeValidator _labelStarCodeValidator;
        private readonly IFileService _fileService;
        private readonly ICodeService _codeService;
        private readonly ILoggerService _loggerService;
        private readonly IMediator _mediator;

        //queries
        private FindGTINProductQuery FindProduct { get; } = new();
        private FindDatabaseQuery FindDatabase { get; } = new();

        //commands
        private AddDatabaseCommand AddFileToDb { get; set; } = new();
        private ChangeDbStatusCommand ChangeDbStatus { get; set; } = new();

        //properties
        public DatabaseListDTO CurrentDb { get; set; } = new();
        public ProductDTO CurrentProduct { get; set; } = new();

        public CurrentDbService(IFileService fileService, ILoggerService loggerService,
            IMediator mediator, ICodeService codeService, LabelStarCodeValidator labelValidator)
        {
            _fileService = fileService;
            _loggerService = loggerService;
            _mediator = mediator;
            _codeService = codeService;
            _labelStarCodeValidator = labelValidator;
        }

        public void AddNewDb(out bool isAdded)
        {
            isAdded = false;
            if (_fileService.FileText.FirstOrDefault() != string.Empty)
            {
                CurrentDb.Name = _fileService.FileName;
                CurrentDb.FirstCode = _fileService.FileText.FirstOrDefault()!;
                CurrentDb.Status = DbStatus.Исходный;
                _loggerService.LogInformation($"Начато новое задание");
                _loggerService.LogInformation($"Добавлен новый файл {CurrentDb.Name} со статусом {CurrentDb.Status}");
                isAdded = true;
            }
            else _loggerService.LogWarning("Первая строка файла пуста");
        }

        public async Task<(bool, string)> VerifyProduct()
        {
            FindProduct.GTIN = CurrentDb.FirstCode.Substring(2, 14);
            CurrentProduct = await _mediator.Send(FindProduct).ConfigureAwait(false);
            if (CurrentProduct is not null)
            {
                _loggerService.LogInformation("Продукт прошел верификацию в базе данных!");
                return (true, CurrentProduct.AboutProduct);
            }
            else
            {
                _loggerService.LogError("Продукт в базе данных не найден!");
                return (false, string.Empty);
            }
        }

        public async Task<int> VerifyStage1Db()
        {
            FindDatabase.FirstCode = CurrentDb.FirstCode;
            FindDatabase.Name = CurrentDb.Name;
            FindDatabase.IsAnother = false;
            var findDatabase = await _mediator.Send(FindDatabase).ConfigureAwait(false);
            if (findDatabase is null)
            {
                _fileService.CopyFileToLabelStarFolder();
                CurrentDb.Status = DbStatus.После_проверки;
                CurrentDb.CreatedDate = DateTime.Now;
                AddFileToDb.DatabaseListDTO = CurrentDb;
                var id = await _mediator.Send(AddFileToDb).ConfigureAwait(false);
                if (id.Succeeded)
                {
                    CurrentDb.Id = id.Data!;
                    _loggerService.LogInformation($"Статус {CurrentDb.Name} изменен на {CurrentDb.Status}. Файл готов к печати");
                    return 0;
                }
                _loggerService.LogError($"Статус {CurrentDb.Name} не был изменен. Файл не готов к печати");
                return -1;
            }
            else
            {
                switch (findDatabase.Status)
                {
                    case DbStatus.Начата_печать:
                        _loggerService.LogWarning("Файл с текущим первым кодом уже был запущен на печать");
                        return 2;
                    case DbStatus.После_проверки:
                        CurrentDb.Id = findDatabase.Id;
                        _fileService.CopyFileToLabelStarFolder();
                        _loggerService.LogInformation($"Файл {CurrentDb.Name} с текущим первым кодом ранее проходил проверку, готов к печати");
                        FindDatabase.IsAnother = true;
                        var findDB = await _mediator.Send(FindDatabase).ConfigureAwait(false);
                        if (findDB is not null)
                        {
                            switch (findDB.Status)
                            {
                                case DbStatus.После_проверки:
                                    CurrentDb.Id = findDatabase.Id;
                                    _fileService.CopyFileToLabelStarFolder();
                                    _loggerService.LogInformation($"Файл {CurrentDb.Name} с текущим первым кодом ранее проходил проверку, готов к печати");
                                    return 3;
                                case DbStatus.Начата_печать:
                                    return 2;
                                case DbStatus.Сбой:
                                    return 4;
                                case DbStatus.Архив:
                                    return 5;
                            }
                        }
                        return 3;
                    case DbStatus.Сбой:
                        _loggerService.LogWarning("Выбран файл после сбоя");
                        return 4;
                    case DbStatus.Архив:
                        _loggerService.LogWarning("Файл с текущим первым кодом уже находится в архиве");
                        return 5;
                }
                return -2;
            }
        }

        public (int, string) VerifyStage2Db(string scannedCode)
        {
            LabelStarCodeVM labelCode = new() { Code = scannedCode };
            var isCodeValid = _labelStarCodeValidator.Validate(labelCode);
            if (isCodeValid.IsValid)
            {
                if (!_codeService.CodeContainsGS(CurrentDb.FirstCode))
                {
                    _loggerService.LogError($"Отсутствует GS-разделитель в исходной базе");
                    return (1, "Коды в файле не содержат GS-разделитель.");
                }
                else
                {
                    bool result = false;
                    switch (CurrentDb.FirstCode.Length)
                    {
                        case 78:
                            result = _codeService.VerifyMeatCodes(CurrentDb.FirstCode, scannedCode, 24, 31);
                            break;
                        case 85:
                        {
                            int firstGs = CurrentDb.FirstCode.IndexOf((char)29);
                            int lastGs = CurrentDb.FirstCode.LastIndexOf((char)29);
                            if (firstGs >= 0 && lastGs > firstGs)
                            {
                                result = _codeService.VerifyMeatCodes(CurrentDb.FirstCode, scannedCode, firstGs, lastGs);
                            }
                            break;
                        }
                        case 33:
                            result = _codeService.VerifyCodes(CurrentDb.FirstCode, scannedCode, 26);
                            break;
                        case 31:
                            result = _codeService.VerifyCodes(CurrentDb.FirstCode, scannedCode, 24);
                            break;
                        case 32:
                            result = _codeService.VerifyCodes(CurrentDb.FirstCode, scannedCode, 25);
                            break;
                        case 38:
                        case 39:
                            result = _codeService.VerifyCodes(
                                CurrentDb.FirstCode,
                                scannedCode,
                                CurrentDb.FirstCode.IndexOf((char)29));
                            break;
                    }
                    if (result)
                    {
                        _loggerService.LogInformation($"Печать кодов разрешена!");
                        return (0, string.Empty);
                    }
                    
                    else
                    {
                        _loggerService.LogError($"Печать кодов запрещена! Первый код не совпадает");
                        return (2, "Печать кодов запрещена!\nПервый код не совпадает!");
                    }
                }
            }
            else
            {
                string error = isCodeValid.Errors.FirstOrDefault()!.ErrorMessage;
                _loggerService.LogError(error);
                return (3, error);
            }
        }

        public async void FinishPrint()
        {
            CurrentDb.Status = DbStatus.Архив;
            ChangeDbStatus.DbStatus = CurrentDb.Status;
            var result = await _mediator.Send(ChangeDbStatus).ConfigureAwait(false);
            if (result.Succeeded)
            {
                // First, save the validated file to the "Good codes" folder
                // This will create a validated version of the file after successful printing
                _fileService.SaveValidatedFileToGoodCodesFolder();

                _loggerService.LogInformation($"Статус {CurrentDb.Name} изменен на {CurrentDb.Status}. Печать завершена. Файл сохранен в 'Хорошие коды'. Перемещение выполняется отдельно.");
            }
            else _loggerService.LogError($"Статус {CurrentDb.Name} не изменен при окончании печати. Файл не будет перемещен в архив");
        }

        public async void StartPrint()
        {
            CurrentDb.Status = DbStatus.Начата_печать;
            ChangeDbStatus.Id = CurrentDb.Id;
            ChangeDbStatus.DbStatus = CurrentDb.Status;
            var result = await _mediator.Send(ChangeDbStatus).ConfigureAwait(false);
            if(result.Succeeded)
                _loggerService.LogInformation($"Статус {CurrentDb.Name} изменен на {CurrentDb.Status}");
            else _loggerService.LogError($"Статус {CurrentDb.Name} не изменен при старте задания");
        }

        public async void RePrint()
        {
            CurrentDb.Status = DbStatus.Сбой;
            ChangeDbStatus.DbStatus = CurrentDb.Status;
            var result = await _mediator.Send(ChangeDbStatus).ConfigureAwait(false);
            if(result.Succeeded)
                _loggerService.LogInformation($"Статус {CurrentDb.Name} изменен на {CurrentDb.Status}");
            else _loggerService.LogError($"Статус {CurrentDb.Name} не изменен при перепечати");
        }
    }
}
