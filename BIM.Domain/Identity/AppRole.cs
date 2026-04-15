using Microsoft.AspNetCore.Identity;

namespace BIM.Domain.Identity
{
    public class AppRole : IdentityRole
    {
        public string? Definition { get; set; }

        public virtual ICollection<AppRoleClaim> RoleClaims { get; set; }
        public virtual ICollection<AppUserRole> UserRoles { get; set; }

        public AppRole() : base()
        {
            RoleClaims = new HashSet<AppRoleClaim>();
            UserRoles = new HashSet<AppUserRole>();
        }

        public AppRole(string roleName) : base(roleName)
        {
            RoleClaims = new HashSet<AppRoleClaim>();
            UserRoles = new HashSet<AppUserRole>();
        }
    }
}
