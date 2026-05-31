using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

// ── E_Issue ──────────────────────────────────────────────────────────

public sealed class IssueRow
{
    public int      ID             { get; set; }
    public string   IssueID        { get; set; } = "";
    public string   OutletID       { get; set; } = "";
    public string   DistributorCode{ get; set; } = "";
    public string   SalesmanCode   { get; set; } = "";
    public DateTime? Date          { get; set; }
    public DateTime? VisitDate     { get; set; }
    public string   Content        { get; set; } = "";
    public string   Resolve        { get; set; } = "";
    public int      Status         { get; set; }
    public int      ImageCount     { get; set; }
    public DateTime? UpdateDate    { get; set; }
}

// ── M_Task ───────────────────────────────────────────────────────────

public sealed class TaskRow
{
    public string   RefNbr        { get; set; } = "";
    public string   Title         { get; set; } = "";
    public string   Desc          { get; set; } = "";
    public DateTime? StartDate    { get; set; }
    public DateTime? EndDate      { get; set; }
    public string   ApplyTo       { get; set; } = "";
    public int?     Type          { get; set; }
    public bool     Active        { get; set; }
    public string   CreatedByID   { get; set; } = "";
    public DateTime? CreatedDateTime { get; set; }
}

// ── Service ─────────────────────────────────────────────────────────

public sealed class IssuesService
{
    private readonly string _cs;

    public IssuesService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    // ── Issues ────────────────────────────────────────────────────────

    public async Task<List<IssueRow>> GetIssuesAsync(
        DateTime? fromDate = null, DateTime? toDate = null,
        string salesmanCode = "", int status = -1)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<IssueRow>(
            """
            SELECT ID, IssueID, OutletID, DistributorCode, SalesmanCode,
                   Date, VisitDate, Content, Resolve, Status, ImageCount, UpdateDate
            FROM E_Issue WITH(NOLOCK)
            WHERE (@FromDate IS NULL OR VisitDate >= @FromDate)
              AND (@ToDate   IS NULL OR VisitDate <= @ToDate)
              AND (@SalesmanCode='' OR SalesmanCode=@SalesmanCode)
              AND (@Status=-1 OR Status=@Status)
            ORDER BY VisitDate DESC
            """,
            new { FromDate=fromDate, ToDate=toDate, SalesmanCode=salesmanCode, Status=status },
            commandTimeout: 30);
        return rows.ToList();
    }

    public async Task<bool> UpdateIssueResolveAsync(int id, string resolve)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE E_Issue SET Resolve=@Resolve, Status=1, UpdateDate=@UpdateDate WHERE ID=@ID",
            new { Resolve=resolve, UpdateDate=DateTime.Now, ID=id });
        return n > 0;
    }

    // ── Task Management ───────────────────────────────────────────────

    public async Task<List<TaskRow>> GetTasksAsync(
        DateTime? fromDate = null, DateTime? toDate = null, bool? active = null)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<TaskRow>(
            """
            SELECT RefNbr, Title, [Desc], StartDate, EndDate, ApplyTo, [Type], Active, CreatedByID, CreatedDateTime
            FROM M_Task WITH(NOLOCK)
            WHERE (@FromDate IS NULL OR StartDate >= @FromDate)
              AND (@ToDate   IS NULL OR EndDate   <= @ToDate)
              AND (@Active   IS NULL OR Active    = @Active)
            ORDER BY CreatedDateTime DESC
            """,
            new { FromDate=fromDate, ToDate=toDate, Active=active });
        return rows.ToList();
    }

    public async Task<string> CreateTaskAsync(TaskRow task, string username)
    {
        await using var conn = Conn();
        var refNbr = $"TASK-{DateTime.Now:yyyyMMddHHmmss}";
        await conn.ExecuteAsync(
            """
            INSERT INTO M_Task (RefNbr, Title, [Desc], StartDate, EndDate, ApplyTo, [Type], Active, CreatedByID, CreatedDateTime)
            VALUES (@RefNbr, @Title, @Desc, @StartDate, @EndDate, @ApplyTo, @Type, 1, @CreatedByID, @CreatedDateTime)
            """,
            new { RefNbr=refNbr, task.Title, task.Desc, task.StartDate, task.EndDate,
                  task.ApplyTo, task.Type, CreatedByID=username, CreatedDateTime=DateTime.Now });
        return refNbr;
    }

    public async Task<bool> UpdateTaskAsync(TaskRow task)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE M_Task SET Title=@Title, [Desc]=@Desc, StartDate=@StartDate, EndDate=@EndDate, ApplyTo=@ApplyTo, [Type]=@Type, LastModifiedByID=@CreatedByID, LastModifiedDateTime=@Now WHERE RefNbr=@RefNbr",
            new { task.Title, task.Desc, task.StartDate, task.EndDate, task.ApplyTo, task.Type, task.CreatedByID, Now=DateTime.Now, task.RefNbr });
        return n > 0;
    }

    public async Task<bool> ToggleTaskActiveAsync(string refNbr, bool active)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE M_Task SET Active=@Active WHERE RefNbr=@RefNbr",
            new { Active=active, RefNbr=refNbr });
        return n > 0;
    }
}
