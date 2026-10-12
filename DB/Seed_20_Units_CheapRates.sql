/* ============================================================================
   Seed Script: 20 Storage Units & Cheap Pricing (<= 10,000 VND)
   Purpose:
     1. Seed đúng 20 kho (Storage Units) cho 2 cơ sở (HCM & Hà Nội) với đầy đủ
        loại kho, tầng, khu vực, trạng thái và tọa độ bản đồ 2D (unit_map_positions).
     2. Điều chỉnh toàn bộ giá thuê kho (monthly_rate), tiền đặt cọc (deposit_amount),
        phí đặt trước (booking_fee), khung giá (price_ranges) và phụ phí (fee_rules)
        về mức <= 10.000 VNĐ để test quét mã VietQR chuyển khoản thật qua SePay / MBBank.
     3. Cập nhật mật khẩu chuẩn cho các tài khoản demo (Customer, Staff, Manager, Admin).
============================================================================ */

USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'Starting data seed for 20 units and low pricing (<= 10.000 VND)...';

BEGIN TRANSACTION;

-- ----------------------------------------------------------------------------
-- 1. ĐỒNG BỘ MẬT KHẨU TÀI KHOẢN DEMO
-- ----------------------------------------------------------------------------
-- Đặt hash bcrypt giống với user Khôi Võ (hoặc hash chuẩn) để login không bị 'invalid credentials'
DECLARE @DefaultHash NVARCHAR(255) = (SELECT TOP 1 password_hash FROM [core].[users] WHERE id = 7);
IF @DefaultHash IS NULL
    SET @DefaultHash = N'$2a$11$Dp.xCM36JCoary5KwUaJeOfAVQtLeJBrUMoczEo2sQMdUUi74CRWC';

UPDATE [core].[users]
SET password_hash = @DefaultHash, status = 'active'
WHERE id IN (1, 2, 3, 4, 5, 6);

-- ----------------------------------------------------------------------------
-- 2. ĐẢM BẢO CÁC LOẠI KHO (UNIT TYPES)
-- ----------------------------------------------------------------------------
SET IDENTITY_INSERT [core].[unit_types] ON;

MERGE [core].[unit_types] AS target
USING (VALUES
    (1, 'S-DRY', 'Small Dry Goods Storage 3 m2', 1.50, 2.00, 2.50, 0, 600.00, 'Ambient dry storage optimal for non-perishable food, grains, boxes, and documents', 1),
    (2, 'M-DRY', 'Medium Dry Goods Storage 6 m2', 2.00, 3.00, 2.50, 0, 1200.00, 'Spacious ambient dry storage room for packaged inventory, textiles, and household furniture', 1),
    (3, 'M-SEAFOOD', 'Medium Seafood & Deep Freeze Storage 6 m2', 2.00, 3.00, 2.50, 1, 1000.00, 'Sub-zero cold storage specially calibrated for seafood preservation, frozen fish, and perishables', 1),
    (4, 'L-WARM', 'Large Heated Warm Storage 10 m2', 2.50, 4.00, 2.80, 1, 2000.00, 'Warmed temperature-regulated storage (22C to 26C) for musical instruments, delicate crafts, and audio gear', 1),
    (5, 'XL-CLIMATE', 'Extra Large Climate-Controlled Storage 16 m2', 4.00, 4.00, 3.00, 1, 3500.00, 'High-capacity dual climate and humidity controlled storage for commercial enterprise logistics', 1),
    (6, 'MINI-DRY', 'Mini Smart Dry Locker 1 m2', 1.00, 1.00, 1.20, 0, 200.00, 'High-security compact dry locker for personal gadgets, travel luggage, and confidential papers', 1),
    (7, 'S-SEAFOOD', 'Small Seafood Cold Storage 3 m2', 1.50, 2.00, 2.50, 1, 600.00, 'Compact chilled cold room for seafood samples, fishery batches, and frozen food containers', 1),
    (8, 'M-WARM', 'Medium Heated Warm Storage 6 m2', 2.00, 3.00, 2.50, 1, 1200.00, 'Medium heated constant-temperature unit designed for wooden instruments, vintage art, and dry electronics', 1)
) AS source (id, code, name, width_m, length_m, height_m, climate_controlled, max_weight_kg, description, is_active)
ON (target.id = source.id)
WHEN MATCHED THEN
    UPDATE SET 
        target.code = source.code,
        target.name = source.name,
        target.width_m = source.width_m,
        target.length_m = source.length_m,
        target.height_m = source.height_m,
        target.climate_controlled = source.climate_controlled,
        target.max_weight_kg = source.max_weight_kg,
        target.description = source.description,
        target.is_active = source.is_active
