using BIM.Application.Common.Interfaces;
using BIM.Domain.Entities;
using BIM.Domain.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BIM.Infrastructure.Perseverance
{
    public class AppDbContext : IdentityDbContext<AppUser, AppRole, string,
        AppUserClaim, AppUserRole, AppUserLogin, AppRoleClaim, AppUserToken>, IAppDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DatabaseList> DatabaseLists { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Logger> Loggers { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken token = default)
            => base.SaveChangesAsync(token);

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            //builder.ApplyGlobalFilters();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }
    }
}
