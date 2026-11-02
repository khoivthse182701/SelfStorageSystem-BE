-- =====================================================================================
-- KỊCH BẢN SEED DỮ LIỆU TOÀN DIỆN ĐỂ TEST MỌI TÍNH NĂNG CỦA FACILITY MANAGER
-- Database: [SelfStoragePRN222]
-- Facility ID: 1 (Cơ sở Thủ Đức / HCM)
-- Manager Login: manager@gmail.com / 123@Test
-- =====================================================================================

USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT N'>>> BẮT ĐẦU SEED DỮ LIỆU TEST TOÀN DIỆN CHO FACILITY MANAGER...';

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();
    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @FacilityId BIGINT = 1;
    DECLARE @PassHash NVARCHAR(255) = N'$2a$11$pLt2NvvvBf9WS4YvVQzPruLslMex9ua.g4jpf7vi1h.ldVXK5h6s6'; -- 123@Test

    -- Lấy thông tin bảng giá hợp lệ của Cơ sở 1 từ cơ sở dữ liệu để thỏa mãn Trigger trg_reservations_validate_rate
    DECLARE @UnitTypeId BIGINT;
    DECLARE @FacilityRateId BIGINT;
    DECLARE @MonthlyRate NUMERIC(12,2);
    DECLARE @DepositAmount NUMERIC(12,2);
    DECLARE @BookingFee NUMERIC(12,2);
    DECLARE @ValidFrom DATE;

    SELECT TOP 1 
        @FacilityRateId = id,
        @UnitTypeId = unit_type_id,
        @MonthlyRate = monthly_rate,
        @DepositAmount = deposit_amount,
        @BookingFee = booking_fee,
        @ValidFrom = valid_from
    FROM [core].[facility_rates]
    WHERE facility_id = @FacilityId
    ORDER BY id ASC;

    DECLARE @PolicyVersionId BIGINT = (SELECT TOP 1 id FROM [core].[policy_versions]);

    -- =================================================================================
    -- 0. NỚI LỎNG CHECK CONSTRAINT CHO STATUS NẾU CẦN
    -- =================================================================================
    -- 0.1 Refunds
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.refunds') AND definition LIKE '%status%'
    )
    BEGIN
        DECLARE @ChkRefundStatus NVARCHAR(256);
        SELECT TOP 1 @ChkRefundStatus = name FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.refunds') AND definition LIKE '%status%';
        DECLARE @SqlRefund NVARCHAR(MAX) = N'ALTER TABLE [core].[refunds] DROP CONSTRAINT ' + QUOTENAME(@ChkRefundStatus) + N';'
           + N'ALTER TABLE [core].[refunds] ADD CONSTRAINT ' + QUOTENAME(@ChkRefundStatus) 
           + N' CHECK ([status] IN (''pending'', ''requested'', ''approved'', ''processing'', ''succeeded'', ''failed'', ''rejected'', ''completed'', ''pending_manager_payout'', ''pending_counter_payout''));';
        EXEC sp_executesql @SqlRefund;
    END;

    -- 0.2 Refund Approvals
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.refund_approvals') AND definition LIKE '%decision%'
    )
    BEGIN
        DECLARE @ChkApprovalDecision NVARCHAR(256);
        SELECT TOP 1 @ChkApprovalDecision = name FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.refund_approvals') AND definition LIKE '%decision%';
        DECLARE @SqlApproval NVARCHAR(MAX) = N'ALTER TABLE [core].[refund_approvals] DROP CONSTRAINT ' + QUOTENAME(@ChkApprovalDecision) + N';'
           + N'ALTER TABLE [core].[refund_approvals] ADD CONSTRAINT ' + QUOTENAME(@ChkApprovalDecision) 
           + N' CHECK ([decision] IN (''approved'', ''rejected'', ''completed''));';
        EXEC sp_executesql @SqlApproval;
    END;

    -- 0.3 Support Tickets Status
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.support_tickets') AND definition LIKE '%status%'
    )
    BEGIN
        DECLARE @ChkTicketStatus NVARCHAR(256);
        SELECT TOP 1 @ChkTicketStatus = name FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.support_tickets') AND definition LIKE '%status%';
        DECLARE @SqlTicket NVARCHAR(MAX) = N'ALTER TABLE [core].[support_tickets] DROP CONSTRAINT ' + QUOTENAME(@ChkTicketStatus) + N';'
           + N'ALTER TABLE [core].[support_tickets] ADD CONSTRAINT ' + QUOTENAME(@ChkTicketStatus) 
           + N' CHECK ([status] IN (''open'', ''pending_manager_review'', ''assigned'', ''in_progress'', ''waiting_for_customer'', ''waiting_for_maintenance'', ''severe_need_relocation'', ''assessed'', ''resolved'', ''closed'', ''cancelled''));';
        EXEC sp_executesql @SqlTicket;
    END;

    -- =================================================================================
    -- 1. DỌN DẸP CÁC DỮ LIỆU TEST CŨ CỦA SEED NÀY (ĐỂ CHẠY LẠI KHÔNG BỊ TRÙNG LẶP)
    -- =================================================================================
    PRINT N'- Dọn dẹp dữ liệu seed cũ...';

    -- Xóa refunds & approvals test
    DELETE FROM [core].[refund_approvals] WHERE refund_id IN (9301, 9302);
    DELETE FROM [core].[refunds] WHERE id IN (9301, 9302);
    DELETE FROM [core].[payments] WHERE id IN (9301, 9302);

    -- Xóa support tickets & staff tasks test
    DELETE FROM [core].[staff_tasks] WHERE id IN (9201, 9202, 9203);
    DELETE FROM [core].[support_tickets] WHERE id IN (9101, 9102);

    -- Xóa inspections test
    DELETE FROM [core].[inspection_items] WHERE inspection_id IN (9401);
    DELETE FROM [core].[inspections] WHERE id IN (9401);

    -- Xóa move-out requests test
    DELETE FROM [core].[move_out_requests] WHERE id IN (9501);

    -- Xóa shift assignments test
    DELETE FROM [core].[shift_assignments] WHERE shift_id IN (9601, 9602);
    DELETE FROM [core].[staff_shifts] WHERE id IN (9601, 9602);

    -- Xóa access credentials & allocations & invoices & agreements test
    DELETE FROM [core].[access_credentials] WHERE agreement_id IN (9001, 9002);
    DELETE FROM [core].[unit_allocations] WHERE agreement_id IN (9001, 9002);
    DELETE FROM [core].[invoices] WHERE id IN (9301) OR agreement_id IN (9001, 9002);
    DELETE FROM [core].[rental_agreements] WHERE id IN (9001, 9002);
    DELETE FROM [core].[reservations] WHERE id IN (9001, 9002);

    -- Xóa test units (901 -> 906)
    DELETE FROM [core].[storage_units] WHERE id IN (901, 902, 903, 904, 905, 906);

    -- Xóa staff facility assignments của các user test
    DELETE FROM [core].[staff_facility_assignments] WHERE employee_id IN (911, 912);
    DELETE FROM [core].[employee_profiles] WHERE user_id IN (911, 912);
    DELETE FROM [core].[customer_profiles] WHERE user_id IN (921, 922);
    DELETE FROM [core].[user_roles] WHERE user_id IN (911, 912, 921, 922);
    DELETE FROM [core].[users] WHERE id IN (911, 912, 921, 922);

    -- =================================================================================
    -- 2. TẠO TÀI KHOẢN FACILITY MANAGER (manager@gmail.com)
    -- =================================================================================
    PRINT N'- Cài đặt tài khoản Facility Manager (manager@gmail.com / 123@Test)...';
    DECLARE @ManagerUserId BIGINT = (SELECT TOP 1 id FROM [core].[users] WHERE email = N'manager@gmail.com');
    DECLARE @ManagerRoleId SMALLINT = (SELECT TOP 1 id FROM [core].[roles] WHERE code = N'facility_manager');
    DECLARE @StaffRoleId SMALLINT = (SELECT TOP 1 id FROM [core].[roles] WHERE code = N'facility_staff');
    DECLARE @CustomerRoleId SMALLINT = (SELECT TOP 1 id FROM [core].[roles] WHERE code = N'storage_customer');
    DECLARE @AdminUserId BIGINT = (SELECT TOP 1 u.id FROM [core].[users] u INNER JOIN [core].[user_roles] ur ON u.id = ur.user_id WHERE ur.role_id = 5);
    IF @AdminUserId IS NULL SET @AdminUserId = (SELECT TOP 1 id FROM [core].[users]);

    IF @ManagerUserId IS NULL
    BEGIN
        INSERT INTO [core].[users] (email, phone_number, password_hash, status, created_at, updated_at)
        VALUES (N'manager@gmail.com', N'0909999888', @PassHash, N'active', @Now, @Now);
        SET @ManagerUserId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE [core].[users]
        SET password_hash = @PassHash, status = N'active', updated_at = @Now
        WHERE id = @ManagerUserId;
    END;

    IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @ManagerUserId AND role_id = @ManagerRoleId)
    BEGIN
        INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at)
        VALUES (@ManagerUserId, @ManagerRoleId, @AdminUserId, @Now);
    END;

    IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE user_id = @ManagerUserId)
    BEGIN
        INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
        VALUES (@ManagerUserId, N'MGR-HCM-001', N'Trần Quản Lý Cơ Sở', @Today, N'active', @Now, @Now);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM [core].[staff_facility_assignments]
        WHERE employee_id = @ManagerUserId AND facility_id = @FacilityId AND ends_at IS NULL
    )
    BEGIN
        INSERT INTO [core].[staff_facility_assignments] (employee_id, facility_id, assignment_role, starts_at, assigned_by, created_at)
        VALUES (@ManagerUserId, @FacilityId, N'facility_manager', DATEADD(month, -6, @Now), @AdminUserId, @Now);
    END;

    -- =================================================================================
    -- 3. TẠO NHÂN VIÊN CƠ SỞ (STAFF) ĐỂ MANAGER QUẢN LÝ (MODULE 2)
    -- =================================================================================
    PRINT N'- Tạo nhân viên cơ sở test (Staff An & Staff Bình)...';
    
    SET IDENTITY_INSERT [core].[users] ON;
    INSERT INTO [core].[users] (id, email, phone_number, password_hash, status, created_at, updated_at)
    VALUES 
        (911, N'staff.an@gmail.com', N'0911000111', @PassHash, N'active', @Now, @Now),
        (912, N'staff.binh@gmail.com', N'0912000222', @PassHash, N'disabled', @Now, @Now);
    SET IDENTITY_INSERT [core].[users] OFF;

    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at)
    VALUES 
        (911, @StaffRoleId, @ManagerUserId, @Now),
        (912, @StaffRoleId, @ManagerUserId, @Now);

    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES 
        (911, N'EMP-STAFF-911', N'Nguyễn Văn An (NV Kỹ thuật)', @Today, N'active', @Now, @Now),
        (912, N'EMP-STAFF-912', N'Trần Thị Bình (NV Vệ sinh - Inactive)', @Today, N'leave', @Now, @Now);

    INSERT INTO [core].[staff_facility_assignments] (employee_id, facility_id, assignment_role, starts_at, assigned_by, created_at)
    VALUES 
        (911, @FacilityId, N'facility_staff', DATEADD(month, -1, @Now), @ManagerUserId, @Now),
        (912, @FacilityId, N'facility_staff', DATEADD(month, -1, @Now), @ManagerUserId, @Now);

    -- =================================================================================
    -- 4. TẠO KHÁCH HÀNG TEST (CUSTOMER) CỦA CƠ SỞ (MODULE 2)
    -- =================================================================================
    PRINT N'- Tạo khách hàng test (Lê Hoàng Nam & Phạm Minh Tuấn)...';

    SET IDENTITY_INSERT [core].[users] ON;
    INSERT INTO [core].[users] (id, email, phone_number, password_hash, status, created_at, updated_at)
    VALUES 
        (921, N'cus.nam@gmail.com', N'0921000333', @PassHash, N'active', @Now, @Now),
        (922, N'cus.tuan@gmail.com', N'0922000444', @PassHash, N'active', @Now, @Now);
    SET IDENTITY_INSERT [core].[users] OFF;

    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at)
    VALUES 
        (921, @CustomerRoleId, @AdminUserId, @Now),
        (922, @CustomerRoleId, @AdminUserId, @Now);

    INSERT INTO [core].[customer_profiles] (user_id, full_name, identity_number, created_at, updated_at)
    VALUES 
        (921, N'Lê Hoàng Nam', N'079090123456', @Now, @Now),
        (922, N'Phạm Minh Tuấn', N'079090654321', @Now, @Now);

    -- =================================================================================
    -- 5. TẠO DANH SÁCH Ô KHO TEST VỚI ĐỦ CÁC TRẠNG THÁI (DASHBOARD & REASSIGN)
    -- =================================================================================
    PRINT N'- Tạo danh sách ô kho test (A-901 đến A-906)...';

    SET IDENTITY_INSERT [core].[storage_units] ON;
    INSERT INTO [core].[storage_units] (id, facility_id, unit_type_id, unit_code, floor_label, zone_label, physical_status, is_listed, created_at, updated_at)
    VALUES 
        (901, @FacilityId, @UnitTypeId, N'A-901-OCCUPIED', N'Tầng 1', N'Khu A', N'occupied', 0, @Now, @Now),
        (902, @FacilityId, @UnitTypeId, N'A-902-AVAILABLE', N'Tầng 1', N'Khu A', N'available', 1, @Now, @Now),
        (903, @FacilityId, @UnitTypeId, N'A-903-AVAILABLE', N'Tầng 1', N'Khu A', N'available', 1, @Now, @Now),
        (904, @FacilityId, @UnitTypeId, N'A-904-MAINTENANCE', N'Tầng 1', N'Khu A', N'maintenance', 0, @Now, @Now),
        (905, @FacilityId, @UnitTypeId, N'A-905-CLEANING', N'Tầng 1', N'Khu A', N'cleaning', 0, @Now, @Now),
        (906, @FacilityId, @UnitTypeId, N'A-906-PENDING-INSPECT', N'Tầng 1', N'Khu A', N'pending_inspection', 0, @Now, @Now);
    SET IDENTITY_INSERT [core].[storage_units] OFF;

    -- =================================================================================
    -- 6. TẠO HỢP ĐỒNG TEST
    --    HĐ 9001: Đang Active tại kho A-901 (cho khách Lê Hoàng Nam)
    --    HĐ 9002: Sắp hết hạn (Expiring Soon) hoặc có MoveOut (cho khách Phạm Minh Tuấn)
    -- =================================================================================
    PRINT N'- Tạo hợp đồng thuê test (HĐ #9001 và HĐ #9002)...';

    DECLARE @Hdr1Total NUMERIC(12,2) = (@MonthlyRate * 6) + @BookingFee;
    DECLARE @Hdr2Total NUMERIC(12,2) = (@MonthlyRate * 3) + @BookingFee;

    SET IDENTITY_INSERT [core].[reservations] ON;
    INSERT INTO [core].[reservations] (
        id, reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status, confirmed_at, created_at, updated_at
    )
    VALUES 
        (9001, N'RSV-9001', 921, @FacilityId, @UnitTypeId, @FacilityRateId,
         DATEADD(month, -1, @Today), DATEADD(month, 5, @Today), @MonthlyRate, @DepositAmount, @BookingFee,
         0, @Hdr1Total, DATEADD(minute, 10, @Now), N'confirmed', @Now, @Now, @Now),
        (9002, N'RSV-9002', 922, @FacilityId, @UnitTypeId, @FacilityRateId,
         DATEADD(month, -3, @Today), DATEADD(day, 2, @Today), @MonthlyRate, @DepositAmount, @BookingFee,
         0, @Hdr2Total, DATEADD(minute, 10, @Now), N'confirmed', @Now, @Now, @Now);
    SET IDENTITY_INSERT [core].[reservations] OFF;

    SET IDENTITY_INSERT [core].[rental_agreements] ON;
    INSERT INTO [core].[rental_agreements] (
        id, agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, created_at, updated_at
    )
    VALUES 
        (9001, N'AGR-HCM-2026-9001', 9001, 921, @FacilityId, @PolicyVersionId,
         DATEADD(month, -1, @Today), DATEADD(month, 5, @Today), @MonthlyRate, @DepositAmount, @DepositAmount,
         N'active', @Now, @Now, @Now, @Now),
        (9002, N'AGR-HCM-2026-9002', 9002, 922, @FacilityId, @PolicyVersionId,
         DATEADD(month, -3, @Today), DATEADD(day, 2, @Today), @MonthlyRate, @DepositAmount, @DepositAmount,
         N'active', @Now, @Now, @Now, @Now);
    SET IDENTITY_INSERT [core].[rental_agreements] OFF;

    -- Unit Allocations cho HĐ
    DISABLE TRIGGER [core].[trg_unit_allocations_validate_scope] ON [core].[unit_allocations];

    INSERT INTO [core].[unit_allocations] (
        storage_unit_id, agreement_id, allocation_kind, allocation_start_date, allocation_end_date, status, created_at
    )
    VALUES 
        (901, 9001, N'rental', DATEADD(month, -1, @Today), DATEADD(month, 5, @Today), N'active', @Now),
        (906, 9002, N'rental', DATEADD(month, -3, @Today), DATEADD(day, 2, @Today), N'active', @Now);

    ENABLE TRIGGER [core].[trg_unit_allocations_validate_scope] ON [core].[unit_allocations];

    -- Mã PIN mở cửa ô kho
    INSERT INTO [core].[access_credentials] (
        agreement_id, credential_type, secret_digest, display_hint, status, issued_at, created_at
    )
    VALUES 
        (9001, N'pin', @PassHash, N'123456', N'active', @Now, @Now),
        (9002, N'pin', @PassHash, N'654321', N'active', @Now, @Now);

    -- =================================================================================
    -- 7. TẠO MOVE-OUT REQUEST CHO HĐ 9002 (ĐỂ TEST PHÂN CÔNG NGHIỆM THU - MODULE 4)
    -- =================================================================================
    PRINT N'- Tạo yêu cầu trả kho chờ phân công nghiệm thu (HĐ #9002)...';

    SET IDENTITY_INSERT [core].[move_out_requests] ON;
    INSERT INTO [core].[move_out_requests] (
        id, agreement_id, requested_by, requested_move_out_date, status, reason, created_at, updated_at
    )
    VALUES (
        9501, 9002, 922, DATEADD(day, 1, @Today), N'requested', N'Hết nhu cầu lưu trữ đồ đạc văn phòng.', @Now, @Now
    );
    SET IDENTITY_INSERT [core].[move_out_requests] OFF;

    -- =================================================================================
    -- 8. TẠO TICKET SỰ CỐ ĐỂ TEST PHÂN CÔNG STAFF & ĐỔI KHO KHẨN CẤP (MODULE 3)
    -- =================================================================================
    PRINT N'- Tạo Ticket sự cố: 1 Chờ Manager phân công, 1 Báo hỏng nặng cần đổi kho...';

    SET IDENTITY_INSERT [core].[support_tickets] ON;
    INSERT INTO [core].[support_tickets] (
        id, ticket_no, customer_id, facility_id, agreement_id, storage_unit_id,
        category, priority, subject, description, status, created_at, updated_at
    )
    VALUES 
        -- Ticket 1: Khách báo ô kho cần bảo trì -> Chờ Manager xem và phân công Staff xử lý
        (9101, N'TCK-2026-9101', 921, @FacilityId, 9001, 901,
         N'unit', N'normal', N'Kho A-901 bị kẹt then cài cửa',
         N'Khách vừa check-in phát hiện then cài cửa bị rỉ sét khó kéo, cần nhân viên kỹ thuật qua xịt dầu bảo dưỡng.',
         N'pending_manager_review', @Now, @Now),

        -- Ticket 2: Staff đã kiểm tra và báo cáo Hỏng nặng (Severe_Need_Relocation) -> Chờ Manager đổi kho
        (9102, N'TCK-2026-9102', 921, @FacilityId, 9001, 901,
         N'maintenance', N'urgent', N'Trần ô kho A-901 bị dột nước nghiêm trọng',
         N'Nước ngấm từ trần tầng 2 chảy xuống thành dòng, có nguy cơ làm ướt hàng hóa điện tử của khách.',
         N'severe_need_relocation', DATEADD(hour, -2, @Now), @Now);
    SET IDENTITY_INSERT [core].[support_tickets] OFF;

    -- =================================================================================
    -- 9. TẠO HÓA ĐƠN & THANH TOÁN & HOÀN TIỀN CỌC ĐỂ TEST MANAGER CHUYỂN KHOẢN NGÂN HÀNG (MODULE 4)
    -- =================================================================================
    PRINT N'- Tạo khoản hoàn tiền cọc chờ Manager chuyển khoản (Refund #9301)...';

    -- Tạo hóa đơn trước
    SET IDENTITY_INSERT [core].[invoices] ON;
    INSERT INTO [core].[invoices] (
        id, invoice_no, customer_id, agreement_id, issue_date, due_date, currency,
        subtotal_amount, discount_amount, tax_amount, total_amount, paid_amount,
        status, created_at, updated_at
    )
    VALUES (
        9301, N'INV-TEST-9301', 921, 9001, @Today, DATEADD(day, 7, @Today), N'VND',
        @DepositAmount, 0, 0, @DepositAmount, @DepositAmount, N'paid', @Now, @Now
    );
    SET IDENTITY_INSERT [core].[invoices] OFF;

    -- Tạo 1 payment gốc trước để thỏa mãn Foreign Key & Trigger trg_refunds_guard
    SET IDENTITY_INSERT [core].[payments] ON;
    INSERT INTO [core].[payments] (
        id, customer_id, target_invoice_id, amount, currency, method, provider,
        provider_transaction_id, idempotency_key, status, paid_at, metadata, created_at, updated_at
    )
    VALUES (
        9301, 921, 9301, @DepositAmount, N'VND', N'bank_transfer', N'manual_bank',
        N'TXN-9301', N'PAY-IDEMP-9301', N'succeeded', @Now, N'{}', @Now, @Now
    );
    SET IDENTITY_INSERT [core].[payments] OFF;

    -- Bản ghi Refund #9301: Chờ Manager thanh toán qua ngân hàng
    SET IDENTITY_INSERT [core].[refunds] ON;
    INSERT INTO [core].[refunds] (
        id, payment_id, agreement_id, amount, currency, reason, provider,
        idempotency_key, status, requested_by, created_at, updated_at
    )
    VALUES (
        9301, 9301, 9001, @DepositAmount, N'VND',
        N'{"bank":"MBBank","acc":"0987654321","holder":"LE HOANG NAM","note":"Khach yeu cau hoan coc som qua chuyen khoan"}',
        N'bank_transfer', N'REF-KEY-9301-TEST', N'pending_manager_payout', 921, @Now, @Now
    );
    SET IDENTITY_INSERT [core].[refunds] OFF;

    -- =================================================================================
    -- 10. TẠO LỊCH TRỰC (SHIFTS & ROSTER) CHO TUẦN NÀY
    -- =================================================================================
    PRINT N'- Tạo ca trực & phân công lịch làm việc tuần này...';

    SET IDENTITY_INSERT [core].[staff_shifts] ON;
    INSERT INTO [core].[staff_shifts] (
        id, facility_id, shift_name, starts_at, ends_at, status, created_by, created_at
    )
    VALUES 
        (9601, @FacilityId, N'Ca Sáng (Thủ Đức)', 
         TODATETIMEOFFSET(DATEADD(hour, 7, CAST(@Today AS datetime)), '+07:00'),
         TODATETIMEOFFSET(DATEADD(hour, 15, CAST(@Today AS datetime)), '+07:00'),
         N'planned', @ManagerUserId, @Now),
        (9602, @FacilityId, N'Ca Chiều (Thủ Đức)', 
         TODATETIMEOFFSET(DATEADD(hour, 15, CAST(@Today AS datetime)), '+07:00'),
         TODATETIMEOFFSET(DATEADD(hour, 23, CAST(@Today AS datetime)), '+07:00'),
         N'planned', @ManagerUserId, @Now);
    SET IDENTITY_INSERT [core].[staff_shifts] OFF;

    INSERT INTO [core].[shift_assignments] (shift_id, employee_id, duty_role, status)
    VALUES 
        (9601, 911, N'staff', N'scheduled');

    COMMIT TRANSACTION;
    PRINT N'>>> SEED DỮ LIỆU THÀNH CÔNG 100%! BẠN CÓ THỂ ĐĂNG NHẬP VÀ TEST NGAY.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    ENABLE TRIGGER [core].[trg_unit_allocations_validate_scope] ON [core].[unit_allocations];
    DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
    PRINT N'LỖI KHI SEED DỮ LIỆU: ' + @Err;
    THROW;
END CATCH;
GO