-- ======================================================================================
-- SCRIPT THIẾT LẬP HỆ THỐNG G1SELFSTORAGE CHUẨN:
-- 1. Hai cơ sở: Thủ Đức (ID=1) & Quận 1 (ID=2)
-- 2. Đúng 10 ô kho mỗi cơ sở (A-101..A-105, B-101..B-105)
-- 3. Bốn tài khoản chuẩn mật khẩu "1" (admin, manager, staff, customer)
--    + Quản lý (manager) chỉ quản lý đúng 1 cơ sở Thủ Đức
--    + Nhân viên (staff) phân công cơ sở Thủ Đức
--    + Khách hàng (customer) có 1 ô kho đang thuê và ĐÃ HẾT HẠN (Overdue)
-- ======================================================================================

USE [SelfStoragePRN222];
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT N'>>> 1. Vô hiệu hóa constraints và triggers...';
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

    PRINT N'>>> 3. Chuẩn hóa 2 cơ sở (Thủ Đức & Quận 1)...';
    -- Đặt cơ sở 1 (Thủ Đức) & cơ sở 2 (Quận 1) là 'active'
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

    UPDATE [core].[facilities]
    SET [code] = 'HCM-Q1',
        [name] = N'Kho Quận 1 - Bến Nghé Central Storage',
        [address_line] = N'12 Lê Duẩn',
        [ward] = N'Bến Nghé',
        [district] = N'Quận 1',
        [city] = N'Hồ Chí Minh',
        [status] = 'active',
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] = 2;

    -- Vô hiệu hóa các cơ sở còn lại (3..10) để hệ thống chỉ hoạt động trên 2 cơ sở này
    UPDATE [core].[facilities]
    SET [status] = 'inactive',
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] NOT IN (1, 2);

    PRINT N'>>> 4. Chuẩn hóa 10 ô kho mỗi cơ sở...';
    -- Xóa 2 ô kho 918, 919 ở cơ sở 1 (nếu có)
    DELETE FROM [core].[storage_units] WHERE [id] IN (918, 919);

    -- Ẩn các ô kho của các cơ sở không kích hoạt
    UPDATE [core].[storage_units]
    SET [is_listed] = 0,
        [updated_at] = SYSUTCDATETIME()
    WHERE [facility_id] NOT IN (1, 2);

    -- Đặt 19 ô kho về available, is_listed = 1
    UPDATE [core].[storage_units]
    SET [physical_status] = 'available',
        [is_listed] = 1,
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] IN (122,123,124,125,126,127,128,129,130, 131,132,133,134,135,136,137,138,139,140);

    -- Ô kho A-101 (ID=121, cơ sở 1 Thủ Đức) là ô kho đang thuê và hết hạn -> đặt occupied
    UPDATE [core].[storage_units]
    SET [physical_status] = 'occupied',
        [is_listed] = 1,
        [updated_at] = SYSUTCDATETIME()
    WHERE [id] = 121;

    PRINT N'>>> 5. Thiết lập 4 tài khoản chuẩn với mật khẩu = 1...';
    -- Mật khẩu "1" (BCrypt work factor 11): $2a$11$HWcRgUvFaROkssbvAG.1COvFAbaL1dHcF0tCNYHP5B3vyCsdSGZ0O
    DECLARE @PasswordHash NVARCHAR(255) = N'$2a$11$HWcRgUvFaROkssbvAG.1COvFAbaL1dHcF0tCNYHP5B3vyCsdSGZ0O';

    -- 5.1. Admin (admin@gmail.com)
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

    DECLARE @AdminUserId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'admin@gmail.com');

    -- Đảm bảo quyền system_administrator (role_id = 5)
    DELETE FROM [core].[user_roles] WHERE [user_id] = @AdminUserId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@AdminUserId, 5);

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @AdminUserId)
    BEGIN
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@AdminUserId, 'ADM-001', N'Hệ Thống Admin', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[employee_profiles]
        SET [full_name] = N'Hệ Thống Admin', [employment_status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [user_id] = @AdminUserId;
    END

    -- 5.2. Manager (manager@gmail.com) -> quản lý đúng 1 cơ sở Thủ Đức (ID=1)
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

    DECLARE @ManagerUserId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'manager@gmail.com');

    -- Đảm bảo quyền facility_manager (role_id = 3)
    DELETE FROM [core].[user_roles] WHERE [user_id] = @ManagerUserId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@ManagerUserId, 3);

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @ManagerUserId)
    BEGIN
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@ManagerUserId, 'MGR-HCM-TD', N'Quản Lý Thủ Đức', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[employee_profiles]
        SET [full_name] = N'Quản Lý Thủ Đức', [employment_status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [user_id] = @ManagerUserId;
    END

    -- Phân công DUY NHẤT cho cơ sở 1 (Thủ Đức)
    DELETE FROM [core].[staff_facility_assignments] WHERE [employee_id] = @ManagerUserId;
    INSERT INTO [core].[staff_facility_assignments] ([employee_id], [facility_id], [assignment_role], [starts_at], [assigned_by], [created_at])
    VALUES (@ManagerUserId, 1, 'facility_manager', '2026-01-01', @AdminUserId, SYSUTCDATETIME());

    -- 5.3. Staff (staff@gmail.com) -> nhân viên cơ sở Thủ Đức (ID=1)
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

    DECLARE @StaffUserId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'staff@gmail.com');

    -- Đảm bảo quyền facility_staff (role_id = 2)
    DELETE FROM [core].[user_roles] WHERE [user_id] = @StaffUserId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@StaffUserId, 2);

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE [user_id] = @StaffUserId)
    BEGIN
        INSERT INTO [core].[employee_profiles] ([user_id], [employee_code], [full_name], [hire_date], [employment_status], [created_at], [updated_at])
        VALUES (@StaffUserId, 'STF-HCM-TD', N'Nhân Viên Thủ Đức', '2026-01-01', 'active', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[employee_profiles]
        SET [full_name] = N'Nhân Viên Thủ Đức', [employment_status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [user_id] = @StaffUserId;
    END

    DELETE FROM [core].[staff_facility_assignments] WHERE [employee_id] = @StaffUserId;
    INSERT INTO [core].[staff_facility_assignments] ([employee_id], [facility_id], [assignment_role], [starts_at], [assigned_by], [created_at])
    VALUES (@StaffUserId, 1, 'facility_staff', '2026-01-01', @AdminUserId, SYSUTCDATETIME());

    -- 5.4. Customer (customer@gmail.com)
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

    DECLARE @CustomerUserId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'customer@gmail.com');

    -- Đảm bảo quyền storage_customer (role_id = 1)
    DELETE FROM [core].[user_roles] WHERE [user_id] = @CustomerUserId;
    INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@CustomerUserId, 1);

    IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE [user_id] = @CustomerUserId)
    BEGIN
        INSERT INTO [core].[customer_profiles] ([user_id], [full_name], [identity_number], [address], [created_at], [updated_at])
        VALUES (@CustomerUserId, N'Khách Hàng Mẫu', '079123456789', N'Hồ Chí Minh', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE [core].[customer_profiles]
        SET [full_name] = N'Khách Hàng Mẫu', [identity_number] = '079123456789', [address] = N'Hồ Chí Minh', [updated_at] = SYSUTCDATETIME()
        WHERE [user_id] = @CustomerUserId;
    END

    -- Đồng bộ cả tài khoản cá nhân của bạn yu9696000@gmail.com mật khẩu = 1
    IF EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'yu9696000@gmail.com')
    BEGIN
        UPDATE [core].[users]
        SET [password_hash] = @PasswordHash, [status] = 'active', [updated_at] = SYSUTCDATETIME()
        WHERE [email] = 'yu9696000@gmail.com';

        DECLARE @YuUserId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'yu9696000@gmail.com');
        DELETE FROM [core].[user_roles] WHERE [user_id] = @YuUserId;
        INSERT INTO [core].[user_roles] ([user_id], [role_id]) VALUES (@YuUserId, 1);
    END

    PRINT N'>>> 6. Tạo hợp đồng thuê ĐÃ HẾT HẠN (Overdue) cho khách hàng...';
    -- 6.1. Tạo đơn đặt chỗ (Reservation) đã chuyển đổi thành hợp đồng
    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    )
    VALUES (
        'RES-HCM-20261010-00101', @CustomerUserId, 1, 1, 97,
        '2026-10-10', '2026-11-10', 2000.00, 2000.00, 0.00,
        0.00, 4000.00, '2026-10-10 12:00:00', 'converted', '2026-10-10 08:30:00', '2026-10-10 08:00:00', '2026-10-10 08:30:00'
    );

    DECLARE @ReservationId BIGINT = SCOPE_IDENTITY();

    -- Hợp đồng thuê A-101 (Thủ Đức): Bắt đầu 10/10/2026, Hết hạn 10/11/2026 (hôm nay 18/11/2026 -> Đã quá hạn 8 ngày)
    -- Trạng thái: overdue
    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    )
    VALUES (
        'AGR-HCM-20261010-00101', @ReservationId, @CustomerUserId, 1, 1,
        '2026-10-10', '2026-11-10', 2000.00, 2000.00, 2000.00,
        'overdue', '2026-10-10 08:30:00', '2026-10-10 09:00:00',
        '2026-10-10 08:30:00', SYSUTCDATETIME()
    );

    DECLARE @AgreementId BIGINT = SCOPE_IDENTITY();

    -- Phân bổ ô kho A-101 (ID=121) cho hợp đồng
    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind],
        [allocation_start_date], [allocation_end_date], [status],
        [assigned_by], [created_at]
    )
    VALUES (
        121, @AgreementId, 'primary',
        '2026-10-10', '2026-11-10', 'active',
        @StaffUserId, '2026-10-10 09:00:00'
    );

    -- Mã PIN mở kho: Bị tạm khóa ('suspended') do hợp đồng đã quá hạn theo đúng nghiệp vụ
    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint],
        [status], [issued_at], [issued_by], [created_at]
    )
    VALUES (
        @AgreementId, 'pin_code', '123456', '123456',
        'suspended', '2026-10-10 09:00:00', @StaffUserId, '2026-10-10 09:00:00'
    );

    -- Hóa đơn 1: Tiền thuê tháng đầu + tiền cọc (Đã thanh toán đầy đủ lúc bắt đầu hợp đồng)
    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period],
        [issue_date], [due_date], [currency], [subtotal_amount], [discount_amount], [tax_amount],
        [total_amount], [paid_amount], [status], [opened_at], [created_at], [updated_at]
    )
    VALUES (
        'INV-20261010-00101', @CustomerUserId, @AgreementId, '2026-10-10_2026-11-10',
        '2026-10-10', '2026-10-10', 'VND', 4000.00, 0.00, 0.00,
        4000.00, 4000.00, 'paid', '2026-10-10 08:30:00', '2026-10-10 08:30:00', '2026-10-10 08:35:00'
    );

    DECLARE @Invoice1Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price])
    VALUES
        (@Invoice1Id, 'rental', N'Tiền thuê tháng đầu ô kho A-101 (10/10/2026 - 10/11/2026)', 1, 2000.00),
        (@Invoice1Id, 'deposit', N'Tiền đặt cọc bảo đảm ô kho A-101', 1, 2000.00);

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency],
        [method], [provider], [provider_transaction_id], [idempotency_key],
        [status], [paid_at], [created_at], [updated_at]
    )
    VALUES (
        @CustomerUserId, @Invoice1Id, 4000.00, 'VND',
        'vnpay', 'VNPAY', 'VNPAY-20261010-001', 'IDEMP-20261010-001',
        'completed', '2026-10-10 08:35:00', '2026-10-10 08:30:00', '2026-10-10 08:35:00'
    );

    DECLARE @PaymentId BIGINT = SCOPE_IDENTITY();

    INSERT INTO [core].[payment_allocations] ([payment_id], [invoice_id], [allocated_amount], [allocated_at])
    VALUES (@PaymentId, @Invoice1Id, 4000.00, '2026-10-10 08:35:00');

    -- Hóa đơn 2: Hóa đơn gia hạn tự động ĐÃ QUÁ HẠN (Overdue, chưa thanh toán cước thuê tiếp theo 2.000 đ)
    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period],
        [issue_date], [due_date], [currency], [subtotal_amount], [discount_amount], [tax_amount],
        [total_amount], [paid_amount], [status], [opened_at], [created_at], [updated_at]
    )
    VALUES (
        'INV-AUTO-101-20261110', @CustomerUserId, @AgreementId, 'AUTO-RNW-20261110',
        '2026-11-03', '2026-11-10', 'VND', 2000.00, 0.00, 0.00,
        2000.00, 0.00, 'overdue', '2026-11-03 00:00:00', '2026-11-03 00:00:00', SYSUTCDATETIME()
    );

    DECLARE @Invoice2Id BIGINT = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price])
    VALUES
        (@Invoice2Id, 'rental', N'Tiền thuê gia hạn tự động ô kho A-101 (10/11/2026 - 10/12/2026)', 1, 2000.00);

    -- Vụ nợ quá hạn Delinquency Case
    INSERT INTO [core].[delinquency_cases] (
        [agreement_id], [invoice_id], [status], [opened_at], [grace_ends_at], [outstanding_snapshot], [notes]
    )
    VALUES (
        @AgreementId, @Invoice2Id, 'active', '2026-11-11 00:00:00', '2026-11-13 00:00:00', 2000.00,
        N'Hợp đồng quá hạn 8 ngày, chưa thanh toán hóa đơn gia hạn tự động INV-AUTO-101-20261110.'
    );

    -- Thông báo quá hạn gửi cho khách hàng
    INSERT INTO [core].[notifications] (
        [user_id], [channel], [template_code], [subject], [payload], [status], [scheduled_at], [sent_at], [attempts], [created_at]
    )
    VALUES (
        @CustomerUserId, 'in_app', 'AGREEMENT_OVERDUE',
        N'Cảnh báo: Hợp đồng thuê ô kho A-101 đã quá hạn',
        N'{"agreementId":' + CAST(@AgreementId AS NVARCHAR(20)) + N', "unitCode":"A-101", "daysPastDue":8, "amountDue":2000}',
        'sent', '2026-11-11 08:00:00', '2026-11-11 08:00:00', 1, '2026-11-11 08:00:00'
    );

    -- NẾU CÓ TÀI KHOẢN yu9696000@gmail.com, TẠO THÊM 1 HỢP ĐỒNG HẾT HẠN TƯƠNG TỰ ĐỂ DEMO NGAY CẢ KHI DÙNG TÀI KHOẢN NÀY
    IF EXISTS (SELECT 1 FROM [core].[users] WHERE [email] = 'yu9696000@gmail.com')
    BEGIN
        DECLARE @YuCustId BIGINT = (SELECT [id] FROM [core].[users] WHERE [email] = 'yu9696000@gmail.com');

        INSERT INTO [core].[reservations] (
            [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
            [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
            [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
        )
        VALUES (
            'RES-HCM-20261010-00102', @YuCustId, 1, 1, 97,
            '2026-10-10', '2026-11-10', 2000.00, 2000.00, 0.00,
            0.00, 4000.00, '2026-10-10 12:00:00', 'converted', '2026-10-10 08:30:00', '2026-10-10 08:00:00', '2026-10-10 08:30:00'
        );

        DECLARE @YuResId BIGINT = SCOPE_IDENTITY();

        INSERT INTO [core].[rental_agreements] (
            [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
            [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
            [status], [signed_at], [checked_in_at], [created_at], [updated_at]
        )
        VALUES (
            'AGR-HCM-20261010-00102', @YuResId, @YuCustId, 1, 1,
            '2026-10-10', '2026-11-10', 2000.00, 2000.00, 2000.00,
            'overdue', '2026-10-10 08:30:00', '2026-10-10 09:00:00',
            '2026-10-10 08:30:00', SYSUTCDATETIME()
        );

        DECLARE @YuAgrId BIGINT = SCOPE_IDENTITY();

        INSERT INTO [core].[unit_allocations] (
            [storage_unit_id], [agreement_id], [allocation_kind],
            [allocation_start_date], [allocation_end_date], [status],
            [assigned_by], [created_at]
        )
        VALUES (
            121, @YuAgrId, 'primary',
            '2026-10-10', '2026-11-10', 'active',
            @StaffUserId, '2026-10-10 09:00:00'
        );

        INSERT INTO [core].[access_credentials] (
            [agreement_id], [credential_type], [secret_digest], [display_hint],
            [status], [issued_at], [issued_by], [created_at]
        )
        VALUES (
            @YuAgrId, 'pin_code', '123456', '123456',
            'suspended', '2026-10-10 09:00:00', @StaffUserId, '2026-10-10 09:00:00'
        );

        INSERT INTO [core].[invoices] (
            [invoice_no], [customer_id], [agreement_id], [billing_period],
            [issue_date], [due_date], [currency], [subtotal_amount], [discount_amount], [tax_amount],
            [total_amount], [paid_amount], [status], [opened_at], [created_at], [updated_at]
        )
        VALUES (
            'INV-20261010-00102', @YuCustId, @YuAgrId, '2026-10-10_2026-11-10',
            '2026-10-10', '2026-10-10', 'VND', 4000.00, 0.00, 0.00,
            4000.00, 4000.00, 'paid', '2026-10-10 08:30:00', '2026-10-10 08:30:00', '2026-10-10 08:35:00'
        );

        DECLARE @YuInv1Id BIGINT = SCOPE_IDENTITY();

        INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price])
        VALUES
            (@YuInv1Id, 'rental', N'Tiền thuê tháng đầu ô kho A-101 (10/10/2026 - 10/11/2026)', 1, 2000.00),
            (@YuInv1Id, 'deposit', N'Tiền đặt cọc bảo đảm ô kho A-101', 1, 2000.00);

        INSERT INTO [core].[payments] (
            [customer_id], [target_invoice_id], [amount], [currency],
            [method], [provider], [provider_transaction_id], [idempotency_key],
            [status], [paid_at], [created_at], [updated_at]
        )
        VALUES (
            @YuCustId, @YuInv1Id, 4000.00, 'VND',
            'vnpay', 'VNPAY', 'VNPAY-20261010-002', 'IDEMP-20261010-002',
            'completed', '2026-10-10 08:35:00', '2026-10-10 08:30:00', '2026-10-10 08:35:00'
        );

        DECLARE @YuPayId BIGINT = SCOPE_IDENTITY();

        INSERT INTO [core].[payment_allocations] ([payment_id], [invoice_id], [allocated_amount], [allocated_at])
        VALUES (@YuPayId, @YuInv1Id, 4000.00, '2026-10-10 08:35:00');

        INSERT INTO [core].[invoices] (
            [invoice_no], [customer_id], [agreement_id], [billing_period],
            [issue_date], [due_date], [currency], [subtotal_amount], [discount_amount], [tax_amount],
            [total_amount], [paid_amount], [status], [opened_at], [created_at], [updated_at]
        )
        VALUES (
            'INV-AUTO-102-20261110', @YuCustId, @YuAgrId, 'AUTO-RNW-20261110',
            '2026-11-03', '2026-11-10', 'VND', 2000.00, 0.00, 0.00,
            2000.00, 0.00, 'overdue', '2026-11-03 00:00:00', '2026-11-03 00:00:00', SYSUTCDATETIME()
        );

        DECLARE @YuInv2Id BIGINT = SCOPE_IDENTITY();

        INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price])
        VALUES
            (@YuInv2Id, 'rental', N'Tiền thuê gia hạn tự động ô kho A-101 (10/11/2026 - 10/12/2026)', 1, 2000.00);

        INSERT INTO [core].[delinquency_cases] (
            [agreement_id], [invoice_id], [status], [opened_at], [grace_ends_at], [outstanding_snapshot], [notes]
        )
        VALUES (
            @YuAgrId, @YuInv2Id, 'active', '2026-11-11 00:00:00', '2026-11-13 00:00:00', 2000.00,
            N'Hợp đồng quá hạn 8 ngày, chưa thanh toán hóa đơn gia hạn tự động INV-AUTO-102-20261110.'
        );

        INSERT INTO [core].[notifications] (
            [user_id], [channel], [template_code], [subject], [payload], [status], [scheduled_at], [sent_at], [attempts], [created_at]
        )
        VALUES (
            @YuCustId, 'in_app', 'AGREEMENT_OVERDUE',
            N'Cảnh báo: Hợp đồng thuê ô kho A-101 đã quá hạn',
            N'{"agreementId":' + CAST(@YuAgrId AS NVARCHAR(20)) + N', "unitCode":"A-101", "daysPastDue":8, "amountDue":2000}',
            'sent', '2026-11-11 08:00:00', '2026-11-11 08:00:00', 1, '2026-11-11 08:00:00'
        );
    END

    PRINT N'>>> 7. Bật lại constraints và triggers...';
    EXEC sp_msforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';
    EXEC sp_msforeachtable 'ALTER TABLE ? ENABLE TRIGGER ALL';

    COMMIT TRANSACTION;
    PRINT N'>>> THIẾT LẬP TOÀN BỘ HỆ THỐNG THÀNH CÔNG RỰC RỠ!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@ErrMsg, 16, 1);
END CATCH;
