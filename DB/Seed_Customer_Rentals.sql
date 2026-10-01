USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Seeding active rental agreements for customer Khôi Võ (User 7)...';

ALTER TABLE [core].[storage_units] DISABLE TRIGGER [trg_storage_unit_status_transition];
ALTER TABLE [core].[unit_allocations] DISABLE TRIGGER [trg_unit_allocations_validate_scope];

BEGIN TRANSACTION;

DECLARE @CustomerId BIGINT = 7;
DECLARE @FacilityId BIGINT = 1;
DECLARE @PolicyVersionId BIGINT = 1;
DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);
DECLARE @SixMonthsLater DATE = DATEADD(month, 6, @Today);
DECLARE @NowOffset DATETIMEOFFSET = SYSUTCDATETIME();
DECLARE @HoldUntil DATETIMEOFFSET = DATEADD(minute, 15, @NowOffset);

-- Đảm bảo user 7 có profile
IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE user_id = @CustomerId)
BEGIN
    INSERT INTO [core].[customer_profiles] (user_id, full_name, identity_number, date_of_birth, address)
    VALUES (@CustomerId, N'Khôi Võ', N'079200001234', CONVERT(date, '2000-01-01', 23), N'TP. Hồ Chí Minh');
END

-- ----------------------------------------------------------------------------
-- DỌN DẸP DỮ LIỆU CŨ CỦA KHÁCH HÀNG 7 NẾU CÓ
-- ----------------------------------------------------------------------------
DELETE FROM [core].[access_credentials] WHERE agreement_id IN (SELECT id FROM [core].[rental_agreements] WHERE customer_id = @CustomerId);
DELETE FROM [core].[unit_allocations] WHERE storage_unit_id IN (1, 6);
DELETE FROM [core].[invoices] WHERE customer_id = @CustomerId;
DELETE FROM [core].[rental_agreements] WHERE customer_id = @CustomerId;
DELETE FROM [core].[reservations] WHERE customer_id = @CustomerId;

-- Đặt trạng thái storage_units về 'occupied' cho 2 kho đang thuê
UPDATE [core].[storage_units] SET physical_status = 'occupied' WHERE id IN (1, 6);

-- ----------------------------------------------------------------------------
-- 1. TẠO RESERVATION 1 & AGREEMENT 1 CHO KHO A-101 (Unit ID 1, Small 3m2)
-- ----------------------------------------------------------------------------
-- 1.1 Reservation 1
SET IDENTITY_INSERT [core].[reservations] ON;
INSERT INTO [core].[reservations] (
    id, reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
    discount_snapshot, quoted_total, hold_until, status, confirmed_at, created_at, updated_at
)
VALUES (
    1001, N'RSV-HCM-001', @CustomerId, @FacilityId, 1, 1,
    @Today, @SixMonthsLater, 2000.00, 2000.00, 1000.00,
    0.00, 15000.00, @HoldUntil, 'checked_in', @NowOffset, @NowOffset, @NowOffset
);
SET IDENTITY_INSERT [core].[reservations] OFF;

-- 1.2 Agreement 1
SET IDENTITY_INSERT [core].[rental_agreements] ON;
INSERT INTO [core].[rental_agreements] (
    id, agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
    status, signed_at, checked_in_at, created_at, updated_at
)
VALUES (
    1001, N'AGR-HCM-2026-0001', 1001, @CustomerId, @FacilityId, @PolicyVersionId,
    @Today, @SixMonthsLater, 2000.00, 2000.00, 2000.00,
    'active', @NowOffset, @NowOffset, @NowOffset, @NowOffset
);
SET IDENTITY_INSERT [core].[rental_agreements] OFF;

-- 1.3 Invoice 1
SET IDENTITY_INSERT [core].[invoices] ON;
INSERT INTO [core].[invoices] (
    id, customer_id, agreement_id, reservation_id, invoice_no, issue_date, due_date,
    currency, subtotal_amount, discount_amount, tax_amount, total_amount, paid_amount,
    status, created_at, updated_at
)
VALUES (
    1001, @CustomerId, 1001, 1001, N'INV-2026-0001', @Today, @Today,
    N'VND', 5000.00, 0.00, 0.00, 5000.00, 5000.00,
    'paid', @NowOffset, @NowOffset
);
SET IDENTITY_INSERT [core].[invoices] OFF;

-- 1.4 Unit Allocation 1
INSERT INTO [core].[unit_allocations] (
    storage_unit_id, agreement_id, allocation_kind, allocation_start_date,
    allocation_end_date, status, created_at
)
VALUES (
    1, 1001, 'rental', @Today, @SixMonthsLater, 'active', @NowOffset
);

