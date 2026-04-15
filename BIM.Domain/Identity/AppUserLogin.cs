using Microsoft.AspNetCore.Identity;

namespace BIM.Domain.Identity
{
    public class AppUserLogin : IdentityUserLogin<string>
    {
        public virtual AppUser User { get; set; } = default!;
    }
}
