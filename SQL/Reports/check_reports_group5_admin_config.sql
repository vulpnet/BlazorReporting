-- ============================================================
-- Group 5: ADMIN & CONFIG — Quan tri & Cau hinh
-- ============================================================
DECLARE @User NVARCHAR(30) = 'admin'

-- 1. User Management
EXEC pp_UserManagement @UserName=@User

-- 2. Roles va Features
SELECT COUNT(*) AS Role_Count FROM Role
SELECT COUNT(*) AS Feature_Count FROM Feature
SELECT COUNT(*) AS RoleFeature_Count FROM RoleFeature

-- 3. Config tables
SELECT COUNT(*) AS ShiftSetting_Count FROM ShiftSetting
SELECT COUNT(*) AS SystemSetting_Count FROM SystemSetting
SELECT COUNT(*) AS CustomSetting_Count FROM CustomSetting
SELECT COUNT(*) AS Phrase_Count FROM Phrase

-- 4. Security: AppAuditLog va AppUserSession
SELECT
    'AuditLog Stats' AS [Check],
    COUNT(*) AS Total_Logs,
    MIN(EventTime) AS Oldest_Log,
    MAX(EventTime) AS Latest_Log,
    DATEDIFF(DAY, MIN(EventTime), MAX(EventTime)) AS Days_Span
FROM AppAuditLog

SELECT
    'Active Sessions' AS [Check],
    COUNT(*) AS Total_Sessions,
    COUNT(DISTINCT UserName) AS Unique_Users
FROM AppUserSession WHERE IsActive=1

-- 5. Distributor count
SELECT COUNT(*) AS Distributor_Count FROM Distributor WHERE Active=1

-- 6. SFInfo check (dung cho DailyPlan va Timekeeping local SP)
DECLARE @Today DATE = CAST(GETDATE() AS DATE)
SELECT
    'SFInfo today' AS [Check],
    COUNT(DISTINCT SalesmanID) AS SM_Count,
    COUNT(DISTINCT RouteCD) AS Route_Count
FROM SFInfo WHERE VisitDate=@Today

-- 7. Kiem tra SP local da tao
SELECT
    'Custom SPs' AS [Check],
    ROUTINE_NAME
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_TYPE='PROCEDURE'
  AND ROUTINE_NAME IN (
    'pp_GetDashboardKPI','pp_GetSmLocationForMap','pp_GetSmTracking_Local',
    'pp_GetDailyPlan_Local','pp_GetTimekeeping_Local','pp_GetUserLog_Local',
    'pp_GetSummarySales_Local','sp_CleanAuditLog'
  )
ORDER BY ROUTINE_NAME
