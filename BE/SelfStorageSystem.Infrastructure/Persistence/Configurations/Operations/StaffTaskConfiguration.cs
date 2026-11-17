using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class StaffTaskConfiguration : IEntityTypeConfiguration<StaffTask>
{
    public void Configure(EntityTypeBuilder<StaffTask> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__staff_ta__3213E83FFDB96EA1");
        entity.ToTable("staff_tasks", "core", tb =>
        {
            tb.HasTrigger("trg_staff_tasks_validate_scope");
            tb.HasCheckConstraint("CK_staff_tasks_task_type", EnumDbMapper.ToSqlCheckInClause<StaffTaskType>("task_type"));
            tb.HasCheckConstraint("CK_staff_tasks_status", EnumDbMapper.ToSqlCheckInClause<StaffTaskStatus>("status"));
            tb.HasCheckConstraint("CK_staff_tasks_progress", "[progress_percent] BETWEEN 0 AND 100");
            tb.HasCheckConstraint("CK_staff_tasks_done_progress", "([status] = 'done' AND [progress_percent] = 100) OR [status] <> 'done'");
        });

        entity.HasIndex(e => new { e.AssignedEmployeeId, e.Status, e.DueAt }, "staff_tasks_assigned_employee_id_idx").HasFilter("([assigned_employee_id] IS NOT NULL)");
        entity.HasIndex(e => e.CreatedBy, "staff_tasks_created_by_idx");
        entity.HasIndex(e => new { e.FacilityId, e.Status, e.DueAt }, "staff_tasks_facility_queue_idx");
        entity.HasIndex(e => e.MaintenanceWorkOrderId, "staff_tasks_maintenance_id_idx").HasFilter("([maintenance_work_order_id] IS NOT NULL)");
        entity.HasIndex(e => e.ShiftId, "staff_tasks_shift_id_idx").HasFilter("([shift_id] IS NOT NULL)");
        entity.HasIndex(e => e.TicketId, "staff_tasks_ticket_id_idx").HasFilter("([ticket_id] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.AssignedEmployeeId).HasColumnName("assigned_employee_id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.CreatedBy).HasColumnName("created_by");
        entity.Property(e => e.DueAt).HasColumnName("due_at");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.MaintenanceWorkOrderId).HasColumnName("maintenance_work_order_id");
        entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
        entity.Property(e => e.ShiftId).HasColumnName("shift_id");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("todo").HasColumnName("status");
        entity.Property(e => e.TaskType).HasMaxLength(255).HasColumnName("task_type");
        entity.Property(e => e.TicketId).HasColumnName("ticket_id");
        entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");

        entity.HasOne(d => d.AssignedEmployee).WithMany(p => p.StaffTasks)
            .HasForeignKey(d => d.AssignedEmployeeId)
            .HasConstraintName("FK__staff_tas__assig__30242045");

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.StaffTasks)
            .HasForeignKey(d => d.CreatedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__staff_tas__creat__37C5420D");

        entity.HasOne(d => d.Facility).WithMany(p => p.StaffTasks)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__staff_tas__facil__2E3BD7D3");

        entity.HasOne(d => d.MaintenanceWorkOrder).WithMany(p => p.StaffTasks)
            .HasForeignKey(d => d.MaintenanceWorkOrderId)
            .HasConstraintName("FK__staff_tas__maint__320C68B7");

        entity.HasOne(d => d.Shift).WithMany(p => p.StaffTasks)
            .HasForeignKey(d => d.ShiftId)
            .HasConstraintName("FK__staff_tas__shift__2F2FFC0C");

        entity.HasOne(d => d.Ticket).WithMany(p => p.StaffTasks)
            .HasForeignKey(d => d.TicketId)
            .HasConstraintName("FK__staff_tas__ticke__3118447E");
    }
}
