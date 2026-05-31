-- ============================================================
-- DMSPRO Reporting — Check All Reports
-- Muc dich: Kiem tra toan bo SP bao cao chay duoc va co data
-- Server: 10.86.81.159\ADMSTDB2017 | DB: URCeTools
-- Ngay tao: 2026-05-30
-- Cach dung: Chay tung section, kiem tra ket qua khong loi
-- ============================================================

DECLARE @Date       DATE     = CAST(GETDATE() AS DATE)
DECLARE @DateMinus7 DATE     = DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
DECLARE @Month1     DATETIME = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
DECLARE @UserName   NVARCHAR(30) = 'admin'

PRINT '================================================================'
PRINT 'DMSPRO Report Check — ' + CAST(@Date AS NVARCHAR)
PRINT '================================================================'

-- ── DASHBOARD ─────────────────────────────────────────────────────────
PRINT ''
PRINT '[ 1/16 ] pp_GetDashboardKPI'
BEGIN TRY
    EXEC pp_GetDashboardKPI @Date=@Date, @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

-- ── REPORTS ───────────────────────────────────────────────────────────
PRINT ''
PRINT '[ 2/16 ] pp_ReportVisit — Bao cao vieng tham'
BEGIN TRY
    EXEC pp_ReportVisit @VisitDate=@Date, @STLvl0CD='', @STLvl1CD='',
        @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @DistributorID=0, @SaleSupCD='', @RouteCD='', @UserName=@UserName, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 3/16 ] pp_ReportSMVisitSummary — Tong hop KPI'
BEGIN TRY
    EXEC pp_ReportSMVisitSummary @FromDate=@Date, @ToDate=@Date,
        @Level1ID=NULL, @Level2ID=NULL, @Level3ID=NULL, @Level4ID=NULL, @Level5ID=NULL,
        @ProvinceCD=NULL, @DistributorID=NULL, @SaleSupCD=NULL,
        @RouteCD=NULL, @SalesmanCD=NULL, @UserName=@UserName,
        @FirstTimeSync=NULL, @FirstTimeVisitAM=NULL, @FirstTimeVisitPM=NULL,
        @LastTimeVisit=NULL, @OrderDistanceValid=NULL, @TimeVisit=NULL, @STCode=NULL
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 4/16 ] pp_GetSummarySales_Local — Tong hop doanh so'
BEGIN TRY
    EXEC pp_GetSummarySales_Local @FromDate=@DateMinus7, @ToDate=@Date,
        @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 5/16 ] pp_ReportSalesEffective — Ban hang hieu qua'
BEGIN TRY
    EXEC pp_ReportSalesEffective @VisitDate=@Date,
        @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @DistributorID=0, @SaleSupCD='', @RouteCD='', @UserName=@UserName, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 6/16 ] pp_ReportSalesDailyToMTD — Doanh so ngay MTD'
BEGIN TRY
    EXEC pp_ReportSalesDailyToMTD @FromDate=@DateMinus7, @ToDate=@Date,
        @RegionID='', @AreaID='', @ProvinceID='', @DistributorID=0,
        @SaleSupID='', @RouteID='', @SalesmanID='', @IsMCP=1, @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 7/16 ] pp_GetDailyPlan_Local — Ke hoach ngay'
BEGIN TRY
    EXEC pp_GetDailyPlan_Local @VisitDate=@Date, @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 8/16 ] pp_ReportPC_SM — PC theo nhan vien'
BEGIN TRY
    EXEC pp_ReportPC_SM @FromDate=@Date,
        @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @DistributorID=0, @SaleSupCD='', @RouteCD='',
        @ShowAll=1, @Percent=0, @IsGreater=1, @UserName=@UserName, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[ 9/16 ] pp_Report3GOffline — 3G Offline'
BEGIN TRY
    EXEC pp_Report3GOffline @FromDate=@DateMinus7, @ToDate=@Date,
        @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @DistributorID=0, @SalesSupCD='', @RouteCD='',
        @UserName=@UserName, @Time=0, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[10/16] pp_ReportOutletInvalidLocation — Outlet GPS khong hop le'
BEGIN TRY
    EXEC pp_ReportOutletInvalidLocation
        @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @DistributorID=0, @SaleSupCD='', @UserName=@UserName, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[11/16] BSDH_ReportOutletNotVisitInMCP — CH chua VT MCP'
BEGIN TRY
    EXEC BSDH_ReportOutletNotVisitInMCP @FromDate=@Date,
        @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @DistributorID=0, @SaleSupCD='', @RouteCD='', @UserName=@UserName, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[12/16] pp_ReportVisitReason — Ly do vieng tham'
BEGIN TRY
    EXEC pp_ReportVisitReason @FromDate=@DateMinus7, @ToDate=@Date,
        @Level1ID='', @Level2ID='', @Level3ID='', @Level4ID='', @Level5ID='',
        @DistributorID=0, @SaleSupID='', @RouteID='', @UserName=@UserName, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[13/16] pp_ReportIssues — Bao cao Issues'
BEGIN TRY
    EXEC pp_ReportIssues @FromDate=@DateMinus7, @ToDate=@Date,
        @STLvl0CD='', @STLvl1CD='', @STLvl2CD='', @STLvl3CD='', @STLvl4CD='',
        @UserName=@UserName, @Status=0, @STCode=''
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[14/16] pp_GetTimekeeping_Local — Cham cong'
BEGIN TRY
    EXEC pp_GetTimekeeping_Local @FromDate=@DateMinus7, @ToDate=@Date,
        @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[15/16] pp_GetUserLog_Local — Nhat ky nguoi dung'
BEGIN TRY
    EXEC pp_GetUserLog_Local @FromDate=@DateMinus7, @ToDate=@Date,
        @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '[16/16] pp_ReportUserUseMobility — TDV dung App'
BEGIN TRY
    EXEC pp_ReportUserUseMobility @FromDate=@DateMinus7, @ToDate=@Date,
        @UserName=@UserName
    PRINT '  OK'
END TRY BEGIN CATCH PRINT '  ERROR: ' + ERROR_MESSAGE() END CATCH

PRINT ''
PRINT '================================================================'
PRINT 'CHECK COMPLETE. Review ERROR lines above if any.'
PRINT '================================================================'
