-- ============================================================
-- SP: pp_GetDashboardKPI
-- Mục đích: Dashboard KPI tổng hợp
-- Tác giả: WEBAPP
-- Ngày tạo: 2025-05-30
-- Ngày sửa: 2025-05-30 — bám sát đúng DashBoardController.cs Home() DMS2.0
--
-- Logic từng chỉ số (bám sát DMS2.0):
--   #LR = UserRoutesTerritory JOIN Routes WHERE Active=1 (= ControllerHelper.ListRoute)
--   TotalSM         → COUNT DISTINCT SalesmanID trong #LR ngày @Date
--   TotalSMHasSync  → SM có FirstSyncTime IS NOT NULL ngày @Date
--   TotalSMHasVisit → SM IsMCP=1, OutletVisited > 0 ngày @Date
--   TotalSMHasOrder → SM IsMCP=1, OrderCount > 0 ngày @Date
--   TotalVisitPlan  → Sum OutletMustVisit ALL rows ngày @Date (DMS2.0: listSalesAssessment.Sum)
--   TotalOutletVisited → IsMCP=1, OutletVisited ngày @Date (DMS2.0: Where(IsMCP==1).Sum)
--   TotalOrder      → IsMCP=1, OrderCount ngày @Date
--   RevenueToday    → Sum TotalAmount ALL rows ngày @Date
--   AchievedMonth   → Sum MTDTotalAmount ALL rows ngày @Date (DMS2.0: listSalesAssessment.Sum(MTD))
--   TargetMonth     → SalesTargetKPI JOIN #LR.SalesmanID, Type=Month, TypeTarget=Revenue
-- ============================================================
IF OBJECT_ID('pp_GetDashboardKPI', 'P') IS NOT NULL
    DROP PROCEDURE pp_GetDashboardKPI;
GO

CREATE PROCEDURE pp_GetDashboardKPI
    @Date       DATE,
    @UserName   NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    -- ListRoute = UserRoutesTerritory JOIN Routes WHERE Active=1
    -- Bám sát DMS2.0: ControllerHelper.ListRoute (join Route table có Active column)
    SELECT DISTINCT UT.RouteCD, UT.DistributorID, R.SalesmanID
    INTO #LR
    FROM dbo.UserRoutesTerritory UT WITH (NOLOCK)
    JOIN dbo.Routes R WITH (NOLOCK)
        ON UT.RouteCD = R.RouteCD
        AND UT.DistributorID = R.DistributorID
        AND R.Active = 1
    WHERE UT.UserName = @UserName;

    DECLARE @Year  INT = YEAR(@Date);
    DECLARE @Month INT = MONTH(@Date);
    DECLARE @First DATE = DATEFROMPARTS(@Year, @Month, 1);

    -- Result Set 1: KPI tổng hợp
    SELECT
        COUNT(DISTINCT CASE WHEN BL.VisitDate = @Date
                            THEN BL.SalesmanID END)                                                             AS TotalSM,
        COUNT(DISTINCT CASE WHEN BL.VisitDate = @Date AND BL.FirstSyncTime IS NOT NULL
                            THEN BL.SalesmanID END)                                                             AS TotalSMHasSync,
        COUNT(DISTINCT CASE WHEN BL.VisitDate = @Date AND BL.IsMCP = 1 AND BL.OutletVisited > 0
                            THEN BL.SalesmanID END)                                                             AS TotalSMHasVisit,
        COUNT(DISTINCT CASE WHEN BL.VisitDate = @Date AND BL.IsMCP = 1 AND BL.OrderCount > 0
                            THEN BL.SalesmanID END)                                                             AS TotalSMHasOrder,
        SUM(CASE WHEN BL.VisitDate = @Date THEN BL.OutletMustVisit ELSE 0 END)                                 AS TotalVisitPlan,
        SUM(CASE WHEN BL.VisitDate = @Date AND BL.IsMCP = 1 THEN BL.OutletVisited   ELSE 0 END)               AS TotalOutletVisited,
        SUM(CASE WHEN BL.VisitDate = @Date AND BL.IsMCP = 1 THEN BL.OrderCount      ELSE 0 END)               AS TotalOrder,
        SUM(CASE WHEN BL.VisitDate = @Date THEN ISNULL(BL.TotalAmount, 0)    ELSE 0 END)                      AS RevenueToday,
        SUM(CASE WHEN BL.VisitDate = @Date THEN ISNULL(BL.MTDTotalAmount, 0) ELSE 0 END)                      AS AchievedMonth
    FROM dbo.BLSalesKPI BL WITH (NOLOCK)
    JOIN #LR LR ON BL.RouteCD = LR.RouteCD
               AND BL.DistributorID = LR.DistributorID
               AND BL.SalesmanID = LR.SalesmanID
    WHERE BL.VisitDate BETWEEN @First AND @Date;

    -- Result Set 2: Target tháng
    -- Bám sát DMS2.0: GetTargetBySalesTypeTargetType('Revenue')
    -- join SalesTargetKPI.SalesTeamID = ListRoute.SalesmanID, TypeTarget='Revenue'
    SELECT ISNULL(SUM(SK.Target), 0) AS TargetMonth
    FROM dbo.SalesTargetKPI SK WITH (NOLOCK)
    JOIN #LR LR ON SK.SalesTeamID = LR.SalesmanID
    WHERE SK.YearNbr  = @Year
      AND SK.MonthNbr = @Month
      AND SK.Type     = 'Month'
      AND SK.SalesTeamType = 'SM'
      AND SK.TypeTarget    = 'Revenue';

    DROP TABLE IF EXISTS #LR;
END;
GO

-- Test: EXEC pp_GetDashboardKPI @Date='2025-05-30', @UserName='admin'
-- Expected: TotalSM~896, TargetMonth~357B, AchievedMonth~260B
