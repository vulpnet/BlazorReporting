-- ============================================================
-- SP: pp_GetSmLocationForMap
-- Dung rieng cho TerritoryMap — KHONG thay the pp_GetSalemanLastLocation
-- GPS: SalesmanVisit (co Latitude/Longtitude), khong phai BLSalesKPI
-- KPI: BLSalesKPI (OutletMustVisit, TotalAmount...)
-- SF: SFInfo (ten SM, SS, Distributor, Region, Area)
-- Performance: ~3.5s (vs pp_GetSalemanLastLocation: 32s)
-- Ngay tao: 2026-05-30
-- ============================================================
IF OBJECT_ID('pp_GetSmLocationForMap','P') IS NOT NULL DROP PROCEDURE pp_GetSmLocationForMap;
GO
CREATE PROCEDURE pp_GetSmLocationForMap
    @Username      NVARCHAR(30),
    @Date          DATE,
    @SalesupID     NVARCHAR(20) = '',
    @DistributorID INT = 0,
    @SalesmanID    NVARCHAR(40) = ''
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT UT.RouteCD, UT.DistributorID INTO #UT
    FROM dbo.UserRoutesTerritory UT WITH(NOLOCK) WHERE UT.UserName=@Username;

    SELECT SalesmanID,
           Latitude      = MAX(Latitude),
           Longtitude    = MAX(Longtitude),
           LastVisitTime = MAX(VisitTime)
    INTO #GPS
    FROM dbo.SalesmanVisit WITH(NOLOCK)
    WHERE CONVERT(DATE,VisitDate) = @Date
      AND Latitude IS NOT NULL AND Latitude != 0
    GROUP BY SalesmanID;

    SELECT BL.SalesmanID, BL.DistributorID, BL.RouteCD,
           TotalAmount    = SUM(CASE WHEN BL.IsMCP=1 THEN BL.TotalAmount    ELSE 0 END),
           OrderCount     = SUM(CASE WHEN BL.IsMCP=1 THEN BL.OrderCount     ELSE 0 END),
           OutletVisited  = SUM(CASE WHEN BL.IsMCP=1 THEN BL.OutletVisited  ELSE 0 END),
           OutletMustVisit= SUM(CASE WHEN BL.IsMCP=1 THEN BL.OutletMustVisit ELSE 0 END),
           FirstSyncTime    = MAX(BL.FirstSyncTime),
           FirstStartTimeAM = MAX(BL.FirstStartTimeAM),
           LastEndTime      = MAX(BL.LastEndTime),
           TotalDistance    = SUM(BL.DistanceVisit),
           TimeVisit        = SUM(BL.TimeVisit),
           TimeMove         = SUM(BL.TimeMove)
    INTO #KPI
    FROM dbo.BLSalesKPI BL WITH(NOLOCK)
    JOIN #UT UT ON BL.RouteCD=UT.RouteCD AND BL.DistributorID=UT.DistributorID
    WHERE CONVERT(DATE,BL.VisitDate) = @Date
      AND (@DistributorID=0 OR BL.DistributorID=@DistributorID)
    GROUP BY BL.SalesmanID, BL.DistributorID, BL.RouteCD;

    SELECT
        K.SalesmanID,
        ISNULL(SF.SalesmanName, K.SalesmanID) AS SalesmanName,
        ISNULL(SF.SMPhone,'')                 AS Phone,
        ISNULL(SF.SaleSupID,'')               AS SaleSupID,
        ISNULL(SF.SaleSupName,'')             AS SaleSupName,
        ISNULL(SF.DistributorCode,'')         AS DistributorCode,
        ISNULL(SF.DistributorName,'')         AS DistributorName,
        ISNULL(SF.RouteCD, K.RouteCD)         AS RouteCD,
        ISNULL(SF.RouteName, K.RouteCD)       AS RouteName,
        ISNULL(SF.RegionID,'')                AS STLvl0CD,
        ISNULL(SF.RegionName,'')              AS SFLvl0Name,
        ISNULL(SF.AreaID,'')                  AS STLvl1CD,
        ISNULL(SF.AreaName,'')                AS SFLvl1Name,
        Latitude    = ISNULL(G.Latitude, 0),
        Longtitude  = ISNULL(G.Longtitude, 0),
        FirstSyncTime    = K.FirstSyncTime,
        LastSyncTime     = G.LastVisitTime,
        FirstStartTimeAM = K.FirstStartTimeAM,
        LastEndTime      = K.LastEndTime,
        K.TotalAmount, K.OrderCount, K.OutletVisited, K.OutletMustVisit,
        K.TotalDistance, K.TimeVisit, K.TimeMove
    FROM #KPI K
    LEFT JOIN #GPS G ON G.SalesmanID = K.SalesmanID
    LEFT JOIN (
        SELECT DISTINCT SalesmanID, SalesmanName, SMPhone, SaleSupID, SaleSupName,
               DistributorID, DistributorCode, DistributorName,
               RouteCD, RouteName, RegionID, RegionName, AreaID, AreaName
        FROM dbo.SFInfo WITH(NOLOCK) WHERE VisitDate = @Date
    ) SF ON SF.SalesmanID=K.SalesmanID AND SF.DistributorID=K.DistributorID
    WHERE (@SalesupID=''  OR SF.SaleSupID=@SalesupID)
      AND (@SalesmanID='' OR K.SalesmanID=@SalesmanID)
    ORDER BY SF.RegionName, SF.AreaName, ISNULL(SF.SaleSupName,''), K.SalesmanID;

    DROP TABLE IF EXISTS #UT;
    DROP TABLE IF EXISTS #GPS;
    DROP TABLE IF EXISTS #KPI;
END;
GO
-- Test: EXEC pp_GetSmLocationForMap @Username='admin', @Date='2025-05-30'
-- ~3.5s (vs pp_GetSalemanLastLocation: 32s)
