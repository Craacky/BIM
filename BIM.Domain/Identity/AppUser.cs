using Microsoft.AspNetCore.Identity;

namespace BIM.Domain.Identity
{
    public class AppUser : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? Provider { get; set; } = "local";
        public bool IsActive { get; set; }//активен ли сейчас
        public bool IsLive { get; set; }//находится ли он сейчас в нашем excel-списке
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }

        public virtual ICollection<AppUserLogin> Logins { get; set; }
        public virtual ICollection<AppUserRole> Roles { get; set; }
        public virtual ICollection<AppUserClaim> Claims { get; set; }
        public virtual ICollection<AppUserToken> Tokens { get; set; }

        public AppUser() : base()
        {
            Logins = new HashSet<AppUserLogin>();
            Roles = new HashSet<AppUserRole>();
            Claims = new HashSet<AppUserClaim>();
            Tokens = new HashSet<AppUserToken>();
        }
    }
}
