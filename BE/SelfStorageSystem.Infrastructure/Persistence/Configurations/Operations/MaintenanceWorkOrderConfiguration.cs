using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Enums;

namespace SelfStorageSystem.Infrastructure.Persistence.Configurations.Operations;

public sealed class MaintenanceWorkOrderConfiguration : IEntityTypeConfiguration<MaintenanceWorkOrder>
{
    public void Configure(EntityTypeBuilder<MaintenanceWorkOrder> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK__maintena__3213E83F73AEF50C");
        entity.ToTable("maintenance_work_orders", "core", tb =>
        {
            tb.HasTrigger("trg_maintenance_work_orders_validate_scope");
            tb.HasCheckConstraint("CK_maintenance_work_orders_priority", EnumDbMapper.ToSqlCheckInClause<TicketPriority>("priority"));
            tb.HasCheckConstraint("CK_maintenance_work_orders_status", EnumDbMapper.ToSqlCheckInClause<MaintenanceWorkOrderStatus>("status"));
            tb.HasCheckConstraint("CK_maintenance_work_orders_costs", "([estimated_cost] IS NULL OR [estimated_cost] >= 0) AND ([actual_cost] IS NULL OR [actual_cost] >= 0)");
            tb.HasCheckConstraint("CK_maintenance_work_orders_times", "[completed_at] IS NULL OR ([started_at] IS NOT NULL AND [completed_at] >= [started_at])");
            tb.HasCheckConstraint("CK_maintenance_work_orders_completed", "([status] = 'completed' AND [completed_at] IS NOT NULL) OR [status] <> 'completed'");
        });

        entity.HasIndex(e => e.WorkOrderNo, "UQ__maintena__58954FA67EC34FED").IsUnique();
        entity.HasIndex(e => e.AssignedEmployeeId, "maintenance_work_orders_assigned_employee_id_idx").HasFilter("([assigned_employee_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.FacilityId, e.Status, e.Priority, e.CreatedAt }, "maintenance_work_orders_facility_queue_idx");
        entity.HasIndex(e => e.OpenedBy, "maintenance_work_orders_opened_by_idx");
        entity.HasIndex(e => e.SourceInspectionId, "maintenance_work_orders_source_inspection_id_idx").HasFilter("([source_inspection_id] IS NOT NULL)");
        entity.HasIndex(e => e.SourceTicketId, "maintenance_work_orders_source_ticket_id_idx").HasFilter("([source_ticket_id] IS NOT NULL)");
        entity.HasIndex(e => new { e.StorageUnitId, e.Status }, "maintenance_work_orders_storage_unit_id_idx").HasFilter("([storage_unit_id] IS NOT NULL)");
        entity.HasIndex(e => e.VerifiedBy, "maintenance_work_orders_verified_by_idx").HasFilter("([verified_by] IS NOT NULL)");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.ActualCost).HasColumnType("numeric(14, 2)").HasColumnName("actual_cost");
        entity.Property(e => e.AssignedEmployeeId).HasColumnName("assigned_employee_id");
        entity.Property(e => e.BlocksBooking).HasDefaultValue(true).HasColumnName("blocks_booking");
        entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        entity.Property(e => e.EstimatedCost).HasColumnType("numeric(14, 2)").HasColumnName("estimated_cost");
        entity.Property(e => e.FacilityId).HasColumnName("facility_id");
        entity.Property(e => e.OpenedBy).HasColumnName("opened_by");
        entity.Property(e => e.Priority).HasMaxLength(255).HasDefaultValue("normal").HasColumnName("priority");
        entity.Property(e => e.SourceInspectionId).HasColumnName("source_inspection_id");
        entity.Property(e => e.SourceTicketId).HasColumnName("source_ticket_id");
        entity.Property(e => e.StartedAt).HasColumnName("started_at");
        entity.Property(e => e.Status).HasMaxLength(255).HasDefaultValue("open").HasColumnName("status");
        entity.Property(e => e.StorageUnitId).HasColumnName("storage_unit_id");
        entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("updated_at");
        entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");
        entity.Property(e => e.WorkOrderNo).HasMaxLength(255).HasColumnName("work_order_no");

        entity.HasOne(d => d.AssignedEmployee).WithMany(p => p.MaintenanceWorkOrderAssignedEmployees)
            .HasForeignKey(d => d.AssignedEmployeeId)
            .HasConstraintName("FK__maintenan__assig__1E05700A");

        entity.HasOne(d => d.Facility).WithMany(p => p.MaintenanceWorkOrders)
            .HasForeignKey(d => d.FacilityId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__maintenan__facil__1A34DF26");

        entity.HasOne(d => d.OpenedByNavigation).WithMany(p => p.MaintenanceWorkOrders)
            .HasForeignKey(d => d.OpenedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK__maintenan__opene__25A691D2");

        entity.HasOne(d => d.SourceInspection).WithMany(p => p.MaintenanceWorkOrders)
            .HasForeignKey(d => d.SourceInspectionId)
            .HasConstraintName("FK__maintenan__sourc__1D114BD1");

        entity.HasOne(d => d.SourceTicket).WithMany(p => p.MaintenanceWorkOrders)
            .HasForeignKey(d => d.SourceTicketId)
            .HasConstraintName("FK__maintenan__sourc__1C1D2798");

        entity.HasOne(d => d.StorageUnit).WithMany(p => p.MaintenanceWorkOrders)
            .HasForeignKey(d => d.StorageUnitId)
            .HasConstraintName("FK__maintenan__stora__1B29035F");

        entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.MaintenanceWorkOrderVerifiedByNavigations)
            .HasForeignKey(d => d.VerifiedBy)
            .HasConstraintName("FK__maintenan__verif__269AB60B");
    }
}
