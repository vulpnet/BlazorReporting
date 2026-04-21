USE ReportingDB;
GO

-- ================================================================
-- sp_SalesReport
-- Parameterised sales report stored procedure.
-- Call via: /report?Source=sp_SalesReport&Type=SP
-- ================================================================

IF OBJECT_ID('dbo.sp_SalesReport', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SalesReport;
GO

CREATE PROCEDURE dbo.sp_SalesReport
    @FromDate    DATE         = NULL,   -- e.g. '2024-01-01'
    @ToDate      DATE         = NULL,   -- e.g. '2024-12-31'
    @BranchID    INT          = NULL,   -- NULL = all branches
    @Category    NVARCHAR(100)= NULL,   -- NULL = all categories
    @Region      NVARCHAR(50) = NULL    -- NULL = all regions
AS
BEGIN
    SET NOCOUNT ON;

    -- Default date range to current year when omitted
    SET @FromDate = ISNULL(@FromDate, DATEFROMPARTS(YEAR(GETDATE()), 1, 1));
    SET @ToDate   = ISNULL(@ToDate,   CAST(GETDATE() AS DATE));

    SELECT
        o.OrderId,
        o.OrderDate,
        o.BranchId,
        o.BranchName,
        o.ProductId,
        o.ProductName,
        o.Category,
        o.Qty,
        o.UnitPrice,
        o.Amount,
        o.SalespersonId,
        o.SalespersonName,
        o.Region
    FROM dbo.SalesOrders o
    WHERE
        o.OrderDate BETWEEN @FromDate AND @ToDate
        AND (@BranchID IS NULL OR o.BranchId  = @BranchID)
        AND (@Category IS NULL OR o.Category  = @Category)
        AND (@Region   IS NULL OR o.Region    = @Region)
    ORDER BY
        o.OrderDate DESC,
        o.BranchId;
END
GO

-- ================================================================
-- sp_SalesDrillDown  (optional – called directly from drill-down)
-- Returns individual rows matching a pivot cell intersection.
-- ================================================================

IF OBJECT_ID('dbo.sp_SalesDrillDown', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SalesDrillDown;
GO

CREATE PROCEDURE dbo.sp_SalesDrillDown
    @FromDate    DATE          = NULL,
    @ToDate      DATE          = NULL,
    @BranchName  NVARCHAR(100) = NULL,
    @Category    NVARCHAR(100) = NULL,
    @Region      NVARCHAR(50)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @FromDate = ISNULL(@FromDate, DATEFROMPARTS(YEAR(GETDATE()), 1, 1));
    SET @ToDate   = ISNULL(@ToDate,   CAST(GETDATE() AS DATE));

    SELECT
        o.OrderId,
        o.OrderDate,
        o.BranchName,
        o.ProductName,
        o.Category,
        o.SalespersonName,
        o.Region,
        o.Qty,
        o.UnitPrice,
        o.Amount
    FROM dbo.SalesOrders o
    WHERE
        o.OrderDate BETWEEN @FromDate AND @ToDate
        AND (@BranchName IS NULL OR o.BranchName = @BranchName)
        AND (@Category   IS NULL OR o.Category   = @Category)
        AND (@Region     IS NULL OR o.Region     = @Region)
    ORDER BY o.OrderDate DESC;
END
GO

PRINT 'Stored procedures created successfully.';
