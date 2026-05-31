namespace BlazorReporting.Services;

/// <summary>
/// S2 — Data Masking (ISO 27001 A.9.4.1)
/// - TDV chi xem KPI cua chinh minh (khong thay nguoi khac)
/// - NPP A khong thay data NPP B
/// Ap dung tai tang hien thi UI, khong doi SP hay DB.
/// </summary>
public sealed class DataMaskingService
{
    private readonly AuthService _auth;

    public DataMaskingService(AuthService auth) => _auth = auth;

    // ── Kiem tra quyen xem theo role ─────────────────────────────────

    /// <summary>
    /// User hien tai co duoc xem data cua SalesmanID nay khong?
    /// Admin/RSM/ASM/SS -> true.  TDV -> chi xem chinh minh.
    /// </summary>
    public bool CanViewSalesman(string salesmanId)
    {
        if (IsAdminOrManager()) return true;
        return string.Equals(_auth.UserName, salesmanId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// User hien tai co duoc xem data cua DistributorCode nay khong?
    /// Admin/Manager -> true. NPP -> chi xem code cua chinh minh (luu trong Role).
    /// </summary>
    public bool CanViewDistributor(string distributorCode)
    {
        if (IsAdminOrManager()) return true;
        // Role cua NPP luu DistributorCode: "NPP_GA01016"
        if (_auth.Role.StartsWith("NPP_", StringComparison.OrdinalIgnoreCase))
            return _auth.Role[4..].Equals(distributorCode, StringComparison.OrdinalIgnoreCase);
        return true; // TDV noi bo xem duoc
    }

    /// <summary>
    /// Filter list theo SalesmanID — loai cac dong khong duoc phep.
    /// </summary>
    public IEnumerable<T> FilterBySalesman<T>(IEnumerable<T> items, Func<T, string> getSalesmanId)
    {
        if (IsAdminOrManager()) return items;
        return items.Where(i => CanViewSalesman(getSalesmanId(i)));
    }

    /// <summary>
    /// Filter list theo DistributorCode — loai NPP khac.
    /// </summary>
    public IEnumerable<T> FilterByDistributor<T>(IEnumerable<T> items, Func<T, string> getDistributorCode)
    {
        if (IsAdminOrManager()) return items;
        return items.Where(i => CanViewDistributor(getDistributorCode(i)));
    }

    /// <summary>
    /// Mask gia tri so — hien "***" thay vi so thuc neu khong co quyen.
    /// </summary>
    public string MaskValue(decimal value, string salesmanId)
        => CanViewSalesman(salesmanId) ? value.ToString("N0") : "***";

    // ── Helpers ──────────────────────────────────────────────────────

    private bool IsAdminOrManager()
    {
        var role = _auth.Role ?? "";
        return role.Equals("Admin",  StringComparison.OrdinalIgnoreCase)
            || role.Equals("RSM",    StringComparison.OrdinalIgnoreCase)
            || role.Equals("ASM",    StringComparison.OrdinalIgnoreCase)
            || role.Equals("SS",     StringComparison.OrdinalIgnoreCase)
            || role.Equals("FSM",    StringComparison.OrdinalIgnoreCase)
            || role.Equals("HQ",     StringComparison.OrdinalIgnoreCase);
    }
}
