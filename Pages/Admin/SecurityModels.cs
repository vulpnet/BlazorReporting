namespace BlazorReporting.Pages.Admin;

public sealed class AuditLogRow
{
    public DateTime EventTime { get; set; }
    public string  UserName   { get; set; } = "";
    public string  EventType  { get; set; } = "";
    public string? Page       { get; set; }
    public string? Detail     { get; set; }
    public string? IpAddress  { get; set; }
    public bool    IsSuccess  { get; set; }
}

public sealed class SessionRow
{
    public string   UserName   { get; set; } = "";
    public string   SessionID  { get; set; } = "";
    public string?  DeviceInfo { get; set; }
    public string?  IpAddress  { get; set; }
    public DateTime LoginTime  { get; set; }
    public DateTime LastActive { get; set; }
}
