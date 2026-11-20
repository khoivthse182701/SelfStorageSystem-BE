-- ======================================================================================
-- SCRIPT: Seed_20_Units_Each_Facility.sql
-- MÔ TẢ:
-- Thêm/Cập nhật đủ 20 ô kho cho cả 2 cơ sở:
--   1. Cơ sở Thủ Đức (facility_id = 1) -> 20 ô kho (A-101..A-110, B-101..B-110)
--   2. Cơ sở Cầu Giấy (facility_id = 6) -> 20 ô kho (A-101..A-110, B-101..B-110)
-- Chia đều cho 6 loại kho với các cặp tương ứng giống hệt nhau về loại và kích thước:
--   - Cặp 1: MINI-DRY (1 m² - 1.2 m³) -> A-103, A-104
--   - Cặp 2: S-DRY (3 m² - 7.5 m³) -> A-101, A-102 & B-101, B-102
--   - Cặp 3: M-DRY (6 m² - 15.0 m³) -> A-105, A-106 & B-103, B-104
--   - Cặp 4: S-SEAFOOD (3 m² - 7.5 m³) -> A-107, A-108 & B-105, B-106
--   - Cặp 5: M-WARM (6 m² - 15.0 m³) -> A-109, A-110 & B-107, B-108
--   - Cặp 6: XL-CLIMATE (16 m² - 48.0 m³) -> B-109, B-110
--
-- Đặc điểm tối ưu cho UAT / Kiểm thử chuyển kho:
--   - Có cặp cùng loại & cùng kích thước (chuyển đổi ngang hàng)
--   - Có cặp cùng kích thước nhưng khác loại (3m² Khô vs Lạnh, 6m² Khô vs Sưởi)
--   - Đơn giá chuẩn 100đ / m², giá thuê tháng 1.000đ - 2.000đ
--   - Giữ nguyên 'occupied' cho A-101 ở Thủ Đức (ID=121) cho hợp đồng quá hạn
--   - Tọa độ sơ đồ kho map positions chuẩn hóa cho cả 40 ô kho
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

    -- Bảng tạm chứa thông tin 20 mẫu thiết kế ô kho chuẩn
    DECLARE @UnitTemplate TABLE (
        [unit_code] NVARCHAR(50),
        [zone_code] NVARCHAR(20),       -- 'F1-ZA' hoặc 'F1-ZB'
        [zone_label] NVARCHAR(10),      -- 'A' hoặc 'B'
        [unit_type_id] BIGINT,
        [type_code] NVARCHAR(50),
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

    -- Nạp danh sách 20 ô kho chuẩn:
    -- ============================================================================
    -- ZONE A (A-101 -> A-110)
    -- ============================================================================
    INSERT INTO @UnitTemplate
    VALUES
    -- Cặp 1: S-DRY (3 m²) - Kho hàng khô nhỏ
    (N'A-101', N'F1-ZA', N'A', 1, N'S-DRY', N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 5.00, 5.00, 22.00, 16.00),
    (N'A-102', N'F1-ZA', N'A', 1, N'S-DRY', N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 32.00, 5.00, 22.00, 16.00),

    -- Cặp 2: MINI-DRY (1 m²) - Tủ đồ thông minh mini
    (N'A-103', N'F1-ZA', N'A', 6, N'MINI-DRY', N'Kho hàng khô', 1.00, 1.00, 1.20, 1.00, 100.00, 100.00, 1000.00, 59.00, 5.00, 22.00, 16.00),
    (N'A-104', N'F1-ZA', N'A', 6, N'MINI-DRY', N'Kho hàng khô', 1.00, 1.00, 1.20, 1.00, 100.00, 100.00, 1000.00, 86.00, 5.00, 22.00, 16.00),

    -- Cặp 3: M-DRY (6 m²) - Kho hàng khô vừa
    (N'A-105', N'F1-ZA', N'A', 2, N'M-DRY', N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 113.00, 5.00, 22.00, 16.00),
    (N'A-106', N'F1-ZA', N'A', 2, N'M-DRY', N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 5.00, 35.00, 22.00, 16.00),

    -- Cặp 4: S-SEAFOOD (3 m²) - Kho lạnh / Thủy hải sản
    (N'A-107', N'F1-ZA', N'A', 7, N'S-SEAFOOD', N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 32.00, 35.00, 22.00, 16.00),
    (N'A-108', N'F1-ZA', N'A', 7, N'S-SEAFOOD', N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 59.00, 35.00, 22.00, 16.00),

    -- Cặp 5: M-WARM (6 m²) - Kho sưởi ấm vừa
    (N'A-109', N'F1-ZA', N'A', 8, N'M-WARM', N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 86.00, 35.00, 22.00, 16.00),
    (N'A-110', N'F1-ZA', N'A', 8, N'M-WARM', N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 113.00, 35.00, 22.00, 16.00),

    -- ============================================================================
    -- ZONE B (B-101 -> B-110)
    -- ============================================================================
    -- Cặp 6: S-DRY (3 m²) - Kho hàng khô nhỏ
    (N'B-101', N'F1-ZB', N'B', 1, N'S-DRY', N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 5.00, 5.00, 22.00, 16.00),
    (N'B-102', N'F1-ZB', N'B', 1, N'S-DRY', N'Kho hàng khô', 1.50, 2.00, 2.50, 3.00, 100.00, 100.00, 1000.00, 32.00, 5.00, 22.00, 16.00),

    -- Cặp 7: M-DRY (6 m²) - Kho hàng khô vừa
    (N'B-103', N'F1-ZB', N'B', 2, N'M-DRY', N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 59.00, 5.00, 22.00, 16.00),
    (N'B-104', N'F1-ZB', N'B', 2, N'M-DRY', N'Kho hàng khô', 2.00, 3.00, 2.50, 6.00, 100.00, 100.00, 1200.00, 86.00, 5.00, 22.00, 16.00),

    -- Cặp 8: S-SEAFOOD (3 m²) - Kho lạnh / Thủy hải sản
    (N'B-105', N'F1-ZB', N'B', 7, N'S-SEAFOOD', N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 113.00, 5.00, 22.00, 16.00),
    (N'B-106', N'F1-ZB', N'B', 7, N'S-SEAFOOD', N'Kho lạnh / Thủy hải sản', 1.50, 2.00, 2.50, 3.00, 100.00, 1000.00, 1300.00, 5.00, 35.00, 22.00, 16.00),

    -- Cặp 9: M-WARM (6 m²) - Kho sưởi ấm vừa
    (N'B-107', N'F1-ZB', N'B', 8, N'M-WARM', N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 32.00, 35.00, 22.00, 16.00),
    (N'B-108', N'F1-ZB', N'B', 8, N'M-WARM', N'Kho sưởi ấm', 2.00, 3.00, 2.50, 6.00, 100.00, 800.00, 1400.00, 59.00, 35.00, 22.00, 16.00),

    -- Cặp 10: XL-CLIMATE (16 m²) - Kho mát điều hòa cực lớn
    (N'B-109', N'F1-ZB', N'B', 5, N'XL-CLIMATE', N'Kho mát điều hòa', 4.00, 4.00, 3.00, 16.00, 100.00, 500.00, 2000.00, 86.00, 35.00, 22.00, 16.00),
    (N'B-110', N'F1-ZB', N'B', 5, N'XL-CLIMATE', N'Kho mát điều hòa', 4.00, 4.00, 3.00, 16.00, 100.00, 500.00, 2000.00, 113.00, 35.00, 22.00, 16.00);

    PRINT N'>>> 2. Đồng bộ 20 ô kho cho cả Cơ sở 1 (Thủ Đức) và Cơ sở 6 (Cầu Giấy)...';
    DECLARE @Facilities TABLE ([fac_id] BIGINT);
    INSERT INTO @Facilities VALUES (1), (6);

    MERGE INTO [core].[storage_units] AS target
    USING (
        SELECT 
            f.fac_id AS facility_id,
            u.unit_code,
            CASE WHEN u.zone_label = 'A' THEN za.id ELSE zb.id END AS area_id,
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
            CASE WHEN f.fac_id = 1 AND u.unit_code = 'A-101' THEN 'occupied' ELSE 'available' END AS physical_status,
            1 AS is_listed
        FROM @Facilities f
        CROSS JOIN @UnitTemplate u
        LEFT JOIN [core].[facility_areas] za ON za.facility_id = f.fac_id AND za.code = 'F1-ZA'
        LEFT JOIN [core].[facility_areas] zb ON zb.facility_id = f.fac_id AND zb.code = 'F1-ZB'
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

    PRINT N'>>> 3. Đồng bộ tọa độ sơ đồ bản đồ trong [core].[unit_map_positions]...';
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
        WHERE u.facility_id IN (1, 6)
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

    PRINT N'>>> 4. Bật lại constraints và triggers...';
    ALTER TABLE [core].[storage_units] CHECK CONSTRAINT ALL;
    ALTER TABLE [core].[unit_map_positions] CHECK CONSTRAINT ALL;

    COMMIT TRANSACTION;

    PRINT N'======================================================================';
    PRINT N'>>> THÀNH CÔNG RỰC RỠ:';
    PRINT N'    - Cơ sở Thủ Đức (ID=1): 20 ô kho (A-101..A-110, B-101..B-110)';
    PRINT N'    - Cơ sở Cầu Giấy (ID=6): 20 ô kho (A-101..A-110, B-101..B-110)';
    PRINT N'    - Tổng cộng: 40 ô kho chia đều cho 6 loại kho có các cặp tương ứng';
    PRINT N'    - Ô A-101 ở Thủ Đức: physical_status = occupied';
    PRINT N'    - 39 ô kho còn lại: physical_status = available, is_listed = 1';
    PRINT N'======================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'LỖI THỰC THI: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
