USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Seeding Support Tickets, Messages, Attachments & Ratings sample data...';

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @CustomerId BIGINT = 7;
    DECLARE @Customer2Id BIGINT = 1;
    DECLARE @StaffId BIGINT = 3;
    DECLARE @FacilityId BIGINT = 1;
    DECLARE @AgreementId BIGINT = 1001;
    DECLARE @StorageUnitId BIGINT = 1;
    DECLARE @NowOffset DATETIMEOFFSET = SYSUTCDATETIME();
    DECLARE @OneDayAgo DATETIMEOFFSET = DATEADD(day, -1, @NowOffset);
    DECLARE @TwoDaysAgo DATETIMEOFFSET = DATEADD(day, -2, @NowOffset);
    DECLARE @ThreeDaysAgo DATETIMEOFFSET = DATEADD(day, -3, @NowOffset);

    -- 0. Đảm bảo Customer 7 và Profile tồn tại
    IF NOT EXISTS (SELECT 1 FROM [core].[users] WHERE id = @CustomerId)
    BEGIN
        SET IDENTITY_INSERT [core].[users] ON;
        INSERT INTO [core].[users] (id, email, phone_number, password_hash, status)
        VALUES (@CustomerId, 'customer.khoi@example.test', '0907123456', 'demo-hash', 'active');
        SET IDENTITY_INSERT [core].[users] OFF;

        IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @CustomerId AND role_id = 1)
            INSERT INTO [core].[user_roles] (user_id, role_id) VALUES (@CustomerId, 1);
    END

    IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE user_id = @CustomerId)
    BEGIN
        INSERT INTO [core].[customer_profiles] (user_id, full_name, identity_number, date_of_birth, address)
        VALUES (@CustomerId, N'Khôi Võ', N'079200001234', CONVERT(date, '2000-01-01', 23), N'TP. Hồ Chí Minh');
    END

    -- 1. DỌN DẸP DỮ LIỆU SEED TICKET CŨ NẾU CÓ
    DELETE FROM [core].[service_ratings] WHERE ticket_id IN (9001, 9002, 9003, 9004);
    DELETE FROM [core].[ticket_attachments] WHERE ticket_id IN (9001, 9002, 9003, 9004);
    DELETE FROM [core].[ticket_messages] WHERE ticket_id IN (9001, 9002, 9003, 9004);
    DELETE FROM [core].[ticket_assignments] WHERE ticket_id IN (9001, 9002, 9003, 9004);
    DELETE FROM [core].[support_tickets] WHERE id IN (9001, 9002, 9003, 9004);

    -- 2. TẠO CÁC SUPPORT TICKETS MẪU
    SET IDENTITY_INSERT [core].[support_tickets] ON;

    -- Ticket 1: Trạng thái OPEN (Mới tạo, danh mục sự cố ổ khóa/unit)
    INSERT INTO [core].[support_tickets] (
        id, ticket_no, customer_id, facility_id, agreement_id, storage_unit_id,
        category, priority, subject, description, status,
        resolution, resolved_at, created_at, updated_at
    )
    VALUES (
        9001, N'TCK-20261001-OPEN01', @CustomerId, @FacilityId, @AgreementId, @StorageUnitId,
        'unit', 'normal', N'Ổ khóa thông minh kho A-101 báo pin yếu',
        N'Khóa thông minh tại kho A-101 nhấp nháy đèn đỏ báo pin yếu, nhờ kỹ thuật kiểm tra và thay pin giúp tôi.',
        'open', NULL, NULL, @OneDayAgo, @OneDayAgo
    );

    -- Ticket 2: Trạng thái IN_PROGRESS (Đang xử lý, danh mục truy cập)
    INSERT INTO [core].[support_tickets] (
        id, ticket_no, customer_id, facility_id, agreement_id, storage_unit_id,
        category, priority, subject, description, status,
        resolution, resolved_at, created_at, updated_at
    )
    VALUES (
        9002, N'TCK-20261002-PROG02', @CustomerId, @FacilityId, @AgreementId, @StorageUnitId,
        'access', 'high', N'Mã PIN mở cổng chính cơ sở Thủ Đức không nhận',
        N'Hôm qua tôi đến kho lúc 20h nhưng nhập mã PIN qua bàn phím cổng chính thì báo lỗi không mở được.',
        'in_progress', NULL, NULL, @TwoDaysAgo, @OneDayAgo
    );

    -- Ticket 3: Trạng thái RESOLVED (Đã xử lý xong, chờ khách hàng xác nhận & đánh giá)
    INSERT INTO [core].[support_tickets] (
        id, ticket_no, customer_id, facility_id, agreement_id, storage_unit_id,
        category, priority, subject, description, status,
        resolution, resolved_at, created_at, updated_at
    )
    VALUES (
        9003, N'TCK-20260930-RESO03', @CustomerId, @FacilityId, @AgreementId, @StorageUnitId,
        'maintenance', 'normal', N'Đèn chiếu sáng hành lang khu A bị chập chờn',
        N'Đèn LED hành lang trước cửa kho A-101 hay chớp tắt liên tục gây khó khăn khi bốc dỡ hàng.',
        'resolved', N'Kỹ thuật viên đã thay mới bóng đèn LED 18W và kiểm tra đường dây ổn định.',
        @OneDayAgo, @ThreeDaysAgo, @OneDayAgo
    );

    -- Ticket 4: Trạng thái CLOSED (Đã đóng và đã có đánh giá sao)
    INSERT INTO [core].[support_tickets] (
        id, ticket_no, customer_id, facility_id, agreement_id, storage_unit_id,
        category, priority, subject, description, status,
        resolution, resolved_at, created_at, updated_at
    )
    VALUES (
        9004, N'TCK-20260928-CLOS04', @CustomerId, @FacilityId, @AgreementId, @StorageUnitId,
        'payment', 'normal', N'Hỗ trợ xuất hóa đơn VAT điện tử cho hợp đồng thuê',
        N'Tôi cần xuất hóa đơn VAT công ty cho kỳ thanh toán đầu tiên của hợp đồng AGR-HCM-2026-0001.',
        'closed', N'Đã xuất và gửi hóa đơn VAT qua email đăng ký của khách hàng.',
        @TwoDaysAgo, @ThreeDaysAgo, @TwoDaysAgo
    );

    SET IDENTITY_INSERT [core].[support_tickets] OFF;

    -- 3. ASSIGNMENT CHO STAFF (Ticket 9002, 9003)
    SET IDENTITY_INSERT [core].[ticket_assignments] ON;
    INSERT INTO [core].[ticket_assignments] (id, ticket_id, employee_id, assigned_by, assigned_at, ended_at, end_reason)
    VALUES 
        (9001, 9002, @StaffId, @StaffId, @TwoDaysAgo, NULL, NULL),
        (9002, 9003, @StaffId, @StaffId, @ThreeDaysAgo, @OneDayAgo, N'Hoàn thành sửa chữa');
    SET IDENTITY_INSERT [core].[ticket_assignments] OFF;

    -- 4. TIN NHẮN TRAO ĐỔI (TICKET MESSAGES)
    SET IDENTITY_INSERT [core].[ticket_messages] ON;

    -- Messages cho Ticket 9001 (Open - Tin nhắn ban đầu)
    INSERT INTO [core].[ticket_messages] (id, ticket_id, author_user_id, body, is_internal, created_at)
    VALUES 
        (9001, 9001, @CustomerId, N'Khóa thông minh tại kho A-101 nhấp nháy đèn đỏ báo pin yếu, nhờ kỹ thuật kiểm tra và thay pin giúp tôi.', 0, @OneDayAgo);

    -- Messages cho Ticket 9002 (In Progress - Trao đổi qua lại giữa khách và staff)
    INSERT INTO [core].[ticket_messages] (id, ticket_id, author_user_id, body, is_internal, created_at)
    VALUES 
        (9002, 9002, @CustomerId, N'Hôm qua tôi đến kho lúc 20h nhưng nhập mã PIN qua bàn phím cổng chính thì báo lỗi không mở được.', 0, @TwoDaysAgo),
        (9003, 9002, @StaffId, N'Chào anh Khôi, bên em đã ghi nhận sự cố. Em đã đồng bộ lại mã PIN của anh trên hệ thống cổng tự động, anh thử lại giúp em nhé.', 0, DATEADD(hour, 4, @TwoDaysAgo)),
        (9004, 9002, @CustomerId, N'Cảm ơn bạn, chiều nay mình ghé lại sẽ test xem sao nhé.', 0, @OneDayAgo);

    -- Messages cho Ticket 9003 (Resolved)
    INSERT INTO [core].[ticket_messages] (id, ticket_id, author_user_id, body, is_internal, created_at)
    VALUES 
        (9005, 9003, @CustomerId, N'Đèn LED hành lang trước cửa kho A-101 hay chớp tắt liên tục gây khó khăn khi bốc dỡ hàng.', 0, @ThreeDaysAgo),
        (9006, 9003, @StaffId, N'Kỹ thuật đã hoàn tất thay bóng mới lúc 10h sáng nay. Anh kiểm tra giúp em nhé.', 0, @OneDayAgo);

    -- Messages cho Ticket 9004 (Closed)
    INSERT INTO [core].[ticket_messages] (id, ticket_id, author_user_id, body, is_internal, created_at)
    VALUES 
        (9007, 9004, @CustomerId, N'Tôi cần xuất hóa đơn VAT công ty cho kỳ thanh toán đầu tiên.', 0, @ThreeDaysAgo),
        (9008, 9004, @StaffId, N'Dạ hóa đơn số HD-2026-0012 đã được gửi qua email của quý khách.', 0, @TwoDaysAgo);

    SET IDENTITY_INSERT [core].[ticket_messages] OFF;

    -- 5. ĐÍNH KÈM (TICKET ATTACHMENTS)
    SET IDENTITY_INSERT [core].[ticket_attachments] ON;
    INSERT INTO [core].[ticket_attachments] (
        id, ticket_id, message_id, uploaded_by, file_name, mime_type, file_size_bytes, object_url, sha256, created_at
    )
    VALUES
        (9001, 9001, 9001, @CustomerId, N'lock_low_battery.jpg', N'image/jpeg', 245120, N'https://storage.selfstorage.vn/attachments/lock_low_battery.jpg', NULL, @OneDayAgo),
        (9002, 9002, 9002, @CustomerId, N'gate_keypad_error.png', N'image/png', 512000, N'https://storage.selfstorage.vn/attachments/gate_keypad_error.png', NULL, @TwoDaysAgo);
    SET IDENTITY_INSERT [core].[ticket_attachments] OFF;

    -- 6. ĐÁNH GIÁ DỊCH VỤ (SERVICE RATING CHO TICKET 9004 ĐÃ CLOSED)
    INSERT INTO [core].[service_ratings] (ticket_id, customer_id, score, comment, created_at)
    VALUES (9004, @CustomerId, 5, N'Hỗ trợ xuất hóa đơn rất nhanh chóng và nhiệt tình. Cảm ơn đội ngũ!', @TwoDaysAgo);

    COMMIT TRANSACTION;
    PRINT N'Seed Support Tickets data completed successfully!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
GO
