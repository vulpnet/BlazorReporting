-- ============================================================
-- Group 2: OUTLET REPORTS — Bao cao cua hang
-- ============================================================
DECLARE @Date       DATE = CAST(GETDATE() AS DATE)
DECLARE @DateMinus7 DATE = DATEADD(DAY,-7,CAST(GETDATE() AS DATE))
DECLARE @User NVARCHAR(30) = 'admin'

-- 1. Outlet GPS khong hop le
-- Expected: Outlets co Latitude ngoai range hop le (-90 to 90)
-- Logic: Lat/Lng phai trong pham vi Viet Nam (8-24N, 102-110E)
EXEC pp_ReportOutletInvalidLocation
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @DistributorID=0, @SaleSupCD='', @UserName=@User, @STCode=''

-- 2. CH chua VT trong MCP
-- Expected: Outlets trong MCP khong co visit record ngay @Date
EXEC BSDH_ReportOutletNotVisitInMCP @FromDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @DistributorID=0, @SaleSupCD='', @RouteCD='', @UserName=@User, @STCode=''

-- 3. PC theo nhan vien
-- Expected: PCPass + PCNotPass, PC_MCP = PCPass*100/MCP
EXEC pp_ReportPC_SM @FromDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @DistributorID=0, @SaleSupCD='', @RouteCD='',
    @ShowAll=1, @Percent=0, @IsGreater=1, @UserName=@User, @STCode=''

-- 4. Ly do vieng tham (xem lai o group 1 neu can)

-- LOGIC CHECK: So sanh outlet count
SELECT
    'MCP Outlet Count' AS [Check],
    COUNT(DISTINCT CustomerID) AS Total_MCP_Outlets,
    COUNT(DISTINCT CASE WHEN [Status]='A' THEN CustomerID END) AS Active_Outlets
FROM DMSMCPDetail
WHERE DistributorID IN (
    SELECT DISTINCT DistributorID FROM UserRoutesTerritory WHERE UserName=@User
)

SELECT
    'Outlets with GPS' AS [Check],
    COUNT(*) AS Total,
    SUM(CASE WHEN Latitude IS NOT NULL AND Latitude != 0 THEN 1 ELSE 0 END) AS Has_GPS,
    SUM(CASE WHEN Latitude IS NULL OR Latitude = 0 THEN 1 ELSE 0 END) AS No_GPS
FROM Outlets
WHERE DistributorID IN (
    SELECT DISTINCT DistributorID FROM UserRoutesTerritory WHERE UserName=@User
)
