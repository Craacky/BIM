using BIM.Application.Common.Configs;
using BIM.Application.Common.Interfaces;
using BIM.Infrastructure.Extensions;
using BIM.Infrastructure.Perseverance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BIM.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration conf)
        {
            services.Configure<DatabaseSettings>(conf.GetSection(DatabaseSettings.SectionName));
            services.Configure<AppConfigSettings>(conf.GetSection(AppConfigSettings.SectionName));
            services.Configure<LabelStarSettings>(conf.GetSection(LabelStarSettings.SectionName));
            services.Configure<FolderSettings>(conf.GetSection(FolderSettings.SectionName));

            services.AddSingleton(s => s.GetRequiredService<IOptions<DatabaseSettings>>().Value);
            services.AddSingleton(s => s.GetRequiredService<IOptions<AppConfigSettings>>().Value);
            services.AddSingleton(s => s.GetRequiredService<IOptions<LabelStarSettings>>().Value);
            services.AddSingleton(s => s.GetRequiredService<IOptions<FolderSettings>>().Value);

            services.Configure<LicenseSettings>(conf.GetSection(LicenseSettings.SectionName));
            services.AddSingleton(s => s.GetRequiredService<IOptions<LicenseSettings>>().Value);

            services.AddDbContext<AppDbContext>(options =>
            {
                string? connectString = conf.GetValue<string>($"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.ConnectionString)}");
                options.UseSqlServer(
                    connectionString: connectString,
                    builder =>
                    {
                        builder.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                        builder.EnableRetryOnFailure(5, TimeSpan.FromSeconds(15), null);
                        builder.CommandTimeout(20);
                    });
                options.EnableDetailedErrors(true);
                options.EnableSensitiveDataLogging();
            });
            services.AddScoped<IDbContextFactory<AppDbContext>, ContextFactory<AppDbContext>>();
            services.AddTransient<IAppDbContext>(provider => provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
            services.AddAuthenticationService(conf);
            services.AddServices();

            return services;
        }
    }
}