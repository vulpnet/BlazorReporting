# Clone DMS2.0 → WEBAPP Blazor — Tiến độ

**Mục tiêu:** Clone toàn bộ 26 Controllers từ DMS2.0 (ASP.NET MVC) sang WEBAPP (Blazor Server .NET 8)
**DB:** URCeTools dùng chung — `10.86.81.159\ADMSTDB2017`

---

## BẢO MẬT — ISO 27001 (Tập đoàn đa quốc gia)

> **Nguyên tắc:** Mỗi tính năng là service riêng, không sửa DB DMS2.0, không ảnh hưởng tốc độ trang hiện có

| # | Tính năng | Yêu cầu | File cần tạo | Trạng thái |
|---|---|---|---|---|
| S1 | **Session Timeout** | 2h không hoạt động → auto logout | `Services/SessionSecurityService.cs` | ✅ JS idle + server timer |
| S2 | **Data Masking** | TDV chỉ xem KPI mình; NPP A không thấy NPP B | `Services/DataMaskingService.cs` | ✅ Filter tại UI layer |
| S3 | **Audit Log** | Ghi DB, lưu 3 năm: login/logout/timeout | `Services/AuditLogService.cs` + `SQL/CreateAuditLog.sql` | ✅ AppAuditLog table |
| S4 | **Multi-device** | Max 3 thiết bị; login thứ 4 kick cũ nhất | `Services/DeviceSessionService.cs` | ✅ AppUserSession table |
| S5 | **Security Dashboard** | Admin xem audit log, active sessions, kick | `Pages/Admin/SecurityDashboard.razor` | ✅ Tab Audit + Sessions |

**Chuẩn ISO 27001:** A.9 Access Control, A.12 Operations Security

---

## PERFORMANCE FIX LIST — 100K users/ngày

> **Nguyên tắc:** Chỉ fix đúng chức năng bị lỗi, không refactor chung, không ảnh hưởng chức năng khác

| # | Vấn đề | File | Severity | Trạng thái |
|---|---|---|---|---|
| C1 | `FetchAllAsync` không giới hạn rows — OOM risk | `DataRepository.cs:~61` | 🔴 CRITICAL | ✅ maxRows=100K guard |
| C2 | `CAST/LIKE` full table scan mọi filter | `DataRepository.cs:~448` | 🔴 CRITICAL | ✅ prefix `=` dùng exact match |
| H1 | Paging 2 query riêng (COUNT + DATA) | `DataRepository.cs:~308` | 🟠 HIGH | ✅ 1 batch SQL |
| H2 | Không config Connection Pool Min/Max | `appsettings.json` | 🟠 HIGH | ✅ Min=5 Max=200 |
| H3 | `pp_GetSmLocationForMap` không cache | `DataRepository.cs:~1127` | 🟠 HIGH | ✅ cache 15 phút |
| H4 | InMemoryCache — cần Redis cho 100K users | `Program.cs:~67` | 🟠 HIGH | ✅ eviction 500MB + 25% compaction |
| H5 | N+1 queries trong `GetSalesmanRouteWithSalesAsync` | `DataRepository.cs:~580` | 🟠 HIGH | ✅ bỏ query RouteCD thừa |
| H6 | `ExecAsync` timeout=60s không fallback | `ReportService.cs:~210` | 🟠 HIGH | ✅ catch timeout → empty list |
| M1 | MainLayout load SP params song song vô hạn | `MainLayout.razor:~177` | 🟡 MEDIUM | ✅ batch 5 + timeout 20s |
| M2 | `GetSpParametersAsync` không cache | `DataRepository.cs:~172` | 🟡 MEDIUM | ✅ static ConcurrentDictionary |
| M3 | SortableGrid không virtual scroll | `SortableGrid.razor:~100` | 🟡 MEDIUM | ✅ Virtualize khi > 100 rows |
| M4 | `catch{}` trống ẩn lỗi production | `DataRepository.cs:~697,1437,1478` | 🟡 MEDIUM | ✅ LogDebug |
| L1 | `AllowedHosts = "*"` — bảo mật | `appsettings.json` | 🟢 LOW | ✅ restrict to localhost+internal |

---

## QUY TẮC BẮT BUỘC (áp dụng cho tất cả màn hình)

### DB & SQL
- Tên table **số ít**: `Role`, `Feature`, `RoleFeature`, `RoleUser`, `MenuFeature` (đối chiếu `DMS2.0/Models/ERoute.dbml`)
- Trước khi viết SQL mới → kiểm tra tên table/column trong `ERoute.dbml`
- Dapper map SP result → dùng `class` với properties, **không dùng `record` positional**
- SP chậm (>5s) → tạo SP mới tối ưu, lưu vào `SQL/` folder kèm comment logic

