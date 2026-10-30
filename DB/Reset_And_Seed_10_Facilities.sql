/*
  ===================================================================================
  SCRIPT: Reset_And_Seed_10_Facilities.sql
  DESCRIPTION:
    1. Disables all triggers and foreign key checks.
    2. Wipes all booking, agreement, invoice, payment, ticket, and operational data.
    3. Keeps the 4 core accounts:
         - yu9696000@gmail.com (Customer)
         - admin@gmail.com (Admin)
         - staff@gmail.com (Staff)
         - manager@gmail.com (Manager)
    4. Creates 10 new Facilities across 10 distinct locations in Vietnam.
    5. Creates 10 Manager accounts (manager1@gmail.com - manager10@gmail.com)
       with password = '1', each assigned to manage 1 Facility.
    6. Creates 10 Staff accounts (staff1@gmail.com - staff10@gmail.com)
       with password = '1', each stationed at 1 Facility.
    7. Sets passwords for staff@gmail.com and manager@gmail.com to '1'.
    8. Seeds clean building areas, floor plans, facility rates, and storage units
       for all 10 facilities (all units fresh, unbooked & available).
    9. Seeds initial staff shifts for today.
    10. Re-enables all triggers and foreign key constraints.
  ===================================================================================
*/

USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

PRINT N'>> [1/8] Disabling all triggers and constraints...';
EXEC sp_msforeachtable 'ALTER TABLE ? DISABLE TRIGGER all';
EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all';

PRINT N'>> [2/8] Deleting all transactional and operational data (contracts, tickets, bookings, payments)...';
DELETE FROM [core].[service_ratings];
DELETE FROM [core].[stored_items];
DELETE FROM [core].[access_events];
DELETE FROM [core].[access_credentials];
DELETE FROM [core].[authorized_access_members];
DELETE FROM [core].[access_points];
DELETE FROM [core].[handover_records];
DELETE FROM [core].[move_out_requests];
DELETE FROM [core].[delinquency_actions];
DELETE FROM [core].[delinquency_cases];
DELETE FROM [core].[refunds];
DELETE FROM [core].[payment_allocations];
DELETE FROM [core].[payments];
DELETE FROM [core].[invoice_lines];
DELETE FROM [core].[invoices];
DELETE FROM [core].[rental_renewals];
DELETE FROM [core].[unit_transfer_requests];
DELETE FROM [core].[unit_allocations];
DELETE FROM [core].[rental_agreements];
DELETE FROM [core].[reservations];
DELETE FROM [core].[ticket_charge_approvals];
DELETE FROM [core].[ticket_charge_proposals];
DELETE FROM [core].[ticket_messages];
DELETE FROM [core].[ticket_attachments];
DELETE FROM [core].[ticket_assignments];
DELETE FROM [core].[support_tickets];
DELETE FROM [core].[inspections];
DELETE FROM [core].[maintenance_work_orders];
DELETE FROM [core].[staff_tasks];
DELETE FROM [core].[shift_assignments];
DELETE FROM [core].[staff_shifts];
DELETE FROM [core].[unit_status_history];
DELETE FROM [core].[unit_map_positions];
DELETE FROM [core].[storage_units];
DELETE FROM [core].[facility_rates];
DELETE FROM [core].[fee_rules];
DELETE FROM [core].[facility_areas];
DELETE FROM [core].[staff_facility_assignments];
DELETE FROM [core].[appointments];
DELETE FROM [core].[notifications];
DELETE FROM [core].[login_history];
DELETE FROM [core].[audit_logs];
DELETE FROM [core].[facilities];

PRINT N'>> [3/8] Cleaning up old seed users and keeping the 4 core accounts...';
DECLARE @AdminId BIGINT = (SELECT TOP 1 id FROM [core].[users] WHERE email = N'admin@gmail.com');
DECLARE @CustomerId BIGINT = (SELECT TOP 1 id FROM [core].[users] WHERE email = N'yu9696000@gmail.com');
DECLARE @StaffId BIGINT = (SELECT TOP 1 id FROM [core].[users] WHERE email = N'staff@gmail.com');
DECLARE @ManagerId BIGINT = (SELECT TOP 1 id FROM [core].[users] WHERE email = N'manager@gmail.com');

