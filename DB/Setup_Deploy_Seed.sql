-- ======================================================================================
-- SCRIPT RESET & SEED DATA CHUẨN CHO DATABASE DEPLOY (SelfStoragePRN222)
-- Chạy được trực tiếp trên Somee SQL Server (SSMS) mà không cần quyền DROP DATABASE server-level.
-- 
-- Bao gồm:
-- 1. Hai cơ sở: Thủ Đức (ID=1) & Cầu Giấy (ID=6)
-- 2. Đúng 10 ô kho mỗi cơ sở (A-101..A-105, B-101..B-105)
-- 3. 5 loại kho với biểu giá từ 1.000đ - 2.000đ; giá 100đ / m2 cho mỗi ô kho
-- 4. Đầy đủ các tài khoản mẫu (TẤT CẢ MẬT KHẨU ĐỀU LÀ 1):
--    - admin@gmail.com     (Quản trị viên hệ thống)
--    - manager@gmail.com   (Quản lý cơ sở Thủ Đức)
--    - manager1@gmail.com  (Quản lý cơ sở Cầu Giấy)
--    - staff@gmail.com     (Nhân viên cơ sở Thủ Đức)
--    - staff1@gmail.com    (Nhân viên cơ sở Cầu Giấy)
--    - customer@gmail.com  (Khách hàng có ô kho A-101 trễ hạn 1 ngày và chưa thanh toán)
-- ======================================================================================

