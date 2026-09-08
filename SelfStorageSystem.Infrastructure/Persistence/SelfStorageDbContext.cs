using Microsoft.EntityFrameworkCore;

namespace SelfStorageSystem.Infrastructure.Persistence;

public class SelfStorageDbContext : DbContext
{
    public SelfStorageDbContext(DbContextOptions<SelfStorageDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