-- If any core account is missing, handle gracefully
IF @AdminId IS NULL SET @AdminId = 12;
IF @CustomerId IS NULL SET @CustomerId = 7;
IF @StaffId IS NULL SET @StaffId = 11;
IF @ManagerId IS NULL SET @ManagerId = 13;

-- Relink policy_versions and price_ranges to Admin
UPDATE [core].[policy_versions] SET created_by = @AdminId WHERE created_by <> @AdminId;
UPDATE [core].[price_ranges] SET created_by = @AdminId WHERE created_by <> @AdminId;

-- Delete non-core profiles and user roles
DELETE FROM [core].[employee_profiles] WHERE user_id NOT IN (@StaffId, @ManagerId, @AdminId);
DELETE FROM [core].[customer_profiles] WHERE user_id <> @CustomerId;
DELETE FROM [core].[user_roles] WHERE user_id NOT IN (@CustomerId, @AdminId, @StaffId, @ManagerId);

-- Clean up any foreign key references on user_roles
UPDATE [core].[user_roles] SET granted_by = @AdminId WHERE granted_by IS NOT NULL;

-- Delete non-core users
DELETE FROM [core].[users] WHERE email NOT IN (N'yu9696000@gmail.com', N'admin@gmail.com', N'staff@gmail.com', N'manager@gmail.com');

-- BCrypt hash for password '1'
DECLARE @Password1Hash NVARCHAR(255) = N'$2a$11$HWcRgUvFaROkssbvAG.1COvFAbaL1dHcF0tCNYHP5B3vyCsdSGZ0O';
DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();
DECLARE @AssignedStart DATETIMEOFFSET = DATEADD(day, -30, @Now);

-- Update password for staff@gmail.com and manager@gmail.com to '1'
UPDATE [core].[users] 
SET password_hash = @Password1Hash, status = N'active', updated_at = @Now
WHERE id IN (@StaffId, @ManagerId);

-- Ensure profiles for core users exist
IF NOT EXISTS (SELECT 1 FROM [core].[customer_profiles] WHERE user_id = @CustomerId)
    INSERT INTO [core].[customer_profiles] (user_id, full_name, created_at, updated_at) VALUES (@CustomerId, N'Khôi Võ', @Now, @Now);

IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE user_id = @StaffId)
    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES (@StaffId, N'EMP-STAFF-001', N'Staff Member', '2026-01-01', N'active', @Now, @Now);

IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE user_id = @ManagerId)
    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES (@ManagerId, N'MGR-DEMO-001', N'Demo Facility Manager', '2026-01-01', N'active', @Now, @Now);

IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE user_id = @AdminId)
    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES (@AdminId, N'EMP-ADMIN-001', N'System Administrator', '2026-01-01', N'active', @Now, @Now);

-- Ensure user roles
IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @CustomerId AND role_id = 1)
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at) VALUES (@CustomerId, 1, @AdminId, @Now);

IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @AdminId AND role_id = 5)
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at) VALUES (@AdminId, 5, @AdminId, @Now);

IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @StaffId AND role_id = 2)
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at) VALUES (@StaffId, 2, @AdminId, @Now);

IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @ManagerId AND role_id = 3)
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at) VALUES (@ManagerId, 3, @AdminId, @Now);

PRINT N'>> [4/8] Creating 10 Facilities across 10 distinct locations...';
DBCC CHECKIDENT ('core.facilities', RESEED, 0);

SET IDENTITY_INSERT [core].[facilities] ON;

