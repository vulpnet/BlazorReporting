-- ============================================================
-- Group 1: VISIT REPORTS — Bao cao vieng tham
-- ============================================================
DECLARE @Date       DATE = CAST(GETDATE() AS DATE)
DECLARE @DateMinus7 DATE = DATEADD(DAY,-7,CAST(GETDATE() AS DATE))
DECLARE @User NVARCHAR(30) = 'admin'

-- 1. Bao cao vieng tham chi tiet
-- Expected: N rows moi lan VT, co GPS lat/long
-- Logic check: strIsMCP IN ('IsRoute','NoRoute'), HasOrder IN (0,1)
EXEC pp_ReportVisit @VisitDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @DistributorID=0, @SaleSupCD='', @RouteCD='', @UserName=@User, @STCode=''

-- 2. Tong hop KPI theo TDV
-- Expected: N rows, moi SM/ngay 1 row, % numbers phai 0-100
EXEC pp_ReportSMVisitSummary @FromDate=@Date, @ToDate=@Date,
    @Level1ID=NULL, @Level2ID=NULL, @Level3ID=NULL, @Level4ID=NULL, @Level5ID=NULL,
    @ProvinceCD=NULL, @DistributorID=NULL, @SaleSupCD=NULL, @RouteCD=NULL,
    @SalesmanCD=NULL, @UserName=@User,
    @FirstTimeSync=NULL, @FirstTimeVisitAM=NULL, @FirstTimeVisitPM=NULL,
    @LastTimeVisit=NULL, @OrderDistanceValid=NULL, @TimeVisit=NULL, @STCode=NULL

-- 3. Tong hop doanh so (SP local)
-- Expected: N rows theo SM, co TotalAmount, MTDTotalAmount
EXEC pp_GetSummarySales_Local @FromDate=@DateMinus7, @ToDate=@Date, @UserName=@User

-- 4. Doanh so ngay MTD
-- Expected: N rows moi SM/ngay, co TotalAmount + MTDTotalAmount
EXEC pp_ReportSalesDailyToMTD @FromDate=@DateMinus7, @ToDate=@Date,
    @RegionID='', @AreaID='', @ProvinceID='', @DistributorID=0,
    @SaleSupID='', @RouteID='', @SalesmanID='', @IsMCP=1, @UserName=@User

-- 5. Ban hang hieu qua
-- Expected: N rows, TargetRevenue co the = 0 neu chua thiet lap
EXEC pp_ReportSalesEffective @VisitDate=@Date,
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
    @DistributorID=0, @SaleSupCD='', @RouteCD='', @UserName=@User, @STCode=''

-- 6. Ke hoach ngay (SP local, thay pp_ReportDailyPlan)
EXEC pp_GetDailyPlan_Local @VisitDate=@Date, @UserName=@User

-- 7. Ly do vieng tham
EXEC pp_ReportVisitReason @FromDate=@DateMinus7, @ToDate=@Date,
    @Level1ID='', @Level2ID='', @Level3ID='', @Level4ID='', @Level5ID='',
    @DistributorID=0, @SaleSupID='', @RouteID='', @UserName=@User, @STCode=''

-- LOGIC CHECK
SELECT
    'Visit data check' AS [Check],
    SUM(CASE WHEN IsMCP=1 THEN OutletMustVisit ELSE 0 END)  AS Plan_MCP,
    SUM(CASE WHEN IsMCP=1 THEN OutletVisited   ELSE 0 END)  AS Visited_MCP,
    SUM(CASE WHEN IsMCP=1 THEN OrderCount      ELSE 0 END)  AS Orders_MCP,
    CAST(SUM(CASE WHEN IsMCP=1 THEN OutletVisited ELSE 0 END) * 100.0
       / NULLIF(SUM(CASE WHEN IsMCP=1 THEN OutletMustVisit ELSE 0 END),0) AS DECIMAL(5,1)) AS Visit_Rate_Pct
FROM BLSalesKPI BL
JOIN UserRoutesTerritory UT ON UT.RouteCD=BL.RouteCD AND UT.DistributorID=BL.DistributorID
WHERE BL.VisitDate=@Date AND UT.UserName=@User
