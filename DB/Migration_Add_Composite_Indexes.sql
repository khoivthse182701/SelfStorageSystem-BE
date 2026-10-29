SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================================
-- MIGRATION: ADD HIGH-FREQUENCY COMPOSITE INDEXES
-- Purpose: Accelerate invoice delinquency scans, payment matching, and staff access verification.
-- =========================================================================

-- 1. Index on [core].[invoices] (status, due_date)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[core].[invoices]') 
      AND name = N'ix_invoices_status_due_date'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix_invoices_status_due_date] 
    ON [core].[invoices]([status], [due_date]);
    PRINT 'Created index ix_invoices_status_due_date';
END
GO

-- 2. Index on [core].[invoices] (status, total_amount, paid_amount)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[core].[invoices]') 
      AND name = N'ix_invoices_status_amounts'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix_invoices_status_amounts] 
    ON [core].[invoices]([status], [total_amount], [paid_amount]);
    PRINT 'Created index ix_invoices_status_amounts';
END
GO

-- 3. Index on [core].[staff_facility_assignments] (employee_id, facility_id, ends_at)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[core].[staff_facility_assignments]') 
      AND name = N'ix_staff_facility_assignments_lookup'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix_staff_facility_assignments_lookup] 
    ON [core].[staff_facility_assignments]([employee_id], [facility_id], [ends_at]);
    PRINT 'Created index ix_staff_facility_assignments_lookup';
END
GO

-- 4. Index on [core].[payments] (customer_id, target_invoice_id, status)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[core].[payments]') 
      AND name = N'ix_payments_customer_invoice_status'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix_payments_customer_invoice_status] 
    ON [core].[payments]([customer_id], [target_invoice_id], [status]);
    PRINT 'Created index ix_payments_customer_invoice_status';
END
GO
