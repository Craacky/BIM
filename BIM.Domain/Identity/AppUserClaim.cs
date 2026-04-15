using Microsoft.AspNetCore.Identity;

namespace BIM.Domain.Identity
{
    public class AppUserClaim : IdentityUserClaim<string>
    {
        public string? Definition { get; set; }
        public virtual AppUser User { get; set; } = default!;
    }
}
