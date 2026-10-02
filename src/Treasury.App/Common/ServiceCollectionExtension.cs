using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Auth;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Common
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddDatabase(
            this IServiceCollection services,
            ConfigurationManager configuration,
            IWebHostEnvironment environment)
        {
            var connectionString = configuration
                .GetConnectionString("DefaultConnection");

            var shouldUseInMemoryDatabase = environment.IsEnvironment("Testing")
                || environment.IsDevelopment()
                || string.IsNullOrWhiteSpace(connectionString);

            if (shouldUseInMemoryDatabase)
            {
                var inMemoryDbName = $"TreasuryDb-{Guid.NewGuid()}";
                services.AddDbContext<TreasuryDbContext>(options =>
                    options.UseInMemoryDatabase(inMemoryDbName));
            }
            else
            {
                services.AddDbContext<TreasuryDbContext>(options =>
                    options.UseNpgsql(connectionString));
            }
            return services;
        }

        public static IServiceCollection AddAuthenticationAndAuthorization(
            this IServiceCollection services)
        {
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<TreasuryDbContext>()
                .AddDefaultTokenProviders()
                .AddSignInManager<SignInManager<ApplicationUser>>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
                .AddCookie(IdentityConstants.ApplicationScheme, options =>
                {
                    options.Events.OnRedirectToLogin = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect("/auth/login");
                        return Task.CompletedTask;
                    };

                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }

                        context.Response.Redirect("/auth/login");
                        return Task.CompletedTask;
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.OwnerOnly, policy => policy.RequireAuthenticatedUser());
                options.AddPolicy(Policies.SharedReadOnly, policy => policy.RequireAuthenticatedUser());
            });
            return services;
        }
    }
}