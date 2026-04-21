-- ================================================================
-- BlazorReporting – Database setup script
-- Compatible with SQL Server 2016+
-- ================================================================

USE master;
GO

IF DB_ID('ReportingDB') IS NULL
    CREATE DATABASE ReportingDB;
GO

USE ReportingDB;
GO

-- ----------------------------------------------------------------
-- Table: UserPivotLayouts
-- Stores per-user pivot configurations as JSON.
-- ----------------------------------------------------------------
IF OBJECT_ID('dbo.UserPivotLayouts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPivotLayouts
    (
        Id         INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId     NVARCHAR(128)  NOT NULL,
        ReportName NVARCHAR(256)  NOT NULL,
        LayoutName NVARCHAR(128)  NOT NULL CONSTRAINT DF_UserPivotLayouts_LayoutName DEFAULT ('Default'),
        LayoutJson NVARCHAR(MAX)  NOT NULL CONSTRAINT DF_UserPivotLayouts_LayoutJson DEFAULT ('{}'),
        CreatedAt  DATETIME2      NOT NULL CONSTRAINT DF_UserPivotLayouts_CreatedAt  DEFAULT SYSUTCDATETIME(),
        UpdatedAt  DATETIME2      NOT NULL CONSTRAINT DF_UserPivotLayouts_UpdatedAt  DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX UX_UserPivotLayouts_User_Report_Layout
        ON dbo.UserPivotLayouts (UserId, ReportName, LayoutName);
END
GO

-- ----------------------------------------------------------------
-- Sample data table: SalesOrders
-- ----------------------------------------------------------------
IF OBJECT_ID('dbo.SalesOrders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesOrders
    (
        OrderId     INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderDate   DATE          NOT NULL,
        BranchId    INT           NOT NULL,
        BranchName  NVARCHAR(100) NOT NULL,
        ProductId   INT           NOT NULL,
        ProductName NVARCHAR(200) NOT NULL,
        Category    NVARCHAR(100) NOT NULL,
        Qty         INT           NOT NULL,
        UnitPrice   DECIMAL(18,2) NOT NULL,
        Amount      AS (Qty * UnitPrice) PERSISTED,
        SalespersonId   INT           NOT NULL,
        SalespersonName NVARCHAR(100) NOT NULL,
        Region      NVARCHAR(50)  NOT NULL
    );

    -- Seed ~50 000 demo rows
    WITH Nums AS
    (
        SELECT TOP 50000 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
        FROM sys.all_objects a CROSS JOIN sys.all_objects b
    ),
    Seed AS
    (
        SELECT
            n,
            DATEADD(DAY, -(n % 730), CAST(GETDATE() AS DATE))           AS OrderDate,
            (n % 5) + 1                                                   AS BranchId,
            'Branch ' + CAST((n % 5) + 1 AS VARCHAR)                    AS BranchName,
            (n % 20) + 1                                                  AS ProductId,
            'Product ' + CHAR(65 + (n % 20))                             AS ProductName,
            CASE (n % 4) WHEN 0 THEN 'Electronics'
                         WHEN 1 THEN 'Clothing'
                         WHEN 2 THEN 'Food'
                         ELSE 'Other' END                                AS Category,
            (n % 10) + 1                                                  AS Qty,
            CAST(((n % 500) + 10) AS DECIMAL(18,2))                     AS UnitPrice,
            (n % 8) + 1                                                   AS SalespersonId,
            'Salesperson ' + CAST((n % 8) + 1 AS VARCHAR)               AS SalespersonName,
            CASE (n % 3) WHEN 0 THEN 'North'
                         WHEN 1 THEN 'South'
                         ELSE 'Central' END                              AS Region
        FROM Nums
    )
    INSERT INTO dbo.SalesOrders
        (OrderDate, BranchId, BranchName, ProductId, ProductName,
         Category, Qty, UnitPrice, SalespersonId, SalespersonName, Region)
    SELECT OrderDate, BranchId, BranchName, ProductId, ProductName,
           Category, Qty, UnitPrice, SalespersonId, SalespersonName, Region
    FROM Seed;
END
GO

-- ----------------------------------------------------------------
-- View: vw_SalesSummary  (useful as a View-type data source)
-- ----------------------------------------------------------------
IF OBJECT_ID('dbo.vw_SalesSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_SalesSummary;
GO

CREATE VIEW dbo.vw_SalesSummary AS
SELECT
    YEAR(OrderDate)   AS SaleYear,
    MONTH(OrderDate)  AS SaleMonth,
    BranchName,
    Category,
    Region,
    SalespersonName,
    SUM(Amount)       AS TotalAmount,
    SUM(Qty)          AS TotalQty,
    COUNT(*)          AS OrderCount
FROM dbo.SalesOrders
GROUP BY
    YEAR(OrderDate), MONTH(OrderDate),
    BranchName, Category, Region, SalespersonName;
GO

PRINT 'Setup complete. Run sp_SalesReport.sql next.';
