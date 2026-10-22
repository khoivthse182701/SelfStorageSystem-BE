/*
  Demo/UAT data for the self-storage booking flow.

  Prerequisites:
    1. Run SelfStoragePRN222_SQLServer_CleanInstall.sql.
    2. Run Seed_20_Units_CheapRates.sql (the script uses facility 1, user 7,
       rates 1/2 and units A-101 to A-108).

  The script is intentionally idempotent.  It never deletes a customer's
  existing business data; it only creates its own DEMO-UAT-* records once.
*/
USE [SelfStoragePRN222];
GO

SET XACT_ABORT ON;
GO

IF EXISTS (SELECT 1 FROM [core].[reservations] WHERE reservation_code = N'DEMO-UAT-PENDING')
BEGIN
    PRINT N'Demo/UAT seed already exists. No changes were made.';
    RETURN;
END;
GO

BEGIN TRANSACTION;

DECLARE @CustomerId BIGINT = 7;
DECLARE @FacilityId BIGINT = 1;
DECLARE @PolicyVersionId BIGINT = 1;
DECLARE @Today DATE = CONVERT(date, SYSUTCDATETIME());
DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE user_id = @CustomerId)
    THROW 51000, 'Missing demo customer (user 7). Run the clean-install seed first.', 1;
IF NOT EXISTS (SELECT 1 FROM [core].[facilities] WHERE id = @FacilityId AND status = 'active')
    THROW 51001, 'Missing active facility 1. Run the clean-install seed first.', 1;
IF NOT EXISTS (SELECT 1 FROM [core].[policy_versions] WHERE id = @PolicyVersionId)
    THROW 51002, 'Missing policy version 1. Run the clean-install seed first.', 1;
IF NOT EXISTS (SELECT 1 FROM [core].[facility_rates] WHERE id IN (1, 2))
    THROW 51003, 'Missing facility rates 1/2. Run Seed_20_Units_CheapRates.sql first.', 1;
IF NOT EXISTS (SELECT 1 FROM [core].[storage_units] WHERE id IN (1, 2, 3, 4, 6, 8))
    THROW 51004, 'Missing demo units. Run Seed_20_Units_CheapRates.sql first.', 1;

-- Unit states used to demonstrate the catalogue and operational exceptions.
ALTER TABLE [core].[storage_units] DISABLE TRIGGER [trg_storage_unit_status_transition];
UPDATE [core].[storage_units]
SET physical_status = CASE id
    WHEN 1 THEN 'available'          -- A-101: can be reserved
    WHEN 2 THEN 'available'          -- A-102: prior reservation expired
    WHEN 3 THEN 'reserved'           -- A-103: pending payment / 15-minute hold
    WHEN 4 THEN 'occupied'           -- A-104: active rental
    WHEN 6 THEN 'maintenance'        -- A-106: incident reported
    WHEN 8 THEN 'pending_inspection' -- A-108: not published until staff confirms
    ELSE physical_status
END,
updated_at = @Now
WHERE id IN (1, 2, 3, 4, 6, 8);
ALTER TABLE [core].[storage_units] ENABLE TRIGGER [trg_storage_unit_status_transition];

-- Pending payment: 15-minute reservation hold for concurrency/payment demos.
INSERT INTO [core].[reservations] (
    reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
    discount_snapshot, quoted_total, hold_until, status, created_at, updated_at
)
VALUES (
    N'DEMO-UAT-PENDING', @CustomerId, @FacilityId, 1, 1,
    @Today, DATEADD(month, 1, @Today), 2000, 2000, 1000,
    0, 5000, DATEADD(minute, 15, @Now), 'pending', @Now, @Now
);
DECLARE @PendingReservationId BIGINT = SCOPE_IDENTITY();

INSERT INTO [core].[unit_allocations] (
    storage_unit_id, reservation_id, allocation_kind, allocation_start_date,
    allocation_end_date, status, created_at
)
VALUES (3, @PendingReservationId, 'reservation_hold', @Today, DATEADD(month, 1, @Today), 'active', @Now);

INSERT INTO [core].[invoices] (
    customer_id, reservation_id, invoice_no, issue_date, due_date, currency,
    subtotal_amount, discount_amount, tax_amount, total_amount, paid_amount, status, created_at, updated_at
)
VALUES (@CustomerId, @PendingReservationId, N'DEMO-UAT-INV-PENDING', @Today, @Today, N'VND',
        5000, 0, 0, 5000, 0, 'open', @Now, @Now);

