using BIM.Application.Common.Interfaces;
using BIM.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BIM.Infrastructure.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services
                .AddScoped<ICultureSettingsService, CultureSettingsService>()
                .AddScoped<IFileService, FileService>()
                .AddScoped<ICurrentUserService, CurrentUserService>()
                .AddScoped<IFolderService, FolderService>()
                .AddScoped<IGeneralService, GeneralService>()
                .AddScoped<ICodeService, CodeService>()
                .AddScoped<ICurrentDbService, CurrentDbService>()
                .AddSingleton<ILoggerService, LoggerService>()
                .AddScoped<ILicenseService, LicenseService>();
        }
    }
}