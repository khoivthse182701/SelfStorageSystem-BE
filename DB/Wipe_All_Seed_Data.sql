-- ======================================================================================
-- SCRIPT XÓA SẠCH TOÀN BỘ DỮ LIỆU SEED & GIAO DỊCH TEST (G1SelfStorage)
-- Database: SelfStoragePRN222
-- Giữ nguyên: Danh mục cơ sở (Facilities), Tài khoản người dùng (Users, Roles, Profiles),
--             Loại kho (UnitTypes, Categories), Bảng giá (PricingConfig, Rates), Ca trực.
-- Đặt lại: Toàn bộ 100% ô kho về trạng thái 'available' (sẵn sàng cho thuê).
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

    PRINT N'>>> 2. Xóa sạch dữ liệu giao dịch seed...';

    -- Support tickets & interactions
    DELETE FROM [core].[ticket_messages];
    DELETE FROM [core].[ticket_attachments];
    DELETE FROM [core].[ticket_assignments];
    DELETE FROM [core].[ticket_charge_approvals];
    DELETE FROM [core].[ticket_charge_proposals];
    DELETE FROM [core].[staff_tasks];
    DELETE FROM [core].[service_ratings];
    DELETE FROM [core].[maintenance_work_orders];
    DELETE FROM [core].[support_tickets];

    -- Delinquency
    DELETE FROM [core].[delinquency_actions];
    DELETE FROM [core].[delinquency_cases];

    -- Payments, Invoices & Refunds
    DELETE FROM [core].[refund_approvals];
    DELETE FROM [core].[refunds];
    DELETE FROM [core].[payment_allocations];
    DELETE FROM [core].[payments];
    DELETE FROM [core].[invoice_lines];
    DELETE FROM [core].[invoices];
    DELETE FROM [core].[promotion_redemptions];

    -- Inspections & Handover
    DELETE FROM [core].[handover_records];
    DELETE FROM [core].[inspection_items];
    DELETE FROM [core].[inspections];

    -- Access & Stored items
    DELETE FROM [core].[access_events];
    DELETE FROM [core].[access_credentials];
    DELETE FROM [core].[authorized_access_members];
    DELETE FROM [core].[stored_items];

    -- Move outs, Transfers, Renewals, Agreements, Reservations
    DELETE FROM [core].[appointments];
    DELETE FROM [core].[identity_verifications];
    DELETE FROM [core].[move_out_requests];
    DELETE FROM [core].[unit_transfer_requests];
    DELETE FROM [core].[rental_renewals];
    DELETE FROM [core].[unit_allocations];
    DELETE FROM [core].[rental_agreements];
    DELETE FROM [core].[reservations];

    -- Clean logs / notifications / status history
    DELETE FROM [core].[notifications];
    DELETE FROM [core].[integration_events];
    DELETE FROM [core].[unit_status_history];

    PRINT N'>>> 3. Đặt lại trạng thái toàn bộ ô kho về "available"...';
    UPDATE [core].[storage_units]
    SET [physical_status] = N'available',
        [is_listed] = 1,
        [updated_at] = SYSUTCDATETIME();

    PRINT N'>>> 4. Bật lại constraints và triggers...';
    EXEC sp_msforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';
    EXEC sp_msforeachtable 'ALTER TABLE ? ENABLE TRIGGER ALL';

    COMMIT TRANSACTION;
    PRINT N'>>> ĐÃ XÓA SẠCH TOÀN BỘ DATA SEED THÀNH CÔNG!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@ErrMsg, 16, 1);
END CATCH;
