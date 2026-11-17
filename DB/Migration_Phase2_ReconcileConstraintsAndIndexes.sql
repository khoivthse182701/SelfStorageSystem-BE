/* ============================================================================
   Phase 2 Idempotent SQL Server Reconciliation Script
   Target: Existing SelfStoragePRN222 databases created prior to Phase 2.
   Safe to re-run multiple times against the currently selected database.
   ============================================================================ */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/* 1. Drop legacy HaTDT compatibility views */
DROP VIEW IF EXISTS [core].[UnitTypeHaTDT];
DROP VIEW IF EXISTS [core].[StorageUnitHaTDT];
GO

/* 2. Add missing columns for Cum 1 (Profile, RentalAgreements, UnitTransferRequests, MoveOutRequests) */
IF OBJECT_ID(N'[core].[customer_profiles]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[core].[customer_profiles]', N'profile_edit_month') IS NULL
        ALTER TABLE [core].[customer_profiles] ADD [profile_edit_month] char(7) NULL;

    IF COL_LENGTH(N'[core].[customer_profiles]', N'profile_edit_count') IS NULL
        ALTER TABLE [core].[customer_profiles]
            ADD [profile_edit_count] int NOT NULL
                CONSTRAINT [DF_customer_profiles_profile_edit_count] DEFAULT (0)
                CONSTRAINT [CK_customer_profiles_edit_count] CHECK ([profile_edit_count] >= 0 AND [profile_edit_count] <= 2);
END;
GO

IF OBJECT_ID(N'[core].[rental_agreements]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[core].[rental_agreements]', N'negative_debt_notified_at') IS NULL
        ALTER TABLE [core].[rental_agreements] ADD [negative_debt_notified_at] datetimeoffset(7) NULL;

    IF COL_LENGTH(N'[core].[rental_agreements]', N'overdue_bank_reminder_sent_at') IS NULL
        ALTER TABLE [core].[rental_agreements] ADD [overdue_bank_reminder_sent_at] datetimeoffset(7) NULL;
END;
GO

IF OBJECT_ID(N'[core].[unit_transfer_requests]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[core].[unit_transfer_requests]', N'cleaning_fee') IS NULL
        ALTER TABLE [core].[unit_transfer_requests]
            ADD [cleaning_fee] decimal(18, 2) NOT NULL
                CONSTRAINT [DF_unit_transfer_requests_cleaning_fee] DEFAULT (0);

    IF COL_LENGTH(N'[core].[unit_transfer_requests]', N'transfer_type') IS NULL
        ALTER TABLE [core].[unit_transfer_requests]
            ADD [transfer_type] nvarchar(32) NOT NULL
                CONSTRAINT [DF_unit_transfer_requests_transfer_type] DEFAULT (N'upgrade')
                CONSTRAINT [CK_unit_transfer_requests_type] CHECK ([transfer_type] IN (N'upgrade', N'downgrade', N'same_tier', N'relocation'));

    IF COL_LENGTH(N'[core].[unit_transfer_requests]', N'target_invoice_id') IS NULL
        ALTER TABLE [core].[unit_transfer_requests] ADD [target_invoice_id] bigint NULL;
END;
GO

IF OBJECT_ID(N'[core].[move_out_requests]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[core].[move_out_requests]', N'bank_name') IS NULL
        ALTER TABLE [core].[move_out_requests] ADD [bank_name] nvarchar(128) NULL;

    IF COL_LENGTH(N'[core].[move_out_requests]', N'bank_account_no') IS NULL
        ALTER TABLE [core].[move_out_requests] ADD [bank_account_no] nvarchar(64) NULL;

    IF COL_LENGTH(N'[core].[move_out_requests]', N'bank_account_holder') IS NULL
        ALTER TABLE [core].[move_out_requests] ADD [bank_account_holder] nvarchar(128) NULL;

    IF COL_LENGTH(N'[core].[move_out_requests]', N'bank_submitted_at') IS NULL
        ALTER TABLE [core].[move_out_requests] ADD [bank_submitted_at] datetimeoffset(7) NULL;

    IF COL_LENGTH(N'[core].[move_out_requests]', N'damage_deduction_amount') IS NULL
        ALTER TABLE [core].[move_out_requests]
            ADD [damage_deduction_amount] numeric(14, 2) NOT NULL
                CONSTRAINT [DF_move_out_requests_damage_deduction] DEFAULT (0);

    IF COL_LENGTH(N'[core].[move_out_requests]', N'overdue_deduction_amount') IS NULL
        ALTER TABLE [core].[move_out_requests]
            ADD [overdue_deduction_amount] numeric(14, 2) NOT NULL
                CONSTRAINT [DF_move_out_requests_overdue_deduction] DEFAULT (0);

    IF COL_LENGTH(N'[core].[move_out_requests]', N'net_refund_amount') IS NULL
        ALTER TABLE [core].[move_out_requests] ADD [net_refund_amount] numeric(14, 2) NULL;
