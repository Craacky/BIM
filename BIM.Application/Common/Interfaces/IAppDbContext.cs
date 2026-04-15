using BIM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BIM.Application.Common.Interfaces
{
    public interface IAppDbContext
    {
        Task<int> SaveChangesAsync(CancellationToken token);
        DbSet<Logger> Loggers { get; set; }
        DbSet<DatabaseList> DatabaseLists { get; set; }
        DbSet<Product> Products { get; set; }
    }
}
