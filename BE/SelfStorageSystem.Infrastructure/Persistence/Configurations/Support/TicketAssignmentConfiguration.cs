using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Support;

public sealed class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__ticket_a__3213E83FD8FED18D");
        entity.ToTable("ticket_assignments", "core", tb =>
        {
            tb.HasTrigger("trg_ticket_assignments_validate_scope");
            tb.HasCheckConstraint("CK_ticket_assignments_dates", "[ended_at] IS NULL OR [ended_at] >= [assigned_at]");
        });

        entity.HasIndex(e => e.AssignedBy, "ticket_assignments_assigned_by_idx").HasFilter("([assigned_by] IS NOT NULL)");
        entity.HasIndex(e => new { e.EmployeeId, e.EndedAt }, "ticket_assignments_employee_id_idx");
        entity.HasIndex(e => e.TicketId, "ticket_assignments_one_active_uidx")
            .IsUnique()
            .HasFilter("([ended_at] IS NULL)");
        entity.HasIndex(e => new { e.TicketId, e.AssignedAt }, "ticket_assignments_ticket_history_idx").IsDescending(false, true);

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AssignedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("assigned_at");
        entity.Property(e => e.AssignedBy).HasColumnName("assigned_by");
        entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
        entity.Property(e => e.EndReason).HasMaxLength(255).HasColumnName("end_reason");
        entity.Property(e => e.EndedAt).HasColumnName("ended_at");
        entity.Property(e => e.TicketId).HasColumnName("ticket_id");

        entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.TicketAssignments)
            .HasForeignKey(d => d.AssignedBy)
            .HasConstraintName("FK__ticket_as__assig__1975C517");

        entity.HasOne(d => d.Employee).WithMany(p => p.TicketAssignments)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_as__emplo__1881A0DE");

        entity.HasOne(d => d.Ticket).WithOne(p => p.TicketAssignment)
            .HasForeignKey<TicketAssignment>(d => d.TicketId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__ticket_as__ticke__178D7CA5");
    }
}
