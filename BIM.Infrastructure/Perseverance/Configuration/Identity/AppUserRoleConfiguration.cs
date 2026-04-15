using BIM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BIM.Infrastructure.Perseverance.Configuration.Identity
{
    public class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
    {
        public void Configure(EntityTypeBuilder<AppUserRole> builder)
        {
            builder.HasOne(q => q.Role)
                .WithMany(a => a.UserRoles)
                .HasForeignKey(z => z.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(q => q.User)
                .WithMany(a => a.Roles)
                .HasForeignKey(z => z.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
