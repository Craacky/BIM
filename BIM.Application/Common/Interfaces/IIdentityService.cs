using BIM.Application.Common.Interfaces.Identity.DTO;
using BIM.Application.Models;

namespace BIM.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<bool> IsInRoleAsync(string userId, string role, CancellationToken token = default);
        Task<Result<bool>> LoginAsync(TokenRequest request, CancellationToken cancelToken = default);
        Task<bool> AuthorizeAsync(string userId, string policy, CancellationToken token = default);
    }
}
