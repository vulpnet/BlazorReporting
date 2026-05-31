using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

// ── Models ──────────────────────────────────────────────────────────

public sealed class ShiftSettingRow
{
    public string ShiftID      { get; set; } = "";
    public string StartTime    { get; set; } = "";
    public string EndTime      { get; set; } = "";
    public string DesVN        { get; set; } = "";
    public string DesEn        { get; set; } = "";
    public bool   Active       { get; set; }
    public string UserLogin    { get; set; } = "";
    public DateTime? CreatedDate { get; set; }
}

public sealed class SystemSettingRow
{
    public string ID           { get; set; } = "";   // NVarChar(10) — không phải int
    public int    Number       { get; set; }
    public string Desr         { get; set; } = "";
    public string UserLogin    { get; set; } = "";
    public DateTime? CreatedDate { get; set; }
}

public sealed class ScheduleSubmitRow
{
    public int      ID         { get; set; }
    public string   EmployeeID { get; set; } = "";
    public DateTime Date       { get; set; }
    public string   Type       { get; set; } = "";  // AM | PM
    public string   Note       { get; set; } = "";
    public int      Status     { get; set; }         // 0=mở, 1=đóng
    public string   UserLogin  { get; set; } = "";
    public int      CloseTime  { get; set; }
    public DateTime CreatedDate{ get; set; }
    public bool     IsDatabase { get; set; }
}

public sealed class PhraseRow
{
    public string PhraseCode   { get; set; } = "";
    public string PhraseText   { get; set; } = "";
    public int    LanguageID   { get; set; }
}

public sealed class LanguageRow
{
    public int    LangID   { get; set; }
    public string LangName { get; set; } = "";
    public bool   Active   { get; set; }
    public string Code     { get; set; } = "";
}

public sealed class CustomSettingRow
{
    public string SettingCode  { get; set; } = "";
    public string SettingGroup { get; set; } = "";
    public string SettingName  { get; set; } = "";
    public string SettingValue { get; set; } = "";
}

// ── Service ─────────────────────────────────────────────────────────

public sealed class ConfigService
{
    private readonly string _cs;

    public ConfigService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    // ── ShiftSetting ─────────────────────────────────────────────────

