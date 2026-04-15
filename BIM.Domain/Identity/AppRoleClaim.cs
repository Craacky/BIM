using Microsoft.AspNetCore.Identity;

namespace BIM.Domain.Identity
{
    public class AppRoleClaim : IdentityRoleClaim<string>
    {
        public string? Definition { get; set; }
        public string? Group { get; set; }

        public virtual AppRole Role { get; set; } = default!;
    }
}
