using BIM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BIM.Infrastructure.Perseverance.Configuration.Identity
{
    public class AppUserLoginConfiguration : IEntityTypeConfiguration<AppUserLogin>
    {
        public void Configure(EntityTypeBuilder<AppUserLogin> builder)
        {
            builder.HasOne(q => q.User)
                .WithMany(a => a.Logins)
                .HasForeignKey(z => z.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
