using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SelfStorageSystem.Infrastructure.Persistence;

public sealed class SelfStorageDbContextFactory : IDesignTimeDbContextFactory<SelfStorageDbContext>
{
    public SelfStorageDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            basePath,
            Path.GetFullPath(Path.Combine(basePath, "..", "SelfStorageSystem")),
            Path.GetFullPath(Path.Combine(basePath, "SelfStorageSystem")),
            Path.GetFullPath(Path.Combine(basePath, "BE", "SelfStorageSystem"))
        };
        var configPath = candidates.FirstOrDefault(dir => File.Exists(Path.Combine(dir, "appsettings.json"))) ?? basePath;

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        if (args is { Length: > 0 })
        {
            configBuilder.AddCommandLine(args);
        }

        var configuration = configBuilder.Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=SelfStoragePRN222_Migrations;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var optionsBuilder = new DbContextOptionsBuilder<SelfStorageDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(SelfStorageDbContext).Assembly.FullName);
            sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

        return new SelfStorageDbContext(optionsBuilder.Options);
    }
}
