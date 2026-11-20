-- ======================================================================================
-- SCRIPT: Seed_20_Units_Each_Facility.sql
-- MÔ TẢ:
-- Tự động tìm ID của 2 cơ sở (Thủ Đức & Cầu Giấy), tự động đảm bảo có đủ Zone A và Zone B,
-- và nạp đủ 20 ô kho cho mỗi cơ sở (tổng 40 ô kho) chia cho 6 loại kho có các cặp tương ứng.
-- 
-- KHẮC PHỤC TRIỆT ĐỂ:
-- Lỗi "Cannot insert the value NULL into column 'area_id'" bằng cách tự động dò tìm hoặc
-- tạo mới Zone A và Zone B cho từng cơ sở trước khi nạp ô kho.
-- ======================================================================================

USE [SelfStoragePRN222];
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT N'>>> 1. Vô hiệu hóa constraints và triggers tạm thời...';
    ALTER TABLE [core].[storage_units] NOCHECK CONSTRAINT ALL;
    ALTER TABLE [core].[unit_map_positions] NOCHECK CONSTRAINT ALL;

    PRINT N'>>> 2. Xác định ID của Cơ sở Thủ Đức và Cơ sở Cầu Giấy...';
    DECLARE @FacTD BIGINT = (SELECT TOP 1 id FROM [core].[facilities] WHERE [code] = 'HCM-TD' OR [name] LIKE N'%Thủ Đức%');
    IF @FacTD IS NULL SET @FacTD = 1;

    DECLARE @FacCG BIGINT = (SELECT TOP 1 id FROM [core].[facilities] WHERE [code] = 'HN-CG' OR [city] = N'Hà Nội' OR [name] LIKE N'%Cầu Giấy%');
    IF @FacCG IS NULL SET @FacCG = 2;

    PRINT N'   -> Cơ sở Thủ Đức ID: ' + CAST(@FacTD AS NVARCHAR(10));
    PRINT N'   -> Cơ sở Cầu Giấy ID: ' + CAST(@FacCG AS NVARCHAR(10));

    -- Kích hoạt cả 2 cơ sở active
    UPDATE [core].[facilities]
    SET [status] = 'active', [updated_at] = SYSUTCDATETIME()
    WHERE [id] IN (@FacTD, @FacCG);

    -- Ẩn các cơ sở khác nếu có
    UPDATE [core].[facilities]
    SET [status] = 'inactive', [updated_at] = SYSUTCDATETIME()
    WHERE [id] NOT IN (@FacTD, @FacCG);

    -- Dọn dẹp các ô kho cũ ngoài danh mục A-101..A-110, B-101..B-110
    DELETE FROM [core].[unit_map_positions] 
    WHERE [unit_id] IN (
        SELECT id FROM [core].[storage_units] 
        WHERE [facility_id] IN (@FacTD, @FacCG) AND [unit_code] NOT LIKE 'A-%' AND [unit_code] NOT LIKE 'B-%'
    );
    DELETE FROM [core].[storage_units] 
    WHERE [facility_id] IN (@FacTD, @FacCG) AND [unit_code] NOT LIKE 'A-%' AND [unit_code] NOT LIKE 'B-%';

    PRINT N'>>> 3. Đảm bảo mỗi cơ sở đều có đủ Zone A và Zone B (chống NULL area_id)...';
    DECLARE @FacilityZones TABLE (
        [facility_id] BIGINT,
        [zone_label] NVARCHAR(10),
        [area_id] BIGINT
    );

    DECLARE @CurFac BIGINT;
    DECLARE fac_cur CURSOR LOCAL FAST_FORWARD FOR SELECT @FacTD UNION SELECT @FacCG;
    OPEN fac_cur;
    FETCH NEXT FROM fac_cur INTO @CurFac;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Tìm Floor tầng 1
        DECLARE @FloorId BIGINT = (
            SELECT TOP 1 id FROM [core].[facility_areas] 
            WHERE facility_id = @CurFac AND (code LIKE '%F1%' OR name LIKE '%Floor 1%')
            ORDER BY id ASC
        );
        IF @FloorId IS NULL
        BEGIN
            INSERT INTO [core].[facility_areas] ([facility_id], [code], [name], [area_type], [display_order], [map_metadata], [is_active])
            VALUES (@CurFac, 'F1', N'Floor 1', 'floor', 1, N'{}', 1);
            SET @FloorId = SCOPE_IDENTITY();
        END;

        -- Tìm hoặc tạo Zone A
        DECLARE @ZaId BIGINT = (
            SELECT TOP 1 id FROM [core].[facility_areas] 
            WHERE facility_id = @CurFac AND (code LIKE '%ZA%' OR name LIKE '%Zone A%')
            ORDER BY id ASC
        );
        IF @ZaId IS NULL
        BEGIN
            INSERT INTO [core].[facility_areas] ([facility_id], [parent_area_id], [code], [name], [area_type], [display_order], [map_metadata], [is_active])
            VALUES (@CurFac, @FloorId, 'F1-ZA', N'Floor 1 - Zone A', 'zone', 1, N'{}', 1);
            SET @ZaId = SCOPE_IDENTITY();
        END;

        -- Tìm hoặc tạo Zone B
        DECLARE @ZbId BIGINT = (
            SELECT TOP 1 id FROM [core].[facility_areas] 
            WHERE facility_id = @CurFac AND (code LIKE '%ZB%' OR name LIKE '%Zone B%')
            ORDER BY id ASC
        );
        IF @ZbId IS NULL
        BEGIN
            INSERT INTO [core].[facility_areas] ([facility_id], [parent_area_id], [code], [name], [area_type], [display_order], [map_metadata], [is_active])
            VALUES (@CurFac, @FloorId, 'F1-ZB', N'Floor 1 - Zone B', 'zone', 2, N'{}', 1);
            SET @ZbId = SCOPE_IDENTITY();
        END;

        INSERT INTO @FacilityZones ([facility_id], [zone_label], [area_id])
        VALUES (@CurFac, 'A', @ZaId), (@CurFac, 'B', @ZbId);

        FETCH NEXT FROM fac_cur INTO @CurFac;
    END;
    CLOSE fac_cur;
    DEALLOCATE fac_cur;

    PRINT N'>>> 4. Cập nhật biểu giá facility_rates (1.000đ - 2.000đ) cho cả 2 cơ sở...';
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
    WHERE [facility_id] IN (@FacTD, @FacCG);

    -- Cập nhật phân công nhân viên Cầu Giấy (manager1, staff1)
    UPDATE [core].[staff_facility_assignments]
    SET [facility_id] = @FacCG
    WHERE [employee_id] IN (
        SELECT id FROM [core].[users] WHERE email IN ('manager1@gmail.com', 'staff1@gmail.com')
    );

    PRINT N'>>> 5. Thiết lập danh mục 20 mẫu ô kho chia 6 loại có các cặp tương ứng...';
    DECLARE @UnitTemplate TABLE (
        [unit_code] NVARCHAR(50),
        [zone_label] NVARCHAR(10),      -- 'A' hoặc 'B'
        [unit_type_id] BIGINT,
        [storage_category] NVARCHAR(100),
        [width_m] DECIMAL(10,2),
        [length_m] DECIMAL(10,2),
        [height_m] DECIMAL(10,2),
        [area_m2] DECIMAL(10,2),
        [base_price_sqm] DECIMAL(18,2),
        [category_surcharge] DECIMAL(18,2),
        [custom_monthly_rate] DECIMAL(18,2),
        [map_x] DECIMAL(10,2),
        [map_y] DECIMAL(10,2),
        [map_width] DECIMAL(10,2),
        [map_height] DECIMAL(10,2)
    );

    INSERT INTO @UnitTemplate
    VALUES
    -- ============================================================================
    -- ZONE A (A-101 -> A-110)
    -- ============================================================================
    -- Cặp 1: S-DRY (3 m²) - Kho hàng khô nhỏ
    (N'A-101', N'A', 1, N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 5.00, 5.00, 22.00, 16.00),
    (N'A-102', N'A', 1, N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 32.00, 5.00, 22.00, 16.00),

    -- Cặp 2: MINI-DRY (1 m²) - Tủ đồ thông minh mini
    (N'A-103', N'A', 6, N'Kho hàng khô', 1.00, 1.00, 1.20, 1.00, 100.00, 100.00, 1000.00, 59.00, 5.00, 22.00, 16.00),
    (N'A-104', N'A', 6, N'Kho hàng khô', 1.00, 1.00, 1.20, 1.00, 100.00, 100.00, 1000.00, 86.00, 5.00, 22.00, 16.00),

    -- Cặp 3: M-DRY (6 m²) - Kho hàng khô vừa
    (N'A-105', N'A', 2, N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 113.00, 5.00, 22.00, 16.00),
    (N'A-106', N'A', 2, N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 5.00, 35.00, 22.00, 16.00),

    -- Cặp 4: S-SEAFOOD (3 m²) - Kho lạnh / Thủy hải sản
    (N'A-107', N'A', 7, N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 32.00, 35.00, 22.00, 16.00),
    (N'A-108', N'A', 7, N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 59.00, 35.00, 22.00, 16.00),

    -- Cặp 5: M-WARM (6 m²) - Kho sưởi ấm vừa
    (N'A-109', N'A', 8, N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 86.00, 35.00, 22.00, 16.00),
    (N'A-110', N'A', 8, N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 113.00, 35.00, 22.00, 16.00),

    -- ============================================================================
    -- ZONE B (B-101 -> B-110)
    -- ============================================================================
    -- Cặp 6: S-DRY (3 m²) - Kho hàng khô nhỏ
    (N'B-101', N'B', 1, N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 5.00, 5.00, 22.00, 16.00),
    (N'B-102', N'B', 1, N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 32.00, 5.00, 22.00, 16.00),

    -- Cặp 7: M-DRY (6 m²) - Kho hàng khô vừa
    (N'B-103', N'B', 2, N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 59.00, 5.00, 22.00, 16.00),
    (N'B-104', N'B', 2, N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 86.00, 5.00, 22.00, 16.00),

    -- Cặp 8: S-SEAFOOD (3 m²) - Kho lạnh / Thủy hải sản
    (N'B-105', N'B', 7, N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 113.00, 5.00, 22.00, 16.00),
    (N'B-106', N'B', 7, N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 5.00, 35.00, 22.00, 16.00),

    -- Cặp 9: M-WARM (6 m²) - Kho sưởi ấm vừa
    (N'B-107', N'B', 8, N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 32.00, 35.00, 22.00, 16.00),
    (N'B-108', N'B', 8, N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 59.00, 35.00, 22.00, 16.00),

    -- Cặp 10: XL-CLIMATE (16 m²) - Kho mát điều hòa cực lớn
    (N'B-109', N'B', 5, N'Kho mát điều hòa', 4.00, 4.00, 3.00, 16.00, 100.00, 500.00, 2000.00, 86.00, 35.00, 22.00, 16.00),
    (N'B-110', N'B', 5, N'Kho mát điều hòa', 4.00, 4.00, 3.00, 16.00, 100.00, 500.00, 2000.00, 113.00, 35.00, 22.00, 16.00);

    PRINT N'>>> 6. Đồng bộ 20 ô kho vào [core].[storage_units]...';
    MERGE INTO [core].[storage_units] AS target
    USING (
        SELECT 
            f.fac_id AS facility_id,
            u.unit_code,
            z.area_id,
            u.zone_label,
            N'1' AS floor_label,
            u.unit_type_id,
            u.storage_category,
            u.width_m AS custom_width_m,
            u.length_m AS custom_length_m,
            u.height_m AS custom_height_m,
            u.area_m2 AS custom_area_m2,
            u.base_price_sqm AS base_price_per_sqm,
            u.category_surcharge,
            u.custom_monthly_rate,
            u.storage_category + N' (' + CAST(u.area_m2 AS NVARCHAR(10)) + N' m2)' AS notes,
            CASE WHEN f.fac_id = @FacTD AND u.unit_code = 'A-101' THEN 'occupied' ELSE 'available' END AS physical_status,
            1 AS is_listed
        FROM (SELECT @FacTD AS fac_id UNION SELECT @FacCG) f
        CROSS JOIN @UnitTemplate u
        JOIN @FacilityZones z ON z.facility_id = f.fac_id AND z.zone_label = u.zone_label
    ) AS src
    ON (target.facility_id = src.facility_id AND target.unit_code = src.unit_code)
    WHEN MATCHED THEN
        UPDATE SET 
            target.unit_type_id = src.unit_type_id,
            target.area_id = src.area_id,
            target.floor_label = src.floor_label,
            target.zone_label = src.zone_label,
            target.physical_status = src.physical_status,
            target.is_listed = src.is_listed,
            target.custom_width_m = src.custom_width_m,
            target.custom_length_m = src.custom_length_m,
            target.custom_height_m = src.custom_height_m,
            target.custom_area_m2 = src.custom_area_m2,
            target.storage_category = src.storage_category,
            target.base_price_per_sqm = src.base_price_per_sqm,
            target.category_surcharge = src.category_surcharge,
            target.custom_monthly_rate = src.custom_monthly_rate,
            target.notes = src.notes,
            target.updated_at = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (
            facility_id, unit_type_id, area_id, unit_code,
            floor_label, zone_label, physical_status, is_listed,
            custom_width_m, custom_length_m, custom_height_m, custom_area_m2,
            storage_category, base_price_per_sqm, category_surcharge,
            custom_monthly_rate, notes, created_at, updated_at
        )
        VALUES (
            src.facility_id, src.unit_type_id, src.area_id, src.unit_code,
            src.floor_label, src.zone_label, src.physical_status, src.is_listed,
            src.custom_width_m, src.custom_length_m, src.custom_height_m, src.custom_area_m2,
            src.storage_category, src.base_price_per_sqm, src.category_surcharge,
            src.custom_monthly_rate, src.notes, SYSUTCDATETIME(), SYSUTCDATETIME()
        );

    -- Gán lại hợp đồng quá hạn vào ô A-101 ở Thủ Đức nếu chưa được gán
    DECLARE @UnitA101_TD BIGINT = (SELECT id FROM [core].[storage_units] WHERE facility_id = @FacTD AND unit_code = 'A-101');
    DECLARE @AgrId BIGINT = (SELECT TOP 1 id FROM [core].[rental_agreements] WHERE facility_id = @FacTD AND status = 'overdue');
    IF @UnitA101_TD IS NOT NULL AND @AgrId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [core].[unit_allocations] WHERE agreement_id = @AgrId)
        BEGIN
            INSERT INTO [core].[unit_allocations] (
                [storage_unit_id], [agreement_id], [allocation_kind], [allocation_start_date], [status], [assigned_by], [created_at]
            )
            VALUES (
                @UnitA101_TD, @AgrId, 'primary', DATEADD(DAY, -31, CAST(GETDATE() AS DATE)), 'active', 1, SYSUTCDATETIME()
            );
        END
        ELSE
        BEGIN
            UPDATE [core].[unit_allocations]
            SET [storage_unit_id] = @UnitA101_TD
            WHERE [agreement_id] = @AgrId;
        END;
    END;

    PRINT N'>>> 7. Đồng bộ tọa độ sơ đồ bản đồ trong [core].[unit_map_positions]...';
    MERGE INTO [core].[unit_map_positions] AS target
    USING (
        SELECT 
            u.id AS unit_id,
            u.area_id,
            t.map_x AS x,
            t.map_y AS y,
            t.map_width AS width,
            t.map_height AS height,
            0 AS rotation_degrees,
            N'{}' AS metadata
        FROM [core].[storage_units] u
        JOIN @UnitTemplate t ON u.unit_code = t.unit_code
        WHERE u.facility_id IN (@FacTD, @FacCG)
    ) AS src
    ON (target.unit_id = src.unit_id)
    WHEN MATCHED THEN
        UPDATE SET 
            target.area_id = src.area_id,
            target.x = src.x,
            target.y = src.y,
            target.width = src.width,
            target.height = src.height,
            target.rotation_degrees = src.rotation_degrees
    WHEN NOT MATCHED THEN
        INSERT (unit_id, area_id, x, y, width, height, rotation_degrees, metadata)
        VALUES (src.unit_id, src.area_id, src.x, src.y, src.width, src.height, src.rotation_degrees, src.metadata);

    PRINT N'>>> 8. Bật lại constraints và triggers...';
    ALTER TABLE [core].[storage_units] CHECK CONSTRAINT ALL;
    ALTER TABLE [core].[unit_map_positions] CHECK CONSTRAINT ALL;

    COMMIT TRANSACTION;

    PRINT N'======================================================================';
    PRINT N'>>> THÀNH CÔNG RỰC RỠ:';
    PRINT N'    - Cơ sở Thủ Đức (ID=' + CAST(@FacTD AS NVARCHAR(10)) + N'): 20 ô kho (A-101..A-110, B-101..B-110)';
    PRINT N'    - Cơ sở Cầu Giấy (ID=' + CAST(@FacCG AS NVARCHAR(10)) + N'): 20 ô kho (A-101..A-110, B-101..B-110)';
    PRINT N'    - Tổng cộng: 40 ô kho chia đều cho 6 loại kho có các cặp tương ứng';
    PRINT N'    - Ô A-101 ở Thủ Đức: physical_status = occupied (Hợp đồng quá hạn)';
    PRINT N'    - 39 ô kho còn lại: physical_status = available, is_listed = 1';
    PRINT N'======================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'LỖI THỰC THI: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
