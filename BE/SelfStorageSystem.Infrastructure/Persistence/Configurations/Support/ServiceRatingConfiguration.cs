using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class ServiceRatingConfiguration : IEntityTypeConfiguration<ServiceRating>
{
    public void Configure(EntityTypeBuilder<ServiceRating> entity)
    {
        entity.HasKey(e => e.TicketId).HasName("PK__service___D596F96B2887DCB4");
        entity.ToTable("service_ratings", "core", tb =>
        {
            tb.HasTrigger("trg_service_ratings_validate_scope");
            tb.HasCheckConstraint("CK_service_ratings_score", "[score] BETWEEN 1 AND 5");
        });

        entity.HasIndex(e => e.CustomerId, "service_ratings_customer_id_idx");

        entity.Property(e => e.TicketId).ValueGeneratedNever().HasColumnName("ticket_id");
        entity.Property(e => e.Comment).HasMaxLength(255).HasColumnName("comment");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CustomerId).HasColumnName("customer_id");
        entity.Property(e => e.Score).HasColumnName("score");

        entity.HasOne(d => d.Customer).WithMany(p => p.ServiceRatings)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__service_r__custo__3F9B6DFF");

        entity.HasOne(d => d.Ticket).WithOne(p => p.ServiceRating)
            .HasForeignKey<ServiceRating>(d => d.TicketId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__service_r__ticke__3EA749C6");
    }
}
