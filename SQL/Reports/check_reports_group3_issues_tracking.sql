-- ============================================================
-- Group 3: ISSUES & TRACKING REPORTS
-- ============================================================
DECLARE @Date       DATE = CAST(GETDATE() AS DATE)
DECLARE @DateMinus7 DATE = DATEADD(DAY,-7,CAST(GETDATE() AS DATE))
DECLARE @User NVARCHAR(30) = 'admin'

-- 1. Issues Report
-- Expected: Status IN (0=chua xu ly, 1=da xu ly)
EXEC pp_ReportIssues @FromDate=@DateMinus7, @ToDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @UserName=@User, @Status=0, @STCode=''

-- Check all statuses
EXEC pp_ReportIssues @FromDate=@DateMinus7, @ToDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @UserName=@User, @Status=-1, @STCode=''  -- -1 = all

-- 2. 3G Offline
-- Expected: TDV co ActualTime vuot qua TimeLimit
EXEC pp_Report3GOffline @FromDate=@DateMinus7, @ToDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @DistributorID=0, @SalesSupCD='', @RouteCD='',
    @UserName=@User, @Time=0, @STCode=''

-- 3. Cham cong (SP local)
-- Expected: ScheduleDateWork, SynchronizeDate, VisitedDate, VisitedDateRate
EXEC pp_GetTimekeeping_Local @FromDate=@DateMinus7, @ToDate=@Date,
    @UserName=@User

-- 4. Nhat ky nguoi dung (SP local, bo NhatNhatDMS linked server)
-- Expected: UserName, EventType, Click count by day
EXEC pp_GetUserLog_Local @FromDate=@DateMinus7, @ToDate=@Date,
    @UserName=@User

-- 5. TDV dung App (pp_ReportUserUseMobility)
-- Expected: SM=X/blank, ASM=X/blank, Register=Y/N, IsSync=Y/N
EXEC pp_ReportUserUseMobility @FromDate=@DateMinus7, @ToDate=@Date,
    @UserName=@User

-- LOGIC CHECK: Issues
SELECT
    'Issues Summary' AS [Check],
    COUNT(*) AS Total_Issues,
    SUM(CASE WHEN Status=0 THEN 1 ELSE 0 END) AS Pending,
    SUM(CASE WHEN Status=1 THEN 1 ELSE 0 END) AS Resolved,
    MIN(VisitDate) AS Oldest,
    MAX(VisitDate) AS Latest
FROM E_Issue
WHERE VisitDate BETWEEN @DateMinus7 AND @Date

-- Audit Log check
SELECT TOP 20
    EventType, COUNT(*) AS Count
FROM AppAuditLog
WHERE EventTime >= DATEADD(DAY,-7,GETDATE())
GROUP BY EventType
ORDER BY Count DESC
