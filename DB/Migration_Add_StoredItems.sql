/* ============================================================================
   SelfStoragePRN222 - Database Migration Script
   Mục đích:
     - Dành cho các thành viên trong nhóm ĐÃ CÓ SẴN DATABASE từ trước
       và muốn giữ nguyên dữ liệu test hiện tại.
     - Tự động kiểm tra và tạo mới bảng [core].stored_items nếu chưa có.
     - Tự động bổ sung cột [location_note] nếu bảng đã tồn tại nhưng thiếu cột.

   Cách dùng:
     - Mở file này trong SSMS (hoặc công cụ Database Tool bất kỳ).
     - Nhấn Execute (F5) để chạy. Hoàn toàn an toàn, không làm mất dữ liệu cũ.
============================================================================ */

USE [SelfStoragePRN222];
GO

-- 1. Nếu máy chưa có bảng stored_items -> Tự động tạo mới
IF OBJECT_ID(N'[core].[stored_items]', N'U') IS NULL
BEGIN
    CREATE TABLE [core].stored_items (
        id bigint IDENTITY(1,1) PRIMARY KEY,
        agreement_id bigint NOT NULL REFERENCES [core].rental_agreements(id) ON DELETE CASCADE,
        item_name nvarchar(255) NOT NULL,
        category nvarchar(100),
        description nvarchar(max),
        quantity int NOT NULL DEFAULT 1 CHECK (quantity > 0),
        estimated_value numeric(14, 2) CHECK (estimated_value IS NULL OR estimated_value >= 0),
        risk_classification nvarchar(50) NOT NULL DEFAULT 'standard',
        photo_url nvarchar(500),
        location_note nvarchar(255),
        declared_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at datetimeoffset(7) NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX stored_items_agreement_id_idx 
        ON [core].stored_items (agreement_id, category);

    PRINT N'Đã tạo mới bảng [core].stored_items thành công!';
END
ELSE
BEGIN
    -- 2. Nếu máy đã có bảng stored_items nhưng thiếu cột location_note -> Tự động thêm cột
    IF COL_LENGTH(N'[core].[stored_items]', N'location_note') IS NULL
    BEGIN
        ALTER TABLE [core].stored_items ADD location_note nvarchar(255) NULL;
        PRINT N'Đã bổ sung cột location_note thành công!';
    END
    ELSE
    BEGIN
        PRINT N'Database đã đầy đủ bảng và cột, không cần chỉnh sửa gì thêm!';
    END
END
GO
