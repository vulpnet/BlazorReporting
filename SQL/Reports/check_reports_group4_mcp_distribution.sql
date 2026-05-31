-- ============================================================
-- Group 4: MCP & DISTRIBUTION REPORTS
-- ============================================================
DECLARE @Date       DATE = CAST(GETDATE() AS DATE)
DECLARE @DateMinus7 DATE = DATEADD(DAY,-7,CAST(GETDATE() AS DATE))
DECLARE @User NVARCHAR(30) = 'admin'

-- 1. MCP Report
-- Expected: RegionName, DistributorName, CusomerInRoute, Covered, % Coverage
EXEC pp_ReportCustomerMCP
    @STLvl0CD='', @STLvl1CD='', @STLvl2CD='',
    @STLvl3CD='', @STLvl4CD='', @DistributorID=0, @STCode=''

-- 2. MCP Detail theo tuyen mau
-- Lay 1 route co data de test
DECLARE @SampleRoute NVARCHAR(50)
SELECT TOP 1 @SampleRoute = BL.RouteCD
FROM BLSalesKPI BL
JOIN UserRoutesTerritory UT ON UT.RouteCD=BL.RouteCD AND UT.DistributorID=BL.DistributorID
WHERE UT.UserName=@User AND BL.VisitDate=@Date AND BL.IsMCP=1 AND BL.OrderCount>0

PRINT 'Testing MCP Detail with RouteCD: ' + ISNULL(@SampleRoute, 'N/A')
IF @SampleRoute IS NOT NULL
    EXEC pp_GetOutletMCPBy @RouteCD=@SampleRoute, @ListOutlet='', @TypeAll=1

-- 3. Distribution SKU
-- Expected: SalesmanCode, SKUCode, StockSalesman, StockSKU
DECLARE @SampleDist NVARCHAR(20)
SELECT TOP 1 @SampleDist = DistributorCode FROM Distributor
    WHERE DistributorID IN (SELECT DISTINCT DistributorID FROM UserRoutesTerritory WHERE UserName=@User)

PRINT 'Testing Distribution with Distributor: ' + ISNULL(@SampleDist, 'N/A')
IF @SampleDist IS NOT NULL
    EXEC DMS_DistributionManagement @Date=@Date, @DistributorCode=@SampleDist, @UserName=@User

-- 4. Budget Assign
EXEC DMS_GetAllBudgetAssign @UserName=@User
EXEC DMS_GetBudget @UserName=@User

-- LOGIC CHECK: MCP coverage
SELECT
    'MCP Coverage Check' AS [Check],
    COUNT(DISTINCT RouteCD) AS Routes,
    COUNT(DISTINCT CustomerID) AS Total_Outlets,
    COUNT(DISTINCT CASE WHEN [Status]='A' THEN CustomerID END) AS Active_Outlets,
    -- Outlets co visit hom nay
    (SELECT COUNT(DISTINCT OutletID) FROM OrderHeader
     WHERE VisitDate=@Date
     AND DistributorID IN (SELECT DISTINCT DistributorID FROM UserRoutesTerritory WHERE UserName=@User)
    ) AS Visited_Today
FROM DMSMCPDetail
WHERE DistributorID IN (SELECT DISTINCT DistributorID FROM UserRoutesTerritory WHERE UserName=@User)
