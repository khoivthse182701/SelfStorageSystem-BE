-- =====================================================================================
-- Seed Operational Test Data for Facility Manager Console (FacilityId = 1: HCM-TD)
-- Targets:
--   1. Unit Transfer Requests  (core.unit_transfer_requests)
--   2. Move-Out Requests       (core.move_out_requests)
--   3. Ticket Charge Proposals (core.ticket_charge_proposals)
--   4. Refund Approvals        (core.payments, core.refunds)
--   5. Delinquency Cases       (core.invoices, core.delinquency_cases, core.delinquency_actions)
--
-- Notes:
--   - Uses UTF-8 (Vietnamese diacritics N'...')
--   - Respects unique filtered indexes:
--       * unit_transfer_requests_one_open_uidx (at most 1 open request per agreement)
--       * move_out_requests_one_open_uidx (at most 1 non-completed request per agreement)
--       * delinquency_cases_one_open_invoice_uidx (1 case per invoice)
--   - Adds reservation 1003, agreement 1003, allocation 5 on unit 2 (A-102) for customer 1
-- =====================================================================================

USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

PRINT N'Starting Seeding for Manager Operations (Transfer Requests, Move-outs, Charge Proposals, Refunds, Delinquent Cases)...';

BEGIN TRANSACTION;

