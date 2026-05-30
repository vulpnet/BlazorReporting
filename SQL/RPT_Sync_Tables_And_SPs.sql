-- ============================================================
-- DMSPRO Reporting — RPT Tables & Sync Stored Procedures
-- Generated: 2026-05-30 12:57
-- Database  : URCeTools
-- Server    : 10.86.81.159\ADMSTDB2017
-- ============================================================


-- ============================================================
-- 1. TABLE: RPT_DailySales
--    Aggregate OrderHeader by (VisitDate, SalesmanID, RouteCD)
--    Sync: EXEC sp_Sync_RPT_DailySales @FromDate, @ToDate
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RPT_DailySales')
BEGIN
    CREATE TABLE RPT_DailySales (
        VisitDate       DATE          NOT NULL,
        SalesmanID      NVARCHAR(20)  NOT NULL,
        RouteCD         NVARCHAR(50)  NOT NULL,
        DistributorID   INT           NULL,
        DistributorCode NVARCHAR(20)  NULL,
        DistributorName NVARCHAR(200) NULL,
        SaleSupID       NVARCHAR(20)  NULL,
        STLvl0CD        NVARCHAR(50)  NULL,
        STLvl1CD        NVARCHAR(50)  NULL,
        STLvl2CD        NVARCHAR(50)  NULL,
        OrderCount      INT           NOT NULL DEFAULT 0,
        TotalAmt        DECIMAL(18,4) NOT NULL DEFAULT 0,
        TotalQty        INT           NOT NULL DEFAULT 0,
        TotalSKU        INT           NOT NULL DEFAULT 0,
        OutletCount     INT           NOT NULL DEFAULT 0,
        UpdatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_RPT_DailySales PRIMARY KEY (VisitDate, SalesmanID, RouteCD)
    );
    CREATE INDEX IX_RPT_DailySales_Date  ON RPT_DailySales (VisitDate)   INCLUDE (TotalAmt, OrderCount);
    CREATE INDEX IX_RPT_DailySales_Route ON RPT_DailySales (RouteCD, VisitDate);
    CREATE INDEX IX_RPT_DailySales_SM    ON RPT_DailySales (SalesmanID, VisitDate);
    PRINT 'Created RPT_DailySales';
END
ELSE
    PRINT 'RPT_DailySales already exists';
GO


-- ============================================================
-- 2. TABLE: RPT_ProductSales
--    Aggregate OrderDetail by (Year, Month, SalesmanID, DistributorCode, InventoryCD)
--    Sync: EXEC sp_Sync_RPT_ProductSales @FromDate, @ToDate
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RPT_ProductSales')
BEGIN
    CREATE TABLE RPT_ProductSales (
        SalesYear       SMALLINT      NOT NULL,
        SalesMonth      TINYINT       NOT NULL,
        SalesmanID      NVARCHAR(20)  NOT NULL,
        DistributorCode NVARCHAR(20)  NOT NULL DEFAULT '',
        InventoryCD     NVARCHAR(50)  NOT NULL,
        InventoryName   NVARCHAR(200) NULL,
        OrderQty        BIGINT        NOT NULL DEFAULT 0,
        LineAmt         DECIMAL(18,4) NOT NULL DEFAULT 0,
        OrderCount      INT           NOT NULL DEFAULT 0,
        UpdatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_RPT_ProductSales PRIMARY KEY (SalesYear, SalesMonth, SalesmanID, DistributorCode, InventoryCD)
    );
    CREATE INDEX IX_RPT_ProductSales_Period ON RPT_ProductSales (SalesYear, SalesMonth) INCLUDE (InventoryCD, LineAmt, OrderQty);
    CREATE INDEX IX_RPT_ProductSales_Inv    ON RPT_ProductSales (InventoryCD, SalesYear, SalesMonth);
    PRINT 'Created RPT_ProductSales';
END
ELSE
    PRINT 'RPT_ProductSales already exists';
GO


