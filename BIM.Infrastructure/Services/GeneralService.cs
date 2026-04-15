using BIM.Application.Common.Configs;
using BIM.Application.Common.Interfaces;

namespace BIM.Infrastructure.Services
{
    public class GeneralService : IGeneralService
    {
        private readonly AppConfigSettings _appSettings;

        public string PCName { get; set; } = string.Empty;

        public GeneralService(AppConfigSettings appSettings)
        {
            _appSettings = appSettings;
            PCName = _appSettings.PC_Name;
        }
    }
}