BEGIN TRY
    -- 0. RELAX REFUND STATUS CHECK CONSTRAINT IF NEEDED TO ALLOW 'pending'
    IF EXISTS (
        SELECT 1 FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.refunds') 
          AND definition LIKE '%processing%' 
          AND definition NOT LIKE '%pending%'
    )
    BEGIN
        DECLARE @ChkName NVARCHAR(256);
        SELECT TOP 1 @ChkName = name FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID('core.refunds') AND definition LIKE '%processing%';

        DECLARE @Sql NVARCHAR(MAX) = N'ALTER TABLE [core].[refunds] DROP CONSTRAINT ' + QUOTENAME(@ChkName) + N';'
            + N' ALTER TABLE [core].[refunds] ADD CONSTRAINT ' + QUOTENAME(@ChkName) 
            + N' CHECK ([status] IN (''pending'', ''requested'', ''approved'', ''processing'', ''succeeded'', ''failed'', ''rejected''));';
        EXEC sp_executesql @Sql;
        PRINT N'- Altered core.refunds status check constraint to permit ''pending''.';
    END;

    -- 1. CLEANUP PREVIOUS SEED RUNS (IN DEPENDENCY ORDER)
    DELETE FROM [core].[delinquency_actions] WHERE id IN (8401, 8402);
    DELETE FROM [core].[delinquency_cases] WHERE id IN (8401, 8402);
    DELETE FROM [core].[refunds] WHERE id IN (8301, 8302);
    DELETE FROM [core].[payments] WHERE id IN (8301, 8302);
    DELETE FROM [core].[invoices] WHERE id IN (1003, 1004);
    DELETE FROM [core].[ticket_charge_proposals] WHERE id IN (8201, 8202);
    DELETE FROM [core].[move_out_requests] WHERE id IN (8101, 8102, 8103);
    DELETE FROM [core].[unit_transfer_requests] WHERE id IN (8001, 8002, 8003);
    DELETE FROM [core].[unit_allocations] WHERE id IN (5);
    DELETE FROM [core].[rental_agreements] WHERE id IN (1003);
    DELETE FROM [core].[reservations] WHERE id IN (1003);

    -- 2. SEED RESERVATION 1003, AGREEMENT 1003, ALLOCATION 5 FOR CUSTOMER 1 (Nguyen Minh Anh) ON UNIT 2 (A-102)
    SET IDENTITY_INSERT [core].[reservations] ON;

    INSERT INTO [core].[reservations] (
        id, reservation_code, customer_id, facility_id, unit_type_id, facility_rate_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, booking_fee_snapshot,
        discount_snapshot, quoted_total, hold_until, status, confirmed_at, created_at, updated_at
    )
    VALUES (
        1003, 'RSV-HCM-003', 1, 1, 1, 1,
        CAST(GETUTCDATE() AS date), DATEADD(month, 6, CAST(GETUTCDATE() AS date)),
        2000.00, 2000.00, 1000.00, 0.00, 15000.00,
        DATEADD(minute, 15, DATEADD(day, -7, SYSUTCDATETIME())), 'checked_in', DATEADD(day, -7, SYSUTCDATETIME()),
        DATEADD(day, -7, SYSUTCDATETIME()), DATEADD(day, -7, SYSUTCDATETIME())
    );

    SET IDENTITY_INSERT [core].[reservations] OFF;

    SET IDENTITY_INSERT [core].[rental_agreements] ON;

    INSERT INTO [core].[rental_agreements] (
        id, agreement_no, reservation_id, customer_id, facility_id, policy_version_id,
        start_date, end_date, monthly_rate_snapshot, deposit_snapshot, deposit_balance,
        status, signed_at, checked_in_at, created_at, updated_at
    )
    VALUES (
        1003, 'AGR-HCM-2026-0003', 1003, 1, 1, 1,
        CAST(GETUTCDATE() AS date), DATEADD(month, 6, CAST(GETUTCDATE() AS date)),
        2000.00, 2000.00, 2000.00,
        'active', DATEADD(day, -7, SYSUTCDATETIME()), DATEADD(day, -7, SYSUTCDATETIME()),
        DATEADD(day, -7, SYSUTCDATETIME()), DATEADD(day, -7, SYSUTCDATETIME())
    );

    SET IDENTITY_INSERT [core].[rental_agreements] OFF;

    SET IDENTITY_INSERT [core].[unit_allocations] ON;

    INSERT INTO [core].[unit_allocations] (
        id, storage_unit_id, reservation_id, agreement_id, allocation_kind,
        allocation_start_date, allocation_end_date, status, reason, assigned_by, created_at, ended_at
    )
    VALUES (
        5, 2, NULL, 1003, 'rental',
        CAST(GETUTCDATE() AS date), DATEADD(month, 6, CAST(GETUTCDATE() AS date)),
        'active', N'Phân bổ ô A-102 cho hợp đồng AGR-HCM-2026-0003', 3, SYSUTCDATETIME(), NULL
    );

    SET IDENTITY_INSERT [core].[unit_allocations] OFF;
    PRINT N'- Seeded reservation 1003 and agreement 1003 successfully.';

    -- 3. SEED UNIT TRANSFER REQUESTS (core.unit_transfer_requests)
    -- Distinct agreements to satisfy unit_transfer_requests_one_open_uidx: 1001, 1002, 1003
    SET IDENTITY_INSERT [core].[unit_transfer_requests] ON;

    INSERT INTO [core].[unit_transfer_requests] (
        id, agreement_id, requested_unit_type_id, from_unit_id, to_unit_id,
        requested_effective_date, reason, status, requested_by, reviewed_by, reviewed_at, completed_at, created_at
    )
    VALUES
        (8001, 1001, 2, 1, 4, 
         DATEADD(day, 5, CAST(GETUTCDATE() AS date)), 
         N'Hàng hóa gia tăng dịp cuối năm, cần đổi từ ô nhỏ 3m2 lên ô vừa 6m2 khô thoáng', 
         'pending', 7, NULL, NULL, NULL, DATEADD(hour, -5, SYSUTCDATETIME())),

        (8002, 1002, 4, 6, NULL, 
         DATEADD(day, 10, CAST(GETUTCDATE() AS date)), 
         N'Cần chuyển sang lưu trữ thiết bị âm thanh và nhạc cụ tại kho kiểm soát nhiệt độ ấm', 
         'pending', 7, NULL, NULL, NULL, DATEADD(day, -1, SYSUTCDATETIME())),

        (8003, 1003, 1, 2, 3, 
         DATEADD(day, 3, CAST(GETUTCDATE() AS date)), 
         N'Đổi sang ô A-103 gần lối đi chính để thuận tiện di chuyển pallet hàng', 
         'approved', 1, 4, DATEADD(hour, -2, SYSUTCDATETIME()), NULL, DATEADD(day, -2, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[unit_transfer_requests] OFF;
    PRINT N'- Seeded unit_transfer_requests successfully.';

    -- 4. SEED MOVE-OUT REQUESTS (core.move_out_requests)
    -- Distinct agreements to satisfy move_out_requests_one_open_uidx: 1001, 1002, 1003
    SET IDENTITY_INSERT [core].[move_out_requests] ON;

    INSERT INTO [core].[move_out_requests] (
        id, agreement_id, requested_by, requested_move_out_date, appointment_id,
        status, reason, finalized_at, created_at, updated_at
    )
    VALUES
        (8101, 1001, 7, CAST(GETUTCDATE() AS date), NULL, 
         'scheduled', N'Hết thời hạn dự án kinh doanh, hoàn trả mặt bằng ô kho A-101 đúng hạn', NULL, 
         DATEADD(day, -2, SYSUTCDATETIME()), DATEADD(day, -1, SYSUTCDATETIME())),

        (8102, 1002, 7, DATEADD(day, 1, CAST(GETUTCDATE() AS date)), NULL, 
         'requested', N'Chuyển cơ sở kinh doanh sang chi nhánh mới, đề nghị làm thủ tục thanh lý', NULL, 
         DATEADD(hour, -6, SYSUTCDATETIME()), DATEADD(hour, -6, SYSUTCDATETIME())),

        (8103, 1003, 1, CAST(GETUTCDATE() AS date), NULL, 
         'inspection_pending', N'Đã dọn sạch đồ đạc trong ô A-102, chờ nhân viên kiểm tra hiện trạng bàn giao', NULL, 
         DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(hour, -2, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[move_out_requests] OFF;
    PRINT N'- Seeded move_out_requests successfully.';

    -- 5. SEED TICKET CHARGE PROPOSALS (core.ticket_charge_proposals)
    SET IDENTITY_INSERT [core].[ticket_charge_proposals] ON;

    INSERT INTO [core].[ticket_charge_proposals] (
        id, ticket_id, proposed_by, description, amount, status, created_at, updated_at
    )
    VALUES
        (8201, 9001, 3, 
         N'Chi phí thay pin chuyên dụng 4 viên AA Lithium và bảo dưỡng bo mạch khóa ô kho A-101', 
         150000.00, 'pending', DATEADD(hour, -4, SYSUTCDATETIME()), DATEADD(hour, -4, SYSUTCDATETIME())),

        (8202, 9002, 3, 
         N'Phí cấp lại thẻ từ RFID vật lý bảo mật dự phòng cho khách hàng', 
         80000.00, 'pending', DATEADD(hour, -2, SYSUTCDATETIME()), DATEADD(hour, -2, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[ticket_charge_proposals] OFF;
    PRINT N'- Seeded ticket_charge_proposals successfully.';

    -- 6. SEED PAYMENTS & REFUNDS (core.payments & core.refunds)
    SET IDENTITY_INSERT [core].[payments] ON;

    INSERT INTO [core].[payments] (
        id, customer_id, target_invoice_id, amount, currency, method, provider,
        provider_transaction_id, idempotency_key, status, paid_at, created_at, updated_at
    )
    VALUES
        (8301, 7, 1001, 2000.00, 'VND', 'bank_transfer', 'vnpay', 
         'VNPAY-TEST-8301', 'IDEM-PAY-8301', 'succeeded', 
         DATEADD(day, -20, SYSUTCDATETIME()), DATEADD(day, -20, SYSUTCDATETIME()), DATEADD(day, -20, SYSUTCDATETIME())),

        (8302, 7, 1002, 2000.00, 'VND', 'bank_transfer', 'vnpay', 
         'VNPAY-TEST-8302', 'IDEM-PAY-8302', 'succeeded', 
         DATEADD(day, -10, SYSUTCDATETIME()), DATEADD(day, -10, SYSUTCDATETIME()), DATEADD(day, -10, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[payments] OFF;

    SET IDENTITY_INSERT [core].[refunds] ON;

    INSERT INTO [core].[refunds] (
        id, payment_id, agreement_id, amount, currency,
        reason, provider, idempotency_key, status, requested_by,
        created_at, updated_at
    )
    VALUES
        (8301, 8301, 1001, 2000.00, 'VND', 
         N'Hoàn trả tiền đặt cọc kết thúc hợp đồng AGR-HCM-2026-0001 (ô A-101 hoàn trả nguyên trạng)', 
         'vnpay', 'IDEM-REF-8301', 'pending', 7, 
         DATEADD(hour, -8, SYSUTCDATETIME()), DATEADD(hour, -8, SYSUTCDATETIME())),

        (8302, 8302, 1002, 1800.00, 'VND', 
         N'Hoàn cọc trả kho A-106 sau khi khấu trừ 200.000đ chi phí vệ sinh ô kho', 
         'vnpay', 'IDEM-REF-8302', 'pending', 7, 
         DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(day, -1, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[refunds] OFF;
    PRINT N'- Seeded refunds successfully.';

    -- 7. SEED OVERDUE INVOICES, DELINQUENCY CASES & ACTIONS (core.invoices, core.delinquency_cases, core.delinquency_actions)
    SET IDENTITY_INSERT [core].[invoices] ON;

    INSERT INTO [core].[invoices] (
        id, invoice_no, customer_id, agreement_id, billing_period, issue_date, due_date,
        currency, subtotal_amount, discount_amount, tax_amount, total_amount, paid_amount,
        status, opened_at, created_at, updated_at
    )
    VALUES
        (1003, 'INV-2026-0003', 7, 1001, '2026-09', 
         DATEADD(day, -45, CAST(GETUTCDATE() AS date)), 
         DATEADD(day, -35, CAST(GETUTCDATE() AS date)), 
         'VND', 3500000.00, 0.00, 350000.00, 3850000.00, 0.00, 
         'overdue', DATEADD(day, -45, SYSUTCDATETIME()), 
         DATEADD(day, -45, SYSUTCDATETIME()), DATEADD(day, -35, SYSUTCDATETIME())),

        (1004, 'INV-2026-0004', 1, 1003, '2026-10', 
         DATEADD(day, -10, CAST(GETUTCDATE() AS date)), 
         DATEADD(day, -3, CAST(GETUTCDATE() AS date)), 
         'VND', 2000000.00, 0.00, 200000.00, 2200000.00, 0.00, 
         'overdue', DATEADD(day, -10, SYSUTCDATETIME()), 
         DATEADD(day, -10, SYSUTCDATETIME()), DATEADD(day, -3, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[invoices] OFF;
    PRINT N'- Seeded overdue invoices successfully.';

    SET IDENTITY_INSERT [core].[delinquency_cases] ON;

    INSERT INTO [core].[delinquency_cases] (
        id, agreement_id, invoice_id, status, opened_at, grace_ends_at, outstanding_snapshot, notes
    )
    VALUES
        (8401, 1001, 1003, 'delinquent', 
         DATEADD(day, -35, SYSUTCDATETIME()), 
         DATEADD(day, -20, SYSUTCDATETIME()), 
         3850000.00, 
         N'Khách hàng chậm thanh toán tiền thuê kỳ 09/2026, quá hạn hơn 30 ngày.'),

        (8402, 1003, 1004, 'grace', 
         DATEADD(day, -3, SYSUTCDATETIME()), 
         DATEADD(day, 4, SYSUTCDATETIME()), 
         2200000.00, 
         N'Kỳ thanh toán 10/2026 quá hạn 3 ngày, hiện đang trong thời gian ân hạn 7 ngày.');

    SET IDENTITY_INSERT [core].[delinquency_cases] OFF;
    PRINT N'- Seeded delinquency_cases successfully.';

    SET IDENTITY_INSERT [core].[delinquency_actions] ON;

    INSERT INTO [core].[delinquency_actions] (
        id, delinquency_case_id, action_type, performed_by, details, occurred_at
    )
    VALUES
        (8401, 8401, 'reminder_sent', 4, 
         N'{"channel":"email_and_sms","note":"Đã gửi văn bản nhắc nợ lần 2 qua Email và tin nhắn SMS đến số 0907123456"}', 
         DATEADD(day, -15, SYSUTCDATETIME())),

        (8402, 8401, 'customer_contacted', 4, 
         N'{"contact_method":"phone_call","result":"Khách xác nhận đang đi công tác tỉnh, hứa sắp xếp thanh toán trước thứ 6"}', 
         DATEADD(day, -5, SYSUTCDATETIME()));

    SET IDENTITY_INSERT [core].[delinquency_actions] OFF;
    PRINT N'- Seeded delinquency_actions successfully.';

    COMMIT TRANSACTION;
    PRINT N'==> ALL MANAGER OPERATIONS TEST DATA SEEDED SUCCESSFULLY! <==';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSev INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR(@ErrMsg, @ErrSev, @ErrState);
END CATCH;
GO
