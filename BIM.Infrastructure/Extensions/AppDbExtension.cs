using BIM.Infrastructure.Constants.Database;
using Microsoft.EntityFrameworkCore;

namespace BIM.Infrastructure.Extensions
{
    public static class AppDbExtension
    {
        public static DbContextOptionsBuilder UseDatabase(this DbContextOptionsBuilder builder, string provider, string connection)
        {
            switch (provider.ToLowerInvariant())
            {
                case DbProviderKey.SqlServer:
                    return builder.UseSqlServer(connection, q => q.MigrationsAssembly("BIM.Migrators.MSSQL"));
                //case DbProviderKey.SqLite:
                //    return builder.UseSqlite(connection, q => q.MigrationsAssembly("ProjectAccounting.Migrators.SqLite"));
                default:
                    throw new InvalidOperationException($"Database provider '{provider}' is not supported.");
            }
        }
    }
}
