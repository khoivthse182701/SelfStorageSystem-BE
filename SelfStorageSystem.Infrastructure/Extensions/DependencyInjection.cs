using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=SelfStorageDB;Username=postgres;Password=postgres";

        services.AddDbContext<SelfStorageDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddMemoryCache();

        return services;
    }
}