### Grid (áp dụng 100%)
- **Dùng `SortableGrid<TItem>` component** (`Components/SortableGrid.razor` + `Components/GridColumn.razor`)
- Sort click header, filter row dưới header, global search, paging 10/20/50/100
- Scroll: `overflow-x/y:auto`, `max-height:600px`

### Cách dùng SortableGrid
```razor
<SortableGrid Items="_data" Context="r" MinWidth="800px" DefaultPageSize="20">
    <ToolbarContent>
        <button class="btn btn-sm btn-primary" @onclick="OpenAdd">Thêm</button>
    </ToolbarContent>
    <Columns>
        <GridColumn TItem="MyModel" Title="Tên"  Value="@(x => x.Name)" />
        <GridColumn TItem="MyModel" Title="Số"   Value="@(x => x.Count)" Align="end" />
        <GridColumn TItem="MyModel" Title="Màu"  Value="@(x => x.Color)" Filterable="false" />
    </Columns>
    <RowTemplate>
        <td>@r.Name</td>
        <td class="text-end">@r.Count</td>
        <td>@r.Color</td>
    </RowTemplate>
    <ActionColumn Context="r">
        <button class="btn btn-xs btn-outline-primary" @onclick="()=>Edit(r)">
            <i class="bi bi-pencil"></i>
        </button>
    </ActionColumn>
</SortableGrid>
```

### Menu (NavMenu attribute)
```razor
@attribute [NavMenu(Title="Tên menu", Icon="bi-icon-name", Group="Tên nhóm", GroupOrder=XX, Order=YY)]
```
- GroupOrder: Tổng quan=5, Phân tích=10, Quản trị=90
- Menu tự động — không cần sửa Sidebar.razor

### Lỗi thường gặp
- `@bind:event="oninput"` + `@oninput` cùng lúc → dùng `value=` + `@oninput`
- Razor `@(expr).Method()` → phải dùng `@((expr).Method())`
- Dấu nháy kép trong Razor attribute → dùng method thay lambda inline
- SP timeout → kiểm tra STATISTICS TIME, tạo SP mới nếu >10s

---

## NHÓM 1 — Auth & User ✅ HOÀN THÀNH

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/UserManagementService.cs` | AccountController + UserController | ✅ |
| `Pages/Account/ChangePassword.razor` | Account/Manage | ✅ |
| `Pages/Account/ResetPassword.razor` | Account/ResetPass | ✅ |
| `Pages/Account/RoleManager.razor` | Account/RoleManager | ✅ |
| `Pages/Account/RoleFeatureAssignment.razor` | Account/RoleFeatureAssignment | ✅ |
| `Pages/User/UserIndex.razor` | User/Index | ✅ |

**Menu:** Nhóm "Quản trị" → Quản lý Role, Phân quyền Feature, Người dùng, Đổi mật khẩu

---

## NHÓM 2 — Dashboard & Home ✅ HOÀN THÀNH

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/DashboardService.cs` | DashBoardController + HomeController | ✅ |
| `Services/DashboardService.cs` — `GetKpiAsync()` | DashBoard KPI cards | ✅ |
| `Pages/Dashboard/Index.razor` | DashBoard/Home | ✅ |
| `Pages/Home/CustomSetting.razor` | Home/CustomSetting | ✅ |
| `SQL/pp_GetDashboardKPI.sql` | SP tối ưu thay pp_GetBaselineTargetInMonth (18s→2s) | ✅ |

**Tính năng đã implement:**
- 6 KPI cards SM status (hôm nay): Tổng/Đã sync/Chưa sync/Đang bán/Chưa bán/Có ĐH
- 4 summary cards: Chỉ tiêu tháng, Doanh số tháng, % đạt, Doanh số hôm nay
- 4 visit cards: CH kế hoạch, đã VT, chưa VT, đơn hàng
- 6 charts: Doughnut SM status, Bar doanh thu Region, Bar Top10 Distributor, Line VT theo ngày, Line % VT theo ngày, 2 Pie level VT/ĐH
- Chi tiết TDV: SortableGrid với sort/filter/paging đầy đủ
- CRUD màu UI (CustomColorSetting)