END;
GO

/* 3. Normalize legacy physical_status ('under_maintenance' -> 'maintenance') before constraint hardening */
IF OBJECT_ID(N'[core].[storage_units]', N'U') IS NOT NULL
BEGIN
    UPDATE [core].[storage_units]
    SET [physical_status] = N'maintenance',
        [updated_at] = SYSUTCDATETIME()
    WHERE [physical_status] = N'under_maintenance';
END;
GO

/* 4. Reconcile single-column & cross-column CHECK constraints idempotently */
DECLARE @sql nvarchar(max) = N'';

;WITH target_column_checks AS (
    SELECT N'[core].[facilities]' AS tbl, N'status' AS col
    UNION ALL SELECT N'[core].[storage_units]', N'physical_status'
    UNION ALL SELECT N'[core].[fee_rules]', N'fee_type'
    UNION ALL SELECT N'[core].[promotion_rules]', N'rule_type'
    UNION ALL SELECT N'[core].[rental_agreements]', N'status'
    UNION ALL SELECT N'[core].[support_tickets]', N'status'
    UNION ALL SELECT N'[core].[invoice_lines]', N'line_type'
    UNION ALL SELECT N'[core].[payments]', N'method'
    UNION ALL SELECT N'[core].[refunds]', N'status'
)
SELECT @sql += N'ALTER TABLE ' + t.tbl + N' DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
FROM target_column_checks t
JOIN sys.check_constraints cc
    ON cc.parent_object_id = OBJECT_ID(t.tbl)
   AND cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(t.tbl), t.col, N'ColumnId');

SELECT @sql += N'ALTER TABLE [core].[refunds] DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'[core].[refunds]')
  AND cc.definition LIKE N'%refunded_at%'
  AND cc.definition LIKE N'%succeeded%';

SELECT @sql += N'ALTER TABLE [core].[rental_agreements] DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';' + CHAR(10)
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'[core].[rental_agreements]')
  AND cc.definition LIKE N'%deposit_balance%';

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;
GO

ALTER TABLE [core].[facilities] ADD CONSTRAINT [CK_facilities_status]
    CHECK ([status] IN ('draft', 'active', 'inactive', 'maintenance', 'suspended_new_bookings', 'temporarily_closed', 'closed'));

ALTER TABLE [core].[storage_units] ADD CONSTRAINT [CK_storage_units_physical_status]
    CHECK ([physical_status] IN ('available', 'reserved', 'occupied', 'pending_inspection', 'cleaning', 'maintenance', 'damaged', 'out_of_service'));

ALTER TABLE [core].[fee_rules] ADD CONSTRAINT [CK_fee_rules_fee_type]
    CHECK ([fee_type] IN ('late_fee', 'damage_fee', 'lost_key_fee', 'maintenance_fee', 'booking', 'booking_fee', 'tax', 'vat_tax', 'other'));

ALTER TABLE [core].[promotion_rules] ADD CONSTRAINT [CK_promotion_rules_rule_type]
    CHECK ([rule_type] IN ('minimum_months', 'month', 'minimum_amount', 'facility', 'unit_type', 'new_customer'));

