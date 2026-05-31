-- ============================================================
-- SP: pp_GetDailyPlan_Local
-- Thay the: pp_ReportDailyPlan (dung AZRDMSPDB04 linked server)
-- Nguon du lieu: SFInfo + BLSalesKPI (URCeTools local)
-- Ngay tao: 2026-05-30
-- ============================================================
IF OBJECT_ID('pp_GetDailyPlan_Local','P') IS NOT NULL DROP PROCEDURE pp_GetDailyPlan_Local;
GO
CREATE PROCEDURE pp_GetDailyPlan_Local
    @VisitDate     DATE,
    @RegionID      NVARCHAR(30) = '',
    @AreaID        NVARCHAR(30) = '',
    @DistributorID NVARCHAR(30) = '',
    @SaleSupID     NVARCHAR(20) = '',
    @RouteID       NVARCHAR(30) = '',
    @UserName      NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT
        SF.RegionID, SF.RegionName, SF.AreaID, SF.AreaName,
        SF.DistributorID, SF.DistributorCode, SF.DistributorName,
        SF.RouteCD, SF.RouteName, SF.SaleSupID, SF.SaleSupName,
        SF.SalesmanID, SF.SalesmanName, SF.SMPhone,
        VisitDate       = @VisitDate,
        OutletMustVisit = ISNULL(BL.OutletMustVisit, 0),
        OutletVisited   = ISNULL(BL.OutletVisited, 0),
        OrderCount      = ISNULL(BL.OrderCount, 0),
        TotalAmount     = ISNULL(BL.TotalAmount, 0),
        TotalSKU        = ISNULL(BL.TotalSKU, 0),
        FirstSyncTime   = BL.FirstSyncTime,
        Visit_MCP       = ISNULL(BL.Visit_MCP, 0),
        SO_MCP          = ISNULL(BL.SO_MCP, 0),
        LPPC            = ISNULL(BL.LPPC, 0)
    FROM dbo.SFInfo SF WITH (NOLOCK)
    JOIN dbo.UserRoutesTerritory UT WITH (NOLOCK)
        ON UT.RouteCD = SF.RouteCD AND UT.DistributorID = SF.DistributorID AND UT.UserName = @UserName
    LEFT JOIN (
        SELECT RouteCD, SalesmanID, DistributorID,
               SUM(OutletMustVisit) AS OutletMustVisit,
               SUM(OutletVisited)   AS OutletVisited,
               SUM(OrderCount)      AS OrderCount,
               SUM(CASE WHEN IsMCP=1 THEN TotalAmount ELSE 0 END) AS TotalAmount,
               SUM(TotalSKU) AS TotalSKU,
               MAX(FirstSyncTime)   AS FirstSyncTime,
               AVG(Visit_MCP) AS Visit_MCP,
               AVG(SO_MCP) AS SO_MCP,
               AVG(LPPC) AS LPPC
        FROM dbo.BLSalesKPI WITH (NOLOCK)
        WHERE CONVERT(DATE, VisitDate) = @VisitDate AND IsMCP = 1
        GROUP BY RouteCD, SalesmanID, DistributorID
    ) BL ON BL.RouteCD=SF.RouteCD AND BL.SalesmanID=SF.SalesmanID AND BL.DistributorID=SF.DistributorID
    WHERE SF.VisitDate = @VisitDate
      AND (@RegionID='' OR SF.RegionID=@RegionID)
      AND (@AreaID='' OR SF.AreaID=@AreaID)
      AND (@SaleSupID='' OR SF.SaleSupID=@SaleSupID)
      AND (@RouteID='' OR SF.RouteCD=@RouteID)
    ORDER BY SF.RegionName, SF.AreaName, SF.DistributorName, SF.RouteCD, SF.SalesmanName;
END;
GO
-- Test: EXEC pp_GetDailyPlan_Local @VisitDate='2025-05-30', @UserName='admin'