    public async Task<List<ShiftSettingRow>> GetShiftsAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<ShiftSettingRow>(
            "SELECT ShiftID, StartTime, EndTime, DesVN, DesEn, Active, UserLogin, CreatedDate FROM ShiftSetting ORDER BY ShiftID");
        return rows.ToList();
    }

    public async Task<bool> UpdateShiftAsync(ShiftSettingRow s, string username)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE ShiftSetting SET StartTime=@StartTime, EndTime=@EndTime, DesVN=@DesVN, DesEn=@DesEn, Active=@Active, UserLogin=@UserLogin, CreatedDate=@CreatedDate WHERE ShiftID=@ShiftID",
            new { s.StartTime, s.EndTime, s.DesVN, s.DesEn, s.Active, UserLogin=username, CreatedDate=DateTime.Now, s.ShiftID });
        return n > 0;
    }

    public async Task<bool> AddShiftAsync(ShiftSettingRow s, string username)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "INSERT INTO ShiftSetting (ShiftID, StartTime, EndTime, DesVN, DesEn, Active, UserLogin, CreatedDate) VALUES (@ShiftID, @StartTime, @EndTime, @DesVN, @DesEn, @Active, @UserLogin, @CreatedDate)",
            new { s.ShiftID, s.StartTime, s.EndTime, s.DesVN, s.DesEn, s.Active, UserLogin=username, CreatedDate=DateTime.Now });
        return n > 0;
    }

    // ── SystemSetting ────────────────────────────────────────────────

    public async Task<List<SystemSettingRow>> GetSystemSettingsAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<SystemSettingRow>(
            "SELECT ID, Number, Desr, UserLogin, CreatedDate FROM SystemSetting ORDER BY ID");
        return rows.ToList();
    }

    public async Task<bool> UpdateSystemSettingAsync(SystemSettingRow s, string username)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE SystemSetting SET Number=@Number, Desr=@Desr, UserLogin=@UserLogin, CreatedDate=@CreatedDate WHERE ID=@ID",
            new { s.Number, s.Desr, UserLogin = username, CreatedDate = DateTime.Now, s.ID });
        return n > 0;
    }

    public async Task<bool> AddSystemSettingAsync(SystemSettingRow s, string username)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "INSERT INTO SystemSetting (ID, Number, Desr, UserLogin, CreatedDate) VALUES (@ID, @Number, @Desr, @UserLogin, @CreatedDate)",
            new { s.ID, s.Number, s.Desr, UserLogin = username, CreatedDate = DateTime.Now });
        return n > 0;
    }

    // ── ScheduleSubmitSetting ────────────────────────────────────────

    public async Task<List<ScheduleSubmitRow>> GetScheduleSubmitsAsync(
        string employeeId = "", DateTime? fromDate = null, DateTime? toDate = null, string type = "")
    {
        await using var conn = Conn();
        var sql = """
            SELECT ID, EmployeeID, [Date], [Type], [Note], [Status], UserLogin, CloseTime, CreatedDate, IsDatabase
            FROM ScheduleSubmitSetting
            WHERE (@EmployeeID='' OR EmployeeID=@EmployeeID)
              AND (@FromDate IS NULL OR [Date] >= @FromDate)
              AND (@ToDate   IS NULL OR [Date] <= @ToDate)
              AND (@Type=''  OR [Type]=@Type)
            ORDER BY [Date] DESC, EmployeeID
            """;
        var rows = await conn.QueryAsync<ScheduleSubmitRow>(sql,
            new { EmployeeID=employeeId, FromDate=fromDate, ToDate=toDate, Type=type });
        return rows.ToList();
    }

    public async Task<bool> OpenScheduleAsync(ScheduleSubmitRow s, string username)
    {
        await using var conn = Conn();
        var exists = await conn.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM ScheduleSubmitSetting WHERE EmployeeID=@EmployeeID AND [Date]=@Date AND [Type]=@Type",
            new { s.EmployeeID, s.Date, s.Type });
        if (exists > 0) return false; // đã tồn tại

        await conn.ExecuteAsync(
            "INSERT INTO ScheduleSubmitSetting (EmployeeID, [Date], [Type], [Note], [Status], UserLogin, CloseTime, CreatedDate, IsDatabase) VALUES (@EmployeeID, @Date, @Type, @Note, 0, @UserLogin, @CloseTime, @CreatedDate, 1)",
            new { s.EmployeeID, s.Date, s.Type, s.Note, UserLogin=username, s.CloseTime, CreatedDate=DateTime.Now });
        return true;
    }

    public async Task<bool> CloseScheduleAsync(int id, string username)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE ScheduleSubmitSetting SET Status=1, UserLogin=@UserLogin, CreatedDate=@CreatedDate WHERE ID=@ID",
            new { UserLogin=username, CreatedDate=DateTime.Now, ID=id });
        return n > 0;
    }

    public async Task<bool> DeleteScheduleAsync(int id)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync("DELETE FROM ScheduleSubmitSetting WHERE ID=@ID", new { ID=id });
        return n > 0;
    }

    // ── Phrase (từ điển đa ngôn ngữ) ────────────────────────────────

    public async Task<List<LanguageRow>> GetLanguagesAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<LanguageRow>(
            "SELECT LangID, LangName, Active, Code FROM Language WHERE Active=1 ORDER BY LangID");
        return rows.ToList();
    }

    public async Task<List<PhraseRow>> SearchPhrasesAsync(string search, int? langId = null)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<PhraseRow>(
            """
            SELECT PhraseCode, PhraseText, LanguageID
            FROM Phrase
            WHERE (@Search='' OR PhraseText LIKE '%'+@Search+'%' OR PhraseCode LIKE '%'+@Search+'%')
              AND (@LangID IS NULL OR LanguageID=@LangID)
            ORDER BY PhraseCode
            """,
            new { Search=search ?? "", LangID=langId });
        return rows.ToList();
    }

    public async Task<bool> UpdatePhraseAsync(string code, int langId, string text)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE Phrase SET PhraseText=@PhraseText WHERE PhraseCode=@PhraseCode AND LanguageID=@LanguageID",
            new { PhraseText=text, PhraseCode=code, LanguageID=langId });
        return n > 0;
    }

    // ── CustomSetting (cấu hình hệ thống) ───────────────────────────

    public async Task<List<CustomSettingRow>> GetCustomSettingsAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<CustomSettingRow>(
            "SELECT SettingCode, SettingGroup, SettingName, SettingValue FROM CustomSetting ORDER BY SettingGroup, SettingCode");
        return rows.ToList();
    }

    public async Task<bool> UpdateCustomSettingAsync(string code, string group, string value)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE CustomSetting SET SettingValue=@Value WHERE SettingCode=@Code AND SettingGroup=@Group",
            new { Value=value, Code=code, Group=group });
        return n > 0;
    }
}
