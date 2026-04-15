using BIM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BIM.Infrastructure.Perseverance.Configuration.Identity
{
    public class AppUserTokenConfiguration : IEntityTypeConfiguration<AppUserToken>
    {
        public void Configure(EntityTypeBuilder<AppUserToken> builder)
        {
            builder.HasOne(q => q.User)
                .WithMany(a => a.Tokens)
                .HasForeignKey(z => z.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