INSERT INTO [core].[facilities] (
    id, code, name, address_line, ward, district, city, 
    latitude, longitude, timezone, opening_time, closing_time, status, created_at, updated_at
)
VALUES
(1, N'HCM-TD', N'Thu Duc Self Storage', N'01 Võ Văn Ngân', N'Phường Linh Chiểu', N'TP. Thủ Đức', N'Hồ Chí Minh', 10.850700, 106.771900, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(2, N'HCM-Q1', N'District 1 Central Self Storage', N'45 Lê Duẩn', N'Phường Bến Nghé', N'Quận 1', N'Hồ Chí Minh', 10.781800, 106.698900, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(3, N'HCM-Q7', N'Phu My Hung Smart Storage', N'105 Tôn Dật Tiên', N'Phường Tân Phong', N'Quận 7', N'Hồ Chí Minh', 10.729400, 106.721800, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(4, N'HCM-BT', N'Binh Thanh Riverside Storage', N'208 Nguyễn Hữu Cảnh', N'Phường 22', N'Quận Bình Thạnh', N'Hồ Chí Minh', 10.791500, 106.719800, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(5, N'HCM-TB', N'Tan Binh Airport Self Storage', N'18 Trường Sơn', N'Phường 2', N'Quận Tân Bình', N'Hồ Chí Minh', 10.814200, 106.666500, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(6, N'HN-CG', N'Cau Giay Tech Hub Storage', N'10 Trần Thái Tông', N'Phường Dịch Vọng Hậu', N'Quận Cầu Giấy', N'Hà Nội', 21.033300, 105.787500, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(7, N'HN-DD', N'Dong Da Premier Storage', N'54 Huỳnh Thúc Kháng', N'Phường Láng Hạ', N'Quận Đống Đa', N'Hà Nội', 21.018900, 105.811200, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(8, N'HN-HBT', N'Hai Ba Trung City Self Storage', N'191 Bà Triệu', N'Phường Lê Đại Hành', N'Quận Hai Bà Trưng', N'Hà Nội', 21.009400, 105.849600, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(9, N'DN-HC', N'Da Nang Seaside Storage', N'88 Bạch Đằng', N'Phường Hải Châu 1', N'Quận Hải Châu', N'Đà Nẵng', 16.067800, 108.223500, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now),
(10, N'BD-TDM', N'Binh Duong Logistics Storage', N'230 Đại lộ Bình Dương', N'Phường Phú Hòa', N'TP. Thủ Dầu Một', N'Bình Dương', 10.978200, 106.671500, N'Asia/Ho_Chi_Minh', '07:00:00', '21:00:00', N'active', @Now, @Now);

SET IDENTITY_INSERT [core].[facilities] OFF;

PRINT N'>> [5/8] Creating 10 Managers and 10 Staff (all passwords = 1)...';
DECLARE @ManagerRoleId SMALLINT = (SELECT id FROM [core].[roles] WHERE code = N'facility_manager');
DECLARE @StaffRoleId SMALLINT = (SELECT id FROM [core].[roles] WHERE code = N'facility_staff');

DECLARE @i INT = 1;
WHILE @i <= 10
BEGIN
    DECLARE @MgrEmail NVARCHAR(255) = N'manager' + CAST(@i AS NVARCHAR(10)) + N'@gmail.com';
    DECLARE @MgrCode NVARCHAR(50) = N'MGR-' + RIGHT('000' + CAST(@i AS NVARCHAR(10)), 3);
    DECLARE @MgrName NVARCHAR(255) = N'Facility Manager ' + CAST(@i AS NVARCHAR(10));
    DECLARE @MgrUserId BIGINT;

    -- Insert Manager user
    INSERT INTO [core].[users] (email, phone_number, password_hash, status, created_at, updated_at)
    VALUES (@MgrEmail, N'0901000' + RIGHT('000' + CAST(@i AS NVARCHAR(10)), 3), @Password1Hash, N'active', @Now, @Now);
    SET @MgrUserId = SCOPE_IDENTITY();

    -- Role
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at)
    VALUES (@MgrUserId, @ManagerRoleId, @AdminId, @Now);

    -- Profile
    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES (@MgrUserId, @MgrCode, @MgrName, '2026-01-01', N'active', @Now, @Now);

    -- Facility Assignment (starts in past to satisfy shift triggers)
    INSERT INTO [core].[staff_facility_assignments] (employee_id, facility_id, assignment_role, starts_at, assigned_by, created_at)
    VALUES (@MgrUserId, @i, N'facility_manager', @AssignedStart, @AdminId, @Now);

    -- Insert Staff user
    DECLARE @StfEmail NVARCHAR(255) = N'staff' + CAST(@i AS NVARCHAR(10)) + N'@gmail.com';
    DECLARE @StfCode NVARCHAR(50) = N'STF-' + RIGHT('000' + CAST(@i AS NVARCHAR(10)), 3);
    DECLARE @StfName NVARCHAR(255) = N'Staff Member ' + CAST(@i AS NVARCHAR(10));
    DECLARE @StfUserId BIGINT;

    INSERT INTO [core].[users] (email, phone_number, password_hash, status, created_at, updated_at)
    VALUES (@StfEmail, N'0902000' + RIGHT('000' + CAST(@i AS NVARCHAR(10)), 3), @Password1Hash, N'active', @Now, @Now);
    SET @StfUserId = SCOPE_IDENTITY();

    -- Role
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at)
    VALUES (@StfUserId, @StaffRoleId, @AdminId, @Now);

    -- Profile
    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES (@StfUserId, @StfCode, @StfName, '2026-01-01', N'active', @Now, @Now);

    -- Facility Assignment (starts in past to satisfy shift triggers)
    INSERT INTO [core].[staff_facility_assignments] (employee_id, facility_id, assignment_role, starts_at, assigned_by, created_at)
    VALUES (@StfUserId, @i, N'facility_staff', @AssignedStart, @AdminId, @Now);

    -- Seed Shift for Staff
    DECLARE @ShiftId BIGINT;
    DECLARE @TodayStart DATETIMEOFFSET = CAST(CAST(@Now AS DATE) AS DATETIMEOFFSET);
    INSERT INTO [core].[staff_shifts] (facility_id, shift_name, starts_at, ends_at, status, created_by, created_at)
    VALUES (@i, N'Morning Shift (07:00 - 15:00)', DATEADD(hour, 7, @TodayStart), DATEADD(hour, 15, @TodayStart), N'open', @MgrUserId, @Now);
    SET @ShiftId = SCOPE_IDENTITY();

    INSERT INTO [core].[shift_assignments] (shift_id, employee_id, duty_role, status)
    VALUES (@ShiftId, @StfUserId, N'staff', N'scheduled');

    SET @i = @i + 1;
END;

-- Also assign staff@gmail.com and manager@gmail.com to Facility 1
INSERT INTO [core].[staff_facility_assignments] (employee_id, facility_id, assignment_role, starts_at, assigned_by, created_at)
VALUES 
(@StaffId, 1, N'facility_staff', @AssignedStart, @AdminId, @Now),
(@ManagerId, 1, N'facility_manager', @AssignedStart, @AdminId, @Now);

PRINT N'>> [6/8] Seeding Facility Areas (Building / Floor / Zones) for each facility...';
DBCC CHECKIDENT ('core.facility_areas', RESEED, 0);

DECLARE @FacId INT = 1;
WHILE @FacId <= 10
BEGIN
    DECLARE @BldgId BIGINT, @FloorId BIGINT, @ZoneAId BIGINT, @ZoneBId BIGINT;

    -- Building A
    INSERT INTO [core].[facility_areas] (facility_id, parent_area_id, code, name, area_type, display_order, map_metadata, is_active)
    VALUES (@FacId, NULL, N'BLDG-A', N'Building A', N'building', 1, N'{}', 1);
    SET @BldgId = SCOPE_IDENTITY();

    -- Floor 1
    INSERT INTO [core].[facility_areas] (facility_id, parent_area_id, code, name, area_type, display_order, map_metadata, is_active)
    VALUES (@FacId, @BldgId, N'F1', N'Floor 1', N'floor', 1, N'{}', 1);
    SET @FloorId = SCOPE_IDENTITY();

    -- Zone A
    INSERT INTO [core].[facility_areas] (facility_id, parent_area_id, code, name, area_type, display_order, map_metadata, is_active)
    VALUES (@FacId, @FloorId, N'F1-ZA', N'Floor 1 - Zone A', N'zone', 1, N'{}', 1);
    SET @ZoneAId = SCOPE_IDENTITY();

    -- Zone B
    INSERT INTO [core].[facility_areas] (facility_id, parent_area_id, code, name, area_type, display_order, map_metadata, is_active)
    VALUES (@FacId, @FloorId, N'F1-ZB', N'Floor 1 - Zone B', N'zone', 2, N'{}', 1);
    SET @ZoneBId = SCOPE_IDENTITY();

    -- Seed 8 standard Facility Rates for this facility
    INSERT INTO [core].[facility_rates] (facility_id, unit_type_id, monthly_rate, deposit_amount, booking_fee, valid_from, valid_to, created_by, created_at)
    VALUES
    (@FacId, 1, 2000.00, 2000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 2, 4000.00, 4000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 3, 5000.00, 5000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 4, 7000.00, 7000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 5, 10000.00, 10000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 6, 1000.00, 1000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 7, 3000.00, 3000.00, 0, '2026-01-01', NULL, @AdminId, @Now),
    (@FacId, 8, 5000.00, 5000.00, 0, '2026-01-01', NULL, @AdminId, @Now);

    -- Seed 10 Clean Storage Units for this facility (5 in Zone A, 5 in Zone B) - all available!
    DECLARE @u1 BIGINT, @u2 BIGINT, @u3 BIGINT, @u4 BIGINT, @u5 BIGINT;
    DECLARE @u6 BIGINT, @u7 BIGINT, @u8 BIGINT, @u9 BIGINT, @u10 BIGINT;

    -- Zone A units
    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 1, @ZoneAId, N'A-101', N'1', N'A', N'available', 1, N'Standard Dry 3m2', @Now, @Now);
    SET @u1 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 2, @ZoneAId, N'A-102', N'1', N'A', N'available', 1, N'Medium Dry 6m2', @Now, @Now);
    SET @u2 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 3, @ZoneAId, N'A-103', N'1', N'A', N'available', 1, N'Deep Freeze 6m2', @Now, @Now);
    SET @u3 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 4, @ZoneAId, N'A-104', N'1', N'A', N'available', 1, N'Large Warm 10m2', @Now, @Now);
    SET @u4 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 5, @ZoneAId, N'A-105', N'1', N'A', N'available', 1, N'XL Climate 16m2', @Now, @Now);
    SET @u5 = SCOPE_IDENTITY();

    -- Zone B units
    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 6, @ZoneBId, N'B-101', N'1', N'B', N'available', 1, N'Mini Locker 1m2', @Now, @Now);
    SET @u6 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 7, @ZoneBId, N'B-102', N'1', N'B', N'available', 1, N'Small Seafood 3m2', @Now, @Now);
    SET @u7 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 8, @ZoneBId, N'B-103', N'1', N'B', N'available', 1, N'Medium Warm 6m2', @Now, @Now);
    SET @u8 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 1, @ZoneBId, N'B-104', N'1', N'B', N'available', 1, N'Standard Dry 3m2', @Now, @Now);
    SET @u9 = SCOPE_IDENTITY();

    INSERT INTO [core].[storage_units] (facility_id, unit_type_id, area_id, unit_code, floor_label, zone_label, physical_status, is_listed, notes, created_at, updated_at)
    VALUES (@FacId, 2, @ZoneBId, N'B-105', N'1', N'B', N'available', 1, N'Medium Dry 6m2', @Now, @Now);
    SET @u10 = SCOPE_IDENTITY();

    -- Floor plan coordinates (clean spacing, no overlap)
    INSERT INTO [core].[unit_map_positions] (unit_id, area_id, x, y, width, height, rotation_degrees, metadata)
    VALUES
    (@u1, @ZoneAId, 20.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u2, @ZoneAId, 75.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u3, @ZoneAId, 130.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u4, @ZoneAId, 185.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u5, @ZoneAId, 240.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u6, @ZoneBId, 20.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u7, @ZoneBId, 75.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u8, @ZoneBId, 130.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u9, @ZoneBId, 185.00, 30.00, 45.00, 40.00, 0, N'{}'),
    (@u10, @ZoneBId, 240.00, 30.00, 45.00, 40.00, 0, N'{}');

    SET @FacId = @FacId + 1;
END;

PRINT N'>> [7/8] Re-enabling all triggers and constraints...';
EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all';
EXEC sp_msforeachtable 'ALTER TABLE ? ENABLE TRIGGER all';

COMMIT TRANSACTION;
PRINT N'>> [8/8] TRANSACTION COMMITTED SUCCESSFULLY!';
GO