-- ============================================================
-- 3. SP: sp_Sync_RPT_DailySales
--    Sync OrderHeader -> RPT_DailySales for a date range
--    Usage:
--      EXEC sp_Sync_RPT_DailySales                        -- today
--      EXEC sp_Sync_RPT_DailySales '2026-05-20','2026-05-20'
--      EXEC sp_Sync_RPT_DailySales '2024-01-01','2026-05-31'  -- backfill
-- ============================================================
CREATE OR ALTER PROCEDURE sp_Sync_RPT_DailySales
    @FromDate DATE = NULL,
    @ToDate   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE)
    IF @ToDate   IS NULL SET @ToDate   = @FromDate

    -- Lấy toàn bộ cột từ SP rồi chỉ lấy những gì cần
    CREATE TABLE #SmAll (
        SalesmanID      NVARCHAR(20),
        DistributorID   INT,
        DistributorCode NVARCHAR(20),
        DistributorName NVARCHAR(200),
        RouteCD         NVARCHAR(50),
        SaleSupID       NVARCHAR(20),
        STLvl0CD        NVARCHAR(50),
        STLvl1CD        NVARCHAR(50),
        STLvl2CD        NVARCHAR(50)
    )

    -- Dùng view pp_GetSalemanLastLocation_view nếu có, không thì dùng Salesman join
    INSERT INTO #SmAll (SalesmanID, DistributorID, DistributorCode, DistributorName, RouteCD, SaleSupID, STLvl0CD, STLvl1CD, STLvl2CD)
    SELECT DISTINCT
        h.SalesmanID,
        h.DistributorID,
        d.DistributorCode,
        d.DistributorName,
        h.RouteCD,
        NULL AS SaleSupID,
        NULL AS STLvl0CD,
        NULL AS STLvl1CD,
        NULL AS STLvl2CD
    FROM OrderHeader h
    LEFT JOIN Distributor d ON d.DistributorID = h.DistributorID
    WHERE CAST(h.VisitDate AS DATE) BETWEEN @FromDate AND @ToDate

    MERGE RPT_DailySales AS tgt
    USING (
        SELECT
            CAST(h.VisitDate AS DATE)   AS VisitDate,
            h.SalesmanID,
            ISNULL(h.RouteCD, '')       AS RouteCD,
            MAX(o.DistributorID)        AS DistributorID,
            MAX(o.DistributorCode)      AS DistributorCode,
            MAX(o.DistributorName)      AS DistributorName,
            MAX(o.SaleSupID)            AS SaleSupID,
            MAX(o.STLvl0CD)             AS STLvl0CD,
            MAX(o.STLvl1CD)             AS STLvl1CD,
            MAX(o.STLvl2CD)             AS STLvl2CD,
            COUNT(*)                    AS OrderCount,
            SUM(h.TotalAmt)             AS TotalAmt,
            SUM(h.TotalQty)             AS TotalQty,
            SUM(h.TotalSKU)             AS TotalSKU,
            COUNT(DISTINCT h.OutletID)  AS OutletCount
        FROM OrderHeader h
        LEFT JOIN #SmAll o ON o.SalesmanID = h.SalesmanID
                           AND o.RouteCD   = ISNULL(h.RouteCD,'')
        WHERE CAST(h.VisitDate AS DATE) BETWEEN @FromDate AND @ToDate
          AND h.TotalAmt > 0
        GROUP BY CAST(h.VisitDate AS DATE), h.SalesmanID, ISNULL(h.RouteCD,'')
    ) AS src ON tgt.VisitDate  = src.VisitDate
             AND tgt.SalesmanID = src.SalesmanID
             AND tgt.RouteCD    = src.RouteCD
    WHEN MATCHED THEN UPDATE SET
        DistributorID   = src.DistributorID,
        DistributorCode = src.DistributorCode,
        DistributorName = src.DistributorName,
        SaleSupID       = src.SaleSupID,
        STLvl0CD        = src.STLvl0CD,
        STLvl1CD        = src.STLvl1CD,
        STLvl2CD        = src.STLvl2CD,
        OrderCount      = src.OrderCount,
        TotalAmt        = src.TotalAmt,
        TotalQty        = src.TotalQty,
        TotalSKU        = src.TotalSKU,
        OutletCount     = src.OutletCount,
        UpdatedAt       = GETDATE()
    WHEN NOT MATCHED THEN INSERT (
        VisitDate, SalesmanID, RouteCD,
        DistributorID, DistributorCode, DistributorName, SaleSupID,
        STLvl0CD, STLvl1CD, STLvl2CD,
        OrderCount, TotalAmt, TotalQty, TotalSKU, OutletCount, UpdatedAt
    ) VALUES (
        src.VisitDate, src.SalesmanID, src.RouteCD,
        src.DistributorID, src.DistributorCode, src.DistributorName, src.SaleSupID,
        src.STLvl0CD, src.STLvl1CD, src.STLvl2CD,
        src.OrderCount, src.TotalAmt, src.TotalQty, src.TotalSKU, src.OutletCount, GETDATE()
    );

    DROP TABLE #SmAll
    SELECT @@ROWCOUNT AS AffectedRows, @FromDate AS FromDate, @ToDate AS ToDate
