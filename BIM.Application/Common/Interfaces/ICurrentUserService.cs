using BIM.Domain.Identity;

namespace BIM.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        string UserName { get; set; }
        string UserId { get; set; }
        AppUser User { get; set; }
        bool IsAuthenticated { get; }

        void SetCurrentUser(AppUser user);
        void SetCurrentUserName(string userName);
        void ClearCurrentUser();
    }
}