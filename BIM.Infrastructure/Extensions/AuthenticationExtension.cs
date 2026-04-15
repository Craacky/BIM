using BIM.Domain.Identity;
using BIM.Infrastructure.Perseverance;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BIM.Infrastructure.Extensions
{
    public static class AuthenticationExtension
    {
        public static IServiceCollection AddAuthenticationService(this IServiceCollection services, IConfiguration conf)
        {
            services.AddIdentity<AppUser, AppRole>()
                    .AddEntityFrameworkStores<AppDbContext>()
                    .AddDefaultTokenProviders();

            services.Configure<IdentityOptions>(opts =>
            {
                opts.Password.RequireDigit = true;
                opts.Password.RequiredLength = 4;
                opts.Password.RequireNonAlphanumeric = false;
                opts.Password.RequireUppercase = false;
                opts.Password.RequireLowercase = false;

                opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                opts.Lockout.MaxFailedAccessAttempts = 5;
                opts.Lockout.AllowedForNewUsers = true;
            });

            return services;
        }
    }
}