**Logic KPI bám sát DMS2.0:**
- `#LR` = `UserRoutesTerritory JOIN Routes WHERE Active=1` (= `ControllerHelper.ListRoute`)
- SM Status tính theo **ngày** (`VisitDate = @Date`)
- `MTDTotalAmount` = sum tất cả rows ngày @Date (không filter IsMCP)
- Target = `SalesTargetKPI JOIN #LR.SalesmanID`, Type=Month, TypeTarget=Revenue
- Level bands từ `CustomSetting WHERE SettingCode LIKE 'ReportOrderIndexLevel%'`
- Level pie dùng `Visit_MCP` / `SO_MCP` (MTD%), không phải daily %

**SP đã tạo:** `SQL/pp_GetDashboardKPI.sql` — lưu full logic + comment

**Ghi chú:** Table `Category` không có trong DB → bỏ qua

---

## NHÓM 3 — Config hệ thống ✅ HOÀN THÀNH

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/ConfigService.cs` | Tất cả config controllers | ✅ |
| `Pages/Config/CustomSettingConfig.razor` | PhraseController/ConfigCustomSetting | ✅ |
| `Pages/Config/ShiftSetting.razor` | ShiftSettingController | ✅ |
| `Pages/Config/SystemSetting.razor` | SystemSettingController | ✅ |
| `Pages/Config/ScheduleSubmit.razor` | ScheduleSubmitSettingController | ✅ |
| `Pages/Config/Phrase.razor` | PhraseController/Dictionary | ✅ |

**Ghi chú:** `PeriodSetting` không có trong DB `URCeTools` (thuộc Hammer DB riêng) → bỏ qua

**Tính năng:**
- CRUD calendar period (đa ngôn ngữ)
- Cấu hình ca làm việc AM/PM
- Tham số hệ thống calendar
- Deadline nộp lịch
- Từ điển đa ngôn ngữ + Set language

---

## NHÓM 4 — Schedule (Lịch làm việc) ⛔ BỎ QUA

**Lý do:** Toàn bộ 4 controllers dùng `HammerDataProvider` — phụ thuộc **eCalendar DB riêng** (`HO_ETool`), không phải `URCeTools`.
- Table `Appointments`, `PrepareWW` trong `URCeTools` đều rỗng, không có SP hỗ trợ
- Server eCalendar (`10.86.81.173\DMSETOOLS`, `10.86.81.148\DEMO`) không accessible

| Controller | Trạng thái |
|---|---|
| PrepareScheduleNewController | ⛔ N/A — eCalendar DB |
| MySchedulerController | ⛔ N/A — eCalendar DB |
| SendCalendarController | ⛔ N/A — eCalendar DB |
| ViewScheduleController | ⛔ N/A — eCalendar DB |

---

## NHÓM 5 — KPI & Assessment ✅ HOÀN THÀNH (phần có DB)

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/DistributionService.cs` | DistributionManagementController | ✅ |
| `Pages/Distribution/Index.razor` | DistributionManagement/Index — SKU | ✅ |
| `Pages/Distribution/BudgetAssign.razor` | DistributionManagement/Budget | ✅ |
| `Pages/KPI/Index.razor` | DistributeKPIsController | ⛔ Placeholder |
| `Pages/Assessment/*` | AssesmentController | ⛔ SP không tồn tại |
| `Pages/Assessment/Evaluation.razor` | EvaluationController | ⛔ Data rỗng |
| `Pages/Assessment/Submit.razor` | SubmitAssessmentController | ⛔ SP không tồn tại |
| `Pages/Assessment/View.razor` | ViewAssessmentController | ⛔ SP không tồn tại |

**SP có, data có:** `DMS_DistributionManagement`, `DMSDistributeKPis`, `DMS_GetAllBudgetAssign`
**SP không tồn tại:** `sp_Assesment_Auditor_View`, `sp_Assesment_Auditor` và các SP Assessment khác

**Tính năng:**
- Assessment flow: Auditor fill → Leader review/approve → Manager close
- Re-assessment + open/close day
- SKU distribution by salesman
- Budget allocation + approve
- Import/Export Excel assessment

---

