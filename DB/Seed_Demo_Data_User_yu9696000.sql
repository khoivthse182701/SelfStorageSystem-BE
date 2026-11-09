-- ======================================================================================
-- SEED DEMO DATA FOR ACCOUNT yu9696000@gmail.com (UserId = 7, Name: Khôi Võ)
-- 5 Kho tại Cơ sở 1 (Thu Duc Self Storage) tương ứng 5 kịch bản kiểm thử nghiệp vụ.
-- TẤT CẢ GIÁ TIỀN & HÓA ĐƠN ĐƯỢC THIẾT KẾ DƯỚI 10.000 VNĐ ĐỂ DỄ DÀNG TEST THANH TOÁN THẬT:
--   1. Chuyển kho (Unit Transfer): Cọc 2.000 đ, Thuê 2.000 đ
--   2. Gia hạn hợp đồng (Renewal): Thuê 2.500 đ/tháng -> Hóa đơn gia hạn 2.500 đ
--   3. Hết hạn hợp đồng chờ hoàn cọc (Refund): Đơn hoàn tiền 3.000 đ
--   4. Đền bù khi hết hạn làm kho bị hư: Cọc 3.500 đ, Hóa đơn đền bù bù thêm ~2.000 đ
--   5. Nợ quá hạn & Tạm khóa mã PIN: Nợ quá hạn 4.000 đ -> Thanh toán 4.000 đ để mở khóa
-- ======================================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    -- 0. Nới rộng trường notes để lưu trữ URL ảnh an toàn
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'storage_units' AND COLUMN_NAME = 'notes' AND CHARACTER_MAXIMUM_LENGTH < 1000
    )
    BEGIN
        ALTER TABLE [core].[storage_units] ALTER COLUMN [notes] NVARCHAR(1000) NULL;
    END

    -- Khai báo 6 URL hình ảnh do người dùng cung cấp
    DECLARE @img1 NVARCHAR(500) = N'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcR6vQvGRB6gtHfaCHV6HGkSMY2MX6yms6f0fIWviNxwV-VPGg3qifeMjHvx&s=10';
    DECLARE @img2 NVARCHAR(500) = N'https://hips.hearstapps.com/hmg-prod/images/self-storage-facility-interior-with-tools-royalty-free-image-1727987323.jpg?crop=0.671xw:1.00xh;0.325xw,0&resize=1200:*';
    DECLARE @img3 NVARCHAR(500) = N'https://cdn-zero-one.storagekingusa.com/media/44f1a0b5-ad57-4a4c-b8af-06ebd9c6d8c9.webp';
    DECLARE @img4 NVARCHAR(500) = N'https://spacevalet.in/wp-content/uploads/2024/07/SpaceValet_Sewri_0042-6-scaled.webp';
    DECLARE @img5 NVARCHAR(500) = N'https://spacehubstore.com/assets/static/articles/v4/head/background-desktop.png';
    DECLARE @img6 NVARCHAR(500) = N'https://www.aa-ministorage.com/wp-content/uploads/2019/09/AA-139.jpg';

    -- Tạm thời disable triggers trong lúc làm sạch dữ liệu và nạp seed
    EXEC('DISABLE TRIGGER ALL ON [core].[invoices];');
    EXEC('DISABLE TRIGGER ALL ON [core].[invoice_lines];');
    EXEC('DISABLE TRIGGER ALL ON [core].[payments];');
    EXEC('DISABLE TRIGGER ALL ON [core].[refunds];');
    EXEC('DISABLE TRIGGER ALL ON [core].[rental_agreements];');
    EXEC('DISABLE TRIGGER ALL ON [core].[reservations];');
    EXEC('DISABLE TRIGGER ALL ON [core].[unit_allocations];');
    EXEC('DISABLE TRIGGER ALL ON [core].[storage_units];');

    -- 1. XÓA SẠCH DỮ LIỆU CŨ (TICKETS, TASKS, CONTRACTS, INVOICES, REFUNDS, ALLOCATIONS)
    DELETE FROM [core].[ticket_attachments];
    DELETE FROM [core].[ticket_messages];
    DELETE FROM [core].[ticket_charge_proposals];
    DELETE FROM [core].[ticket_assignments];
    DELETE FROM [core].[service_ratings];
    DELETE FROM [core].[staff_tasks];
    DELETE FROM [core].[support_tickets];

    DELETE FROM [core].[unit_transfer_requests];
    DELETE FROM [core].[rental_renewals];
    DELETE FROM [core].[move_out_requests];
    DELETE FROM [core].[handover_records];
    DELETE FROM [core].[delinquency_actions];
    DELETE FROM [core].[delinquency_cases];
    DELETE FROM [core].[authorized_access_members];
    DELETE FROM [core].[access_credentials];
    DELETE FROM [core].[appointments];
    DELETE FROM [core].[stored_items];
    DELETE FROM [core].[inspection_items];
    DELETE FROM [core].[inspections];
    DELETE FROM [core].[maintenance_work_orders];

    DELETE FROM [core].[refund_approvals];
    DELETE FROM [core].[refunds];
    DELETE FROM [core].[payment_allocations];
    DELETE FROM [core].[payments];
    DELETE FROM [core].[promotion_redemptions];
    DELETE FROM [core].[invoice_lines];
    DELETE FROM [core].[invoices];

    DELETE FROM [core].[unit_allocations];
    DELETE FROM [core].[rental_agreements];
    DELETE FROM [core].[identity_verifications];
    DELETE FROM [core].[reservations];

    -- 2. CẬP NHẬT BẢNG GIÁ TOÀN BỘ CƠ SỞ VỀ DƯỚI 10.000 Đ ĐỂ TEST
    UPDATE [core].[facility_rates]
    SET [monthly_rate] = CASE ([unit_type_id] % 5)
            WHEN 1 THEN 2000.00
            WHEN 2 THEN 2500.00
            WHEN 3 THEN 3000.00
            WHEN 4 THEN 3500.00
            ELSE 4000.00
        END,
        [deposit_amount] = CASE ([unit_type_id] % 5)
            WHEN 1 THEN 2000.00
            WHEN 2 THEN 2500.00
            WHEN 3 THEN 3000.00
            WHEN 4 THEN 3500.00
            ELSE 4000.00
        END,
        [booking_fee] = 0.00;

    -- 3. CẬP NHẬT ẢNH CHO TẤT CẢ Ô KHO TRÊN TOÀN HỆ THỐNG
    UPDATE [core].[storage_units]
    SET [physical_status] = 'available',
        [is_listed] = 1,
        [notes] = '[IMG:' + CASE ([id] % 6)
            WHEN 0 THEN @img1
            WHEN 1 THEN @img2
            WHEN 2 THEN @img3
            WHEN 3 THEN @img4
            WHEN 4 THEN @img5
            ELSE @img6
        END + '] ' + ISNULL([unit_code], 'Kho tiêu chuẩn') + ' - Sạch sẽ, an ninh 24/7',
        [updated_at] = SYSUTCDATETIME();

    -- Gán ảnh chỉ định riêng biệt cho 6 kho chính tại Facility 1
    UPDATE [core].[storage_units]
    SET [notes] = '[IMG:' + @img1 + '] Kho tiêu chuẩn khô ráo 3m2'
    WHERE [facility_id] = 1 AND [unit_code] = 'A-101';

    UPDATE [core].[storage_units]
    SET [notes] = '[IMG:' + @img2 + '] Kho đồ gia đình vừa 6m2'
    WHERE [facility_id] = 1 AND [unit_code] = 'A-102';

    UPDATE [core].[storage_units]
    SET [notes] = '[IMG:' + @img3 + '] Kho đông lạnh hải sản 6m2'
    WHERE [facility_id] = 1 AND [unit_code] = 'A-103';

    UPDATE [core].[storage_units]
    SET [notes] = '[IMG:' + @img4 + '] Kho sưởi nhiệt ấm 10m2'
    WHERE [facility_id] = 1 AND [unit_code] = 'A-104';

    UPDATE [core].[storage_units]
    SET [notes] = '[IMG:' + @img5 + '] Kho điều hòa VIP cao cấp 16m2'
    WHERE [facility_id] = 1 AND [unit_code] = 'A-105';

    UPDATE [core].[storage_units]
    SET [notes] = '[IMG:' + @img6 + '] Kho dự phòng chuyển kho 6m2',
        [physical_status] = 'available'
    WHERE [facility_id] = 1 AND [unit_code] = 'A-106';

    -- 4. LẤY ID CÁC ĐƠN VỊ KHO VÀ ĐỐI TƯỢNG CỦA FACILITY 1
    DECLARE @cust_id BIGINT = 7; -- yu9696000@gmail.com (Khôi Võ)
    DECLARE @fac_id BIGINT = 1;  -- Thu Duc Self Storage
    DECLARE @policy_id BIGINT = 1;
    DECLARE @now DATETIMEOFFSET = SYSUTCDATETIME();
    DECLARE @today DATE = CAST(@now AS DATE);

    DECLARE @unit1_id BIGINT = (SELECT [id] FROM [core].[storage_units] WHERE [facility_id] = 1 AND [unit_code] = 'A-101');
    DECLARE @unit2_id BIGINT = (SELECT [id] FROM [core].[storage_units] WHERE [facility_id] = 1 AND [unit_code] = 'A-102');
    DECLARE @unit3_id BIGINT = (SELECT [id] FROM [core].[storage_units] WHERE [facility_id] = 1 AND [unit_code] = 'A-103');
    DECLARE @unit4_id BIGINT = (SELECT [id] FROM [core].[storage_units] WHERE [facility_id] = 1 AND [unit_code] = 'A-104');
    DECLARE @unit5_id BIGINT = (SELECT [id] FROM [core].[storage_units] WHERE [facility_id] = 1 AND [unit_code] = 'A-105');

    -- Biến lưu ID tự sinh
    DECLARE @rsv1 BIGINT, @rsv2 BIGINT, @rsv3 BIGINT, @rsv4 BIGINT, @rsv5 BIGINT;
    DECLARE @agr1 BIGINT, @agr2 BIGINT, @agr3 BIGINT, @agr4 BIGINT, @agr5 BIGINT;
    DECLARE @inv1 BIGINT, @inv2 BIGINT, @inv3 BIGINT, @inv4 BIGINT, @inv5_1 BIGINT, @inv5_2 BIGINT;
    DECLARE @pay1 BIGINT, @pay2 BIGINT, @pay3 BIGINT, @pay4 BIGINT, @pay5 BIGINT;

    -- ==================================================================================
    -- KỊCH BẢN 1: CHUYỂN KHO (UNIT TRANSFER)
    -- Ô kho A-101: Cọc 2.000 đ, Thuê 2.000 đ/tháng. HĐ active, còn hạn dài.
    -- ==================================================================================
    DECLARE @created1 DATETIMEOFFSET = DATEADD(month, -1, @now);
    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    ) VALUES (
        N'RSV-2026-TF01', @cust_id, @fac_id, 1, 97,
        DATEADD(month, -1, @today), DATEADD(month, 3, @today), 2000.00, 2000.00, 0.00,
        0.00, 4000.00, DATEADD(minute, 15, @created1), N'confirmed', @created1, @created1, @created1
    );
    SET @rsv1 = SCOPE_IDENTITY();

    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    ) VALUES (
        N'AGR-2026-TF01', @rsv1, @cust_id, @fac_id, @policy_id,
        DATEADD(month, -1, @today), DATEADD(month, 3, @today), 2000.00, 2000.00, 2000.00,
        N'active', @created1, @created1, @created1, @created1
    );
    SET @agr1 = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind], [allocation_start_date], [allocation_end_date],
        [status], [assigned_by], [created_at]
    ) VALUES (
        @unit1_id, @agr1, N'rental', DATEADD(month, -1, @today), DATEADD(month, 3, @today),
        N'active', 11, @created1
    );
    UPDATE [core].[storage_units] SET [physical_status] = 'occupied' WHERE [id] = @unit1_id;

    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period], [issue_date], [due_date],
        [currency], [subtotal_amount], [discount_amount], [tax_amount], [total_amount], [paid_amount],
        [status], [opened_at], [created_at], [updated_at]
    ) VALUES (
        N'INV-TF01-01', @cust_id, @agr1, N'2026-10', DATEADD(month, -1, @today), DATEADD(day, 7, DATEADD(month, -1, @today)),
        'VND', 4000.00, 0.00, 0.00, 4000.00, 4000.00,
        N'paid', @created1, @created1, @created1
    );
    SET @inv1 = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price], [metadata])
    VALUES 
        (@inv1, N'deposit', N'Tiền cọc ô kho A-101', 1, 2000.00, N'{}'),
        (@inv1, N'rent', N'Tiền thuê kỳ 1 ô kho A-101', 1, 2000.00, N'{}');

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency], [method], [provider],
        [idempotency_key], [status], [paid_at], [metadata], [created_at], [updated_at]
    ) VALUES (
        @cust_id, @inv1, 4000.00, 'VND', N'bank_transfer', N'vnpay',
        N'PAY-TF01-01', N'succeeded', @created1, N'{}', @created1, @created1
    );

    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint], [issued_at], [status], [created_at]
    ) VALUES (
        @agr1, N'pin', NULL, N'135790', @created1, N'active', @created1
    );

    -- ==================================================================================
    -- KỊCH BẢN 2: GIA HẠN HỢP ĐỒNG (RENEWAL)
    -- Ô kho A-102: Cọc 2.500 đ, Thuê 2.500 đ/tháng. HĐ active, hết hạn trong 5 ngày tới.
    -- Khi gia hạn 1 tháng, hóa đơn gia hạn tự động sinh ra chỉ đúng: 2.500 VNĐ!
    -- ==================================================================================
    DECLARE @end2 DATE = DATEADD(day, 5, @today);
    DECLARE @start2 DATE = DATEADD(month, -1, @end2);
    DECLARE @created2 DATETIMEOFFSET = DATEADD(month, -1, DATEADD(day, 5, @now));

    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    ) VALUES (
        N'RSV-2026-RN02', @cust_id, @fac_id, 2, 98,
        @start2, @end2, 2500.00, 2500.00, 0.00,
        0.00, 5000.00, DATEADD(minute, 15, @created2), N'confirmed', @created2, @created2, @created2
    );
    SET @rsv2 = SCOPE_IDENTITY();

    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    ) VALUES (
        N'AGR-2026-RN02', @rsv2, @cust_id, @fac_id, @policy_id,
        @start2, @end2, 2500.00, 2500.00, 2500.00,
        N'active', @created2, @created2, @created2, @created2
    );
    SET @agr2 = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind], [allocation_start_date], [allocation_end_date],
        [status], [assigned_by], [created_at]
    ) VALUES (
        @unit2_id, @agr2, N'rental', @start2, @end2,
        N'active', 11, @created2
    );
    UPDATE [core].[storage_units] SET [physical_status] = 'occupied' WHERE [id] = @unit2_id;

    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period], [issue_date], [due_date],
        [currency], [subtotal_amount], [discount_amount], [tax_amount], [total_amount], [paid_amount],
        [status], [opened_at], [created_at], [updated_at]
    ) VALUES (
        N'INV-RN02-01', @cust_id, @agr2, N'2026-10', @start2, DATEADD(day, 7, @start2),
        'VND', 5000.00, 0.00, 0.00, 5000.00, 5000.00,
        N'paid', @created2, @created2, @created2
    );
    SET @inv2 = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price], [metadata])
    VALUES 
        (@inv2, N'deposit', N'Tiền cọc ô kho A-102', 1, 2500.00, N'{}'),
        (@inv2, N'rent', N'Tiền thuê tháng 1 ô kho A-102', 1, 2500.00, N'{}');

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency], [method], [provider],
        [idempotency_key], [status], [paid_at], [metadata], [created_at], [updated_at]
    ) VALUES (
        @cust_id, @inv2, 5000.00, 'VND', N'bank_transfer', N'vnpay',
        N'PAY-RN02-01', N'succeeded', @created2, N'{}', @created2, @created2
    );

    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint], [issued_at], [status], [created_at]
    ) VALUES (
        @agr2, N'pin', NULL, N'246802', @created2, N'active', @created2
    );

    -- ==================================================================================
    -- KỊCH BẢN 3: HẾT HẠN HỢP ĐỒNG CHỜ HOÀN CỌC (EXPIRED - AWAITING REFUND)
    -- Ô kho A-103: Cọc 3.000 đ. HĐ move_out_scheduled, nghiệm thu tốt, Refund pending: 3.000 VNĐ.
    -- ==================================================================================
    DECLARE @start3 DATE = DATEADD(month, -2, @today);
    DECLARE @end3 DATE = DATEADD(day, -2, @today);
    DECLARE @created3 DATETIMEOFFSET = DATEADD(month, -2, @now);

    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    ) VALUES (
        N'RSV-2026-RF03', @cust_id, @fac_id, 3, 99,
        @start3, @end3, 3000.00, 3000.00, 0.00,
        0.00, 6000.00, DATEADD(minute, 15, @created3), N'confirmed', @created3, @created3, @created3
    );
    SET @rsv3 = SCOPE_IDENTITY();

    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [actual_end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    ) VALUES (
        N'AGR-2026-RF03', @rsv3, @cust_id, @fac_id, @policy_id,
        @start3, @end3, @end3, 3000.00, 3000.00, 3000.00,
        N'move_out_scheduled', @created3, @created3, @created3, DATEADD(day, -1, @now)
    );
    SET @agr3 = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind], [allocation_start_date], [allocation_end_date],
        [status], [assigned_by], [created_at], [ended_at]
    ) VALUES (
        @unit3_id, @agr3, N'rental', @start3, @end3,
        N'released', 11, @created3, DATEADD(day, -2, @now)
    );
    UPDATE [core].[storage_units] SET [physical_status] = 'available' WHERE [id] = @unit3_id;

    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [reservation_id], [billing_period], [issue_date], [due_date],
        [currency], [subtotal_amount], [discount_amount], [tax_amount], [total_amount], [paid_amount],
        [status], [opened_at], [created_at], [updated_at]
    ) VALUES (
        N'INV-RF03-DEP', @cust_id, @agr3, @rsv3, N'2026-09', @start3, DATEADD(day, 7, @start3),
        'VND', 3000.00, 0.00, 0.00, 3000.00, 3000.00,
        N'paid', @created3, @created3, @created3
    );
    SET @inv3 = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price], [metadata])
    VALUES (@inv3, N'deposit', N'Tiền cọc ô kho A-103', 1, 3000.00, N'{}');

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency], [method], [provider],
        [idempotency_key], [status], [paid_at], [metadata], [created_at], [updated_at]
    ) VALUES (
        @cust_id, @inv3, 3000.00, 'VND', N'bank_transfer', N'vnpay',
        N'PAY-RF03-DEP', N'succeeded', @created3, N'{}', @created3, @created3
    );
    SET @pay3 = SCOPE_IDENTITY();

    INSERT INTO [core].[move_out_requests] (
        [agreement_id], [requested_by], [requested_move_out_date], [status], [reason], [finalized_at], [created_at], [updated_at]
    ) VALUES (
        @agr3, @cust_id, @end3, N'completed', N'Hết hạn hợp đồng, yêu cầu hoàn trả kho', DATEADD(day, -1, @now), DATEADD(day, -5, @now), DATEADD(day, -1, @now)
    );

    INSERT INTO [core].[inspections] (
        [facility_id], [storage_unit_id], [agreement_id], [inspected_by], [inspection_type],
        [status], [overall_condition], [summary], [inspected_at], [created_at]
    ) VALUES (
        @fac_id, @unit3_id, @agr3, 11, N'check_out',
        N'completed', N'good', N'Kho nguyên vẹn, dọn dẹp sạch sẽ, không hư hại. Đủ điều kiện hoàn 100% cọc.', DATEADD(day, -1, @now), DATEADD(day, -1, @now)
    );

    INSERT INTO [core].[refunds] (
        [payment_id], [agreement_id], [amount], [currency], [reason], [provider],
        [idempotency_key], [status], [requested_by], [created_at], [updated_at]
    ) VALUES (
        @pay3, @agr3, 3000.00, 'VND',
        N'Hoàn trả 100% tiền cọc cho hợp đồng #AGR-2026-RF03. Nghiệm thu hiện trạng tốt, không hư hại.',
        N'pending_choice', N'REFUND-AGR-RF03-3000', N'pending', 11, DATEADD(day, -1, @now), DATEADD(day, -1, @now)
    );

    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint], [issued_at], [status], [revoked_at], [created_at]
    ) VALUES (
        @agr3, N'pin', NULL, N'369124', @created3, N'revoked', DATEADD(day, -1, @now), @created3
    );

    -- ==================================================================================
    -- KỊCH BẢN 4: ĐỀN BÙ KHI HẾT HẠN NHƯNG LÀM KHO BỊ HƯ
    -- Ô kho A-104: Cọc 3.500 đ, Thuê 3.500 đ. MoveOutRequest inspection_pending.
    -- StaffTask đang chờ Staff nghiệm thu để báo hư hỏng (ví dụ: thiệt hại 5.500 đ -> bù thêm 2.000 đ).
    -- ==================================================================================
    DECLARE @start4 DATE = DATEADD(month, -1, @today);
    DECLARE @end4 DATE = @today;
    DECLARE @created4 DATETIMEOFFSET = DATEADD(month, -1, @now);

    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    ) VALUES (
        N'RSV-2026-DM04', @cust_id, @fac_id, 4, 100,
        @start4, @end4, 3500.00, 3500.00, 0.00,
        0.00, 7000.00, DATEADD(minute, 15, @created4), N'confirmed', @created4, @created4, @created4
    );
    SET @rsv4 = SCOPE_IDENTITY();

    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    ) VALUES (
        N'AGR-2026-DM04', @rsv4, @cust_id, @fac_id, @policy_id,
        @start4, @end4, 3500.00, 3500.00, 3500.00,
        N'move_out_scheduled', @created4, @created4, @created4, @now
    );
    SET @agr4 = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind], [allocation_start_date], [allocation_end_date],
        [status], [assigned_by], [created_at]
    ) VALUES (
        @unit4_id, @agr4, N'rental', @start4, @end4,
        N'active', 11, @created4
    );
    UPDATE [core].[storage_units] SET [physical_status] = 'pending_inspection' WHERE [id] = @unit4_id;

    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [reservation_id], [billing_period], [issue_date], [due_date],
        [currency], [subtotal_amount], [discount_amount], [tax_amount], [total_amount], [paid_amount],
        [status], [opened_at], [created_at], [updated_at]
    ) VALUES (
        N'INV-DM04-DEP', @cust_id, @agr4, @rsv4, N'2026-10', @start4, DATEADD(day, 7, @start4),
        'VND', 7000.00, 0.00, 0.00, 7000.00, 7000.00,
        N'paid', @created4, @created4, @created4
    );
    SET @inv4 = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price], [metadata])
    VALUES 
        (@inv4, N'deposit', N'Tiền cọc ô kho A-104', 1, 3500.00, N'{}'),
        (@inv4, N'rent', N'Tiền thuê tháng 1 ô kho A-104', 1, 3500.00, N'{}');

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency], [method], [provider],
        [idempotency_key], [status], [paid_at], [metadata], [created_at], [updated_at]
    ) VALUES (
        @cust_id, @inv4, 7000.00, 'VND', N'bank_transfer', N'vnpay',
        N'PAY-DM04-DEP', N'succeeded', @created4, N'{}', @created4, @created4
    );

    INSERT INTO [core].[move_out_requests] (
        [agreement_id], [requested_by], [requested_move_out_date], [status], [reason], [created_at], [updated_at]
    ) VALUES (
        @agr4, @cust_id, @end4, N'inspection_pending', N'Hết hạn hợp đồng, yêu cầu trả kho kiểm tra hư hại', @now, @now
    );

    -- Nhiệm vụ trên Staff Dashboard cho Staff kiểm tra hiện trường
    INSERT INTO [core].[staff_tasks] (
        [facility_id], [assigned_employee_id], [task_type], [title], [due_at],
        [status], [progress_percent], [created_by], [created_at], [updated_at]
    ) VALUES (
        @fac_id, 11, N'inspection', N'[Nghiệm thu trả kho HĐ #AGR-2026-DM04] Ô kho A-104', DATEADD(hour, 24, @now),
        N'todo', 0, 13, @now, @now
    );

    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint], [issued_at], [status], [created_at]
    ) VALUES (
        @agr4, N'pin', NULL, N'482615', @created4, N'active', @created4
    );

    -- ==================================================================================
    -- KỊCH BẢN 5: NỢ QUÁ HẠN & TẠM KHÓA MÃ PIN (DELINQUENCY & SUSPENDED PIN)
    -- Ô kho A-105: Cọc 4.000 đ, Thuê 4.000 đ/tháng.
    -- Hóa đơn kỳ 2 quá hạn 7 ngày: CHỈ 4.000 VNĐ! (Thanh toán 4.000 đ để mở khóa mã PIN).
    -- ==================================================================================
    DECLARE @start5 DATE = DATEADD(month, -2, @today);
    DECLARE @end5 DATE = DATEADD(month, 1, @today);
    DECLARE @created5 DATETIMEOFFSET = DATEADD(month, -2, @now);

    INSERT INTO [core].[reservations] (
        [reservation_code], [customer_id], [facility_id], [unit_type_id], [facility_rate_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [booking_fee_snapshot],
        [discount_snapshot], [quoted_total], [hold_until], [status], [confirmed_at], [created_at], [updated_at]
    ) VALUES (
        N'RSV-2026-DQ05', @cust_id, @fac_id, 5, 101,
        @start5, @end5, 4000.00, 4000.00, 0.00,
        0.00, 8000.00, DATEADD(minute, 15, @created5), N'confirmed', @created5, @created5, @created5
    );
    SET @rsv5 = SCOPE_IDENTITY();

    INSERT INTO [core].[rental_agreements] (
        [agreement_no], [reservation_id], [customer_id], [facility_id], [policy_version_id],
        [start_date], [end_date], [monthly_rate_snapshot], [deposit_snapshot], [deposit_balance],
        [status], [signed_at], [checked_in_at], [created_at], [updated_at]
    ) VALUES (
        N'AGR-2026-DQ05', @rsv5, @cust_id, @fac_id, @policy_id,
        @start5, @end5, 4000.00, 4000.00, 4000.00,
        N'active', @created5, @created5, @created5, @now
    );
    SET @agr5 = SCOPE_IDENTITY();

    INSERT INTO [core].[unit_allocations] (
        [storage_unit_id], [agreement_id], [allocation_kind], [allocation_start_date], [allocation_end_date],
        [status], [assigned_by], [created_at]
    ) VALUES (
        @unit5_id, @agr5, N'rental', @start5, @end5,
        N'active', 11, @created5
    );
    UPDATE [core].[storage_units] SET [physical_status] = 'occupied' WHERE [id] = @unit5_id;

    -- Hóa đơn kỳ 1 (đã thanh toán cọc 4.000 đ + tiền tháng đầu 4.000 đ = 8.000 đ)
    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period], [issue_date], [due_date],
        [currency], [subtotal_amount], [discount_amount], [tax_amount], [total_amount], [paid_amount],
        [status], [opened_at], [created_at], [updated_at]
    ) VALUES (
        N'INV-DQ05-01', @cust_id, @agr5, N'2026-09', @start5, DATEADD(day, 7, @start5),
        'VND', 8000.00, 0.00, 0.00, 8000.00, 8000.00,
        N'paid', @created5, @created5, @created5
    );
    SET @inv5_1 = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price], [metadata])
    VALUES 
        (@inv5_1, N'deposit', N'Tiền cọc ô kho A-105', 1, 4000.00, N'{}'),
        (@inv5_1, N'rent', N'Tiền thuê tháng 1 ô kho A-105', 1, 4000.00, N'{}');

    INSERT INTO [core].[payments] (
        [customer_id], [target_invoice_id], [amount], [currency], [method], [provider],
        [idempotency_key], [status], [paid_at], [metadata], [created_at], [updated_at]
    ) VALUES (
        @cust_id, @inv5_1, 8000.00, 'VND', N'bank_transfer', N'vnpay',
        N'PAY-DQ05-01', N'succeeded', @created5, N'{}', @created5, @created5
    );

    -- Hóa đơn kỳ 2 (QUÁ HẠN CHƯA THANH TOÁN: CHỈ 4.000 VNĐ)
    INSERT INTO [core].[invoices] (
        [invoice_no], [customer_id], [agreement_id], [billing_period], [issue_date], [due_date],
        [currency], [subtotal_amount], [discount_amount], [tax_amount], [total_amount], [paid_amount],
        [status], [opened_at], [created_at], [updated_at]
    ) VALUES (
        N'INV-DQ05-02', @cust_id, @agr5, N'2026-10', DATEADD(day, -15, @today), DATEADD(day, -7, @today),
        'VND', 4000.00, 0.00, 0.00, 4000.00, 0.00,
        N'overdue', DATEADD(day, -15, @now), DATEADD(day, -15, @now), @now
    );
    SET @inv5_2 = SCOPE_IDENTITY();

    INSERT INTO [core].[invoice_lines] ([invoice_id], [line_type], [description], [quantity], [unit_price], [metadata])
    VALUES (@inv5_2, N'rent', N'Tiền thuê tháng 2 ô kho A-105 (Quá hạn)', 1, 4000.00, N'{}');

    -- Mở hồ sơ nợ xấu DelinquencyCase với số tiền nợ đúng 4.000 đ
    INSERT INTO [core].[delinquency_cases] (
        [agreement_id], [invoice_id], [status], [opened_at], [grace_ends_at], [outstanding_snapshot], [notes]
    ) VALUES (
        @agr5, @inv5_2, N'delinquent', DATEADD(day, -6, @now), DATEADD(day, 4, @now), 4000.00,
        N'Đã gửi thông báo nhắc nợ hóa đơn #INV-DQ05-02 kỳ tháng 10/2026. Số tiền nợ: 4.000 VNĐ.'
    );

    -- Tạm khóa mã PIN mở kho do nợ đọng
    INSERT INTO [core].[access_credentials] (
        [agreement_id], [credential_type], [secret_digest], [display_hint], [issued_at], [status], [created_at]
    ) VALUES (
        @agr5, N'pin', NULL, N'591738', @created5, N'suspended', @created5
    );

    -- Kích hoạt lại toàn bộ trigger của hệ thống
    EXEC('ENABLE TRIGGER ALL ON [core].[invoices];');
    EXEC('ENABLE TRIGGER ALL ON [core].[invoice_lines];');
    EXEC('ENABLE TRIGGER ALL ON [core].[payments];');
    EXEC('ENABLE TRIGGER ALL ON [core].[refunds];');
    EXEC('ENABLE TRIGGER ALL ON [core].[rental_agreements];');
    EXEC('ENABLE TRIGGER ALL ON [core].[reservations];');
    EXEC('ENABLE TRIGGER ALL ON [core].[unit_allocations];');
    EXEC('ENABLE TRIGGER ALL ON [core].[storage_units];');

    COMMIT TRANSACTION;
    PRINT N'>>> ĐÃ SEED THÀNH CÔNG DỮ LIỆU DEMO DƯỚI 10.000 VNĐ CHO TÀI KHOẢN yu9696000@gmail.com!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    -- Đảm bảo triggers luôn được phục hồi ngay cả khi lỗi
    EXEC('ENABLE TRIGGER ALL ON [core].[invoices];');
    EXEC('ENABLE TRIGGER ALL ON [core].[invoice_lines];');
    EXEC('ENABLE TRIGGER ALL ON [core].[payments];');
    EXEC('ENABLE TRIGGER ALL ON [core].[refunds];');
    EXEC('ENABLE TRIGGER ALL ON [core].[rental_agreements];');
    EXEC('ENABLE TRIGGER ALL ON [core].[reservations];');
    EXEC('ENABLE TRIGGER ALL ON [core].[unit_allocations];');
    EXEC('ENABLE TRIGGER ALL ON [core].[storage_units];');

    DECLARE @errMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @errLine INT = ERROR_LINE();
    RAISERROR(N'Lỗi khi seed dữ liệu demo (Dòng %d): %s', 16, 1, @errLine, @errMsg);
END CATCH;
