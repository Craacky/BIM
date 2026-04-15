using Microsoft.AspNetCore.Identity;

namespace BIM.Domain.Identity
{
    public class AppUserToken : IdentityUserToken<string>
    {
        public virtual AppUser User { get; set; } = default!;
    }
}
