SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Status constraint
DECLARE @transferStatusConstraint sysname;
SELECT @transferStatusConstraint = kc.name
FROM sys.check_constraints kc
INNER JOIN sys.columns c ON c.object_id = kc.parent_object_id AND c.column_id = kc.parent_column_id
WHERE kc.parent_object_id = OBJECT_ID(N'[core].[unit_transfer_requests]')
  AND c.name = N'status';

IF @transferStatusConstraint IS NOT NULL
    EXEC(N'ALTER TABLE [core].[unit_transfer_requests] DROP CONSTRAINT [' + @transferStatusConstraint + N']');
GO

ALTER TABLE [core].[unit_transfer_requests]
    ADD CONSTRAINT CK_unit_transfer_requests_status
    CHECK (status IN ('pending_payment', 'paid', 'moved_out_in_transit', 'cancelled_overdue_move_out', 'pending', 'approved', 'scheduled', 'completed', 'rejected', 'cancelled'));
GO

-- 2. Open unique index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'unit_transfer_requests_one_open_uidx')
    DROP INDEX unit_transfer_requests_one_open_uidx ON [core].[unit_transfer_requests];
GO

CREATE UNIQUE INDEX unit_transfer_requests_one_open_uidx ON [core].[unit_transfer_requests] (agreement_id)
    WHERE status IN ('pending_payment', 'paid', 'moved_out_in_transit', 'pending', 'approved', 'scheduled');
GO

-- 3. Requested facility if null or missing FK
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'requested_facility_id')
BEGIN
    ALTER TABLE [core].unit_transfer_requests ADD requested_facility_id bigint NULL;
END
GO

UPDATE transfer
SET requested_facility_id = unit.facility_id
FROM [core].unit_transfer_requests AS transfer
INNER JOIN [core].storage_units AS unit ON unit.id = transfer.from_unit_id
WHERE transfer.requested_facility_id IS NULL;
GO

ALTER TABLE [core].unit_transfer_requests ALTER COLUMN requested_facility_id bigint NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_unit_transfer_requests_requested_facility')
BEGIN
    ALTER TABLE [core].unit_transfer_requests
    ADD CONSTRAINT FK_unit_transfer_requests_requested_facility
    FOREIGN KEY (requested_facility_id) REFERENCES [core].facilities(id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'unit_transfer_requests_requested_facility_id_idx')
BEGIN
    CREATE INDEX unit_transfer_requests_requested_facility_id_idx ON [core].unit_transfer_requests (requested_facility_id);
END
GO

-- 4. Add missing transfer workflow columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'move_out_date')
    ALTER TABLE [core].unit_transfer_requests ADD move_out_date date NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'move_in_date')
    ALTER TABLE [core].unit_transfer_requests ADD move_in_date date NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'reschedule_count')
    ALTER TABLE [core].unit_transfer_requests ADD reschedule_count int NOT NULL CONSTRAINT DF_unit_transfer_reschedule_count DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'diff_rent_amount')
    ALTER TABLE [core].unit_transfer_requests ADD diff_rent_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_diff_rent DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'diff_deposit_amount')
    ALTER TABLE [core].unit_transfer_requests ADD diff_deposit_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_diff_deposit DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'penalty_amount')
    ALTER TABLE [core].unit_transfer_requests ADD penalty_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_penalty DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'credit_amount')
    ALTER TABLE [core].unit_transfer_requests ADD credit_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_credit DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'total_paid_amount')
    ALTER TABLE [core].unit_transfer_requests ADD total_paid_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_total_paid DEFAULT 0;
GO

UPDATE [core].unit_transfer_requests
SET move_out_date = ISNULL(move_out_date, requested_effective_date),
    move_in_date = ISNULL(move_in_date, requested_effective_date);
GO

ALTER TABLE [core].unit_transfer_requests ALTER COLUMN move_out_date date NOT NULL;
GO
ALTER TABLE [core].unit_transfer_requests ALTER COLUMN move_in_date date NOT NULL;
GO

-- 5. Allow cross-facility and cross-unit-type transfers in triggers
CREATE OR ALTER TRIGGER [core].[trg_rental_agreements_validate_reservation]
ON [core].[rental_agreements]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM deleted) AND EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN [core].[reservations] r ON r.id = i.reservation_id
        WHERE r.id IS NULL
           OR r.customer_id <> i.customer_id
           OR r.facility_id <> i.facility_id
           OR r.start_date <> i.start_date
           OR r.monthly_rate_snapshot <> i.monthly_rate_snapshot
           OR r.deposit_snapshot <> i.deposit_snapshot
    )
    BEGIN
        THROW 51024, 'Agreement identity and price snapshots must match its reservation.', 1;
    END
END;
GO

CREATE OR ALTER TRIGGER [core].[trg_unit_allocations_validate_scope]
ON [core].[unit_allocations]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN [core].[storage_units] su ON su.id = i.storage_unit_id
        WHERE i.status = 'active'
          AND (
            (i.allocation_kind = 'reservation_hold' AND (su.is_listed = 0 OR su.physical_status IN ('occupied','maintenance','under_maintenance','out_of_service','pending_inspection')))
            OR (i.allocation_kind = 'rental' AND su.physical_status IN ('maintenance','under_maintenance','out_of_service','pending_inspection'))
          )
    )
    BEGIN
        THROW 51025, 'Storage unit is not bookable.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN [core].[storage_units] su ON su.id = i.storage_unit_id
        JOIN [core].[reservations] r ON r.id = i.reservation_id
        WHERE i.status = 'active' AND i.reservation_id IS NOT NULL
          AND (su.facility_id <> r.facility_id
               OR su.unit_type_id <> r.unit_type_id
               OR i.allocation_start_date <> r.start_date
               OR i.allocation_end_date <> r.end_date)
    )
    BEGIN
        THROW 51026, 'Reservation allocation must match facility, unit type and rental period.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN [core].[storage_units] su ON su.id = i.storage_unit_id
        JOIN [core].[rental_agreements] a ON a.id = i.agreement_id
        WHERE i.status = 'active' AND i.agreement_id IS NOT NULL
          AND (su.facility_id <> a.facility_id
               OR i.allocation_start_date < a.start_date
               OR i.allocation_end_date > a.end_date)
    )
    BEGIN
        THROW 51027, 'Agreement allocation must match facility, allowed unit type and agreement period.', 1;
    END
END;
GO
