using BIM.Application.Common.Configs;
using BIM.Infrastructure.Constants.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using System.Data;
using System;
using System.IO;

namespace BIM.Infrastructure.Extensions
{
    public static class SerilogExtension
    {
        private const string loggerTable = "Loggers";
        public static void RegisterLogger(this IHostBuilder service)
        {
            service.UseSerilog((context, conf) =>
                    conf.ReadFrom.Configuration(context.Configuration)
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Error)
                        .MinimumLevel.Override("Serilog", LogEventLevel.Error)
                        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Update", LogEventLevel.Error)
                        .Enrich.FromLogContext()
                        .WriteTo.Async(wt => wt.File("./log/log-.txt", rollingInterval: RollingInterval.Day))
                        .WriteTo.Async(wt => wt.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIM", "log", "log-.txt"), rollingInterval: RollingInterval.Day))
                //.WriteTo.Async(wt => wt.File($"./log/log-.txt", rollingInterval: RollingInterval.Day))
                //.WriteTo.Async(wt => wt.Console())
                //.WriteToDatabase(context.Configuration)
            );
            //{Path.Combine(FolderService.folderPath, FolderService.logFolder)}
        }

        private static void WriteToDatabase(this LoggerConfiguration loggerConfig, IConfiguration config)
        {
            if (config.GetValue<bool>("UseInMemoryDatabase"))
                return;
            string? dbProvider = config.GetValue<string>($"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.DbProvider)}");
            string? connectString = config.GetValue<string>($"{nameof(DatabaseSettings)}:{nameof(DatabaseSettings.ConnectionString)}");
            switch (dbProvider)
            {
                case DbProviderKey.SqlServer:
                    WriteToSqlServer(loggerConfig, connectString);
                    break;
                //case DbProviderKey.SqLite:
                //    WriteToSqLite(loggerConfig, connectString);
                //    break;
            }
        }

        private static void WriteToSqlServer(LoggerConfiguration loggerConfig, string? connection)
        {
            if (string.IsNullOrEmpty(connection))
                return;
            MSSqlServerSinkOptions sinkOpts = new()
            {
                TableName = loggerTable,
                SchemaName = "dbo",
                AutoCreateSqlDatabase = false,
                AutoCreateSqlTable = false,
                BatchPostingLimit = 100,
                BatchPeriod = new TimeSpan(0, 0, 20)
            };
            ColumnOptions columnOpts = new();
            columnOpts.Store.Add(StandardColumn.LogEvent);
            columnOpts.AdditionalColumns = new Collection<SqlColumn>
            {
                new()
                {
                    ColumnName = "ClientIP",
                    PropertyName = "ClientIp",
                    DataType = SqlDbType.NVarChar,
                    DataLength = 64
                },
                new()
                {
                    ColumnName = "UserName",
                    PropertyName = "UserName",
                    DataType = SqlDbType.NVarChar,
                    DataLength = 64
                },
                new()
                {
                    ColumnName = "ClientAgent",
                    PropertyName = "ClientAgent",
                    DataType = SqlDbType.NVarChar,
                    DataLength = -1
                }
            };
            columnOpts.LogEvent.DataLength = 2048;
            columnOpts.PrimaryKey = columnOpts.Id;
            columnOpts.TimeStamp.NonClusteredIndex = true;

            loggerConfig.WriteTo.Async(wt => wt.MSSqlServer(connection,
                sinkOpts,
                columnOptions: columnOpts
            ));
        }

        //private static void WriteToSqLite(LoggerConfiguration loggerConfig, string? connection)
        //{
        //    if (string.IsNullOrEmpty(connection))
        //        return;
        //    loggerConfig.WriteTo.Async(q => q.SQLite(
        //        connection,
        //        loggerTable,
        //        LogEventLevel.Information
        //    ));
        //}
    }
}
