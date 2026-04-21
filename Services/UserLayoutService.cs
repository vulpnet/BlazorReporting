using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using BlazorReporting.Models;

namespace BlazorReporting.Services;

public sealed class UserLayoutService : IUserLayoutService
{
    private readonly string _cs;

    public UserLayoutService(IConfiguration cfg) =>
        _cs = cfg.GetConnectionString("DefaultConnection")!;

    public async Task<UserPivotLayout?> LoadAsync(
        string userId, string reportName, string layoutName = "Default")
    {
        await using var conn = new SqlConnection(_cs);
        return await conn.QueryFirstOrDefaultAsync<UserPivotLayout>(
            """
            SELECT Id, UserId, ReportName, LayoutName, LayoutJson, CreatedAt, UpdatedAt
            FROM   UserPivotLayouts
            WHERE  UserId = @userId AND ReportName = @reportName AND LayoutName = @layoutName
            """,
            new { userId, reportName, layoutName });
    }

    public async Task<IReadOnlyList<UserPivotLayout>> ListAsync(string userId, string reportName)
    {
        await using var conn = new SqlConnection(_cs);
        var rows = await conn.QueryAsync<UserPivotLayout>(
            """
            SELECT Id, UserId, ReportName, LayoutName, LayoutJson, CreatedAt, UpdatedAt
            FROM   UserPivotLayouts
            WHERE  UserId = @userId AND ReportName = @reportName
            ORDER BY UpdatedAt DESC
            """,
            new { userId, reportName });
        return rows.ToList();
    }

    public async Task SaveAsync(UserPivotLayout layout)
    {
        await using var conn = new SqlConnection(_cs);
        // MERGE requires SQL Server 2008+; safe for SS2016.
        await conn.ExecuteAsync(
            """
            MERGE UserPivotLayouts AS t
            USING (VALUES (@UserId, @ReportName, @LayoutName))
                  AS s(UserId, ReportName, LayoutName)
            ON  t.UserId = s.UserId
            AND t.ReportName = s.ReportName
            AND t.LayoutName = s.LayoutName
            WHEN MATCHED THEN
                UPDATE SET LayoutJson = @LayoutJson, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (UserId, ReportName, LayoutName, LayoutJson, CreatedAt, UpdatedAt)
                VALUES (@UserId, @ReportName, @LayoutName, @LayoutJson,
                        SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            layout);
    }

    public async Task DeleteAsync(int id)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.ExecuteAsync(
            "DELETE FROM UserPivotLayouts WHERE Id = @id",
            new { id });
    }
}
