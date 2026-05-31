using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

// ── Models ──────────────────────────────────────────────────────────

public sealed class DistributionRow
{
    public string SalesmanCode   { get; set; } = "";
    public string SalesmanName   { get; set; } = "";
    public decimal StockSalesman { get; set; }
    public string SalesSupCode   { get; set; } = "";
    public string SalesSupName   { get; set; } = "";
    public string Type           { get; set; } = "";
    public string SKUCode        { get; set; } = "";
    public string SKUName        { get; set; } = "";
    public decimal StockSKU      { get; set; }
    public string DistributorCode{ get; set; } = "";
    public string DistributorName{ get; set; } = "";
    public DateTime? CreateDate  { get; set; }
    public DateTime? DistributeDate { get; set; }
}

public sealed class DistributorRow
{
    public int    DistributorID   { get; set; }
    public string DistributorCode { get; set; } = "";
    public string DistributorName { get; set; } = "";
}

public sealed class BudgetRow
{
    public int    BudgetID   { get; set; }
    public string BudgetCode { get; set; } = "";
    public string BudgetName { get; set; } = "";
    public decimal Value     { get; set; }
}

public sealed class BudgetAssignRow
{
    public string SalesmanCode { get; set; } = "";
    public string SalesmanName { get; set; } = "";
    public decimal Target      { get; set; }
    public string SalesSupCode { get; set; } = "";
    public string SalesSupName { get; set; } = "";
    public string BudgetCode   { get; set; } = "";
    public string BudgetName   { get; set; } = "";
    public decimal BudgetValue { get; set; }
}

// ── Service ─────────────────────────────────────────────────────────

public sealed class DistributionService
{
    private readonly string _cs;

    public DistributionService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    // ── Distribution Management (SKU) ────────────────────────────────

    public async Task<List<DistributionRow>> GetDistributionAsync(DateTime date, string distributorCode, string username)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<DistributionRow>(
            "EXEC DMS_DistributionManagement @Date, @DistributorCode, @UserName",
            new { Date = date.Date, DistributorCode = distributorCode, UserName = username },
            commandTimeout: 30);
        return rows.ToList();
    }

    public async Task<List<DistributorRow>> GetDistributorsAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<DistributorRow>(
            "SELECT DistributorID, DistributorCode, DistributorName FROM Distributor ORDER BY DistributorName");
        return rows.ToList();
    }

    public async Task<(bool Ok, string Msg)> InsertDistributionAsync(
        string distributorCode, string salesmanCode, string skuCode, decimal stock, string type, string username)
    {
        await using var conn = Conn();
        try
        {
            await conn.ExecuteAsync(
                "EXEC DMS_InsertDistributionManagement @DistributorCode, @SalesmanCode, @SKUCode, @Stock, @Type, @UserName",
                new { DistributorCode=distributorCode, SalesmanCode=salesmanCode, SKUCode=skuCode, Stock=stock, Type=type, UserName=username });
            return (true, "Thêm thành công.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Msg)> DeleteDistributionAsync(string salesmanCode, string skuCode, string distributorCode)
    {
        await using var conn = Conn();
        try
        {
            await conn.ExecuteAsync(
                "EXEC DMS_DeleteDistributionManagement @SalesmanCode, @SKUCode, @DistributorCode",
                new { SalesmanCode=salesmanCode, SKUCode=skuCode, DistributorCode=distributorCode });
            return (true, "Đã xóa.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── Budget Assignment ─────────────────────────────────────────────

    public async Task<List<BudgetRow>> GetBudgetsAsync(string username)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<BudgetRow>(
            "EXEC DMS_GetBudget @UserName", new { UserName = username });
        return rows.ToList();
    }

    public async Task<List<BudgetAssignRow>> GetBudgetAssignsAsync(string username)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<BudgetAssignRow>(
            "EXEC DMS_GetAllBudgetAssign @UserName", new { UserName = username });
        return rows.ToList();
    }

    public async Task<(bool Ok, string Msg)> InsertBudgetAssignAsync(BudgetAssignRow row, string username)
    {
        await using var conn = Conn();
        try
        {
            var p = new DynamicParameters();
            p.Add("@SalesmanCode", row.SalesmanCode);
            p.Add("@SalesmanName", row.SalesmanName);
            p.Add("@Target",       row.Target);
            p.Add("@SalesSupCode", row.SalesSupCode);
            p.Add("@SalesSupName", row.SalesSupName);
            p.Add("@BudgetCode",   row.BudgetCode);
            p.Add("@BudgetName",   row.BudgetName);
            p.Add("@BudgetValue",  row.BudgetValue);
            p.Add("@ValReturn",    dbType: System.Data.DbType.String, size: 255, direction: System.Data.ParameterDirection.Output);
            p.Add("@UserName",     username);
            await conn.ExecuteAsync("EXEC DMS_InsertBudgetAssign @SalesmanCode, @SalesmanName, @Target, @SalesSupCode, @SalesSupName, @BudgetCode, @BudgetName, @BudgetValue, @ValReturn OUTPUT, @UserName", p);
            var result = p.Get<string>("@ValReturn");
            return (true, result ?? "Thêm thành công.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Ok, string Msg)> ApproveBudgetAsync(string salesmanCode, string budgetCode, string username)
    {
        await using var conn = Conn();
        try
        {
            var p = new DynamicParameters();
            p.Add("@SalesmanCode", salesmanCode);
            p.Add("@BudgetCode",   budgetCode);
            p.Add("@ValReturn",    dbType: System.Data.DbType.String, size: 255, direction: System.Data.ParameterDirection.Output);
            p.Add("@UserName",     username);
            await conn.ExecuteAsync("EXEC DMS_AprroveBudgetAssign @SalesmanCode, @BudgetCode, @ValReturn OUTPUT, @UserName", p);
            var result = p.Get<string>("@ValReturn");
            return (true, result ?? "Duyệt thành công.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
