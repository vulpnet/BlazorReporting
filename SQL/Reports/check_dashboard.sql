-- ============================================================
-- Group: DASHBOARD — KPI & Charts
-- ============================================================
DECLARE @Date DATE = CAST(GETDATE() AS DATE), @User NVARCHAR(30) = 'admin'

-- 1. KPI tong hop (TotalSM, AchievedMonth, TargetMonth...)
-- Expected: 1 row + 1 row (TargetMonth)
EXEC pp_GetDashboardKPI @Date=@Date, @UserName=@User

-- 2. Detail table (pp_ReportSalesAssessment)
-- Expected: N rows theo so SM * 2 (IsMCP=0 va 1)
EXEC pp_ReportSalesAssessment
    @FromDate=@Date, @RegionID='', @AreaID='', @ProvinceID='',
    @DistributorID=0, @SaleSupID='', @RouteID='', @SalesmanID='', @UserName=@User

-- 3. Visit in month (pp_GetReportVisitInMonth)
-- Note: SP nay chay 150s — KHONG dung trong WEBAPP, chi test khi can thiet
-- EXEC pp_GetReportVisitInMonth @FromDate=DATEFROMPARTS(YEAR(@Date),MONTH(@Date),1), @ToDate=@Date, @UserName=@User

-- 4. Location for Territory Map
-- Expected: N rows moi SM co GPS
EXEC pp_GetSmLocationForMap @Username=@User, @Date=@Date

-- DATA CHECK: Kiem tra so luong
SELECT
    'BLSalesKPI today'   AS [Table],
    COUNT(DISTINCT SalesmanID) AS SM_Count,
    COUNT(*)                   AS Row_Count,
    SUM(TotalAmount)           AS Total_Revenue
FROM BLSalesKPI
WHERE VisitDate = @Date

SELECT
    'SalesmanVisit today' AS [Table],
    COUNT(DISTINCT SalesmanID) AS SM_Count,
    COUNT(*) AS Row_Count
FROM SalesmanVisit
WHERE VisitDate = @Date AND Latitude IS NOT NULL AND Latitude != 0
