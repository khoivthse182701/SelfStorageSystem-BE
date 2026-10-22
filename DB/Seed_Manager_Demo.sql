/* Creates or refreshes the demo Facility Manager account.
   Login:    manager@gmail.com
   Password: 123@Test
   Scope:    Facility 1
*/
USE [SelfStoragePRN222];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @Email NVARCHAR(255) = N'manager@gmail.com';
DECLARE @PasswordHash NVARCHAR(255) = N'$2a$11$pLt2NvvvBf9WS4YvVQzPruLslMex9ua.g4jpf7vi1h.ldVXK5h6s6';
DECLARE @FacilityId BIGINT = 1;
DECLARE @ManagerRoleId SMALLINT;
DECLARE @UserId BIGINT;
DECLARE @AssignedBy BIGINT = (SELECT TOP 1 id FROM [core].[users] WHERE email = N'administrator@example.test');
DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM [core].[facilities] WHERE id = @FacilityId AND status = N'active')
    THROW 51000, 'Facility 1 must exist and be active before seeding manager@gmail.com.', 1;

SELECT @ManagerRoleId = id FROM [core].[roles] WHERE code = N'facility_manager';
IF @ManagerRoleId IS NULL
    THROW 51001, 'The facility_manager role is missing.', 1;

SELECT @UserId = id FROM [core].[users] WHERE email = @Email;

IF @UserId IS NULL
BEGIN
    INSERT INTO [core].[users] (email, phone_number, password_hash, status, created_at, updated_at)
    VALUES (@Email, N'0900000999', @PasswordHash, N'active', @Now, @Now);
    SET @UserId = SCOPE_IDENTITY();
END
ELSE
BEGIN
    UPDATE [core].[users]
    SET password_hash = @PasswordHash, status = N'active', updated_at = @Now
    WHERE id = @UserId;
END;

IF NOT EXISTS (SELECT 1 FROM [core].[user_roles] WHERE user_id = @UserId AND role_id = @ManagerRoleId)
BEGIN
    INSERT INTO [core].[user_roles] (user_id, role_id, granted_by, granted_at)
    VALUES (@UserId, @ManagerRoleId, @AssignedBy, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM [core].[employee_profiles] WHERE user_id = @UserId)
BEGIN
    INSERT INTO [core].[employee_profiles] (user_id, employee_code, full_name, hire_date, employment_status, created_at, updated_at)
    VALUES (@UserId, N'MGR-DEMO-001', N'Demo Facility Manager', CONVERT(date, @Now), N'active', @Now, @Now);
END
ELSE
BEGIN
    UPDATE [core].[employee_profiles]
    SET full_name = N'Demo Facility Manager', employment_status = N'active', updated_at = @Now
    WHERE user_id = @UserId;
END;

IF NOT EXISTS (
    SELECT 1 FROM [core].[staff_facility_assignments]
    WHERE employee_id = @UserId AND facility_id = @FacilityId
      AND assignment_role = N'facility_manager' AND ends_at IS NULL
)
BEGIN
    INSERT INTO [core].[staff_facility_assignments] (employee_id, facility_id, assignment_role, starts_at, assigned_by, created_at)
    VALUES (@UserId, @FacilityId, N'facility_manager', @Now, @AssignedBy, @Now);
END;

COMMIT TRANSACTION;
GO

SELECT u.id, u.email, u.status, ep.employee_code, ep.full_name, r.code AS role_code,
       f.code AS facility_code, f.name AS facility_name
FROM [core].[users] u
JOIN [core].[employee_profiles] ep ON ep.user_id = u.id
JOIN [core].[user_roles] ur ON ur.user_id = u.id
JOIN [core].[roles] r ON r.id = ur.role_id
JOIN [core].[staff_facility_assignments] sfa ON sfa.employee_id = u.id AND sfa.ends_at IS NULL
JOIN [core].[facilities] f ON f.id = sfa.facility_id
WHERE u.email = N'manager@gmail.com';
GO
