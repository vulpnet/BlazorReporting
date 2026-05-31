# SQL Check Scripts — DMSPRO Reporting

**Server:** `10.86.81.159\ADMSTDB2017` | **DB:** `URCeTools`
**User:** `adms` | **Purpose:** Verify SP execution + data logic

## Files

| File | Nội dung |
|---|---|
| `check_all_reports.sql` | **Chạy tất cả 16 SP một lần** — nhanh nhất để phát hiện lỗi |
| `check_dashboard.sql` | Dashboard KPI, location map, BLSalesKPI |
| `check_reports_group1_visit.sql` | Visit, Summary Sales, Daily MTD, Sales Effective |
| `check_reports_group2_outlet.sql` | GPS Invalid, Not Visit MCP, PC by SM |
| `check_reports_group3_issues_tracking.sql` | Issues, 3G Offline, Timekeeping, User Log, Mobility |
| `check_reports_group4_mcp_distribution.sql` | MCP Report, MCP Detail, SKU Distribution, Budget |
| `check_reports_group5_admin_config.sql` | Users, Roles, Config tables, Security, Custom SPs |

## Cách dùng / How to use

```sql
-- Chay file tong de kiem tra nhanh
:r check_all_reports.sql

-- Hoac chay tung group khi debug
:r check_reports_group1_visit.sql
```

## Ket qua mong doi / Expected results

- Moi SP phai chay khong loi (ERROR line trong check_all_reports)
- Row count > 0 voi ngay co data thuc te
- % numbers phai trong khoang 0-100
- MTD values phai >= daily values

## SP Local (tao rieng, khong phai SP goc DMS2.0)

| SP Local | Thay the SP goc | Ly do |
|---|---|---|
| `pp_GetDashboardKPI` | `pp_GetBaselineTargetInMonth` | SP goc mat 18s |
| `pp_GetSmLocationForMap` | `pp_GetSalemanLastLocation` | SP goc mat 32s |
| `pp_GetSmTracking_Local` | `pp_GetVisitSMTracking` | SP goc mat 10s |
| `pp_GetDailyPlan_Local` | `pp_ReportDailyPlan` | Cross-DB AZRDMSPDB04 |
| `pp_GetTimekeeping_Local` | `pp_ReportTimekeeping` | Cross-DB NhatNhateTools |
| `pp_GetUserLog_Local` | `pp_ReportUserLog` | Cross-DB NhatNhatDMS |
| `pp_GetSummarySales_Local` | `pp_ReportSummarySales` | SP goc mat 61s, 600K rows |
| `sp_CleanAuditLog` | *(moi)* | Xoa audit log cu hon 3 nam |