-- 1.5 Access Credential 1 (PIN: 482910)
INSERT INTO [core].[access_credentials] (
    agreement_id, credential_type, secret_digest, display_hint,
    issued_at, expires_at, status, created_at
)
VALUES (
    1001, 'pin', N'$2a$11$eACCYoNO32Om5vG8A1aL3e1Q7.nU.fA7gGqWn9F4uN5N9E9qj4Yeq', N'482910',
    @NowOffset, DATEADD(month, 6, @NowOffset), 'active', @NowOffset
);

-- ----------------------------------------------------------------------------
-- 2. TẠO RESERVATION 2 & AGREEMENT 2 CHO KHO A-106 (Unit ID 6, Máy Lạnh 6m2)
-- ----------------------------------------------------------------------------
-- 2.1 Reservation 2
SET IDENTITY_INSERT [core].[reservations] ON;
INSERT INTO [core].[reservations] (
    id, reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
    discount_snapshot, quoted_total, hold_until, status, confirmed_at, created_at, updated_at
)
VALUES (
    1002, N'RSV-HCM-002', @CustomerId, @FacilityId, 3, 3,
    @Today, @SixMonthsLater, 5000.00, 5000.00, 1000.00,
    0.00, 36000.00, @HoldUntil, 'checked_in', @NowOffset, @NowOffset, @NowOffset
);
SET IDENTITY_INSERT [core].[reservations] OFF;

-- 2.2 Agreement 2
SET IDENTITY_INSERT [core].[rental_agreements] ON;
INSERT INTO [core].[rental_agreements] (
    id, agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
    start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
    status, signed_at, checked_in_at, created_at, updated_at
)
VALUES (
    1002, N'AGR-HCM-2026-0002', 1002, @CustomerId, @FacilityId, @PolicyVersionId,
    @Today, @SixMonthsLater, 5000.00, 5000.00, 5000.00,
    'active', @NowOffset, @NowOffset, @NowOffset, @NowOffset
);
SET IDENTITY_INSERT [core].[rental_agreements] OFF;

-- 2.3 Invoice 2
SET IDENTITY_INSERT [core].[invoices] ON;
INSERT INTO [core].[invoices] (
    id, customer_id, agreement_id, reservation_id, invoice_no, issue_date, due_date,
    currency, subtotal_amount, discount_amount, tax_amount, total_amount, paid_amount,
    status, created_at, updated_at
)
VALUES (
    1002, @CustomerId, 1002, 1002, N'INV-2026-0002', @Today, @Today,
    N'VND', 11000.00, 0.00, 0.00, 11000.00, 11000.00,
    'paid', @NowOffset, @NowOffset
);
SET IDENTITY_INSERT [core].[invoices] OFF;

-- 2.4 Unit Allocation 2
INSERT INTO [core].[unit_allocations] (
    storage_unit_id, agreement_id, allocation_kind, allocation_start_date,
    allocation_end_date, status, created_at
)
VALUES (
    6, 1002, 'rental', @Today, @SixMonthsLater, 'active', @NowOffset
);

-- 2.5 Access Credential 2 (PIN: 371904)
INSERT INTO [core].[access_credentials] (
    agreement_id, credential_type, secret_digest, display_hint,
    issued_at, expires_at, status, created_at
)
VALUES (
    1002, 'pin', N'$2a$11$eACCYoNO32Om5vG8A1aL3e1Q7.nU.fA7gGqWn9F4uN5N9E9qj4Yeq', N'371904',
    @NowOffset, DATEADD(month, 6, @NowOffset), 'active', @NowOffset
);

COMMIT TRANSACTION;

ALTER TABLE [core].[storage_units] ENABLE TRIGGER [trg_storage_unit_status_transition];
ALTER TABLE [core].[unit_allocations] ENABLE TRIGGER [trg_unit_allocations_validate_scope];

PRINT N'SUCCESS: Seeded 2 active rentals (A-101 & A-106) for Customer 7 successfully!';
GO

SELECT 
    ra.id AS AgreementId,
    ra.agreement_no,
    su.unit_code,
    f.name AS FacilityName,
    ra.monthly_rate_snapshot AS Rate_VND,
    ac.display_hint AS PinCode,
    ra.status
FROM [core].[rental_agreements] ra
JOIN [core].[unit_allocations] ua ON ua.agreement_id = ra.id AND ua.status = 'active'
JOIN [core].[storage_units] su ON su.id = ua.storage_unit_id
JOIN [core].[facilities] f ON f.id = ra.facility_id
LEFT JOIN [core].[access_credentials] ac ON ac.agreement_id = ra.id AND ac.credential_type = 'pin'
WHERE ra.customer_id = 7;
GO