END
GO


-- ============================================================
-- 4. SP: sp_Sync_RPT_ProductSales
--    Sync OrderDetail -> RPT_ProductSales for a month range
--    Usage:
--      EXEC sp_Sync_RPT_ProductSales                          -- current month
--      EXEC sp_Sync_RPT_ProductSales '2026-05-01','2026-05-31'
--      EXEC sp_Sync_RPT_ProductSales '2024-01-01','2026-05-31' -- backfill
-- ============================================================
CREATE OR ALTER PROCEDURE sp_Sync_RPT_ProductSales
    @FromDate DATE = NULL,
    @ToDate   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FromDate IS NULL SET @FromDate = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
    IF @ToDate   IS NULL SET @ToDate   = EOMONTH(GETDATE())

    MERGE RPT_ProductSales AS tgt
    USING (
        SELECT
            YEAR(d.VisitDate)                   AS SalesYear,
            MONTH(d.VisitDate)                  AS SalesMonth,
            d.SalesmanID,
            ISNULL(d.DistributorCode,'')        AS DistributorCode,
            d.InventoryCD,
            MAX(ISNULL(d.InventoryName, d.InventoryCD)) AS InventoryName,
            SUM(d.OrderQty)                     AS OrderQty,
            SUM(d.LineAmt)                      AS LineAmt,
            COUNT(DISTINCT d.OrderCode)         AS OrderCount
        FROM OrderDetail d WITH (NOLOCK)
        WHERE d.VisitDate >= @FromDate AND d.VisitDate <= @ToDate
          AND d.LineAmt > 0
        GROUP BY YEAR(d.VisitDate), MONTH(d.VisitDate),
                 d.SalesmanID, ISNULL(d.DistributorCode,''), d.InventoryCD
    ) AS src ON tgt.SalesYear        = src.SalesYear
             AND tgt.SalesMonth      = src.SalesMonth
             AND tgt.SalesmanID      = src.SalesmanID
             AND tgt.DistributorCode = src.DistributorCode
             AND tgt.InventoryCD     = src.InventoryCD
    WHEN MATCHED THEN UPDATE SET
        InventoryName = src.InventoryName,
        OrderQty      = src.OrderQty,
        LineAmt       = src.LineAmt,
        OrderCount    = src.OrderCount,
        UpdatedAt     = GETDATE()
    WHEN NOT MATCHED THEN INSERT (
        SalesYear, SalesMonth, SalesmanID, DistributorCode,
        InventoryCD, InventoryName, OrderQty, LineAmt, OrderCount, UpdatedAt
    ) VALUES (
        src.SalesYear, src.SalesMonth, src.SalesmanID, src.DistributorCode,
        src.InventoryCD, src.InventoryName, src.OrderQty, src.LineAmt, src.OrderCount, GETDATE()
    );

    SELECT @@ROWCOUNT AS AffectedRows, @FromDate AS FromDate, @ToDate AS ToDate
END
GO


-- ============================================================
-- 5. USAGE EXAMPLES
-- ============================================================
-- Daily job (run end of day):
--   EXEC sp_Sync_RPT_DailySales
--
-- Monthly job (run end of month):
--   EXEC sp_Sync_RPT_ProductSales
--
-- Backfill all history:
--   EXEC sp_Sync_RPT_DailySales    '2024-01-01', '2026-05-31'
--   EXEC sp_Sync_RPT_ProductSales  '2024-01-01', '2026-05-31'
