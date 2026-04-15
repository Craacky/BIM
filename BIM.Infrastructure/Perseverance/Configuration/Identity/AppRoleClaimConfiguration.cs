using BIM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BIM.Infrastructure.Perseverance.Configuration.Identity
{
    public class AppRoleClaimConfiguration : IEntityTypeConfiguration<AppRoleClaim>
    {
        public void Configure(EntityTypeBuilder<AppRoleClaim> builder)
        {
            builder.HasOne(q => q.Role)
                .WithMany(a => a.RoleClaims)
                .HasForeignKey(z => z.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}