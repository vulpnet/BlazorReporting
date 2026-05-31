-- ============================================================
-- SP: pp_GetSummarySales_Local
-- Thay the: pp_ReportSummarySales (61s, 3 result sets, 600K rows detail)
-- Nguon: SFInfo + BLSalesKPI (URCeTools local) — ~600ms
-- Tra ve: 1 row/TDV/ngay tong hop, khong phai detail don hang
-- Ngay tao: 2026-05-30
-- ============================================================
IF OBJECT_ID('pp_GetSummarySales_Local','P') IS NOT NULL DROP PROCEDURE pp_GetSummarySales_Local;
GO
CREATE PROCEDURE pp_GetSummarySales_Local
    @FromDate DATE, @ToDate DATE, @UserName NVARCHAR(30),
    @STLvl0CD NVARCHAR(20)='', @STLvl1CD NVARCHAR(20)='',
    @DistributorID INT=0, @SaleSupCD NVARCHAR(20)='',
    @RouteCD NVARCHAR(20)='', @STCode NVARCHAR(20)=''
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT UT.RouteCD INTO #UT
    FROM UserRoutesTerritory UT WITH(NOLOCK) WHERE UT.UserName=@UserName;

    SELECT
        SF.RegionID, SF.RegionName, SF.AreaID, SF.AreaName,
        SF.DistributorID, SF.DistributorCode, SF.DistributorName,
        SF.RouteCD, SF.RouteName,
        SF.SaleSupID   AS SalesSupCD,
        SF.SaleSupName AS SalesSupName,
        SF.SalesmanID  AS SalesmanCD,
        SF.SalesmanName,
        TotalVisitPlan   = SUM(CASE WHEN BL.IsMCP=1 THEN BL.OutletMustVisit ELSE 0 END),
        OutletVisited    = SUM(CASE WHEN BL.IsMCP=1 THEN BL.OutletVisited ELSE 0 END),
        OrderCount       = SUM(CASE WHEN BL.IsMCP=1 THEN BL.OrderCount ELSE 0 END),
        TotalAmount      = SUM(CASE WHEN BL.IsMCP=1 THEN BL.TotalAmount ELSE 0 END),
        MTDTotalAmount   = SUM(CASE WHEN BL.VisitDate=@ToDate THEN BL.MTDTotalAmount ELSE 0 END),
        TotalSKU         = SUM(BL.TotalSKU),
        VisitMCP         = AVG(CASE WHEN BL.IsMCP=1 AND BL.OutletMustVisit>0 THEN BL.Visit_MCP ELSE NULL END),
        SOMCP            = AVG(CASE WHEN BL.IsMCP=1 AND BL.OutletMustVisit>0 THEN BL.SO_MCP   ELSE NULL END),
        LPPC             = AVG(CASE WHEN BL.IsMCP=1 AND BL.OrderCount>0      THEN BL.LPPC      ELSE NULL END)
    FROM (
        SELECT DISTINCT SF2.RegionID, SF2.RegionName, SF2.AreaID, SF2.AreaName,
               SF2.DistributorID, SF2.DistributorCode, SF2.DistributorName,
               SF2.RouteCD, SF2.RouteName, SF2.SaleSupID, SF2.SaleSupName,
               SF2.SalesmanID, SF2.SalesmanName
        FROM dbo.SFInfo SF2 WITH(NOLOCK)
        JOIN #UT UT ON UT.RouteCD = SF2.RouteCD
        WHERE SF2.VisitDate BETWEEN @FromDate AND @ToDate
          AND (@STLvl0CD=''    OR SF2.RegionID=@STLvl0CD)
          AND (@STLvl1CD=''    OR SF2.AreaID=@STLvl1CD)
          AND (@SaleSupCD=''   OR SF2.SaleSupID=@SaleSupCD)
          AND (@DistributorID=0 OR SF2.DistributorID=@DistributorID)
          AND (@RouteCD=''     OR SF2.RouteCD=@RouteCD)
    ) SF
    JOIN dbo.BLSalesKPI BL WITH(NOLOCK)
        ON BL.RouteCD=SF.RouteCD AND BL.SalesmanID=SF.SalesmanID
        AND BL.DistributorID=SF.DistributorID
        AND CONVERT(DATE,BL.VisitDate) BETWEEN @FromDate AND @ToDate
    GROUP BY SF.RegionID, SF.RegionName, SF.AreaID, SF.AreaName,
             SF.DistributorID, SF.DistributorCode, SF.DistributorName,
             SF.RouteCD, SF.RouteName, SF.SaleSupID, SF.SaleSupName,
             SF.SalesmanID, SF.SalesmanName
    ORDER BY SF.RegionName, SF.AreaName, SF.DistributorName, SF.RouteCD, SF.SalesmanName;

    DROP TABLE IF EXISTS #UT;
END;
GO
-- Test (1 ngay ~600ms, 1 thang ~2-3s):
-- EXEC pp_GetSummarySales_Local @FromDate='2025-05-30', @ToDate='2025-05-30', @UserName='admin'
