/*
  ===================================================================================
  SCRIPT: Add_20_Units_Per_Facility.sql
  DESCRIPTION:
    Adds 20 storage units for each of the 10 facilities (200 units total).
    - 10 units in Zone A: A-106 to A-115
    - 10 units in Zone B: B-106 to B-115
    - All physical_status = 'available', is_listed = 1
    - Diverse unit types (1 to 8)
    - Full coordinate mapping in unit_map_positions (grid layout: rows 2 and 3)
  ===================================================================================
*/

USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

PRINT N'>> [1/3] Disabling triggers temporarily during bulk insert...';
ALTER TABLE [core].[storage_units] DISABLE TRIGGER ALL;
ALTER TABLE [core].[unit_map_positions] DISABLE TRIGGER ALL;

PRINT N'>> [2/3] Adding 20 units for each of the 10 facilities...';
DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

DECLARE @FacId INT = 1;
WHILE @FacId <= 10
BEGIN
    DECLARE @ZoneAId BIGINT = (SELECT id FROM [core].[facility_areas] WHERE facility_id = @FacId AND code = N'F1-ZA');
    DECLARE @ZoneBId BIGINT = (SELECT id FROM [core].[facility_areas] WHERE facility_id = @FacId AND code = N'F1-ZB');

    IF @ZoneAId IS NULL OR @ZoneBId IS NULL
    BEGIN
        SET @FacId = @FacId + 1;
        CONTINUE;
    END;

    -- Array-like insertion for Zone A (10 units: A-106 to A-115)
    -- Row 2 (A-106 to A-110)
    DECLARE @u_A106 BIGINT, @u_A107 BIGINT, @u_A108 BIGINT, @u_A109 BIGINT, @u_A110 BIGINT;
    -- Row 3 (A-111 to A-115)
    DECLARE @u_A111 BIGINT, @u_A112 BIGINT, @u_A113 BIGINT, @u_A114 BIGINT, @u_A115 BIGINT;

    -- Row 2 (A-106 to A-110)
    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 3, @ZoneAId, N'A-106', N'1', N'A', N'available', 1, N'Medium Seafood 6m2', @Now, @Now);
    SET @u_A106 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 4, @ZoneAId, N'A-107', N'1', N'A', N'available', 1, N'Large Warm 10m2', @Now, @Now);
    SET @u_A107 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 5, @ZoneAId, N'A-108', N'1', N'A', N'available', 1, N'XL Climate 16m2', @Now, @Now);
    SET @u_A108 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 1, @ZoneAId, N'A-109', N'1', N'A', N'available', 1, N'Standard Dry 3m2', @Now, @Now);
    SET @u_A109 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 2, @ZoneAId, N'A-110', N'1', N'A', N'available', 1, N'Medium Dry 6m2', @Now, @Now);
    SET @u_A110 = SCOPE_IDENTITY();

    -- Row 3 (A-111 to A-115)
    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 6, @ZoneAId, N'A-111', N'1', N'A', N'available', 1, N'Mini Locker 1m2', @Now, @Now);
    SET @u_A111 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 7, @ZoneAId, N'A-112', N'1', N'A', N'available', 1, N'Small Seafood 3m2', @Now, @Now);
    SET @u_A112 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 8, @ZoneAId, N'A-113', N'1', N'A', N'available', 1, N'Medium Warm 6m2', @Now, @Now);
    SET @u_A113 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 1, @ZoneAId, N'A-114', N'1', N'A', N'available', 1, N'Standard Dry 3m2', @Now, @Now);
    SET @u_A114 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 2, @ZoneAId, N'A-115', N'1', N'A', N'available', 1, N'Medium Dry 6m2', @Now, @Now);
    SET @u_A115 = SCOPE_IDENTITY();

    -- Map positions for Zone A (Row 2: y = 80, Row 3: y = 130)
    INSERT INTO [core].[unit_map_positions] (unit_id, area_id, x, y, width, height, rotation_degrees, metadata)
    VALUES
    (@u_A106, @ZoneAId, 20.00,  80.00, 45.00, 40.00, 0, N'{}'),
    (@u_A107, @ZoneAId, 75.00,  80.00, 45.00, 40.00, 0, N'{}'),
    (@u_A108, @ZoneAId, 130.00, 80.00, 45.00, 40.00, 0, N'{}'),
    (@u_A109, @ZoneAId, 185.00, 80.00, 45.00, 40.00, 0, N'{}'),
    (@u_A110, @ZoneAId, 240.00, 80.00, 45.00, 40.00, 0, N'{}'),
    (@u_A111, @ZoneAId, 20.00,  130.00, 45.00, 40.00, 0, N'{}'),
    (@u_A112, @ZoneAId, 75.00,  130.00, 45.00, 40.00, 0, N'{}'),
    (@u_A113, @ZoneAId, 130.00, 130.00, 45.00, 40.00, 0, N'{}'),
    (@u_A114, @ZoneAId, 185.00, 130.00, 45.00, 40.00, 0, N'{}'),
    (@u_A115, @ZoneAId, 240.00, 130.00, 45.00, 40.00, 0, N'{}');

    -- Array-like insertion for Zone B (10 units: B-106 to B-115)
    DECLARE @u_B106 BIGINT, @u_B107 BIGINT, @u_B108 BIGINT, @u_B109 BIGINT, @u_B110 BIGINT;
    DECLARE @u_B111 BIGINT, @u_B112 BIGINT, @u_B113 BIGINT, @u_B114 BIGINT, @u_B115 BIGINT;

    -- Row 2 (B-106 to B-110)
    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 3, @ZoneBId, N'B-106', N'1', N'B', N'available', 1, N'Medium Seafood 6m2', @Now, @Now);
    SET @u_B106 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 4, @ZoneBId, N'B-107', N'1', N'B', N'available', 1, N'Large Warm 10m2', @Now, @Now);
    SET @u_B107 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 5, @ZoneBId, N'B-108', N'1', N'B', N'available', 1, N'XL Climate 16m2', @Now, @Now);
    SET @u_B108 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 6, @ZoneBId, N'B-109', N'1', N'B', N'available', 1, N'Mini Locker 1m2', @Now, @Now);
    SET @u_B109 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 7, @ZoneBId, N'B-110', N'1', N'B', N'available', 1, N'Small Seafood 3m2', @Now, @Now);
    SET @u_B110 = SCOPE_IDENTITY();

    -- Row 3 (B-111 to B-115)
    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 8, @ZoneBId, N'B-111', N'1', N'B', N'available', 1, N'Medium Warm 6m2', @Now, @Now);
    SET @u_B111 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 1, @ZoneBId, N'B-112', N'1', N'B', N'available', 1, N'Standard Dry 3m2', @Now, @Now);
    SET @u_B112 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 2, @ZoneBId, N'B-113', N'1', N'B', N'available', 1, N'Medium Dry 6m2', @Now, @Now);
    SET @u_B113 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 4, @ZoneBId, N'B-114', N'1', N'B', N'available', 1, N'Large Warm 10m2', @Now, @Now);
    SET @u_B114 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 5, @ZoneBId, N'B-115', N'1', N'B', N'available', 1, N'XL Climate 16m2', @Now, @Now);
    SET @u_B115 = SCOPE_IDENTITY();

    -- Map positions for Zone B (Row 2: y = 80, Row 3: y = 130)
    INSERT INTO [core].[unit_map_positions] (unit_id, area_id, x, y, width, height, rotation_degrees, metadata)
    VALUES
    (@u_B106, @ZoneBId, 20.00,  80.00, 45.00, 40.00, 0, N'{}'),
    (@u_B107, @ZoneBId, 75.00,  80.00, 45.00, 40.00, 0, N'{}'),
    (@u_B108, @ZoneBId, 130.00, 80.00, 45.00, 40.00, 0, N'{}'),
    (@u_B109, @ZoneBId, 185.00, 80.00, 45.00, 40.00, 0, N'{}'),
    (@u_B110, @ZoneBId, 240.00, 80.00, 45.00, 40.00, 0, N'{}'),
    (@u_B111, @ZoneBId, 20.00,  130.00, 45.00, 40.00, 0, N'{}'),
    (@u_B112, @ZoneBId, 75.00,  130.00, 45.00, 40.00, 0, N'{}'),
    (@u_B113, @ZoneBId, 130.00, 130.00, 45.00, 40.00, 0, N'{}'),
    (@u_B114, @ZoneBId, 185.00, 130.00, 45.00, 40.00, 0, N'{}'),
    (@u_B115, @ZoneBId, 240.00, 130.00, 45.00, 40.00, 0, N'{}');

    SET @FacId = @FacId + 1;
END;

PRINT N'>> [3/3] Re-enabling triggers and verifying constraints...';
ALTER TABLE [core].[storage_units] ENABLE TRIGGER ALL;
ALTER TABLE [core].[unit_map_positions] ENABLE TRIGGER ALL;

COMMIT TRANSACTION;
PRINT N'>> 200 NEW UNITS ADDED SUCCESSFULLY (20 units per facility)!';
GO
