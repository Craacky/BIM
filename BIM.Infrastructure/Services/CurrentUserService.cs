using BIM.Application.Common.Interfaces;
using BIM.Domain.Identity;

namespace BIM.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        public string UserName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public bool IsAuthenticated { get; private set; }

        public void SetCurrentUser(AppUser user)
        {
            if (user != null)
            {
                UserName = user.UserName;
                UserId = user.Id;
                User = user;
                IsAuthenticated = true;
            }
            else
            {
                ClearCurrentUser();
            }
        }

        public void SetCurrentUserName(string userName)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                UserName = userName;
                IsAuthenticated = true;
            }
            else
            {
                ClearCurrentUser();
            }
        }

        public void ClearCurrentUser()
        {
            UserName = string.Empty;
            UserId = string.Empty;
            User = null!;
            IsAuthenticated = false;
        }
    }
}