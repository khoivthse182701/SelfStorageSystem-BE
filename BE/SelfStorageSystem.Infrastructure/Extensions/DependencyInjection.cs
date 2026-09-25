using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Infrastructure.Persistence;
using SelfStorageSystem.Infrastructure.Services;

namespace SelfStorageSystem.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Chuỗi kết nối 'DefaultConnection' chưa được cấu hình trong appsettings.json.");

        services.AddDbContext<SelfStorageDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        // 2. Memory Cache
        services.AddMemoryCache();

        // 3. Options Configuration (No hardcoding)
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));
        services.Configure<MailSettings>(configuration.GetSection(MailSettings.SectionName));
        services.Configure<OtpSettings>(configuration.GetSection(OtpSettings.SectionName));
        services.Configure<RateLimitingSettings>(configuration.GetSection(RateLimitingSettings.SectionName));
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        // 4. Domain & Application Services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IAuthService, AuthService>();

        // 5. JWT Authentication
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() 
            ?? throw new InvalidOperationException("Cấu hình 'JwtSettings' chưa được định nghĩa trong appsettings.json.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:Secret bắt buộc phải có ít nhất 32 ký tự.");
        }
        var secret = jwtSettings.Secret;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }
}