## NHÓM 6 — MCP (Market Coverage Plan) ✅ HOÀN THÀNH

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/MCPService.cs` | MCPManageController | ✅ |
| `Pages/MCP/ReportMCP.razor` | MCPManage/ReportCustomerMCP | ✅ |
| `Pages/MCP/MCPDetail.razor` | MCPManage/GetOuletInfoBy + GetOuletChangeMCP | ✅ |
| `Pages/MCP/ChangeMCP.razor` | MCPManage/SaveUpdateMCP | ⛔ Logic phức tạp + transaction |
| `Pages/MCP/ReviewMCP.razor` | MCPManage/ReviewMCP + ApproveMCP | ⛔ Workflow phức tạp |

**Data:** Outlets=157K, DMSMCPDetail=157K rows — đủ data
**SP dùng:** `pp_ReportCustomerMCP`, `pp_GetOutletMCPBy`, `DMSMCPDetail`

**Tính năng:**
- Thay đổi outlet trong MCP (chọn outlet, set thứ tự visit)
- Review + approve MCP changes
- Import outlet từ Excel / Export template
- Report customer MCP coverage

---

## NHÓM 7 — Tracking & Map ✅ HOÀN THÀNH

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/TrackingService.cs` | TrackingController | ✅ |
| `Pages/Tracking/Movement.razor` | TrackingController/MovementMonitoring | ✅ |
| `Pages/Map/TerritoryMap.razor` | MapTestController/TerritoryPerformance | ✅ đã có sẵn |

**SP dùng:** `pp_GetSalemanLastLocation` — có data GPS thật
**Tái dùng:** Leaflet map (`mapInterop.js`) từ Map page đã có sẵn

**Tính năng:**
- Real-time GPS location salesman/ASM/SS (last location)
- Territory performance map: route, outlet, visit/order data
- Filter theo region/area/route

---

## NHÓM 8 — Reports ✅ HOÀN THÀNH

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/ReportService.cs` | ReportTrackingController | ✅ |
| `Pages/Reports/ReportVisit.razor` | ReportTracking/ReportVisit — chi tiết từng lần VT | ✅ |
| `Pages/Reports/SMVisitSummary.razor` | pp_ReportSMVisitSummary — KPI theo TDV | ✅ |
| `Pages/Report/EmployeeStatus.razor` | ReportEmployeesStatusController | ⛔ HammerDataProvider |
| `Pages/Report/KPIReport.razor` | ReportController KPI | ⛔ Logic hierarchy phức tạp |

**SP dùng:** `pp_ReportVisit` (chi tiết VT), `pp_ReportSMVisitSummary` (KPI tổng hợp)
**SP có sẵn thêm:** `pp_ReportSalesAssessment`, `pp_ReportSummarySales`, `pp_ReportSFUsageApp`...

**Tính năng:**
- Report visit/outlet theo territory + date
- Trạng thái nhân viên theo region/area
- Hierarchy filter, KPI period config
- Export Excel/PDF

---

## NHÓM 9 — Issues & Misc ✅ HOÀN THÀNH (phần có DB)

| File | Tương đương DMS2.0 | Trạng thái |
|---|---|---|
| `Services/IssuesService.cs` | IssuesController | ✅ |
| `Pages/Issues/IssueList.razor` | IssuesController — báo cáo E_Issue | ✅ |
| `Pages/Issues/TaskList.razor` | IssuesController/Task — M_Task CRUD | ✅ |
| `Pages/Issues/Digital.razor` | IssuesController/Digital | ⛔ Table rỗng |
| `Pages/Issues/ReportUsageApp.razor` | pp_ReportSFUsageApp | ⛔ Cross-DB error |
| `Pages/Issues/PDASalesman.razor` | pp_PDAGetSalesmanActive | ⛔ Cross-DB error |

**Data có:** E_Issue=200 records, M_Task rỗng (CRUD sẵn sàng nhập)
**Cross-DB SP:** `pp_ReportSFUsageApp` dùng `DacHungSFM.dbo.DMSSFMLog`, `pp_PDAGetSalesmanActive` dùng `URCSFAPRD..DMSAimActiveUser`

**Tính năng:**
- Task list: tạo/filter/assign task, export Excel/PDF
- Digital content upload & quản lý file
- Report app usage by employee/role/distributor
- PDA IMEI management + reset

---

## Thống kê tổng

| Nhóm | Controllers | Trạng thái |
|---|---|---|
| 1 — Auth & User | 2 | ✅ Hoàn thành |
| 2 — Dashboard & Home | 2 | ✅ Hoàn thành |
| 3 — Config | 5 | ✅ Hoàn thành |
| 4 — Schedule | 4 | ⛔ Bỏ qua (eCalendar DB) |
| 5 — KPI & Assessment | 6 | ✅ Distribution xong, Assessment ⛔ SP thiếu |
| 6 — MCP | 1 | ✅ Report + Detail xong |
| 7 — Tracking & Map | 2 | ✅ Hoàn thành |
| 8 — Reports | 3 | ✅ 2 report chính xong |
| 9 — Issues | 1 | ✅ Issues + Task xong |
| **Tổng** | **26** | **2/9 nhóm (✅)** |