-- Expired reservation: useful for testing the expiry worker without waiting.
INSERT INTO [core].[reservations] (
    reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
    discount_snapshot, quoted_total, hold_until, status, created_at, updated_at
)
VALUES (
    N'DEMO-UAT-EXPIRED', @CustomerId, @FacilityId, 1, 1,
    DATEADD(day, -2, @Today), DATEADD(month, 1, DATEADD(day, -2, @Today)), 2000, 2000, 1000,
    0, 5000, DATEADD(minute, -20, @Now), 'expired', DATEADD(day, -1, @Now), @Now
);
DECLARE @ExpiredReservationId BIGINT = SCOPE_IDENTITY();

INSERT INTO [core].[unit_allocations] (
    storage_unit_id, reservation_id, allocation_kind, allocation_start_date,
    allocation_end_date, status, created_at, ended_at
)
VALUES (2, @ExpiredReservationId, 'reservation_hold', DATEADD(day, -2, @Today),
        DATEADD(month, 1, DATEADD(day, -2, @Today)), 'expired', DATEADD(day, -1, @Now), @Now);

INSERT INTO [core].[invoices] (
    customer_id, reservation_id, invoice_no, issue_date, due_date, currency,
    subtotal_amount, discount_amount, tax_amount, total_amount, paid_amount, status, created_at, updated_at, voided_at
)
VALUES (@CustomerId, @ExpiredReservationId, N'DEMO-UAT-INV-EXPIRED', DATEADD(day, -2, @Today),
        DATEADD(day, -2, @Today), N'VND', 5000, 0, 0, 5000, 0, 'voided', DATEADD(day, -1, @Now), @Now, @Now);

-- Active rental: checkout, credential and access-control test data.
INSERT INTO [core].[reservations] (
    reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
    discount_snapshot, quoted_total, hold_until, status, confirmed_at, created_at, updated_at
)
VALUES (
    N'DEMO-UAT-ACTIVE', @CustomerId, @FacilityId, 2, 2,
    @Today, DATEADD(month, 1, @Today), 4000, 4000, 1000,
    0, 9000, DATEADD(minute, 15, @Now), 'checked_in', @Now, @Now, @Now
);
DECLARE @ActiveReservationId BIGINT = SCOPE_IDENTITY();

INSERT INTO [core].[rental_agreements] (
    agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
    status, signed_at, checked_in_at, created_at, updated_at
)
VALUES (N'DEMO-UAT-AGR-A104', @ActiveReservationId, @CustomerId, @FacilityId, @PolicyVersionId,
        @Today, DATEADD(month, 1, @Today), 4000, 4000, 4000, 'active', @Now, @Now, @Now, @Now);
DECLARE @AgreementId BIGINT = SCOPE_IDENTITY();

INSERT INTO [core].[unit_allocations] (
    storage_unit_id, agreement_id, allocation_kind, allocation_start_date,
    allocation_end_date, status, created_at
)
VALUES (4, @AgreementId, 'rental', @Today, DATEADD(month, 1, @Today), 'active', @Now);

INSERT INTO [core].[access_credentials] (
    agreement_id, credential_type, secret_digest, display_hint, issued_at, expires_at, status, created_at
)
VALUES (@AgreementId, 'pin', N'demo-seed-only-not-a-real-hash', N'482910', @Now,
        DATEADD(month, 1, @Now), 'active', @Now);

COMMIT TRANSACTION;
GO

SELECT r.reservation_code, r.status AS reservation_status, su.unit_code, su.physical_status, i.status AS invoice_status
FROM [core].[reservations] r
JOIN [core].[unit_allocations] ua ON ua.reservation_id = r.id
JOIN [core].[storage_units] su ON su.id = ua.storage_unit_id
LEFT JOIN [core].[invoices] i ON i.reservation_id = r.id
WHERE r.reservation_code LIKE N'DEMO-UAT-%'
UNION ALL
SELECT ra.agreement_no, ra.status, su.unit_code, su.physical_status, N'active credential'
FROM [core].[rental_agreements] ra
JOIN [core].[unit_allocations] ua ON ua.agreement_id = ra.id
JOIN [core].[storage_units] su ON su.id = ua.storage_unit_id
WHERE ra.agreement_no = N'DEMO-UAT-AGR-A104';
GO
