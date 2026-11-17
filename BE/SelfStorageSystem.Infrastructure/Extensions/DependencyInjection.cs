using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Application.Settings;
using SelfStorageSystem.Infrastructure.Common;
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
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured in appsettings.json.");

        services.AddDbContext<SelfStorageDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(SelfStorageDbContext).Assembly.FullName);
                sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
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
        services.Configure<ReservationSettings>(configuration.GetSection(ReservationSettings.SectionName));
        services.Configure<PaymentSettings>(configuration.GetSection(PaymentSettings.SectionName));
        services.Configure<RentalSettings>(configuration.GetSection(RentalSettings.SectionName));
        services.Configure<MediaSettings>(configuration.GetSection(MediaSettings.SectionName));

        var pinProtectionKey = configuration["PIN_PROTECTION_KEY"] ?? configuration["SecuritySettings:PinProtectionKey"];
        PinProtectionHelper.Initialize(pinProtectionKey);

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IQrTokenService, QrTokenService>();
        services.AddScoped<IPricingEngine, PricingEngine>();
        services.AddScoped<IReservationHoldExpiryService, ReservationHoldExpiryService>();
        services.AddScoped<IStorageLifecycleService, StorageLifecycleService>();
        services.AddScoped<ICustomerReservationService, CustomerReservationService>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<ICustomerCatalogService, CustomerCatalogService>();
        services.AddScoped<ICustomerRentalService, CustomerRentalService>();
        services.AddScoped<ICustomerSupportTicketService, CustomerSupportTicketService>();
        services.AddScoped<ICustomerStoredItemService, CustomerStoredItemService>();
        services.AddScoped<IFacilityStaffService, FacilityStaffService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IAdminManagementService, AdminManagementService>();
        services.AddScoped<IFacilityManagerService, FacilityManagerService>();
        services.AddScoped<IBusinessOperationsService, BusinessOperationsService>();
        services.AddScoped<ICapabilitiesService, CapabilitiesService>();

        // 4.1. Background Hosted Workers
        services.AddHostedService<SelfStorageSystem.Infrastructure.BackgroundJobs.ReservationHoldExpiryWorker>();
        services.AddHostedService<SelfStorageSystem.Infrastructure.BackgroundJobs.TransferExpiryWorker>();
        services.AddHostedService<SelfStorageSystem.Infrastructure.BackgroundJobs.StorageLifecycleBackgroundService>();

        return services;
    }
}

public static class DatabaseSeederExtensions
{
    public static async Task SeedDatabaseAsync(
        this IServiceProvider serviceProvider,
        bool applyMigrations = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SelfStorageDbContext>();

        if (applyMigrations && db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await DbSeeder.SeedAsync(db, cancellationToken);
    }
}