WHEN NOT MATCHED THEN
    INSERT (id, code, name, width_m, length_m, height_m, climate_controlled, max_weight_kg, description, is_active)
    VALUES (source.id, source.code, source.name, source.width_m, source.length_m, source.height_m, source.climate_controlled, source.max_weight_kg, source.description, source.is_active);

SET IDENTITY_INSERT [core].[unit_types] OFF;

-- ----------------------------------------------------------------------------
-- 3. ĐẢM BẢO CÁC KHU VỰC CƠ SỞ (FACILITY AREAS)
-- ----------------------------------------------------------------------------
SET IDENTITY_INSERT [core].[facility_areas] ON;

MERGE [core].[facility_areas] AS target
USING (VALUES
    -- Facility 1: Thu Duc Self Storage
    (1, 1, NULL, 'BLDG-A', 'Building A', 'building', 1),
    (2, 1, 1, 'F1', 'Floor 1', 'floor', 1),
    (3, 1, 2, 'F1-ZA', 'Floor 1 - Zone A', 'zone', 1),
    (6, 1, 2, 'F1-ZB', 'Floor 1 - Zone B', 'zone', 2),
    (7, 1, 1, 'F2', 'Floor 2', 'floor', 2),
    (8, 1, 7, 'F2-ZA', 'Floor 2 - Zone A', 'zone', 1),
    -- Facility 2: Cau Giay Self Storage
    (4, 2, NULL, 'BLDG-HN', 'Ha Noi Building', 'building', 1),
    (5, 2, 4, 'HN-F1', 'Floor 1', 'floor', 1),
    (9, 2, 5, 'HN-F1-ZA', 'Floor 1 - Zone A', 'zone', 1),
    (10, 2, 4, 'HN-F2', 'Floor 2', 'floor', 2)
) AS source (id, facility_id, parent_area_id, code, name, area_type, display_order)
ON (target.id = source.id)
WHEN MATCHED THEN
    UPDATE SET
        target.facility_id = source.facility_id,
        target.parent_area_id = source.parent_area_id,
        target.code = source.code,
        target.name = source.name,
        target.area_type = source.area_type,
        target.display_order = source.display_order
WHEN NOT MATCHED THEN
    INSERT (id, facility_id, parent_area_id, code, name, area_type, display_order)
    VALUES (source.id, source.facility_id, source.parent_area_id, source.code, source.name, source.area_type, source.display_order);

SET IDENTITY_INSERT [core].[facility_areas] OFF;

-- ----------------------------------------------------------------------------
-- ----------------------------------------------------------------------------
-- 4. KHUNG GIÁ TẬP ĐOÀN (PRICE RANGES) - ĐỀU <= 10.000 VNĐ
-- ----------------------------------------------------------------------------
SET IDENTITY_INSERT [core].[price_ranges] ON;

