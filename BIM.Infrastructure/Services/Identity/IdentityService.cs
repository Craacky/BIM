using BIM.Application.Common.Interfaces;
using BIM.Application.Common.Interfaces.Identity.DTO;
using BIM.Application.Models;
using BIM.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BIM.Infrastructure.Services.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly IUserClaimsPrincipalFactory<AppUser> _userClaimsPrincipalFactory;
        private readonly IAuthorizationService _authorizationService;

        private readonly UserManager<AppUser> _userManager;

        public IdentityService(IServiceScopeFactory scopeFactory)
        {
            var scope = scopeFactory.CreateScope();
            _userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            _userClaimsPrincipalFactory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
            _authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        }

        public async Task<bool> IsInRoleAsync(string userId, string role, CancellationToken token = default)
        {
            var user = await _userManager.Users.SingleOrDefaultAsync(q => q.Id == userId, token)
                ?? throw new Exception("User not found");//NotFountException
            return await _userManager.IsInRoleAsync(user, role);
        }

        public async Task<bool> AuthorizeAsync(string userId, string policy, CancellationToken token = default)
        {
            var user = await _userManager.Users.SingleOrDefaultAsync(q => q.Id == userId, token)
                ?? throw new Exception("User not found");//NotFountException
            var principal = await _userClaimsPrincipalFactory.CreateAsync(user);
            var result = await _authorizationService.AuthorizeAsync(principal, policy);
            return result.Succeeded;
        }

        public async Task<Result<bool>> LoginAsync(TokenRequest request, CancellationToken cancelToken = default)
        {
            var user = await _userManager.FindByNameAsync(request.UserName!);
            if (user is null)
                return await Result<bool>.FailAsync(new string[] { "User not found" });
            if (!user.IsActive)
                return await Result<bool>.FailAsync(new string[] { "User not active" });
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password!);
            if (!passwordValid)
                return await Result<bool>.FailAsync(new string[] { "Invalid credentials" });
            await _userManager.UpdateAsync(user);
            return await Result<bool>.SuccessAsync(true);
        }
    }
}