USE [SelfStoragePRN222];
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT N'>>> 1. Vô hiệu hóa constraints và triggers để làm sạch dữ liệu...';
    EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
    EXEC sp_msforeachtable 'ALTER TABLE ? DISABLE TRIGGER ALL';

    PRINT N'>>> 2. Xóa sạch dữ liệu giao dịch cũ...';
    DELETE FROM [core].[ticket_messages];
    DELETE FROM [core].[ticket_attachments];
    DELETE FROM [core].[ticket_assignments];
    DELETE FROM [core].[ticket_charge_approvals];
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
    DELETE FROM [core].[invoice_lines];
    DELETE FROM [core].[invoices];
    DELETE FROM [core].[promotion_redemptions];

    DELETE FROM [core].[handover_records];
    DELETE FROM [core].[inspection_items];
    DELETE FROM [core].[inspections];

    DELETE FROM [core].[access_events];
    DELETE FROM [core].[access_credentials];
    DELETE FROM [core].[authorized_access_members];
    DELETE FROM [core].[stored_items];

    DELETE FROM [core].[appointments];
    DELETE FROM [core].[identity_verifications];
    DELETE FROM [core].[move_out_requests];
    DELETE FROM [core].[unit_transfer_requests];
    DELETE FROM [core].[rental_renewals];
    DELETE FROM [core].[unit_allocations];
    DELETE FROM [core].[rental_agreements];
    DELETE FROM [core].[reservations];

    DELETE FROM [core].[notifications];
    DELETE FROM [core].[integration_events];
    DELETE FROM [core].[unit_status_history];

    PRINT N'>>> 3. Chuẩn hóa 2 cơ sở: Thủ Đức & Cầu Giấy...';
    -- Cơ sở 1: Thủ Đức
    UPDATE [core].[facilities]
    SET [code] = 'HCM-TD',
        [name] = N'Kho Thủ Đức - G1 Storage Hồ Chí Minh',
        [address_line] = N'01 Võ Văn Ngân',
        [ward] = N'Linh Chiểu',
        [district] = N'Thủ Đức',
        [city] = N'Hồ Chí Minh',
        [status] = 'active',
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] = 1;

    -- Cơ sở 6: Cầu Giấy (Kích hoạt active)
    UPDATE [core].[facilities]
    SET [code] = 'HN-CG',
        [name] = N'Kho Cầu Giấy - Hà Nội Capital Storage',
        [address_line] = N'88 Cầu Giấy',
        [ward] = N'Quan Hoa',
        [district] = N'Cầu Giấy',
        [city] = N'Hà Nội',
        [status] = 'active',
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] = 6;

    -- Vô hiệu hóa tất cả các cơ sở khác (để hệ thống chỉ hiển thị đúng 2 cơ sở)
    UPDATE [core].[facilities]
    SET [status] = 'inactive',
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] NOT IN (1, 6);

    PRINT N'>>> 4. Chuẩn hóa 5 loại kho & biểu giá (1.000đ - 2.000đ, 100đ/m2)...';
    -- Cập nhật price_ranges từ 1.000đ đến 2.000đ
    UPDATE [core].[price_ranges]
    SET [min_monthly_rate] = 1000.00,
        [max_monthly_rate] = 2000.00;

    -- Biểu giá cơ sở (facility_rates) từ 1.000đ - 2.000đ cho cả cơ sở 1 và 6
    -- S-DRY (1): 1.000đ
    -- M-DRY (2): 1.200đ
    -- M-SEAFOOD (3): 1.600đ
    -- L-WARM (4): 1.800đ
    -- XL-CLIMATE (5): 2.000đ
    -- MINI-DRY (6): 1.000đ
    -- S-SEAFOOD (7): 1.300đ
    -- M-WARM (8): 1.400đ
    UPDATE [core].[facility_rates]
    SET [monthly_rate] = CASE [unit_type_id]
            WHEN 1 THEN 1000.00
            WHEN 2 THEN 1200.00
            WHEN 3 THEN 1600.00
            WHEN 4 THEN 1800.00
            WHEN 5 THEN 2000.00
            WHEN 6 THEN 1000.00
            WHEN 7 THEN 1300.00
            WHEN 8 THEN 1400.00
            ELSE 1500.00
        END,
        [deposit_amount] = CASE [unit_type_id]
            WHEN 1 THEN 1000.00
            WHEN 2 THEN 1200.00
            WHEN 3 THEN 1600.00
            WHEN 4 THEN 1800.00
            WHEN 5 THEN 2000.00
            WHEN 6 THEN 1000.00
            WHEN 7 THEN 1300.00
            WHEN 8 THEN 1400.00
            ELSE 1500.00
        END
    WHERE [facility_id] IN (1, 6);

    PRINT N'>>> 5. Chuẩn hóa đúng 10 ô kho mỗi cơ sở (A-101..A-105, B-101..B-105) với giá 100đ/m2...';
    -- Xóa các ô kho phụ ngoài 10 ô chuẩn nếu có ở cơ sở 1
    DELETE FROM [core].[storage_units] WHERE [facility_id] = 1 AND [id] NOT IN (121, 122, 123, 124, 125, 126, 127, 128, 129, 130);

    -- Ẩn tất cả các ô kho ở các cơ sở không kích hoạt
    UPDATE [core].[storage_units]
    SET [is_listed] = 0,
        [updated_at] = SYSUTCDATETIME()
    WHERE [facility_id] NOT IN (1, 6);

    -- Đặt base_price_per_sqm = 100đ cho tất cả ô kho tại Thủ Đức (1) và Cầu Giấy (6)
    UPDATE [core].[storage_units]
    SET [base_price_per_sqm] = 100.00,
        [physical_status] = 'available',
        [is_listed] = 1,
        [custom_monthly_rate] = CASE 
            WHEN [custom_area_m2] IS NOT NULL THEN ([custom_area_m2] * 100.00) + ISNULL([category_surcharge], 0)
            ELSE 1000.00
        END,
        [updated_at] = SYSUTCDATETIME()
    WHERE [facility_id] IN (1, 6);

    -- Riêng ô kho A-101 (ID=121, cơ sở 1 Thủ Đức) sẽ được gán cho hợp đồng hết hạn -> đặt occupied
    UPDATE [core].[storage_units]
    SET [physical_status] = 'occupied',
        [is_listed] = 1,
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] = 121;

    PRINT N'>>> 6. Tạo/Cập nhật các tài khoản với MẬT KHẨU = 1...';
    -- Mật khẩu "1" (BCrypt factor 11): $2a$11$HWcRgUvFaROkssbvAG.1COvFAbaL1dHcF0tCNYHP5B3vyCsdSGZ0O
    DECLARE @PasswordHash NVARCHAR(255) = N'$2a$11$HWcRgUvFaROkssbvAG.1COvFAbaL1dHcF0tCNYHP5B3vyCsdSGZ0O';

    -- 6.1. ADMIN (admin@gmail.com)
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'admin@gmail.com')
    BEGIN
        INSERT INTO [core].[users] ([email], [phone_number], [password_hash], [status], [created_at], [updated_at])
        VALUES ('admin@gmail.com', '0909000001', @PasswordHash, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'admin@gmail.com';
    END

    DECLARE @AdminId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'admin@gmail.com');
    DELETE FROM [core].[user_roles] WHERE [user_id] = @AdminId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@AdminId, 5); -- system_administrator

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @AdminId)
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@AdminId, 'ADM-001', N'Lê Hoàng Long', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
        UPDATE [core].[employee_profiles] SET [full_name] = N'Lê Hoàng Long', [employment_status] = 'active' WHERE [user_id] = @AdminId;

    -- 6.2. MANAGER THỦ ĐỨC (manager@gmail.com) -> Cơ sở 1
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'manager@gmail.com')
    BEGIN
        INSERT INTO [core].[users] ([email], [phone_number], [password_hash], [status], [created_at], [updated_at])
        VALUES ('manager@gmail.com', '0909000002', @PasswordHash, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'manager@gmail.com';
    END

    DECLARE @ManagerId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'manager@gmail.com');
    DELETE FROM [core].[user_roles] WHERE [user_id] = @ManagerId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@ManagerId, 3); -- facility_manager

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @ManagerId)
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@ManagerId, 'MGR-HCM-TD', N'Nguyễn Văn An (Quản lý Thủ Đức)', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
        UPDATE [core].[employee_profiles] SET [full_name] = N'Nguyễn Văn An (Quản lý Thủ Đức)', [employment_status] = 'active' WHERE [user_id] = @ManagerId;

    DELETE FROM [core].[staff_facility_assignments] WHERE [employee_id] = @ManagerId;
    INSERT INTO [core].[staff_facility_assignments] ([employee_id], [facility_id], [assignment_role], [starts_at], [assigned_by], [created_at])
    VALUES (@ManagerId, 1, 'facility_manager', '2026-01-01', @AdminId, SYSUTCDATETIME());

    -- 6.3. MANAGER CẦU GIẤY (manager1@gmail.com) -> Cơ sở 6
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'manager1@gmail.com')
    BEGIN
        INSERT INTO [core].[users] ([email], [phone_number], [password_hash], [status], [created_at], [updated_at])
        VALUES ('manager1@gmail.com', '0909000004', @PasswordHash, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'manager1@gmail.com';
    END

    DECLARE @Manager1Id BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'manager1@gmail.com');
    DELETE FROM [core].[user_roles] WHERE [user_id] = @Manager1Id;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@Manager1Id, 3); -- facility_manager

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @Manager1Id)
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@Manager1Id, 'MGR-HN-CG', N'Trần Văn Bình (Quản lý Cầu Giấy)', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
        UPDATE [core].[employee_profiles] SET [full_name] = N'Trần Văn Bình (Quản lý Cầu Giấy)', [employment_status] = 'active' WHERE [user_id] = @Manager1Id;

    DELETE FROM [core].[staff_facility_assignments] WHERE [employee_id] = @Manager1Id;
    INSERT INTO [core].[staff_facility_assignments] ([employee_id], [facility_id], [assignment_role], [starts_at], [assigned_by], [created_at])
    VALUES (@Manager1Id, 6, 'facility_manager', '2026-01-01', @AdminId, SYSUTCDATETIME());

    -- 6.4. STAFF THỦ ĐỨC (staff@gmail.com) -> Cơ sở 1
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'staff@gmail.com')
    BEGIN
        INSERT INTO [core].[users] ([email], [phone_number], [password_hash], [status], [created_at], [updated_at])
        VALUES ('staff@gmail.com', '0909000003', @PasswordHash, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'staff@gmail.com';
    END

    DECLARE @StaffId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'staff@gmail.com');
    DELETE FROM [core].[user_roles] WHERE [user_id] = @StaffId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@StaffId, 2); -- facility_staff

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @StaffId)
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@StaffId, 'STF-HCM-TD', N'Lê Thị Mai (Nhân viên Thủ Đức)', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
        UPDATE [core].[employee_profiles] SET [full_name] = N'Lê Thị Mai (Nhân viên Thủ Đức)', [employment_status] = 'active' WHERE [user_id] = @StaffId;

    DELETE FROM [core].[staff_facility_assignments] WHERE [employee_id] = @StaffId;
    INSERT INTO [core].[staff_facility_assignments] ([employee_id], [facility_id], [assignment_role], [starts_at], [assigned_by], [created_at])
    VALUES (@StaffId, 1, 'facility_staff', '2026-01-01', @AdminId, SYSUTCDATETIME());

    -- 6.5. STAFF CẦU GIẤY (staff1@gmail.com) -> Cơ sở 6
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'staff1@gmail.com')
    BEGIN
        INSERT INTO [core].[users] ([email], [phone_number], [password_hash], [status], [created_at], [updated_at])
        VALUES ('staff1@gmail.com', '0909000005', @PasswordHash, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'staff1@gmail.com';
    END

    DECLARE @Staff1Id BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'staff1@gmail.com');
    DELETE FROM [core].[user_roles] WHERE [user_id] = @Staff1Id;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@Staff1Id, 2); -- facility_staff

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @Staff1Id)
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@Staff1Id, 'STF-HN-CG', N'Phạm Minh Tuấn (Nhân viên Cầu Giấy)', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
        UPDATE [core].[employee_profiles] SET [full_name] = N'Phạm Minh Tuấn (Nhân viên Cầu Giấy)', [employment_status] = 'active' WHERE [user_id] = @Staff1Id;

    DELETE FROM [core].[staff_facility_assignments] WHERE [employee_id] = @Staff1Id;
    INSERT INTO [core].[staff_facility_assignments] ([employee_id], [facility_id], [assignment_role], [starts_at], [assigned_by], [created_at])
    VALUES (@Staff1Id, 6, 'facility_staff', '2026-01-01', @AdminId, SYSUTCDATETIME());

    -- 6.6. CUSTOMER (customer@gmail.com)
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'customer@gmail.com')
    BEGIN
        INSERT INTO [core].[users] ([email], [phone_number], [password_hash], [status], [created_at], [updated_at])
        VALUES ('customer@gmail.com', '0901234567', @PasswordHash, 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'customer@gmail.com';
    END

    DECLARE @CustomerId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'customer@gmail.com');
    DELETE FROM [core].[user_roles] WHERE [user_id] = @CustomerId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@CustomerId, 1); -- storage_customer

    IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE [user_id] = @CustomerId)
        INSERT INTO [core].[customer_profiles] ([user_id], [full_name], [identity_number], [address], [created_at], [updated_at])
        VALUES (@CustomerId, N'Võ Thái Hoàng Khôi', '079123456789', N'Thành phố Thủ Đức, TP. Hồ Chí Minh', SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
        UPDATE [core].[customer_profiles] SET [full_name] = N'Võ Thái Hoàng Khôi', [identity_number] = '079123456789', [address] = N'Thành phố Thủ Đức, TP. Hồ Chí Minh' WHERE [user_id] = @CustomerId;

    -- Đồng bộ luôn tài khoản yu9696000@gmail.com (nếu có)
    IF EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'yu9696000@gmail.com')
    BEGIN
        UPDATE [core].[users] SET [password_hash] = @PasswordHash, [status] = 'active' WHERE [email] = 'yu9696000@gmail.com';
        DECLARE @YuId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'yu9696000@gmail.com');
        DELETE FROM [core].[user_roles] WHERE [user_id] = @YuId;
        INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@YuId, 1);
    END

    PRINT N'>>> 7. Tạo hợp đồng thuê TRỄ HẠN ĐÚNG 1 NGÀY cho customer@gmail.com...';
    -- Tính toán ngày: Ngày hết hạn hợp đồng = hôm qua (trễ hạn đúng 1 ngày so với ngày hiện tại)
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    DECLARE @Yesterday DATE = DATEADD(DAY, -1, @Today);
    DECLARE @StartDate DATE = DATEADD(MONTH, -1, @Yesterday);

    -- 7.1. Đơn đặt chỗ (converted)
    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    )
    VALUES (
        'RES-HCM-OVERDUE-01', @CustomerId, 1, 1, 97,
        @StartDate, @Yesterday, 1000.00, 1000.00, 0.00,
        0.00, 2000.00, DATEADD(MINUTE, 15, CAST(@StartDate AS DATETIME2)), 'converted',
        CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2)
    );

    DECLARE @ResId BIGINT = SCOPE_IDENTITY();

    -- 7.2. Hợp đồng thuê A-101 (Thủ Đức): Bắt đầu @StartDate, hết hạn @Yesterday -> TRỄ HẠN ĐÚNG 1 NGÀY
    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    )
    VALUES (
        'AGR-HCM-OVERDUE-01', @ResId, @CustomerId, 1, 1,
        @StartDate, @Yesterday, 1000.00, 1000.00, 1000.00,
        'overdue', CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2),
        CAST(@StartDate AS DATETIME2), SYSUTCDATETIME()
    );

    DECLARE @AgrId BIGINT = SCOPE_IDENTITY();

    -- 7.3. Phân bổ ô kho A-101 (ID=121) cho hợp đồng này
    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind],
        [allocation_start_date], [allocation_end_date], [status],
        [assigned_by], [created_at]
    )
    VALUES (
        121, @AgrId, 'primary',
        @StartDate, @Yesterday, 'active',
        @StaffId, CAST(@StartDate AS DATETIME2)
    );

    -- 7.4. Mã PIN mở kho: Trạng thái suspended (bị khóa cửa do trễ hạn thanh toán)
    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint],
        [status], [issued_at], [issued_by], [created_at]
    )
    VALUES (
        @AgrId, 'pin_code', '123456', '123456',
        'suspended', CAST(@StartDate AS DATETIME2), @StaffId, CAST(@StartDate AS DATETIME2)
    );

    -- 7.5. Hóa đơn 1 (Tháng đầu + Cọc): Đã thanh toán đầy đủ lúc ký hợp đồng
    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period],
        [issue_date], [due_date], [currency], [subtotal_amount], [discount_amount], [tax_amount],
        [total_amount], [paid_amount], [status], [opened_at], [created_at], [updated_at]
    )
    VALUES (
        'INV-INIT-OVERDUE-01', @CustomerId, @AgrId, 'INITIAL_PERIOD',
        @StartDate, @StartDate, 'VND', 2000.00, 0.00, 0.00,
        2000.00, 2000.00, 'paid', CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2)
    );

    DECLARE @Inv1Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price])
    VALUES
        (@Inv1Id, 'rental', N'Tiền thuê tháng đầu ô kho A-101', 1, 1000.00),
        (@Inv1Id, 'deposit', N'Tiền cọc bảo đảm ô kho A-101', 1, 1000.00);

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency],
        [method], [provider], [provider_transaction_id], [idempotency_key],
        [status], [paid_at], [created_at], [updated_at]
    )
    VALUES (
        @CustomerId, @Inv1Id, 2000.00, 'VND',
        'vnpay', 'VNPAY', 'PAY-INIT-001', 'IDEMP-INIT-001',
        'completed', CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2), CAST(@StartDate AS DATETIME2)
    );

    DECLARE @PayId BIGINT = SCOPE_IDENTITY();
    INSERT INTO [core].[payment_allocations] ([payment_id], [invoice_id], [allocated_amount], [allocated_at])
    VALUES (@PayId, @Inv1Id, 2000.00, CAST(@StartDate AS DATETIME2));

    -- 7.6. Hóa đơn 2 (Hóa đơn gia hạn tự động): Đến hạn hôm qua (@Yesterday), CHƯA THANH TOÁN -> TRỄ HẠN 1 NGÀY
    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period],
        [issue_date], [due_date], [currency], [subtotal_amount], [discount_amount], [tax_amount],
        [total_amount], [paid_amount], [status], [opened_at], [created_at], [updated_at]
    )
    VALUES (
        'INV-RENEW-OVERDUE-01', @CustomerId, @AgrId, 'RENEWAL_MONTH_2',
        DATEADD(DAY, -7, @Yesterday), @Yesterday, 'VND', 1000.00, 0.00, 0.00,
        1000.00, 0.00, 'overdue', CAST(DATEADD(DAY, -7, @Yesterday) AS DATETIME2), CAST(DATEADD(DAY, -7, @Yesterday) AS DATETIME2), SYSUTCDATETIME()
    );

    DECLARE @Inv2Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price])
    VALUES
        (@Inv2Id, 'rental', N'Cước thuê gia hạn ô kho A-101 (Kỳ 2)', 1, 1000.00);

    -- 7.7. Tạo vụ nợ quá hạn Delinquency Case
    INSERT INTO [core].[delinquency_cases] (
        [agreement_id], [invoice_id], [status], [opened_at], [grace_ends_at], [outstanding_snapshot], [notes]
    )
    VALUES (
        @AgrId, @Inv2Id, 'active', CAST(@Yesterday AS DATETIME2), CAST(@Today AS DATETIME2), 1000.00,
        N'Hợp đồng quá hạn 1 ngày. Chưa thanh toán hóa đơn gia hạn INV-RENEW-OVERDUE-01.'
    );

    -- 7.8. Thông báo gửi đến khách hàng
    INSERT INTO [core].[notifications] (
        [user_id], [channel], [template_code], [subject], [payload], [status], [scheduled_at], [sent_at], [attempts], [created_at]
    )
    VALUES (
        @CustomerId, 'in_app', 'AGREEMENT_OVERDUE',
        N'Cảnh báo: Hợp đồng thuê ô kho A-101 đã quá hạn thanh toán 1 ngày',
        N'{"agreementId":' + CAST(@AgrId AS NVARCHAR(20)) + N', "unitCode":"A-101", "daysPastDue":1, "amountDue":1000}',
        'sent', CAST(@Yesterday AS DATETIME2), CAST(@Yesterday AS DATETIME2), 1, CAST(@Yesterday AS DATETIME2)
    );

    PRINT N'>>> 8. Bật lại constraints và triggers...';
    EXEC sp_msforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';
    EXEC sp_msforeachtable 'ALTER TABLE ? ENABLE TRIGGER ALL';

    COMMIT TRANSACTION;
    PRINT N'======================================================================';
    PRINT N'>>> THIẾT LẬP DATABASE VÀ TẠO DỮ LIỆU SEED THÀNH CÔNG 100%!';
    PRINT N'======================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@ErrMsg, 16, 1);
END CATCH;