ALTER TABLE [core].[rental_agreements] ADD CONSTRAINT [CK_rental_agreements_status]
    CHECK ([status] IN ('draft', 'scheduled', 'active', 'expiring_soon', 'extended', 'overdue', 'defaulted', 'move_out_scheduled', 'checkout_pending', 'expired', 'terminated', 'completed', 'closed', 'cancelled'));

ALTER TABLE [core].[support_tickets] ADD CONSTRAINT [CK_support_tickets_status]
    CHECK ([status] IN ('open', 'pending_manager_review', 'assigned', 'in_progress', 'waiting_for_customer', 'waiting_for_maintenance', 'severe_need_relocation', 'assessed', 'resolved', 'closed', 'cancelled'));

ALTER TABLE [core].[invoice_lines] ADD CONSTRAINT [CK_invoice_lines_line_type]
    CHECK ([line_type] IN ('deposit', 'rent', 'renewal', 'late_fee', 'damage', 'maintenance', 'booking_fee', 'discount', 'penalty', 'service', 'fee', 'tax', 'other'));

ALTER TABLE [core].[payments] ADD CONSTRAINT [CK_payments_method]
    CHECK ([method] IN ('cash', 'bank_transfer', 'credit_card', 'vnpay', 'payos', 'stripe', 'other'));

ALTER TABLE [core].[refunds] ADD CONSTRAINT [CK_refunds_status]
    CHECK ([status] IN ('requested', 'pending', 'approved', 'processing', 'succeeded', 'completed', 'failed', 'rejected', 'pending_manager_payout', 'pending_counter_payout'));

ALTER TABLE [core].[refunds] ADD CONSTRAINT [CK_refunds_refunded_at]
    CHECK (([status] IN ('succeeded', 'completed') AND [refunded_at] IS NOT NULL) OR [status] NOT IN ('succeeded', 'completed'));
GO

