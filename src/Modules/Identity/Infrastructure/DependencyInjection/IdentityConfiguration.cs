namespace Axon.Modules.Identity.Infrastructure.DependencyInjection;

using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Infrastructure.Persistence.Context;
using Axon.Modules.Identity.Infrastructure.Persistence.Stores;
using Axon.Modules.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Microsoft Identity Framework
/// with custom AxonUserAuth entity and passwordless authentication.
/// </summary>
public static class IdentityConfiguration
{
    /// <summary>
    /// Adds and configures Microsoft Identity Framework for Axon
    /// with passwordless, wallet-based authentication support.
    /// </summary>
    public static IServiceCollection AddAxonIdentity(
        this IServiceCollection services)
    {
        // Configure Identity for passwordless, wallet-based authentication
        services.AddIdentity<AxonUserAuth, IdentityRole<Guid>>(options =>
        {
            // Disable password requirements (wallet-based auth only)
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 0;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;

            // Disable sign-in confirmations (wallet verification is the confirmation)
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;

            // User settings for wallet-based usernames (e.g., "Dynamic:wallet_address")
            options.User.RequireUniqueEmail = false; // No email required for wallet auth
            options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+:";

            // Disable lockout for wallet authentication (signature is the validation)
            options.Lockout.AllowedForNewUsers = false;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.Zero;
            options.Lockout.MaxFailedAccessAttempts = int.MaxValue;

            // Store settings (using custom AxonUserStore)
            options.Stores.MaxLengthForKeys = 128;
            options.Stores.ProtectPersonalData = false; // Wallet addresses are public anyway

            // Token providers (for future features like recovery codes)
            options.Tokens.ProviderMap.Add(
                "AxonTOTP",
                new TokenProviderDescriptor(typeof(TotpSecurityStampBasedTokenProvider<AxonUserAuth>)));
            options.Tokens.AuthenticatorTokenProvider = "AxonTOTP";
        })
        .AddEntityFrameworkStores<IdentityContext>()
        .AddUserStore<AxonUserStore>()
        .AddUserManager<UserManager<AxonUserAuth>>()
        .AddSignInManager<SignInManager<AxonUserAuth>>()
        .AddDefaultTokenProviders();

        // Add custom claims transformation for wallet-based claims
        services.AddScoped<IClaimsTransformation, WalletClaimsTransformation>();

        // Configure application cookie (disable redirects for API)
        services.ConfigureApplicationCookie(options =>
        {
            // API should return 401/403, not redirect to login pages
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToLogout = context =>
            {
                context.Response.StatusCode = 200;
                return Task.CompletedTask;
            };

            // Cookie settings (for future session management)
            options.Cookie.Name = ".Axon.Identity";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = true;
        });

        // Authentication state provider not needed for API-only configuration
        // If Blazor components are added later, uncomment and add the required package reference:
        // services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<AxonUserAuth>>();

        return services;
    }

    /// <summary>
    /// Adds Identity services with feature flag support for gradual rollout.
    /// </summary>
    public static IServiceCollection AddAxonIdentityWithFeatureFlag(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useIdentityFramework = configuration.GetValue<bool>("FeatureFlags:UseIdentityFramework", true);

        if (useIdentityFramework)
        {
            services.AddAxonIdentity();
        }

        return services;
    }
}