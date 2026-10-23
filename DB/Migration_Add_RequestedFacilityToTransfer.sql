-- Adds the destination facility selected by a customer to unit transfer requests.
ALTER TABLE [core].unit_transfer_requests
ADD requested_facility_id bigint NULL;
GO

-- Keep the database constraint aligned with the transfer workflow states used by the API.
DECLARE @transferStatusConstraint sysname;
SELECT @transferStatusConstraint = kc.name
FROM sys.check_constraints kc
INNER JOIN sys.columns c ON c.object_id = kc.parent_object_id AND c.column_id = kc.parent_column_id
WHERE kc.parent_object_id = OBJECT_ID(N'[core].[unit_transfer_requests]')
  AND c.name = N'status';

IF @transferStatusConstraint IS NOT NULL
    EXEC(N'ALTER TABLE [core].[unit_transfer_requests] DROP CONSTRAINT [' + @transferStatusConstraint + N']');

ALTER TABLE [core].[unit_transfer_requests]
    ADD CONSTRAINT CK_unit_transfer_requests_status
    CHECK (status IN ('pending_payment', 'paid', 'moved_out_in_transit', 'cancelled_overdue_move_out', 'pending', 'approved', 'scheduled', 'completed', 'rejected', 'cancelled'));
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[unit_transfer_requests]') AND name = N'unit_transfer_requests_one_open_uidx')
    DROP INDEX unit_transfer_requests_one_open_uidx ON [core].[unit_transfer_requests];
GO
CREATE UNIQUE INDEX unit_transfer_requests_one_open_uidx ON [core].[unit_transfer_requests] (agreement_id)
    WHERE status IN ('pending_payment', 'paid', 'moved_out_in_transit', 'pending', 'approved', 'scheduled');
GO

UPDATE transfer
SET requested_facility_id = unit.facility_id
FROM [core].unit_transfer_requests AS transfer
INNER JOIN [core].storage_units AS unit ON unit.id = transfer.from_unit_id;
GO

ALTER TABLE [core].unit_transfer_requests
ALTER COLUMN requested_facility_id bigint NOT NULL;
GO

ALTER TABLE [core].unit_transfer_requests
ADD CONSTRAINT FK_unit_transfer_requests_requested_facility
FOREIGN KEY (requested_facility_id) REFERENCES [core].facilities(id);
GO

CREATE INDEX unit_transfer_requests_requested_facility_id_idx
ON [core].unit_transfer_requests (requested_facility_id);
GO

ALTER TABLE [core].unit_transfer_requests ADD
    move_out_date date NULL,
    move_in_date date NULL,
    reschedule_count int NOT NULL CONSTRAINT DF_unit_transfer_reschedule_count DEFAULT 0,
    diff_rent_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_diff_rent DEFAULT 0,
    diff_deposit_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_diff_deposit DEFAULT 0,
    penalty_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_penalty DEFAULT 0,
    credit_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_credit DEFAULT 0,
    total_paid_amount decimal(18,2) NOT NULL CONSTRAINT DF_unit_transfer_total_paid DEFAULT 0;
GO

UPDATE [core].unit_transfer_requests
SET move_out_date = requested_effective_date,
    move_in_date = requested_effective_date;
GO

ALTER TABLE [core].unit_transfer_requests ALTER COLUMN move_out_date date NOT NULL;
ALTER TABLE [core].unit_transfer_requests ALTER COLUMN move_in_date date NOT NULL;
GO
