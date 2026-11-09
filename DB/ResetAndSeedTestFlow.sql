-- ======================================================================================
-- RESET & SEED DATA SCRIPT CHO TEST LUỒNG NGHIỆP VỤ E2E (G1SelfStorage)
-- Database: SelfStoragePRN222
-- Khách hàng test: Khôi Võ (yu9696000@gmail.com, Password: 1)
-- Nhân viên test: staff@gmail.com (Password: 1, Cơ sở 1: Kho Thủ Đức - G1 Storage Hồ Chí Minh)
-- Quản lý test: manager@gmail.com (Password: 1, Cơ sở 1)
-- ======================================================================================

USE [SelfStoragePRN222];
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT N'>>> BẮT ĐẦU RESET & SEED DỮ LIỆU TOÀN DIỆN...';

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. VÔ HIỆU HÓA TẤT CẢ TRIGGERS ĐỂ THAO TÁC BULK RESET
    ALTER TABLE [core].[access_credentials] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[facility_rates] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[price_ranges] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[delinquency_cases] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[handover_records] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[inspection_items] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[inspections] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[invoice_lines] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[invoices] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[move_out_requests] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[payment_allocations] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[payments] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[promotion_redemptions] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[refunds] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[rental_agreements] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[rental_renewals] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[reservations] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[storage_units] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[stored_items] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[support_tickets] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[unit_allocations] DISABLE TRIGGER ALL;
    ALTER TABLE [core].[unit_transfer_requests] DISABLE TRIGGER ALL;

    -- 2. XÁC ĐỊNH USER ID
    DECLARE @CustomerId BIGINT;
    SELECT @CustomerId = id FROM [core].[users] WHERE email = N'yu9696000@gmail.com';

    DECLARE @StaffUserId BIGINT;
    SELECT @StaffUserId = id FROM [core].[users] WHERE email = N'staff@gmail.com';

    DECLARE @ManagerUserId BIGINT;
    SELECT @ManagerUserId = id FROM [core].[users] WHERE email = N'manager@gmail.com';

    DECLARE @AdminUserId BIGINT;
    SELECT @AdminUserId = id FROM [core].[users] WHERE email = N'admin@gmail.com';

    IF @CustomerId IS NULL
    BEGIN
        THROW 50001, N'Không tìm thấy user yu9696000@gmail.com trong database.', 1;
    END

    -- Cập nhật mật khẩu chuẩn cho customer, staff, manager, admin là '1'
    DECLARE @Password1Hash NVARCHAR(255) = N'$2a$11$HWcRgUvFaROkssbvAG.1COvFAbaL1dHcF0tCNYHP5B3vyCsdSGZ0O';
    UPDATE [core].[users] 
    SET [password_hash] = @Password1Hash, [status] = N'active' 
    WHERE id IN (@CustomerId, @StaffUserId, @ManagerUserId, @AdminUserId);

    -- Đảm bảo customer_profiles chuẩn font tiếng Việt 'Khôi Võ'
    IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE user_id = @CustomerId)
    BEGIN
        INSERT INTO [core].[customer_profiles] (user_id, full_name, created_at, updated_at)
        VALUES (@CustomerId, N'Khôi Võ', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[customer_profiles]
        SET full_name = N'Khôi Võ', updated_at = SYSUTCDATETIME()
        WHERE user_id = @CustomerId;
    END

    -- 3. CHUẨN HÓA 10 CƠ SỞ KHO VỚI TÊN TIẾNG VIỆT CHUẨN
    UPDATE [core].[facilities] SET [name] = N'Kho Thủ Đức - G1 Storage Hồ Chí Minh', [city] = N'Hồ Chí Minh' WHERE id = 1;
    UPDATE [core].[facilities] SET [name] = N'Kho Quận 1 - Bến Nghé Central Storage', [city] = N'Hồ Chí Minh' WHERE id = 2;
    UPDATE [core].[facilities] SET [name] = N'Kho Quận 7 - Phú Mỹ Hưng Smart Storage', [city] = N'Hồ Chí Minh' WHERE id = 3;
    UPDATE [core].[facilities] SET [name] = N'Kho Bình Thạnh - Landmark Central Storage', [city] = N'Hồ Chí Minh' WHERE id = 4;
    UPDATE [core].[facilities] SET [name] = N'Kho Tân Bình - Sân Bay Tân Sơn Nhất', [city] = N'Hồ Chí Minh' WHERE id = 5;
    UPDATE [core].[facilities] SET [name] = N'Kho Cầu Giấy - Hà Nội Smart Storage', [city] = N'Hà Nội' WHERE id = 6;
    UPDATE [core].[facilities] SET [name] = N'Kho Hoàn Kiếm - Phố Cổ Heritage Storage', [city] = N'Hà Nội' WHERE id = 7;
    UPDATE [core].[facilities] SET [name] = N'Kho Thanh Xuân - Hà Nội Tech Storage', [city] = N'Hà Nội' WHERE id = 8;
    UPDATE [core].[facilities] SET [name] = N'Kho Hải Châu - Đà Nẵng Riverside Storage', [city] = N'Đà Nẵng' WHERE id = 9;
    UPDATE [core].[facilities] SET [name] = N'Kho Sơn Trà - Đà Nẵng Ocean Storage', [city] = N'Đà Nẵng' WHERE id = 10;
    PRINT N'>>> Đã cập nhật 10 cơ sở kho tên tiếng Việt chuẩn.';

    -- 4. DỌN DẸP TOÀN BỘ GIAO DỊCH TEST & BẢNG PHỤ THUỘC TRƯỚC KHI THU GỌN Ô KHO
    DELETE FROM [core].[ticket_messages];
    DELETE FROM [core].[ticket_attachments];
    DELETE FROM [core].[ticket_assignments];
    DELETE FROM [core].[ticket_charge_proposals];
    DELETE FROM [core].[staff_tasks];
    DELETE FROM [core].[service_ratings];
    DELETE FROM [core].[maintenance_work_orders];
    DELETE FROM [core].[support_tickets];

    DELETE FROM [core].[delinquency_actions];
    DELETE FROM [core].[delinquency_cases];

    DELETE FROM [core].[refund_approvals];
    DELETE FROM [core].[refunds];
    DELETE FROM [core].[payment_allocations];
    DELETE FROM [core].[payments];
    DELETE FROM [core].[promotion_redemptions];
    DELETE FROM [core].[promotions];
    DELETE FROM [core].[invoice_lines];
    DELETE FROM [core].[invoices];

    DELETE FROM [core].[handover_records];
    DELETE FROM [core].[inspection_items];
    DELETE FROM [core].[inspections];

    DELETE FROM [core].[appointments];
    DELETE FROM [core].[identity_verifications];
    DELETE FROM [core].[move_out_requests];
    DELETE FROM [core].[unit_transfer_requests];
    DELETE FROM [core].[rental_renewals];
    DELETE FROM [core].[stored_items];
    DELETE FROM [core].[access_credentials];
    DELETE FROM [core].[unit_allocations];
    DELETE FROM [core].[rental_agreements];
    DELETE FROM [core].[reservations];

    PRINT N'>>> Đã dọn dẹp sạch sẽ toàn bộ các bảng giao dịch cũ.';

    -- 5. CHUẨN HÓA ĐÚNG 10 Ô KHO CHO MỖI CƠ SỞ (TỔNG ĐÚNG 100 Ô KHO)
    DELETE FROM [core].[unit_status_history];
    DELETE FROM [core].[unit_map_positions];
    DELETE FROM [core].[unit_transfer_requests];
    DELETE FROM [core].[support_tickets];
    DELETE FROM [core].[unit_allocations];
    DELETE FROM [core].[maintenance_work_orders];
    DELETE FROM [core].[inspections];

    ;WITH RankedUnits AS (
        SELECT id, facility_id,
               ROW_NUMBER() OVER (PARTITION BY facility_id ORDER BY id ASC) as rn
        FROM [core].[storage_units]
    )
    DELETE FROM [core].[storage_units]
    WHERE id IN (SELECT id FROM RankedUnits WHERE rn > 10);

    ;WITH RankedUnits AS (
        SELECT id, facility_id,
               ROW_NUMBER() OVER (PARTITION BY facility_id ORDER BY id ASC) as rn
        FROM [core].[storage_units]
    )
    UPDATE u
    SET u.unit_code = CASE r.rn
            WHEN 1 THEN N'A-101'
            WHEN 2 THEN N'A-102'
            WHEN 3 THEN N'A-103'
            WHEN 4 THEN N'A-104'
            WHEN 5 THEN N'A-105'
            WHEN 6 THEN N'B-101'
            WHEN 7 THEN N'B-102'
            WHEN 8 THEN N'B-103'
            WHEN 9 THEN N'B-104'
            WHEN 10 THEN N'B-105'
        END,
        u.unit_type_id = CASE r.rn
            WHEN 1 THEN 1 -- Small Dry 3m2
            WHEN 2 THEN 2 -- Medium Dry 6m2
            WHEN 3 THEN 3 -- Medium Seafood 6m2
            WHEN 4 THEN 4 -- Large Warm 10m2
            WHEN 5 THEN 5 -- Extra Large Climate 16m2
            WHEN 6 THEN 6 -- Mini Dry 1m2
            WHEN 7 THEN 7 -- Small Seafood 3m2
            WHEN 8 THEN 8 -- Medium Warm 6m2
            WHEN 9 THEN 1 -- Small Dry 3m2
            WHEN 10 THEN 2 -- Medium Dry 6m2
        END,
        u.physical_status = N'available',
        u.is_listed = 1,
        u.notes = NULL,
        u.updated_at = SYSUTCDATETIME()
    FROM [core].[storage_units] u
    JOIN RankedUnits r ON u.id = r.id;

    PRINT N'>>> Đã chuẩn hóa đúng 10 ô kho cho từng cơ sở (100 ô kho toàn hệ thống).';

    -- 6. CẬP NHẬT DẢI GIÁ DOANH NGHIỆP DƯỚI 10.000 ₫ (CHO SEPAY TEST THỰC TẾ)
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 1;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 2;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 3;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 4;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 5;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 6;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 7;
    UPDATE [core].[price_ranges] SET min_monthly_rate = 1000, max_monthly_rate = 15000 WHERE unit_type_id = 8;
    PRINT N'>>> Đã cập nhật dải giá doanh nghiệp min 1.000 ₫, max 15.000 ₫.';

    -- 7. CẬP NHẬT BIỂU GIÁ TẤT CẢ CƠ SỞ (TỪ 2.000 ₫ ĐẾN 7.000 ₫, ĐỀU < 10.000 ₫)
    UPDATE [core].[facility_rates] SET monthly_rate = 2000, deposit_amount = 2000, booking_fee = 0 WHERE unit_type_id = 1; -- Small Dry 3m2
    UPDATE [core].[facility_rates] SET monthly_rate = 3000, deposit_amount = 3000, booking_fee = 0 WHERE unit_type_id = 2; -- Medium Dry 6m2
    UPDATE [core].[facility_rates] SET monthly_rate = 4000, deposit_amount = 4000, booking_fee = 0 WHERE unit_type_id = 3; -- Medium Seafood 6m2
    UPDATE [core].[facility_rates] SET monthly_rate = 5000, deposit_amount = 5000, booking_fee = 0 WHERE unit_type_id = 4; -- Large Warm 10m2
    UPDATE [core].[facility_rates] SET monthly_rate = 7000, deposit_amount = 7000, booking_fee = 0 WHERE unit_type_id = 5; -- XL Climate 16m2
    UPDATE [core].[facility_rates] SET monthly_rate = 2000, deposit_amount = 2000, booking_fee = 0 WHERE unit_type_id = 6; -- Mini Dry 1m2
    UPDATE [core].[facility_rates] SET monthly_rate = 3500, deposit_amount = 3500, booking_fee = 0 WHERE unit_type_id = 7; -- Small Seafood 3m2
    UPDATE [core].[facility_rates] SET monthly_rate = 4500, deposit_amount = 4500, booking_fee = 0 WHERE unit_type_id = 8; -- Medium Warm 6m2
    PRINT N'>>> Đã cập nhật biểu giá toàn bộ cơ sở từ 2.000 ₫ đến 7.000 ₫ (< 10.000 ₫).';

    -- 8. THIẾT LẬP CÁC KỊCH BẢN TEST NGHIỆP VỤ CHO KHÁCH HÀNG KHOI VO (CƠ SỞ 1)
    DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    DECLARE @PolicyVersionId BIGINT;
    SELECT TOP 1 @PolicyVersionId = id FROM [core].[policy_versions] ORDER BY id;
    IF @PolicyVersionId IS NULL SET @PolicyVersionId = 1;

    -- Lấy ID các ô kho cơ sở 1 theo mã
    DECLARE @UnitA101 BIGINT, @UnitA102 BIGINT, @UnitA103 BIGINT, @UnitA104 BIGINT, @UnitA105 BIGINT;
    DECLARE @UnitB101 BIGINT, @UnitB102 BIGINT, @UnitB103 BIGINT;
    SELECT @UnitA101 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'A-101';
    SELECT @UnitA102 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'A-102';
    SELECT @UnitA103 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'A-103';
    SELECT @UnitA104 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'A-104';
    SELECT @UnitA105 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'A-105';
    SELECT @UnitB101 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'B-101';
    SELECT @UnitB102 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'B-102';
    SELECT @UnitB103 = id FROM [core].[storage_units] WHERE facility_id = 1 AND unit_code = N'B-103';

    -- Lấy Rate ID cho Facility 1
    DECLARE @RateType1 BIGINT, @RateType2 BIGINT, @RateType3 BIGINT, @RateType4 BIGINT, @RateType5 BIGINT;
    SELECT @RateType1 = id FROM [core].[facility_rates] WHERE facility_id = 1 AND unit_type_id = 1;
    SELECT @RateType2 = id FROM [core].[facility_rates] WHERE facility_id = 1 AND unit_type_id = 2;
    SELECT @RateType3 = id FROM [core].[facility_rates] WHERE facility_id = 1 AND unit_type_id = 3;
    SELECT @RateType4 = id FROM [core].[facility_rates] WHERE facility_id = 1 AND unit_type_id = 4;
    SELECT @RateType5 = id FROM [core].[facility_rates] WHERE facility_id = 1 AND unit_type_id = 5;

    -- ==================================================================================
    -- KỊCH BẢN 1: ĐẶT CHỖ CHỜ THANH TOÁN (Reservation -> Cancel / Pay)
    -- Ô kho A-102 (Type 2 - Medium Dry 6m2): Rate 3.000 ₫, Deposit 3.000 ₫ = Tổng 6.000 ₫
    -- ==================================================================================
    DECLARE @Res1Id BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-202611-TEST01', @CustomerId, 1, 2,
        @RateType2, @Today, DATEADD(month, 1, @Today),
        3000.00, 3000.00, 0.00,
        0.00, 6000.00, DATEADD(minute, 15, @Now), N'pending',
        NULL, NULL, NULL, @Now, @Now
    );
    SET @Res1Id = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at
    ) VALUES (
        @UnitA102, @Res1Id, NULL, N'reservation_hold',
        @Today, DATEADD(month, 1, @Today), N'active', N'Đặt chỗ chờ thanh toán', @StaffUserId, @Now
    );

    UPDATE [core].[storage_units] SET [physical_status] = N'reserved' WHERE id = @UnitA102;

    DECLARE @Inv1Id BIGINT;
    INSERT INTO [core].[invoices] (
        invoice_no, customer_id, reservation_id, agreement_id,
        issue_date, due_date, currency, subtotal_amount, discount_amount, tax_amount,
        total_amount, paid_amount, status, opened_at, created_at, updated_at
    ) VALUES (
        N'INV-RSV-202611-TEST01', @CustomerId, @Res1Id, NULL,
        @Today, DATEADD(day, 2, @Today), N'VND', 6000.00, 0.00, 0.00,
        6000.00, 0.00, N'open', @Now, @Now, @Now
    );
    SET @Inv1Id = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] (invoice_id, line_type, description, quantity, unit_price, metadata)
    VALUES 
    (@Inv1Id, N'rent', N'Tiền thuê tháng đầu - Kho A-102 (Medium Dry 6m²)', 1, 3000.00, N'{"unitCode":"A-102"}'),
    (@Inv1Id, N'deposit', N'Tiền đặt cọc giữ chỗ bảo đảm', 1, 3000.00, N'{"deposit":true}');

    PRINT N'>>> Kịch bản 1: Đã tạo đơn đặt chỗ RSV-202611-TEST01 (Tổng 6.000 ₫, chờ thanh toán/hủy).';

    -- ==================================================================================
    -- KỊCH BẢN 2: HỢP ĐỒNG ĐANG HOẠT ĐỘNG & MỞ KHÓA PIN (Active Storage)
    -- Ô kho A-101 (Type 1 - Small Dry 3m2): Rate 2.000 ₫, Deposit 2.000 ₫ = 4.000 ₫
    -- PIN mở kho: 123456
    -- ==================================================================================
    DECLARE @Res2Id BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-2026-ACT01', @CustomerId, 1, 1,
        @RateType1, DATEADD(day, -5, @Today), DATEADD(month, 1, DATEADD(day, -5, @Today)),
        2000.00, 2000.00, 0.00,
        0.00, 4000.00, DATEADD(minute, 15, DATEADD(day, -5, @Now)), N'confirmed',
        DATEADD(day, -5, @Now), NULL, NULL, DATEADD(day, -5, @Now), @Now
    );
    SET @Res2Id = SCOPE_IDENTITY();

    DECLARE @Agr2Id BIGINT;
    INSERT INTO [core].[rental_agreements] (
        agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, checked_out_at, actual_end_date, created_at, updated_at
    ) VALUES (
        N'AGR-2026-ACT01', @Res2Id, @CustomerId, 1, @PolicyVersionId,
        DATEADD(day, -5, @Today), DATEADD(month, 1, DATEADD(day, -5, @Today)), 2000.00, 2000.00, 2000.00,
        N'active', DATEADD(day, -5, @Now), DATEADD(day, -5, @Now), NULL, NULL, DATEADD(day, -5, @Now), @Now
    );
    SET @Agr2Id = SCOPE_IDENTITY();

    DECLARE @Alloc2Id BIGINT;
    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at
    ) VALUES (
        @UnitA101, NULL, @Agr2Id, N'rental',
        DATEADD(day, -5, @Today), DATEADD(month, 1, DATEADD(day, -5, @Today)), N'active', N'Hợp đồng thuê hiệu lực', @StaffUserId, DATEADD(day, -5, @Now)
    );
    SET @Alloc2Id = SCOPE_IDENTITY();

    UPDATE [core].[storage_units] SET [physical_status] = N'occupied' WHERE id = @UnitA101;

    DECLARE @Inv2Id BIGINT;
    INSERT INTO [core].[invoices] (
        invoice_no, customer_id, reservation_id, agreement_id,
        issue_date, due_date, currency, subtotal_amount, discount_amount, tax_amount,
        total_amount, paid_amount, status, opened_at, created_at, updated_at
    ) VALUES (
        N'INV-ACT-202611-01', @CustomerId, NULL, @Agr2Id,
        DATEADD(day, -5, @Today), DATEADD(day, -3, @Today),
        N'VND', 4000.00, 0.00, 0.00,
        4000.00, 4000.00, N'paid', DATEADD(day, -5, @Now), DATEADD(day, -5, @Now), @Now
    );
    SET @Inv2Id = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] (invoice_id, line_type, description, quantity, unit_price, metadata)
    VALUES 
    (@Inv2Id, N'rent', N'Tiền thuê kỳ đầu - Kho A-101', 1, 2000.00, N'{"unitCode":"A-101"}'),
    (@Inv2Id, N'deposit', N'Tiền đặt cọc bảo đảm', 1, 2000.00, N'{"deposit":true}');

    DECLARE @Pay2Id BIGINT;
    INSERT INTO [core].[payments] (
        customer_id, target_invoice_id, amount, currency, method,
        provider, provider_transaction_id, idempotency_key, status, paid_at, metadata, created_at, updated_at
    ) VALUES (
        @CustomerId, @Inv2Id, 4000.00, N'VND', N'bank_transfer',
        N'sepay', N'TXN-SEPAY-ACT01', N'IDEMP-ACT01', N'succeeded', DATEADD(day, -5, @Now), N'{}', DATEADD(day, -5, @Now), @Now
    );
    SET @Pay2Id = SCOPE_IDENTITY();

    INSERT INTO [core].[payment_allocations] (payment_id, invoice_id, allocated_amount, allocated_at)
    VALUES (@Pay2Id, @Inv2Id, 4000.00, DATEADD(day, -5, @Now));

    -- PIN Code 123456
    INSERT INTO [core].[access_credentials] (
        agreement_id, authorized_member_id, credential_type,
        external_secret_ref, secret_digest, display_hint, issued_at, expires_at, status, issued_by, created_at
    ) VALUES (
        @Agr2Id, NULL, N'pin',
        NULL, N'123456', N'123456', DATEADD(day, -5, @Now), DATEADD(month, 1, DATEADD(day, -5, @Now)), N'active', @StaffUserId, DATEADD(day, -5, @Now)
    );

    -- Check-in inspection for handover
    DECLARE @InspCheckIn2Id BIGINT;
    INSERT INTO [core].[inspections] (
        facility_id, storage_unit_id, reservation_id, agreement_id,
        inspected_by, inspection_type, status, overall_condition,
        summary, inspected_at, created_at
    ) VALUES (
        1, @UnitA101, NULL, @Agr2Id,
        @StaffUserId, N'check_in', N'completed', N'good',
        N'Biên bản kiểm tra bàn giao nhận kho check-in', DATEADD(day, -5, @Now), DATEADD(day, -5, @Now)
    );
    SET @InspCheckIn2Id = SCOPE_IDENTITY();

    -- Handover record
    INSERT INTO [core].[handover_records] (
        agreement_id, unit_allocation_id, inspection_id, handled_by,
        handover_type, customer_signature_ref, staff_signature_ref,
        customer_signed_at, staff_signed_at, notes, created_at
    ) VALUES (
        @Agr2Id, @Alloc2Id, @InspCheckIn2Id, @StaffUserId,
        N'check_in', N'SIG-CUST-ACT01', N'SIG-STAFF-ACT01',
        DATEADD(day, -5, @Now), DATEADD(day, -5, @Now), N'Bàn giao kho A-101 nguyên vẹn, mã PIN 123456 đã cấp.', DATEADD(day, -5, @Now)
    );

    PRINT N'>>> Kịch bản 2: Hợp đồng AGR-2026-ACT01 (Kho A-101, PIN 123456, Active, đã trả đủ 4.000 ₫).';

    -- ==================================================================================
    -- KỊCH BẢN 3: HỢP ĐỒNG SẮP HẾT HẠN (Test Gia hạn Renewal & Đổi ô kho Transfer)
    -- Ô kho A-103 (Type 3 - Medium Seafood 6m2): Rate 4.000 ₫, Deposit 4.000 ₫
    -- Thời hạn còn 3 ngày nữa là hết hạn
    -- ==================================================================================
    DECLARE @Res3Id BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-2026-RNW02', @CustomerId, 1, 3,
        @RateType3, DATEADD(month, -1, DATEADD(day, 3, @Today)), DATEADD(day, 3, @Today),
        4000.00, 4000.00, 0.00,
        0.00, 8000.00, DATEADD(minute, 15, DATEADD(month, -1, DATEADD(day, 3, @Now))), N'confirmed',
        DATEADD(month, -1, DATEADD(day, 3, @Now)), NULL, NULL, DATEADD(month, -1, DATEADD(day, 3, @Now)), @Now
    );
    SET @Res3Id = SCOPE_IDENTITY();

    DECLARE @Agr3Id BIGINT;
    INSERT INTO [core].[rental_agreements] (
        agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, checked_out_at, actual_end_date, created_at, updated_at
    ) VALUES (
        N'AGR-2026-RNW02', @Res3Id, @CustomerId, 1, @PolicyVersionId,
        DATEADD(month, -1, DATEADD(day, 3, @Today)), DATEADD(day, 3, @Today), 4000.00, 4000.00, 4000.00,
        N'active', DATEADD(month, -1, DATEADD(day, 3, @Now)), DATEADD(month, -1, DATEADD(day, 3, @Now)), NULL, NULL, DATEADD(month, -1, DATEADD(day, 3, @Now)), @Now
    );
    SET @Agr3Id = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at
    ) VALUES (
        @UnitA103, NULL, @Agr3Id, N'rental',
        DATEADD(month, -1, DATEADD(day, 3, @Today)), DATEADD(day, 3, @Today), N'active', N'Hợp đồng sắp hết hạn', @StaffUserId, DATEADD(month, -1, DATEADD(day, 3, @Now))
    );

    UPDATE [core].[storage_units] SET [physical_status] = N'occupied' WHERE id = @UnitA103;

    DECLARE @Inv3Id BIGINT;
    INSERT INTO [core].[invoices] (
        invoice_no, customer_id, reservation_id, agreement_id,
        issue_date, due_date, currency, subtotal_amount, discount_amount, tax_amount,
        total_amount, paid_amount, status, opened_at, created_at, updated_at
    ) VALUES (
        N'INV-RNW-202611-01', @CustomerId, NULL, @Agr3Id,
        DATEADD(month, -1, DATEADD(day, 3, @Today)), DATEADD(day, -25, @Today),
        N'VND', 8000.00, 0.00, 0.00,
        8000.00, 8000.00, N'paid', DATEADD(month, -1, DATEADD(day, 3, @Now)), DATEADD(month, -1, DATEADD(day, 3, @Now)), @Now
    );
    SET @Inv3Id = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] (invoice_id, line_type, description, quantity, unit_price, metadata)
    VALUES 
    (@Inv3Id, N'rent', N'Tiền thuê tháng đầu - Kho A-103', 1, 4000.00, N'{"unitCode":"A-103"}'),
    (@Inv3Id, N'deposit', N'Tiền đặt cọc bảo đảm', 1, 4000.00, N'{"deposit":true}');

    DECLARE @Pay3Id BIGINT;
    INSERT INTO [core].[payments] (
        customer_id, target_invoice_id, amount, currency, method,
        provider, provider_transaction_id, idempotency_key, status, paid_at, metadata, created_at, updated_at
    ) VALUES (
        @CustomerId, @Inv3Id, 8000.00, N'VND', N'bank_transfer',
        N'sepay', N'TXN-SEPAY-RNW02', N'IDEMP-RNW02', N'succeeded', DATEADD(month, -1, DATEADD(day, 3, @Now)), N'{}', DATEADD(month, -1, DATEADD(day, 3, @Now)), @Now
    );
    SET @Pay3Id = SCOPE_IDENTITY();

    INSERT INTO [core].[payment_allocations] (payment_id, invoice_id, allocated_amount, allocated_at)
    VALUES (@Pay3Id, @Inv3Id, 8000.00, DATEADD(month, -1, DATEADD(day, 3, @Now)));

    INSERT INTO [core].[access_credentials] (
        agreement_id, authorized_member_id, credential_type,
        external_secret_ref, secret_digest, display_hint, issued_at, expires_at, status, issued_by, created_at
    ) VALUES (
        @Agr3Id, NULL, N'pin',
        NULL, N'654321', N'654321', DATEADD(month, -1, DATEADD(day, 3, @Now)), DATEADD(day, 3, @Now), N'active', @StaffUserId, DATEADD(month, -1, DATEADD(day, 3, @Now))
    );

    PRINT N'>>> Kịch bản 3: Hợp đồng AGR-2026-RNW02 (Kho A-103, còn 3 ngày, sẵn sàng test Gia hạn / Đổi kho).';

    -- ==================================================================================
    -- KỊCH BẢN 4A: CHỜ STAFF NGHIỆM THU TRẢ KHO (Move-out scheduled, Deposit 5.000 ₫)
    -- Ô kho A-104 (Type 4 - Large Warm 10m2): Tiền cọc 5.000 ₫
    -- Biên bản nghiệm thu core.inspections đang ở trạng thái 'draft' (chờ Staff vào nghiệm thu)
    -- ==================================================================================
    DECLARE @Res4AId BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-2026-INSP-4A', @CustomerId, 1, 4,
        @RateType4, DATEADD(month, -1, @Today), @Today,
        5000.00, 5000.00, 0.00,
        0.00, 10000.00, DATEADD(minute, 15, DATEADD(month, -1, @Now)), N'confirmed',
        DATEADD(month, -1, @Now), NULL, NULL, DATEADD(month, -1, @Now), @Now
    );
    SET @Res4AId = SCOPE_IDENTITY();

    DECLARE @Agr4AId BIGINT;
    INSERT INTO [core].[rental_agreements] (
        agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, checked_out_at, actual_end_date, created_at, updated_at
    ) VALUES (
        N'AGR-2026-INSP-4A', @Res4AId, @CustomerId, 1, @PolicyVersionId,
        DATEADD(month, -1, @Today), @Today, 5000.00, 5000.00, 5000.00,
        N'move_out_scheduled', DATEADD(month, -1, @Now), DATEADD(month, -1, @Now), NULL, NULL, DATEADD(month, -1, @Now), @Now
    );
    SET @Agr4AId = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at
    ) VALUES (
        @UnitA104, NULL, @Agr4AId, N'rental',
        DATEADD(month, -1, @Today), @Today, N'active', N'Chờ nghiệm thu trả kho', @StaffUserId, DATEADD(month, -1, @Now)
    );

    UPDATE [core].[storage_units] SET [physical_status] = N'pending_inspection' WHERE id = @UnitA104;

    INSERT INTO [core].[move_out_requests] (
        agreement_id, requested_by, requested_move_out_date, appointment_id,
        status, reason, finalized_at, created_at, updated_at
    ) VALUES (
        @Agr4AId, @CustomerId, @Today, NULL,
        N'scheduled', N'Hết nhu cầu lưu trữ hàng hóa dự án', NULL, @Now, @Now
    );

    -- Tạo inspection ở trạng thái draft (chờ staff bấm "Tiến hành nghiệm thu")
    INSERT INTO [core].[inspections] (
        facility_id, storage_unit_id, reservation_id, agreement_id,
        inspected_by, inspection_type, status, overall_condition,
        summary, inspected_at, created_at
    ) VALUES (
        1, @UnitA104, NULL, @Agr4AId,
        @StaffUserId, N'check_out', N'draft', N'good',
        N'Yêu cầu nghiệm thu trả kho cuối kỳ', NULL, @Now
    );

    PRINT N'>>> Kịch bản 4A: Hợp đồng AGR-2026-INSP-4A (Cọc 5.000 ₫, chờ Staff nghiệm thu trong MoveOut tab).';

    -- ==================================================================================
    -- KỊCH BẢN 4B: ĐÃ NGHIỆM THU XONG -> CHỜ KHÁCH NHẬP STK (Pending Bank Info Card on /my-units)
    -- Ô kho A-105: Cọc 7.000 ₫ (Type 5), Hư hại 2.000 ₫ -> Hoàn cọc 5.000 ₫
    -- Refund status: 'pending', provider: 'bank_transfer'
    -- ==================================================================================
    DECLARE @Res4BId BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-2026-INSP-4B', @CustomerId, 1, 5,
        @RateType5, DATEADD(month, -2, @Today), DATEADD(month, -1, @Today),
        7000.00, 7000.00, 0.00,
        0.00, 14000.00, DATEADD(minute, 15, DATEADD(month, -2, @Now)), N'confirmed',
        DATEADD(month, -2, @Now), NULL, NULL, DATEADD(month, -2, @Now), @Now
    );
    SET @Res4BId = SCOPE_IDENTITY();

    DECLARE @Agr4BId BIGINT;
    INSERT INTO [core].[rental_agreements] (
        agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, checked_out_at, actual_end_date, created_at, updated_at
    ) VALUES (
        N'AGR-2026-INSP-4B', @Res4BId, @CustomerId, 1, @PolicyVersionId,
        DATEADD(month, -2, @Today), DATEADD(month, -1, @Today), 7000.00, 7000.00, 5000.00,
        N'terminated', DATEADD(month, -2, @Now), DATEADD(month, -2, @Now), @Now, DATEADD(month, -1, @Today), DATEADD(month, -2, @Now), @Now
    );
    SET @Agr4BId = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at, ended_at
    ) VALUES (
        @UnitA105, NULL, @Agr4BId, N'rental',
        DATEADD(month, -2, @Today), DATEADD(month, -1, @Today), N'released', N'Đã kết thúc hợp đồng', @StaffUserId, DATEADD(month, -2, @Now), @Now
    );

    UPDATE [core].[storage_units] SET [physical_status] = N'cleaning' WHERE id = @UnitA105;

    INSERT INTO [core].[move_out_requests] (
        agreement_id, requested_by, requested_move_out_date, appointment_id,
        status, reason, finalized_at, created_at, updated_at
    ) VALUES (
        @Agr4BId, @CustomerId, DATEADD(month, -1, @Today), NULL,
        N'completed', N'Trả kho trả phòng', @Now, DATEADD(month, -2, @Now), @Now
    );

    DECLARE @Insp4BId BIGINT;
    INSERT INTO [core].[inspections] (
        facility_id, storage_unit_id, reservation_id, agreement_id,
        inspected_by, inspection_type, status, overall_condition,
        summary, inspected_at, created_at
    ) VALUES (
        1, @UnitA105, NULL, @Agr4BId,
        @StaffUserId, N'check_out', N'completed', N'acceptable',
        N'Kho có vết bẩn sàn, cần vệ sinh tẩy rửa công nghiệp.', @Now, @Now
    );
    SET @Insp4BId = SCOPE_IDENTITY();

    INSERT INTO [core].[inspection_items] (
        inspection_id, item_name, condition, notes, photo_url, charge_amount
    ) VALUES (
        @Insp4BId, N'Dọn dẹp vệ sinh sàn kho & khử mùi', N'damaged', N'Khấu trừ chi phí vệ sinh 2.000 ₫', NULL, 2000.00
    );

    -- Hóa đơn & thanh toán gốc của 4B để refund liên kết
    DECLARE @Inv4BId BIGINT;
    INSERT INTO [core].[invoices] (
        invoice_no, customer_id, reservation_id, agreement_id,
        issue_date, due_date, currency, subtotal_amount, discount_amount, tax_amount,
        total_amount, paid_amount, status, opened_at, created_at, updated_at
    ) VALUES (
        N'INV-INSP-4B-01', @CustomerId, NULL, @Agr4BId,
        DATEADD(month, -2, @Today), DATEADD(month, -2, @Today),
        N'VND', 7000.00, 0.00, 0.00,
        7000.00, 7000.00, N'paid', DATEADD(month, -2, @Now), DATEADD(month, -2, @Now), @Now
    );
    SET @Inv4BId = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] (invoice_id, line_type, description, quantity, unit_price, metadata)
    VALUES 
    (@Inv4BId, N'deposit', N'Tiền đặt cọc bảo đảm', 1, 7000.00, N'{"deposit":true}');

    DECLARE @Pay4BId BIGINT;
    INSERT INTO [core].[payments] (
        customer_id, target_invoice_id, amount, currency, method,
        provider, provider_transaction_id, idempotency_key, status, paid_at, metadata, created_at, updated_at
    ) VALUES (
        @CustomerId, @Inv4BId, 7000.00, N'VND', N'bank_transfer',
        N'sepay', N'TXN-SEPAY-4B', N'IDEMP-4B', N'succeeded', DATEADD(month, -2, @Now), N'{}', DATEADD(month, -2, @Now), @Now
    );
    SET @Pay4BId = SCOPE_IDENTITY();

    INSERT INTO [core].[payment_allocations] (payment_id, invoice_id, allocated_amount, allocated_at)
    VALUES (@Pay4BId, @Inv4BId, 7000.00, DATEADD(month, -2, @Now));

    -- Tạo refund 'pending' để hiện khung vàng PendingRefundBankInfo trên /my-units
    INSERT INTO [core].[refunds] (
        payment_id, agreement_id, amount, currency, reason, provider,
        idempotency_key, status, requested_by, created_at, updated_at
    ) VALUES (
        @Pay4BId, @Agr4BId, 5000.00, N'VND',
        N'Khấu trừ chi phí thiệt hại 2.000 ₫. Hoàn trả cọc còn lại 5.000 ₫ cho khách hàng.',
        N'bank_transfer', N'REF-IDEMP-4B', N'pending', @CustomerId, @Now, @Now
    );

    PRINT N'>>> Kịch bản 4B: Hợp đồng AGR-2026-INSP-4B (Cọc 7k, hư hại 2k -> Cần hoàn 5.000 ₫. Hiển thị khung vàng PendingRefundBankInfo trên /my-units).';

    -- ==================================================================================
    -- KỊCH BẢN 4C: ĐÃ NHẬP STK -> ĐANG CHỜ MANAGER DUYỆT CHUYỂN KHOẢN (Refund Approvals /manager-refunds)
    -- Ô kho B-101: Cọc 5.000 ₫ (Type 4), Hư hại 2.000 ₫, Thực nhận 3.000 ₫
    -- Refund status: 'pending_manager_payout'
    -- Hiển thị đúng: Tiền cọc 5.000 ₫ | Khấu trừ -2.000 ₫ | Thực nhận 3.000 ₫
    -- ==================================================================================
    DECLARE @Res4CId BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-2026-RF03', @CustomerId, 1, 4,
        @RateType4, DATEADD(month, -3, @Today), DATEADD(month, -2, @Today),
        5000.00, 5000.00, 0.00,
        0.00, 10000.00, DATEADD(minute, 15, DATEADD(month, -3, @Now)), N'confirmed',
        DATEADD(month, -3, @Now), NULL, NULL, DATEADD(month, -3, @Now), @Now
    );
    SET @Res4CId = SCOPE_IDENTITY();

    DECLARE @Agr4CId BIGINT;
    INSERT INTO [core].[rental_agreements] (
        agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, checked_out_at, actual_end_date, created_at, updated_at
    ) VALUES (
        N'AGR-2026-RF03', @Res4CId, @CustomerId, 1, @PolicyVersionId,
        DATEADD(month, -3, @Today), DATEADD(month, -2, @Today), 5000.00, 5000.00, 3000.00,
        N'terminated', DATEADD(month, -3, @Now), DATEADD(month, -3, @Now), @Now, DATEADD(month, -2, @Today), DATEADD(month, -3, @Now), @Now
    );
    SET @Agr4CId = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at, ended_at
    ) VALUES (
        @UnitB101, NULL, @Agr4CId, N'rental',
        DATEADD(month, -3, @Today), DATEADD(month, -2, @Today), N'released', N'Đã kết thúc hợp đồng', @StaffUserId, DATEADD(month, -3, @Now), @Now
    );

    UPDATE [core].[storage_units] SET [physical_status] = N'available' WHERE id = @UnitB101;

    INSERT INTO [core].[move_out_requests] (
        agreement_id, requested_by, requested_move_out_date, appointment_id,
        status, reason, finalized_at, created_at, updated_at
    ) VALUES (
        @Agr4CId, @CustomerId, DATEADD(month, -2, @Today), NULL,
        N'completed', N'Đã hoàn tất thanh lý kho', @Now, DATEADD(day, -3, @Now), @Now
    );

    DECLARE @Insp4CId BIGINT;
    INSERT INTO [core].[inspections] (
        facility_id, storage_unit_id, reservation_id, agreement_id,
        inspected_by, inspection_type, status, overall_condition,
        summary, inspected_at, created_at
    ) VALUES (
        1, @UnitB101, NULL, @Agr4CId,
        @StaffUserId, N'check_out', N'completed', N'acceptable',
        N'Hao mòn ổ khóa và trầy xước vách ngăn.', @Now, @Now
    );
    SET @Insp4CId = SCOPE_IDENTITY();

    INSERT INTO [core].[inspection_items] (
        inspection_id, item_name, condition, notes, photo_url, charge_amount
    ) VALUES (
        @Insp4CId, N'Bảo trì khóa và sơn dặm vách ngăn', N'damaged', N'Khấu trừ chi phí sửa chữa hư hại 2.000 ₫', NULL, 2000.00
    );

    -- Hóa đơn & thanh toán gốc của 4C
    DECLARE @Inv4CId BIGINT;
    INSERT INTO [core].[invoices] (
        invoice_no, customer_id, reservation_id, agreement_id,
        issue_date, due_date, currency, subtotal_amount, discount_amount, tax_amount,
        total_amount, paid_amount, status, opened_at, created_at, updated_at
    ) VALUES (
        N'INV-INSP-4C-01', @CustomerId, NULL, @Agr4CId,
        DATEADD(month, -3, @Today), DATEADD(month, -3, @Today),
        N'VND', 5000.00, 0.00, 0.00,
        5000.00, 5000.00, N'paid', DATEADD(month, -3, @Now), DATEADD(month, -3, @Now), @Now
    );
    SET @Inv4CId = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] (invoice_id, line_type, description, quantity, unit_price, metadata)
    VALUES 
    (@Inv4CId, N'deposit', N'Tiền đặt cọc bảo đảm', 1, 5000.00, N'{"deposit":true}');

    DECLARE @Pay4CId BIGINT;
    INSERT INTO [core].[payments] (
        customer_id, target_invoice_id, amount, currency, method,
        provider, provider_transaction_id, idempotency_key, status, paid_at, metadata, created_at, updated_at
    ) VALUES (
        @CustomerId, @Inv4CId, 5000.00, N'VND', N'bank_transfer',
        N'sepay', N'TXN-SEPAY-4C', N'IDEMP-4C', N'succeeded', DATEADD(month, -3, @Now), N'{}', DATEADD(month, -3, @Now), @Now
    );
    SET @Pay4CId = SCOPE_IDENTITY();

    INSERT INTO [core].[payment_allocations] (payment_id, invoice_id, allocated_amount, allocated_at)
    VALUES (@Pay4CId, @Inv4CId, 5000.00, DATEADD(month, -3, @Now));

    -- Tạo refund 'pending_manager_payout' với JSON STK ngân hàng
    DECLARE @Refund4CId BIGINT;
    INSERT INTO [core].[refunds] (
        payment_id, agreement_id, amount, currency, reason, provider,
        idempotency_key, status, requested_by, created_at, updated_at
    ) VALUES (
        @Pay4CId, @Agr4CId, 3000.00, N'VND',
        N'{"bank":"MBBank (Napas 247)","acc":"0987654321","holder":"KHOI VO","note":"Hoàn cọc sau nghiệm thu khấu trừ 2.000 ₫ hư hại"}',
        N'bank_transfer', N'REF-IDEMP-4C', N'pending_manager_payout', @CustomerId, @Now, @Now
    );
    SET @Refund4CId = SCOPE_IDENTITY();

    PRINT N'>>> Kịch bản 4C: Hợp đồng AGR-2026-RF03 (Cọc 5.000 ₫, Khấu trừ -2.000 ₫, Thực nhận 3.000 ₫ -> Hiển thị chuẩn trên màn hình Duyệt hoàn cọc).';

    -- ==================================================================================
    -- KỊCH BẢN 4D: ĐÃ HOÀN CỌC THÀNH CÔNG (Completed Refund)
    -- Ô kho B-102: Cọc 5.000 ₫ (Type 4), Khấu trừ 2.000 ₫, Thực nhận 3.000 ₫ -> Status: 'completed'
    -- ==================================================================================
    DECLARE @Res4DId BIGINT;
    INSERT INTO [core].[reservations] (
        reservation_code, customer_id, facility_id, unit_type_id,
        facility_rate_id, start_date, end_date,
        monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status,
        confirmed_at, cancelled_at, cancellation_reason, created_at, updated_at
    ) VALUES (
        N'RSV-2026-DM04', @CustomerId, 1, 4,
        @RateType4, DATEADD(month, -4, @Today), DATEADD(month, -1, @Today),
        5000.00, 5000.00, 0.00,
        0.00, 10000.00, DATEADD(minute, 15, DATEADD(month, -4, @Now)), N'confirmed',
        DATEADD(month, -4, @Now), NULL, NULL, DATEADD(month, -4, @Now), @Now
    );
    SET @Res4DId = SCOPE_IDENTITY();

    DECLARE @Agr4DId BIGINT;
    INSERT INTO [core].[rental_agreements] (
        agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, checked_out_at, actual_end_date, created_at, updated_at
    ) VALUES (
        N'AGR-2026-DM04', @Res4DId, @CustomerId, 1, @PolicyVersionId,
        DATEADD(month, -4, @Today), DATEADD(month, -1, @Today), 5000.00, 5000.00, 0.00,
        N'terminated', DATEADD(month, -4, @Now), DATEADD(month, -4, @Now), @Now, DATEADD(month, -1, @Today), DATEADD(month, -4, @Now), @Now
    );
    SET @Agr4DId = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at, ended_at
    ) VALUES (
        @UnitB102, NULL, @Agr4DId, N'rental',
        DATEADD(month, -4, @Today), DATEADD(month, -1, @Today), N'released', N'Đã kết thúc hợp đồng', @StaffUserId, DATEADD(month, -4, @Now), @Now
    );

    UPDATE [core].[storage_units] SET [physical_status] = N'available' WHERE id = @UnitB102;

    -- Hóa đơn & thanh toán gốc của 4D
    DECLARE @Inv4DId BIGINT;
    INSERT INTO [core].[invoices] (
        invoice_no, customer_id, reservation_id, agreement_id,
        issue_date, due_date, currency, subtotal_amount, discount_amount, tax_amount,
        total_amount, paid_amount, status, opened_at, created_at, updated_at
    ) VALUES (
        N'INV-INSP-4D-01', @CustomerId, NULL, @Agr4DId,
        DATEADD(month, -4, @Today), DATEADD(month, -4, @Today),
        N'VND', 5000.00, 0.00, 0.00,
        5000.00, 5000.00, N'paid', DATEADD(month, -4, @Now), DATEADD(month, -4, @Now), @Now
    );
    SET @Inv4DId = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] (invoice_id, line_type, description, quantity, unit_price, metadata)
    VALUES 
    (@Inv4DId, N'deposit', N'Tiền đặt cọc bảo đảm', 1, 5000.00, N'{"deposit":true}');

    DECLARE @Pay4DId BIGINT;
    INSERT INTO [core].[payments] (
        customer_id, target_invoice_id, amount, currency, method,
        provider, provider_transaction_id, idempotency_key, status, paid_at, metadata, created_at, updated_at
    ) VALUES (
        @CustomerId, @Inv4DId, 5000.00, N'VND', N'bank_transfer',
        N'sepay', N'TXN-SEPAY-4D', N'IDEMP-4D', N'succeeded', DATEADD(month, -4, @Now), N'{}', DATEADD(month, -4, @Now), @Now
    );
    SET @Pay4DId = SCOPE_IDENTITY();

    INSERT INTO [core].[payment_allocations] (payment_id, invoice_id, allocated_amount, allocated_at)
    VALUES (@Pay4DId, @Inv4DId, 5000.00, DATEADD(month, -4, @Now));

    DECLARE @Refund4DId BIGINT;
    INSERT INTO [core].[refunds] (
        payment_id, agreement_id, amount, currency, reason, provider,
        idempotency_key, status, requested_by, refunded_at, created_at, updated_at
    ) VALUES (
        @Pay4DId, @Agr4DId, 3000.00, N'VND',
        N'{"bank":"Vietcombank (VCB)","acc":"0123456789","holder":"KHOI VO","note":"Đã thanh lý & hoàn cọc thành công"}',
        N'bank_transfer', N'REF-IDEMP-4D', N'completed', @CustomerId, DATEADD(day, -10, @Now), DATEADD(day, -10, @Now), @Now
    );
    SET @Refund4DId = SCOPE_IDENTITY();

    INSERT INTO [core].[refund_approvals] (
        refund_id, decision, decided_by, reason, decided_at
    ) VALUES (
        @Refund4DId, N'approved', @ManagerUserId, N'Đã đối soát và chuyển khoản hoàn cọc 3.000 ₫ thành công qua Vietcombank.', DATEADD(day, -10, @Now)
    );

    PRINT N'>>> Kịch bản 4D: Hợp đồng AGR-2026-DM04 (Đã hoàn cọc completed).';

    -- 9. BẬT LẠI TẤT CẢ TRIGGERS
    ALTER TABLE [core].[access_credentials] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[facility_rates] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[price_ranges] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[delinquency_cases] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[handover_records] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[inspection_items] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[inspections] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[invoice_lines] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[invoices] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[move_out_requests] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[payment_allocations] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[payments] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[promotion_redemptions] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[refunds] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[rental_agreements] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[rental_renewals] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[reservations] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[storage_units] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[stored_items] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[support_tickets] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[unit_allocations] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[unit_transfer_requests] ENABLE TRIGGER ALL;

    COMMIT TRANSACTION;
    PRINT N'========================================================================';
    PRINT N'>>> BỘ DATA SEED TEST FLOWS ĐÃ ĐƯỢC CẬP NHẬT THÀNH CÔNG 100%! <<<';
    PRINT N'========================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    
    ALTER TABLE [core].[access_credentials] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[facility_rates] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[price_ranges] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[delinquency_cases] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[handover_records] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[inspection_items] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[inspections] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[invoice_lines] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[invoices] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[move_out_requests] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[payment_allocations] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[payments] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[promotion_redemptions] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[refunds] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[rental_agreements] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[rental_renewals] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[reservations] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[storage_units] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[stored_items] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[support_tickets] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[unit_allocations] ENABLE TRIGGER ALL;
    ALTER TABLE [core].[unit_transfer_requests] ENABLE TRIGGER ALL;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    PRINT N'LỖI THỰC THI: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
