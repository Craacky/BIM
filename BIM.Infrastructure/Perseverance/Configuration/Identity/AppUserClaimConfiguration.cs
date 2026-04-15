using BIM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BIM.Infrastructure.Perseverance.Configuration.Identity
{
    public class AppUserClaimConfiguration : IEntityTypeConfiguration<AppUserClaim>
    {
        public void Configure(EntityTypeBuilder<AppUserClaim> builder)
        {
            builder.HasOne(q => q.User)
                .WithMany(a => a.Claims)
                .HasForeignKey(z => z.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