/* 5. Reconcile composite & availability indexes */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[staff_facility_assignments]') AND name = N'ix_staff_facility_assignments_lookup')
    CREATE NONCLUSTERED INDEX [ix_staff_facility_assignments_lookup]
        ON [core].[staff_facility_assignments] ([employee_id], [facility_id], [ends_at]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[unit_allocations]') AND name = N'unit_allocations_availability_idx')
    CREATE NONCLUSTERED INDEX [unit_allocations_availability_idx]
        ON [core].[unit_allocations] ([storage_unit_id], [status], [allocation_start_date], [allocation_end_date])
        WHERE [status] = N'active';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[invoices]') AND name = N'ix_invoices_status_due_date')
    CREATE NONCLUSTERED INDEX [ix_invoices_status_due_date]
        ON [core].[invoices] ([status], [due_date]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[invoices]') AND name = N'ix_invoices_status_amounts')
    CREATE NONCLUSTERED INDEX [ix_invoices_status_amounts]
        ON [core].[invoices] ([status], [total_amount], [paid_amount]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[core].[payments]') AND name = N'ix_payments_customer_invoice_status')
    CREATE NONCLUSTERED INDEX [ix_payments_customer_invoice_status]
        ON [core].[payments] ([customer_id], [target_invoice_id], [status]);
GO

/* 6. Update SQL triggers to match Phase 2 UnitStatus ('damaged') & ContractStatus ('expiring_soon') */
IF OBJECT_ID(N'[core].[unit_allocations]', N'U') IS NOT NULL
BEGIN
    EXEC(N'
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
            WHERE i.status = ''active''
              AND (
                (i.allocation_kind = ''reservation_hold'' AND (su.is_listed = 0 OR su.physical_status IN (''occupied'',''maintenance'',''damaged'',''out_of_service'',''pending_inspection'')))
                OR (i.allocation_kind = ''rental'' AND su.physical_status IN (''maintenance'',''damaged'',''out_of_service'',''pending_inspection''))
              )
        )
        BEGIN
            THROW 51025, ''Storage unit is not bookable.'', 1;
        END;

        IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN [core].[storage_units] su ON su.id = i.storage_unit_id
            JOIN [core].[reservations] r ON r.id = i.reservation_id
            WHERE i.status = ''active'' AND i.reservation_id IS NOT NULL
              AND (su.facility_id <> r.facility_id
                   OR su.unit_type_id <> r.unit_type_id
                   OR i.allocation_start_date <> r.start_date
                   OR i.allocation_end_date <> r.end_date)
        )
        BEGIN
            THROW 51026, ''Reservation allocation must match facility, unit type and rental period.'', 1;
        END;

        IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN [core].[storage_units] su ON su.id = i.storage_unit_id
            JOIN [core].[rental_agreements] a ON a.id = i.agreement_id
            WHERE i.status = ''active'' AND i.agreement_id IS NOT NULL
              AND (su.facility_id <> a.facility_id
                   OR i.allocation_start_date < a.start_date
                   OR i.allocation_end_date > a.end_date)
        )
        BEGIN
            THROW 51027, ''Agreement allocation must match facility, allowed unit type and agreement period.'', 1;
        END
    END;');
END;
GO

IF OBJECT_ID(N'[core].[storage_units]', N'U') IS NOT NULL
BEGIN
    EXEC(N'
    CREATE OR ALTER TRIGGER [core].[trg_storage_unit_status_transition]
    ON [core].[storage_units]
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        IF UPDATE(physical_status) AND EXISTS (
            SELECT 1 FROM inserted i JOIN deleted d ON d.id = i.id
            WHERE i.physical_status <> d.physical_status
              AND NOT (
                  (d.physical_status = ''available'' AND i.physical_status IN (''reserved'',''occupied'',''maintenance'',''cleaning'',''damaged'',''pending_inspection'',''out_of_service'')) OR
                  (d.physical_status = ''reserved'' AND i.physical_status IN (''available'',''occupied'',''maintenance'',''cleaning'',''damaged'',''out_of_service'')) OR
                  (d.physical_status = ''occupied'' AND i.physical_status IN (''pending_inspection'',''cleaning'',''maintenance'',''damaged'',''available'',''out_of_service'')) OR
                  (d.physical_status IN (''pending_inspection'',''cleaning'') AND i.physical_status IN (''available'',''maintenance'',''cleaning'',''damaged'',''pending_inspection'',''out_of_service'',''occupied'')) OR
                  (d.physical_status = ''maintenance'' AND i.physical_status IN (''available'',''cleaning'',''pending_inspection'',''out_of_service'',''maintenance'',''damaged'',''occupied'')) OR
                  (d.physical_status = ''damaged'' AND i.physical_status IN (''maintenance'',''cleaning'',''pending_inspection'',''out_of_service'',''available'')) OR
                  (d.physical_status = ''out_of_service'' AND i.physical_status IN (''maintenance'',''damaged'',''available'',''cleaning''))
              )
        )
        BEGIN
            THROW 51050, ''Invalid storage-unit status transition.'', 1;
        END;

        INSERT INTO [core].[unit_status_history](storage_unit_id, old_status, new_status, reason, changed_by)
        SELECT i.id, d.physical_status, i.physical_status, N''status_transition'', NULL
        FROM inserted i JOIN deleted d ON d.id = i.id
        WHERE i.physical_status <> d.physical_status;
    END;');
END;
GO

IF OBJECT_ID(N'[core].[rental_agreements]', N'U') IS NOT NULL
BEGIN
    EXEC(N'
    CREATE OR ALTER TRIGGER [core].[trg_agreement_status_transition]
    ON [core].[rental_agreements]
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        IF UPDATE(status) AND EXISTS (
            SELECT 1 FROM inserted i JOIN deleted d ON d.id = i.id
            WHERE i.status <> d.status
              AND NOT (
                  (d.status = ''draft'' AND i.status IN (''scheduled'',''active'',''cancelled'')) OR
                  (d.status = ''scheduled'' AND i.status IN (''active'',''cancelled'')) OR
                  (d.status = ''active'' AND i.status IN (''expiring_soon'',''extended'',''overdue'',''expired'',''terminated'',''move_out_scheduled'',''checkout_pending'',''cancelled'')) OR
                  (d.status = ''expiring_soon'' AND i.status IN (''active'',''extended'',''overdue'',''expired'',''terminated'',''move_out_scheduled'',''checkout_pending'')) OR
                  (d.status = ''extended'' AND i.status IN (''active'',''expiring_soon'',''overdue'',''expired'',''terminated'',''move_out_scheduled'',''checkout_pending'')) OR
                  (d.status = ''overdue'' AND i.status IN (''active'',''defaulted'',''terminated'',''move_out_scheduled'')) OR
                  (d.status = ''defaulted'' AND i.status IN (''terminated'',''completed'',''closed'')) OR
                  (d.status = ''move_out_scheduled'' AND i.status IN (''checkout_pending'',''active'',''completed'')) OR
                  (d.status = ''checkout_pending'' AND i.status IN (''active'',''completed'',''closed'')) OR
                  (d.status = ''expired'' AND i.status IN (''extended'',''completed'',''closed'')) OR
                  (d.status = ''terminated'' AND i.status IN (''completed'',''closed'')) OR
                  (d.status = ''completed'' AND i.status = ''closed'')
              )
        )
        BEGIN
            THROW 51052, ''Invalid rental-agreement status transition.'', 1;
        END
    END;');
END;
GO

/* 7. Update stored procedures to include expiring_soon agreements alongside active/extended */
IF OBJECT_ID(N'[core].[sp_generate_renewal_invoices_and_reminders]', N'P') IS NOT NULL
BEGIN
    EXEC(N'
    CREATE OR ALTER PROCEDURE [core].[sp_generate_renewal_invoices_and_reminders]
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRAN;

        DECLARE @today date = CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE ''SE Asia Standard Time'');
        DECLARE @newInvoices TABLE(id bigint PRIMARY KEY, agreement_id bigint NOT NULL);

        INSERT INTO [core].[invoices](
            invoice_no, customer_id, agreement_id, billing_period, issue_date, due_date,
            currency, status, opened_at
        )
        OUTPUT inserted.id, inserted.agreement_id INTO @newInvoices(id, agreement_id)
        SELECT
            CONCAT(''INV-'', CONVERT(char(8), @today, 112), ''-'',
                   RIGHT(REPLICATE(''0'', 7) + CONVERT(varchar(20), NEXT VALUE FOR [core].[invoice_no_seq]), 7)),
            a.customer_id, a.id,
            CONCAT(CONVERT(char(10), a.end_date, 23), ''/'', CONVERT(char(10), DATEADD(month, 1, a.end_date), 23)),
            @today, a.end_date, ''VND'', ''draft'', NULL
        FROM [core].[rental_agreements] a
        WHERE a.status IN (''active'', ''expiring_soon'', ''extended'')
          AND DATEDIFF(day, @today, a.end_date) BETWEEN 0 AND 7
          AND NOT EXISTS (
              SELECT 1 FROM [core].[invoices] i
              WHERE i.agreement_id = a.id
                AND i.billing_period = CONCAT(CONVERT(char(10), a.end_date, 23), ''/'', CONVERT(char(10), DATEADD(month, 1, a.end_date), 23))
                AND i.status <> ''voided''
          );

        INSERT INTO [core].[invoice_lines](invoice_id, line_type, description, quantity, unit_price, metadata)
        SELECT n.id, ''renewal'', N''One-month storage renewal'', 1, a.monthly_rate_snapshot,
               CONCAT(N''{"agreement_id":'', a.id, N'',"period_start":"'', CONVERT(nvarchar(10), a.end_date, 23),
                      N''","period_end":"'', CONVERT(nvarchar(10), DATEADD(month, 1, a.end_date), 23), N''"}'')
        FROM @newInvoices n
        JOIN [core].[rental_agreements] a ON a.id = n.agreement_id;

        UPDATE i SET status = ''open'', opened_at = SYSUTCDATETIME(), updated_at = SYSUTCDATETIME()
        FROM [core].[invoices] i JOIN @newInvoices n ON n.id = i.id;

        INSERT INTO [core].[notifications](user_id, channel, template_code, payload, status, scheduled_at, deduplication_key)
        SELECT a.customer_id, ''in_app'', ''renewal_payment_reminder'',
               CONCAT(N''{"agreement_id":'', a.id, N'',"days_remaining":'', x.days_remaining,
                      N'',"end_date":"'', CONVERT(nvarchar(10), a.end_date, 23), N''"}''),
               ''pending'', SYSUTCDATETIME(),
               CONCAT(''renewal-reminder:'', a.id, '':'', x.days_remaining)
        FROM [core].[rental_agreements] a
        CROSS APPLY (SELECT DATEDIFF(day, @today, a.end_date) days_remaining) x
        WHERE a.status IN (''active'', ''expiring_soon'', ''extended'')
          AND x.days_remaining IN (7, 3, 1)
          AND NOT EXISTS (
              SELECT 1 FROM [core].[notifications] n
              WHERE n.deduplication_key = CONCAT(''renewal-reminder:'', a.id, '':'', x.days_remaining)
          );

        COMMIT;
        SELECT COUNT(*) AS invoices_created FROM @newInvoices;
    END;');
END;
GO

IF OBJECT_ID(N'[core].[sp_process_overdue_accounts]', N'P') IS NOT NULL
BEGIN
    EXEC(N'
    CREATE OR ALTER PROCEDURE [core].[sp_process_overdue_accounts]
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRAN;

        DECLARE @today date = CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE ''SE Asia Standard Time'');

        INSERT INTO [core].[invoice_lines](invoice_id, line_type, description, quantity, unit_price, metadata)
        SELECT i.id, ''late_fee'', N''Late fee '' + CONVERT(nvarchar(10), @today, 23), 1,
               ROUND(a.monthly_rate_snapshot / 30.0 * 1.5, 2),
               N''{"rule":"BR-REN-02","rate":1.5,"base":"daily_rent"}''
        FROM [core].[invoices] i
        JOIN [core].[rental_agreements] a ON a.id = i.agreement_id
        WHERE i.status IN (''open'', ''partially_paid'', ''overdue'')
          AND i.due_date < @today AND i.paid_amount < i.total_amount
          AND NOT EXISTS (
              SELECT 1 FROM [core].[invoice_lines] il
              WHERE il.invoice_id = i.id AND il.line_type = ''late_fee''
                AND il.description = N''Late fee '' + CONVERT(nvarchar(10), @today, 23)
          );

        UPDATE [core].[invoices]
           SET status = ''overdue'', updated_at = SYSUTCDATETIME()
        WHERE status IN (''open'', ''partially_paid'')
          AND due_date < @today AND paid_amount < total_amount;

        INSERT INTO [core].[delinquency_cases](agreement_id, invoice_id, status, opened_at, grace_ends_at, outstanding_snapshot, notes)
        SELECT i.agreement_id, i.id, ''delinquent'', SYSUTCDATETIME(), SYSUTCDATETIME(), i.total_amount - i.paid_amount,
               N''BR-REN-02/03: overdue from N+1''
        FROM [core].[invoices] i
        WHERE i.status = ''overdue'' AND i.agreement_id IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM [core].[delinquency_cases] d WHERE d.invoice_id = i.id AND d.resolved_at IS NULL);

        UPDATE a SET status = ''overdue'', updated_at = SYSUTCDATETIME()
        FROM [core].[rental_agreements] a
        WHERE a.status IN (''active'', ''expiring_soon'', ''extended'')
          AND EXISTS (SELECT 1 FROM [core].[invoices] i WHERE i.agreement_id = a.id AND i.status = ''overdue'' AND i.paid_amount < i.total_amount);

        UPDATE c SET status = ''suspended''
        FROM [core].[access_credentials] c
        JOIN [core].[rental_agreements] a ON a.id = c.agreement_id
        WHERE a.status = ''overdue'' AND c.status = ''active'';

        UPDATE a SET status = ''defaulted'', updated_at = SYSUTCDATETIME()
        FROM [core].[rental_agreements] a
        WHERE a.status = ''overdue''
          AND EXISTS (
              SELECT 1 FROM [core].[invoices] i
              WHERE i.agreement_id = a.id AND i.status = ''overdue''
                AND DATEDIFF(day, i.due_date, @today) > 30 AND i.paid_amount < i.total_amount
          );

        COMMIT;
    END;');
END;
GO

PRINT N'Migration_Phase2_ReconcileConstraintsAndIndexes completed successfully.';
GO