MERGE [core].[price_ranges] AS target
USING (VALUES
    (1, 1, 500.00, 3000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (2, 2, 1000.00, 6000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (3, 3, 2000.00, 8000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (4, 4, 3000.00, 9000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (5, 5, 4000.00, 10000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (6, 6, 500.00, 2000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (7, 7, 1500.00, 6000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5),
    (8, 8, 2500.00, 8000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 5)
) AS source (id, unit_type_id, min_monthly_rate, max_monthly_rate, valid_from, valid_to, created_by)
ON (target.id = source.id)
WHEN MATCHED THEN
    UPDATE SET
        target.unit_type_id = source.unit_type_id,
        target.min_monthly_rate = source.min_monthly_rate,
        target.max_monthly_rate = source.max_monthly_rate,
        target.valid_from = source.valid_from,
        target.valid_to = source.valid_to
WHEN NOT MATCHED THEN
    INSERT (id, unit_type_id, min_monthly_rate, max_monthly_rate, valid_from, valid_to, created_by)
    VALUES (source.id, source.unit_type_id, source.min_monthly_rate, source.max_monthly_rate, source.valid_from, source.valid_to, source.created_by);

SET IDENTITY_INSERT [core].[price_ranges] OFF;

-- ----------------------------------------------------------------------------
-- 5. GIÁ THUÊ KHO TẠI TỪNG CƠ SỞ (FACILITY RATES) - ĐỀU <= 10.000 VNĐ
-- ----------------------------------------------------------------------------
SET IDENTITY_INSERT [core].[facility_rates] ON;

MERGE [core].[facility_rates] AS target
USING (VALUES
    -- Facility 1: HCM (Thu Duc)
    (1,  1, 1, 2000.00, 2000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (2,  1, 2, 4000.00, 4000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (3,  1, 3, 5000.00, 5000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (6,  1, 4, 7000.00, 7000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (7,  1, 5, 10000.00, 10000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (8,  1, 6, 1000.00, 1000.00, 0.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (13, 1, 7, 3000.00, 3000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (14, 1, 8, 5000.00, 5000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),

    -- Facility 2: HN (Cau Giay)
    (4,  2, 1, 2000.00, 2000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (5,  2, 2, 4000.00, 4000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (9,  2, 3, 5000.00, 5000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (10, 2, 4, 7000.00, 7000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (11, 2, 5, 9000.00, 9000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (12, 2, 6, 1000.00, 1000.00, 0.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (15, 2, 7, 3000.00, 3000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4),
    (16, 2, 8, 5000.00, 5000.00, 1000.00, CONVERT(date, '2026-01-01', 23), CAST(NULL AS date), 4)
) AS source (id, facility_id, unit_type_id, monthly_rate, deposit_amount, booking_fee, valid_from, valid_to, created_by)
ON (target.id = source.id)
WHEN MATCHED THEN
    UPDATE SET
        target.facility_id = source.facility_id,
        target.unit_type_id = source.unit_type_id,
        target.monthly_rate = source.monthly_rate,
        target.deposit_amount = source.deposit_amount,
        target.booking_fee = source.booking_fee,
        target.valid_from = source.valid_from,
        target.valid_to = source.valid_to
WHEN NOT MATCHED THEN
    INSERT (id, facility_id, unit_type_id, monthly_rate, deposit_amount, booking_fee, valid_from, valid_to, created_by)
    VALUES (source.id, source.facility_id, source.unit_type_id, source.monthly_rate, source.deposit_amount, source.booking_fee, source.valid_from, source.valid_to, source.created_by);

SET IDENTITY_INSERT [core].[facility_rates] OFF;

-- ----------------------------------------------------------------------------
-- 6. PHỤ PHÍ & MÃ GIẢM GIÁ (FEE RULES & PROMOTIONS)
-- ----------------------------------------------------------------------------
UPDATE [core].[fee_rules]
SET amount = 5000.00
WHERE id = 2; -- Phí mất chìa khóa giảm từ 250k về 5k

UPDATE [core].[promotions]
SET max_discount_amount = 2000.00
WHERE id = 1; -- Voucher WELCOME10 giảm 10%, tối đa 2k

IF NOT EXISTS (SELECT 1 FROM [core].[promotions] WHERE code = 'GIAM1K')
BEGIN
    SET IDENTITY_INSERT [core].[promotions] ON;
    INSERT INTO [core].[promotions] (
        id, code, name, description, discount_type, discount_value,
        max_discount_amount, usage_limit, per_customer_limit,
        valid_from, valid_to, is_active, created_by
    )
    VALUES (
        2, 'GIAM1K', 'Giảm 1.000đ trực tiếp', 'Mã giảm cố định 1.000đ khi thanh toán thử nghiệm',
        'fixed', 1000.00, 1000.00, 1000, 10,
        CONVERT(datetimeoffset, '2026-01-01 00:00:00+07:00'), CONVERT(datetimeoffset, '2030-01-01 00:00:00+07:00'),
        1, 5
    );
    SET IDENTITY_INSERT [core].[promotions] OFF;

    INSERT INTO [core].[promotion_rules] (promotion_id, rule_type, operator, rule_value)
    VALUES (2, 'minimum_months', 'gte', N'1');
END;

-- ----------------------------------------------------------------------------
-- 7. SEED ĐÚNG 20 KHO (STORAGE UNITS)
-- ----------------------------------------------------------------------------
-- Xóa unit_map_positions cũ nếu cần để đồng bộ
DELETE FROM [core].[unit_map_positions] WHERE unit_id BETWEEN 1 AND 20;

-- Tạm tắt trigger kiểm tra chuyển trạng thái để merge mượt mà
ALTER TABLE [core].[storage_units] DISABLE TRIGGER [trg_storage_unit_status_transition];

-- Cập nhật hoặc chèn 20 kho
SET IDENTITY_INSERT [core].[storage_units] ON;

MERGE [core].[storage_units] AS target
USING (VALUES
    -- Cơ sở 1: Thu Duc Self Storage (14 kho)
    -- Tầng 1 - Zone A
    (1,  1, 1, 3, 'A-101', '1', 'A', 'available', 1, 'Small ambient dry storage near entrance (non-perishables, dry goods)'),
    (2,  1, 1, 3, 'A-102', '1', 'A', 'available', 1, 'Standard small ambient dry storage unit'),
    (3,  1, 1, 3, 'A-103', '1', 'A', 'available', 1, 'Corner ambient dry storage unit for household boxes'),
    (4,  1, 2, 3, 'A-104', '1', 'A', 'available', 1, 'Medium dry goods storage for packaged stock and furniture'),
    (5,  1, 2, 3, 'A-105', '1', 'A', 'available', 1, 'Spacious ambient dry storage room'),
    (6,  1, 3, 3, 'A-106', '1', 'A', 'available', 1, 'Seafood & deep freeze cold room (-18°C sub-zero chilled)'),
    (7,  1, 7, 3, 'A-107', '1', 'A', 'available', 1, 'Small chilled seafood & perishable food storage unit'),
    (8,  1, 4, 3, 'A-108', '1', 'A', 'available', 1, 'Large heated warm storage (24°C) for acoustic instruments & electronics'),
    (9,  1, 5, 3, 'A-109', '1', 'A', 'available', 1, 'Enterprise dual climate-controlled warehouse unit'),
    (10, 1, 6, 3, 'A-110', '1', 'A', 'available', 1, 'Smart dry locker for personal gadgets and documents'),

    -- Tầng 1 - Zone B
    (11, 1, 7, 6, 'B-101', '1', 'B', 'available', 1, 'Small chilled cold room for seafood & fishery goods in Zone B'),
    (12, 1, 8, 6, 'B-102', '1', 'B', 'available', 1, 'Medium heated room for warmth-sensitive equipment & art in Zone B'),
    (13, 1, 3, 6, 'B-103', '1', 'B', 'available', 1, 'Seafood & deep freeze cold room in Zone B'),
    (14, 1, 4, 6, 'B-104', '1', 'B', 'available', 1, 'Large heated warm storage room in Zone B'),

    -- Cơ sở 2: Cau Giay Self Storage (6 kho)
    (15, 2, 1, 5, 'HN-101', '1', 'A', 'available', 1, 'Hanoi dry storage unit for apparel & records'),
    (16, 2, 7, 5, 'HN-102', '1', 'A', 'available', 1, 'Hanoi cold storage for seafood and chilled freight'),
    (17, 2, 8, 5, 'HN-103', '1', 'A', 'available', 1, 'Hanoi heated warm storage for delicate winter cargo'),
    (18, 2, 3, 5, 'HN-104', '1', 'A', 'available', 1, 'Hanoi commercial seafood deep freeze unit'),
    (19, 2, 4, 5, 'HN-105', '1', 'A', 'available', 1, 'Hanoi large heated storage unit'),
    (20, 2, 5, 5, 'HN-106', '1', 'A', 'available', 1, 'Hanoi extra large climate-controlled logistics space')
) AS source (id, facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes)
ON (target.id = source.id)
WHEN MATCHED THEN
    UPDATE SET
        target.facility_id = source.facility_id,
        target.unit_type_id = source.unit_type_id,
        target.area_id = source.area_id,
        target.unit_code = source.unit_code,
        target.floor_label = source.floor_label,
        target.zone_label = source.zone_label,
        target.physical_status = CASE 
            WHEN target.physical_status IN ('occupied', 'reserved') THEN target.physical_status 
            ELSE source.physical_status 
        END,
        target.is_listed = source.is_listed,
        target.notes = source.notes
WHEN NOT MATCHED THEN
    INSERT (id, facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes)
    VALUES (source.id, source.facility_id, source.unit_type_id, source.area_id, source.unit_code, source.floor_label, source.zone_label, source.physical_status, source.is_listed, source.notes);

SET IDENTITY_INSERT [core].[storage_units] OFF;

ALTER TABLE [core].[storage_units] ENABLE TRIGGER [trg_storage_unit_status_transition];

-- ----------------------------------------------------------------------------
-- 8. TỌA ĐỘ BẢN ĐỒ 2D CHO CẢ 20 KHO (UNIT MAP POSITIONS)
-- ----------------------------------------------------------------------------
INSERT INTO [core].[unit_map_positions] (
    unit_id, area_id, x, y, width, height, rotation_degrees, metadata
)
VALUES
    -- Facility 1: Zone A (Area 3) - 10 units
    (1,  3, 10.00, 10.00, 15.00, 15.00, 0, N'{"col":1,"row":1}'),
    (2,  3, 30.00, 10.00, 15.00, 15.00, 0, N'{"col":2,"row":1}'),
    (3,  3, 50.00, 10.00, 15.00, 15.00, 0, N'{"col":3,"row":1}'),
    (4,  3, 70.00, 10.00, 20.00, 18.00, 0, N'{"col":4,"row":1}'),
    (5,  3, 95.00, 10.00, 20.00, 18.00, 0, N'{"col":5,"row":1}'),
    (6,  3, 10.00, 45.00, 20.00, 20.00, 0, N'{"col":1,"row":2}'),
    (7,  3, 35.00, 45.00, 20.00, 20.00, 0, N'{"col":2,"row":2}'),
    (8,  3, 60.00, 45.00, 25.00, 25.00, 0, N'{"col":3,"row":2}'),
    (9,  3, 90.00, 45.00, 30.00, 30.00, 0, N'{"col":4,"row":2}'),
    (10, 3, 125.00, 10.00, 10.00, 10.00, 0, N'{"col":6,"row":1}'),

    -- Facility 1: Zone B (Area 6) - 4 units
    (11, 6, 10.00, 15.00, 15.00, 15.00, 0, N'{"col":1,"row":1}'),
    (12, 6, 35.00, 15.00, 20.00, 20.00, 0, N'{"col":2,"row":1}'),
    (13, 6, 60.00, 15.00, 20.00, 20.00, 0, N'{"col":3,"row":1}'),
    (14, 6, 85.00, 15.00, 25.00, 25.00, 0, N'{"col":4,"row":1}'),

    -- Facility 2: Ha Noi Floor 1 (Area 5) - 6 units
    (15, 5, 10.00, 10.00, 15.00, 15.00, 0, N'{"col":1,"row":1}'),
    (16, 5, 30.00, 10.00, 15.00, 15.00, 0, N'{"col":2,"row":1}'),
    (17, 5, 50.00, 10.00, 20.00, 20.00, 0, N'{"col":3,"row":1}'),
    (18, 5, 10.00, 45.00, 20.00, 20.00, 0, N'{"col":1,"row":2}'),
    (19, 5, 35.00, 45.00, 25.00, 25.00, 0, N'{"col":2,"row":2}'),
    (20, 5, 65.00, 45.00, 30.00, 30.00, 0, N'{"col":3,"row":2}');

-- Tạo synonym phòng ngừa trường hợp raw query gọi không có schema [core]
IF OBJECT_ID('dbo.promotions', 'SN') IS NULL EXEC(N'CREATE SYNONYM dbo.promotions FOR core.promotions;');
IF OBJECT_ID('dbo.storage_units', 'SN') IS NULL EXEC(N'CREATE SYNONYM dbo.storage_units FOR core.storage_units;');

COMMIT TRANSACTION;

PRINT N'SUCCESS: 20 Storage Units and low price rates (<= 10.000 VND) have been seeded successfully!';
GO

-- ----------------------------------------------------------------------------
-- 9. KIỂM TRA LẠI DỮ LIỆU SAU KHI SEED
-- ----------------------------------------------------------------------------
SELECT 
    su.id AS UnitId,
    su.unit_code AS Code,
    f.name AS FacilityName,
    ut.name AS UnitType,
    fr.monthly_rate AS MonthlyRate_VND,
    fr.deposit_amount AS Deposit_VND,
    fr.booking_fee AS BookingFee_VND,
    su.floor_label AS Floor,
    su.zone_label AS Zone,
    su.physical_status AS Status
FROM [core].[storage_units] su
JOIN [core].[facilities] f ON su.facility_id = f.id
JOIN [core].[unit_types] ut ON su.unit_type_id = ut.id
JOIN [core].[facility_rates] fr ON fr.facility_id = su.facility_id AND fr.unit_type_id = su.unit_type_id
ORDER BY su.id;
GO
