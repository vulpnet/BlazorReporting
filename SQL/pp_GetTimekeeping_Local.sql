-- ============================================================
-- SP: pp_GetTimekeeping_Local
-- Thay the: pp_ReportTimekeeping (dung NhatNhateTools_0907 linked server)
-- Nguon du lieu: SFInfo + BLSalesKPI (URCeTools local)
-- Ngay tao: 2026-05-30
-- ============================================================
IF OBJECT_ID('pp_GetTimekeeping_Local','P') IS NOT NULL DROP PROCEDURE pp_GetTimekeeping_Local;
GO
CREATE PROCEDURE pp_GetTimekeeping_Local
    @FromDate      DATETIME,
    @ToDate        DATETIME,
    @RegionID      NVARCHAR(20) = '',
    @AreaID        NVARCHAR(20) = '',
    @DistributorID INT = 0,
    @SalemanID     NVARCHAR(20) = '',
    @UserName      NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        SF.RegionID, SF.RegionName, SF.AreaID, SF.AreaName,
        DistributorID = 0, DistributorName = '',
        SF.SalesmanID, SF.SalesmanName,
        ScheduleDateWork = SUM(CASE WHEN BL.OutletMustVisit > 0 THEN 1 ELSE 0 END),
        SynchronizeDate  = SUM(CASE WHEN BL.FirstSyncTime IS NOT NULL THEN 1 ELSE 0 END),
        VisitedDate      = SUM(CASE WHEN BL.TotalAmount > 0 THEN 1 ELSE 0 END),
        VisitedDateRate  = CASE WHEN SUM(CASE WHEN BL.OutletMustVisit > 0 THEN 1 ELSE 0 END) > 0
                           THEN CONVERT(DECIMAL(16,2),
                                SUM(CASE WHEN BL.TotalAmount > 0 THEN 1 ELSE 0 END) * 100.0
                                / SUM(CASE WHEN BL.OutletMustVisit > 0 THEN 1 ELSE 0 END))
                           ELSE 0 END
    FROM (
        SELECT DISTINCT SF2.RegionID, SF2.RegionName, SF2.AreaID, SF2.AreaName,
                        SF2.SalesmanID, SF2.SalesmanName
        FROM dbo.SFInfo SF2 WITH (NOLOCK)
        JOIN dbo.UserRoutesTerritory UT WITH (NOLOCK)
            ON UT.RouteCD=SF2.RouteCD AND UT.DistributorID=SF2.DistributorID AND UT.UserName=@UserName
        WHERE (@RegionID='' OR SF2.RegionID=@RegionID)
          AND (@AreaID='' OR SF2.AreaID=@AreaID)
          AND (@SalemanID='' OR SF2.SalesmanID=@SalemanID)
    ) SF
    LEFT JOIN dbo.BLSalesKPI BL WITH (NOLOCK)
        ON BL.SalesmanID = SF.SalesmanID AND BL.IsMCP = 1
        AND CONVERT(DATE,BL.VisitDate) >= CONVERT(DATE,@FromDate)
        AND CONVERT(DATE,BL.VisitDate) <= CONVERT(DATE,@ToDate)
        AND (@DistributorID=0 OR BL.DistributorID=@DistributorID)
    GROUP BY SF.RegionID, SF.RegionName, SF.AreaID, SF.AreaName, SF.SalesmanID, SF.SalesmanName
    ORDER BY SF.RegionName, SF.AreaName, SF.SalesmanName;
END;
GO
-- Test: EXEC pp_GetTimekeeping_Local @FromDate='2025-05-01', @ToDate='2025-05-30', @UserName='admin'
