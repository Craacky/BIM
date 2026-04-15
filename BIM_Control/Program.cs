using BIM.Application;
using BIM.Infrastructure;
using BIM.Infrastructure.Extensions;
using BIM_Control.Forms;
using BIM_Control.Services;
using BIM.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BIM.Infrastructure.Perseverance;

namespace BIM_Control
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var configBuilder = new ConfigurationBuilder()
                                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                    .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true); // Changed to false to ensure it throws if file is missing
            Configuration = configBuilder.Build();

            // Verify configuration was loaded
            if (Configuration == null)
            {
                MessageBox.Show("Ошибка: Не удалось загрузить конфигурацию приложения.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Verify required configuration sections exist
            var printerIP = Configuration["PrinterSettings:IP"];
            if (string.IsNullOrEmpty(printerIP))
            {
                MessageBox.Show("Ошибка: Не найден IP-адрес принтера в конфигурации.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var host = CreateHostBuilder()
                        .ConfigureAppConfiguration(b =>
                        {
                            b.Sources.Clear();
                            b.AddConfiguration(Configuration);
                        })
                        .Build();

            // ServiceProvider = host.Services; // Removed static property assignment
            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<AppDbContext>();
                    context.Database.EnsureCreated();

                    // Retrieve services with better error handling
                    var logger = services.GetRequiredService<ILoggerService>();
                    logger.LogInformation("═══════════════════════════════════════════════════════════════");
                    logger.LogInformation("╔═══════════════════════════════════════════════════════════════╗");
                    logger.LogInformation("║     ЗАПУСК ПРИЛОЖЕНИЯ BIMv2                            ║");
                    logger.LogInformation("║     Дата/Время: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "                          ║");
                    logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝");
                    logger.LogInformation("═══════════════════════════════════════════════════════════════");
                    logger.LogInformation($"Сервис логирования успешно загружен");

                    var printerMonitor = services.GetRequiredService<PrinterMonitorService>();
                    if (printerMonitor == null)
                    {
                        logger.LogError("КРИТИЧЕСКАЯ ОШИБКА: Сервис мониторинга принтера не был создан");
                        MessageBox.Show("Ошибка: Сервис мониторинга принтера не был создан.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    logger.LogInformation($"Сервис мониторинга принтера успешно инициализирован");

                    // License Validation
                    logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    logger.LogInformation("Начинаем проверку лицензии");
                    
                    var licenseService = services.GetRequiredService<ILicenseService>();
                    var licenseSettings = services.GetRequiredService<BIM.Application.Common.Configs.LicenseSettings>();
                    
                    if (!licenseService.ValidateLicense(licenseSettings.SecretKey, out string licenseError))
                    {
                        logger.LogError($"ОШИБКА ЛИЦЕНЗИРОВАНИЯ: {licenseError}");
                        MessageBox.Show($"Ошибка лицензии: {licenseError}\nПриложение будет закрыто.", 
                            "Ошибка лицензирования", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }
                    logger.LogInformation("✓ Лицензия валидна. Приложение может запуститься");

                    // Validate services
                    logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    logger.LogInformation("Инициализация главной формы (ControlForm)");
                    
                    var mainForm = services.GetRequiredService<ControlForm>();
                    if (mainForm == null)
                    {
                        logger.LogError("КРИТИЧЕСКАЯ ОШИБКА: Главная форма не была создана");
                        MessageBox.Show("Ошибка: Главная форма не была создана.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    logger.LogInformation("✓ Главная форма успешно создана");

                    // Show login form first
                    logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    logger.LogInformation("Отображение формы входа...");

                    var loginForm = services.GetRequiredService<LoginForm>();
                    var dialogResult = loginForm.ShowDialog();
    
                    if (dialogResult == DialogResult.OK)
                    {
                        logger.LogInformation("Пользователь успешно вошел в систему. Запуск основного окна...");
                        Application.Run(mainForm);
                    }
                        else
                    {
                        logger.LogInformation("Вход в систему отменен или неудачен. Завершение приложения...");
                    }

                logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    logger.LogInformation("╔═══════════════════════════════════════════════════════════════╗");
                    logger.LogInformation("║     ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ BIMv2                       ║");
                    logger.LogInformation("║     Дата/Время: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "                          ║");
                    logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝");
                    logger.LogInformation("═══════════════════════════════════════════════════════════════");
                }
                catch (Exception ex)
                {
                    // Log the full exception details to help diagnose the issue
                    var errorMessage = $"Критическая ошибка при запуске:\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n\nInner Exception:\n{ex.InnerException.Message}\n\nInner StackTrace:\n{ex.InnerException.StackTrace}";
                    }

                    MessageBox.Show(errorMessage, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static IConfiguration Configuration { get; private set; }

        public static IHostBuilder CreateHostBuilder()
        {
            var builder = Host.CreateDefaultBuilder();
            builder.RegisterLogger();
            return builder
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructureServices(context.Configuration);
                    services.AddAppServices();
                    services.AddScoped<LoginForm>();
                    services.AddScoped<ControlForm>();
                    services.AddScoped<PrinterStatusForm>(); // Add PrinterStatusForm for DI
                    services.AddScoped<PrinterMonitorService>();
                    services.AddSingleton<CameraService>();
                    services.AddSingleton<StatisticsService>(); // Register StatisticsService
                });
        }
    }
}
