using BIM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BIM.Infrastructure.Perseverance.Configuration.Identity
{
    public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> builder)
        {
            builder.HasMany(e => e.Logins)
                .WithOne()
                .HasForeignKey(q => q.UserId)
                .IsRequired();

            builder.HasMany(e => e.Tokens)
                .WithOne()
                .HasForeignKey(q => q.UserId)
                .IsRequired();

            builder.HasMany(e => e.Roles)
                .WithOne()
                .HasForeignKey(q => q.UserId)
                .IsRequired();
        }
    }
}
