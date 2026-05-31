# DMSPRO Reporting — Hướng dẫn sử dụng / User Guide

> **Phiên bản / Version:** 1.0 | **Cập nhật / Updated:** 2026-05-30
> **Ngôn ngữ / Language:** Tiếng Việt · English

---

## Mục lục / Table of Contents

1. [Đăng nhập / Login](#1-đăng-nhập--login)
2. [Dashboard](#2-dashboard)
3. [Báo cáo / Reports](#3-báo-cáo--reports)
4. [Bản đồ / Map](#4-bản-đồ--map)
5. [MCP](#5-mcp)
6. [Phân phối / Distribution](#6-phân-phối--distribution)
7. [Quản trị / Administration](#7-quản-trị--administration)
8. [Trợ lý AI / AI Assistant](#8-trợ-lý-ai--ai-assistant)
9. [Giải thích chỉ số / KPI Glossary](#9-giải-thích-chỉ-số--kpi-glossary)
10. [Tình huống thường gặp / Common Scenarios](#10-tình-huống-thường-gặp--common-scenarios)
11. [FAQ](#11-faq)

---

## 1. Đăng nhập / Login

**VI:** Truy cập hệ thống bằng tài khoản DMS được cấp. Sau khi đăng nhập thành công, hệ thống tự động chào và mở cửa sổ AI Assistant.

**EN:** Access the system with your assigned DMS account. After successful login, the system automatically greets you and opens the AI Assistant window.

| Trường / Field | Mô tả / Description |
|---|---|
| Tên đăng nhập / Username | Mã nhân viên DMS / DMS employee code |
| Mật khẩu / Password | Mật khẩu được cấp hoặc tự đặt / Assigned or self-set password |

> ⚠️ **Lưu ý:** Phiên làm việc tự động đăng xuất sau **2 giờ** không hoạt động.
> ⚠️ **Note:** Session auto-logs out after **2 hours** of inactivity.

---

## 2. Dashboard

**VI:** Trang tổng quan hiển thị KPI toàn hệ thống trong ngày.
**EN:** Overview page showing system-wide KPIs for the current day.

### Các thẻ KPI / KPI Cards

| Thẻ / Card | Ý nghĩa / Meaning |
|---|---|
| **Tổng Nhân viên BH** | Tổng số TDV có lịch bán hàng hôm nay / Total salesmen scheduled today |
| **Đã đồng bộ** | TDV đã gửi dữ liệu về hệ thống / Salesmen who synced data |
| **Chưa đồng bộ** | TDV chưa có tín hiệu sync / Salesmen with no sync signal |
| **Đang bán hàng** | TDV đã viếng thăm ít nhất 1 cửa hàng / Salesmen visited ≥1 outlet |
| **Chưa bán hàng** | Đã sync nhưng chưa VT / Synced but not yet visiting |
| **Đã có đơn hàng** | TDV có ít nhất 1 đơn hàng / Salesmen with ≥1 order |

### Các chỉ số tổng hợp / Summary Metrics

| Chỉ số / Metric | Ý nghĩa / Meaning |
|---|---|
| **Chỉ tiêu doanh số tháng** | Target doanh thu tháng theo SalesTargetKPI / Monthly revenue target |
| **Doanh số trong tháng** | Doanh số lũy kế từ đầu tháng (MTD) / Month-to-date actual sales |
| **% Đạt chỉ tiêu** | Doanh số MTD / Chỉ tiêu × 100 / Achieved % vs target |
| **Doanh số hôm nay** | Doanh thu phát sinh trong ngày / Today's revenue |
| **CH Kế hoạch** | Tổng cửa hàng cần viếng thăm (IsMCP=1) / Planned outlets to visit |
| **CH Đã viếng thăm** | Số cửa hàng đã được viếng thăm / Outlets actually visited |
| **CH Chưa thăm** | Kế hoạch - Đã thăm / Remaining planned outlets |
| **Đơn hàng** | Tổng số đơn hàng trong ngày / Total orders today |

### Biểu đồ / Charts

- **Doughnut — Trạng thái Nhân viên BH:** Phân bố sync/chưa sync/đang bán
- **Bar — Doanh thu theo khu vực:** Top Region theo doanh số MTD
- **Bar — Top 10 NPP:** Top NPP theo doanh số MTD
- **Line — Viếng thăm & Đơn hàng theo ngày:** Xu hướng trong tháng
- **Pie — Mức độ VT/ĐH của TDV:** Phân bố % hoàn thành theo dải (0-20%, 20-40%...)

---

## 3. Báo cáo / Reports

### 3.1 Báo cáo viếng thăm / Visit Report

**VI:** Chi tiết từng lần viếng thăm cửa hàng trong ngày.
**EN:** Detailed record of each outlet visit in a day.

**Cách sử dụng / How to use:**
1. Chọn ngày / Select date
2. Tùy chọn lọc theo Route hoặc Giám sát / Optionally filter by Route or Supervisor
3. Nhấn **Xem** / Click **View**

**Các cột / Columns:**

| Cột / Column | Ý nghĩa / Meaning |
|---|---|
| Nhân viên BH | Tên TDV / Salesman name |
| Giám sát | Tên SS quản lý / Supervising SS |
| Tuyến | Mã tuyến đường / Route code |
| NPP | Nhà phân phối / Distributor |
| Giờ vào/ra | Thời gian bắt đầu/kết thúc viếng thăm / Visit start/end time |
| Thời gian (ph) | Số phút tại cửa hàng / Minutes spent at outlet |
| Km | Khoảng cách di chuyển / Distance traveled |
| Đơn hàng | Số đơn phát sinh / Orders placed |
| Doanh số | Doanh thu chuyến VT này / Revenue this visit |
| MTD Doanh số | Doanh số lũy kế tháng / Month-to-date sales |
| MCP | Đúng tuyến / Ngoài tuyến — In-route / Off-route |

---

### 3.2 Tổng hợp KPI / SM Visit Summary

**VI:** KPI tổng hợp theo TDV trong khoảng thời gian.
**EN:** Aggregated KPIs per salesman over a date range.

| Cột / Column | Ý nghĩa / Meaning |
|---|---|
| CH Kế hoạch | OutletMustVisit — Số CH cần thăm theo MCP |
| Đã viếng thăm | OutletVisited — Số CH đã thực tế thăm |
| % Viếng thăm | VisitMCP = Visited/Plan × 100 |
| Đơn hàng | OrderCount — Số đơn hàng |
| % Có đơn | SOMCP = Orders/Plan × 100 |
| LPPC | Line Per Purchase Call — SKU bình quân/đơn |
| Doanh số | TotalAmount — Doanh thu ngày |
| Doanh số MTD | MTDTotalAmount — Doanh thu lũy kế tháng |

---

### 3.3 Tổng hợp doanh số / Summary Sales

**VI:** Tổng hợp doanh số theo TDV/Route/NPP trong khoảng thời gian.
**EN:** Sales summary by salesman/route/distributor over a period.

> 💡 **Mẹo:** Lọc theo Route để so sánh hiệu quả giữa các tuyến.
> 💡 **Tip:** Filter by Route to compare performance across routes.

---

### 3.4 Doanh số ngày MTD / Sales Daily MTD

**VI:** Doanh số từng ngày kèm giá trị lũy kế tháng (MTD).
**EN:** Daily sales with month-to-date cumulative values.

---

### 3.5 Bán hàng hiệu quả / Sales Effectiveness

**VI:** Đánh giá hiệu quả bán hàng: % viếng thăm, % có đơn, LPPC.
**EN:** Sales effectiveness metrics: visit rate, order rate, LPPC.

---

### 3.6 PC theo nhân viên / PC by Salesman

**VI:** Số lượng sản phẩm PC đạt/không đạt theo TDV.
**EN:** Product Confirmation pass/fail counts per salesman.

| Cột / Column | Ý nghĩa / Meaning |
|---|---|
| PC Đạt | PCPass — Số SP đạt tiêu chuẩn trưng bày |
| PC Không đạt | PCNotPass — Số SP chưa đạt |
| % PC MCP | Tỷ lệ đạt trong tuyến MCP |
| MTD PC Đạt | Lũy kế tháng |

---

### 3.7 Outlet GPS không hợp lệ / Invalid GPS Outlets

**VI:** Danh sách cửa hàng có tọa độ GPS sai hoặc bất thường.
**EN:** List of outlets with incorrect or anomalous GPS coordinates.

---

### 3.8 CH chưa viếng thăm MCP / Unvisited MCP Outlets

**VI:** Cửa hàng có trong kế hoạch MCP nhưng chưa được thăm.
**EN:** Outlets scheduled in MCP but not yet visited.

---

### 3.9 Các báo cáo khác / Other Reports

| Báo cáo / Report | Mục đích / Purpose |
|---|---|
| Lý do viếng thăm | Phân tích lý do không mua hàng / Visit rejection reasons |
| 3G Offline | TDV không có kết nối 3G trong ngày / Salesmen without 3G |
| Hình ảnh trưng bày | Ảnh trưng bày sản phẩm theo chương trình / Display images by program |
| Chấm công | Số ngày làm việc/đồng bộ/có doanh số / Attendance tracking |
| Nhật ký người dùng | Lịch sử thao tác trên hệ thống / User action log |
| TDV dùng App | Thống kê sử dụng app mobile / Mobile app usage stats |
| Báo cáo Issues | Vấn đề ghi nhận tại cửa hàng / Issues recorded at outlets |

---

## 4. Bản đồ / Map

### Territory Performance Map

**VI:** Bản đồ hiển thị vị trí TDV và lộ trình bán hàng theo thời gian thực.
**EN:** Map showing salesman locations and sales routes in real time.

**Cách sử dụng / How to use:**
1. Chọn ngày / Select date
2. Tùy chọn lọc theo Giám sát hoặc mã TDV / Optionally filter by SS or salesman ID
3. Click marker trên bản đồ để xem lộ trình chi tiết / Click map marker to view route detail

**Chú thích màu / Color legend:**
- 🟢 **Xanh:** Đã đồng bộ / Synced
- ⚪ **Xám:** Chưa đồng bộ / Not synced

---

## 5. MCP

### 5.1 Báo cáo MCP / MCP Report

**VI:** Coverage rate của từng tuyến — số CH trong tuyến và số CH đã được phủ.
**EN:** Coverage rate per route — outlets in route vs covered outlets.

| Cột / Column | Ý nghĩa / Meaning |
|---|---|
| CH Trong tuyến | CusomerInRoute — Tổng cửa hàng trong MCP |
| CH Đã phủ | Covered — Số CH đã được viếng thăm |
| % Coverage | Covered / InRoute × 100 |

### 5.2 Chi tiết MCP / MCP Detail

**VI:** Lịch viếng thăm từng ngày (T2-CN) và tần suất của từng cửa hàng trong tuyến.
**EN:** Daily visit schedule (Mon-Sun) and frequency per outlet in a route.

---

## 6. Phân phối / Distribution

### 6.1 Phân phối SKU / SKU Distribution

**VI:** Phân bổ SKU cho TDV theo NPP và ngày.
**EN:** SKU allocation to salesmen by distributor and date.

### 6.2 Phân bổ Budget / Budget Assignment

**VI:** Giao chỉ tiêu ngân sách cho từng TDV.
**EN:** Budget target assignment per salesman.

---

## 7. Quản trị / Administration

### 7.1 Quản lý người dùng / User Management

**VI:** Tạo, sửa, kích hoạt/vô hiệu hóa tài khoản người dùng.
**EN:** Create, edit, activate/deactivate user accounts.

### 7.2 Quản lý Role / Role Management

**VI:** Tạo và cấu hình các nhóm quyền hệ thống.
**EN:** Create and configure system permission groups.

### 7.3 Phân quyền Feature / Feature Assignment

**VI:** Gán tính năng cho từng Role.
**EN:** Assign features/screens to each role.

### 7.4 Bảo mật hệ thống / Security Dashboard *(Admin only)*

**VI:** Xem log hoạt động và quản lý phiên đăng nhập đang hoạt động.
**EN:** View activity logs and manage active login sessions.

| Tab | Nội dung / Content |
|---|---|
| Audit Log | Lịch sử login/logout/timeout/kick session |
| Active Sessions | Danh sách phiên đang hoạt động, có thể kick từ xa |

---

## 8. Trợ lý AI / AI Assistant

**VI:** Nhấn nút chat ở góc dưới phải để mở DMSPro AI.
**EN:** Click the chat button at the bottom right to open DMSPro AI.

### Các lệnh nhanh / Quick Commands

| Bạn nói / Say | Kết quả / Result |
|---|---|
| "Mở báo cáo viếng thăm" | Mở trang /report/visit ngay |
| "Xem doanh số hôm nay" | Mở trang doanh số MTD |
| "Có báo cáo nào?" | Liệt kê tất cả báo cáo |
| "Bản đồ hôm nay" | Mở territory map ngày hiện tại |
| "Giải thích LPPC là gì" | Bot giải thích chỉ số |
| "Tóm tắt dữ liệu" | Bot phân tích báo cáo đang mở |

> 💡 **Lưu ý:** AI cần Ollama chạy local. Nếu hiển thị "Offline", liên hệ IT để kiểm tra.
> 💡 **Note:** AI requires local Ollama to be running. If showing "Offline", contact IT.

---

## 9. Giải thích chỉ số / KPI Glossary

| Chỉ số / KPI | Tiếng Việt | English | Công thức / Formula |
|---|---|---|---|
| **VT / Visit** | Viếng thăm | Visit | Số lần TDV đến cửa hàng |
| **MCP** | Lộ trình bán hàng | Market Coverage Plan | Kế hoạch viếng thăm theo tuyến |
| **% VT MCP** | % Viếng thăm trong tuyến | Visit Rate in-route | Đã VT / Kế hoạch × 100 |
| **SO / SOMCP** | % Có đơn hàng | Sales Order Rate | Đơn hàng / Kế hoạch × 100 |
| **LPPC** | SKU bình quân/đơn | Lines Per Purchase Call | Tổng SKU / Số đơn hàng |
| **MTD** | Lũy kế tháng | Month-To-Date | Giá trị từ đầu tháng đến ngày hiện tại |
| **NPP** | Nhà phân phối | Distributor | Đại lý phân phối sản phẩm |
| **TDV / Nhân viên BH** | Nhân viên bán hàng | Salesman / Sales Rep | |
| **SS / Giám sát** | Giám sát bán hàng | Sales Supervisor | |
| **ASM** | Quản lý bán hàng khu vực | Area Sales Manager | |
| **RSM** | Quản lý bán hàng vùng | Regional Sales Manager | |
| **PC** | Xác nhận sản phẩm | Product Confirmation | Tiêu chuẩn trưng bày đạt/không đạt |
| **Sync** | Đồng bộ dữ liệu | Data Synchronization | Dữ liệu từ app mobile về server |

---

## 10. Tình huống thường gặp / Common Scenarios

### 🎯 Tình huống 1: Kiểm tra hiệu suất TDV hôm nay
**Bước:** Dashboard → Xem thẻ "Đã đồng bộ" và "Đang bán hàng" → Chi tiết TDV
**Use Case:** Dashboard → Check "Synced" and "Selling" cards → Detail table

### 🎯 Tình huống 2: Phân tích vùng nào doanh số thấp
**Bước:** Báo cáo → Tổng hợp doanh số → Lọc theo Region/Area → Sort theo "% VT"
**Use Case:** Reports → Summary Sales → Filter by Region/Area → Sort by "% Visit"

### 🎯 Tình huống 3: Tìm TDV chưa sync trong ngày
**Bước:** Dashboard → Xem badge "Chưa đồng bộ" → Click → Filter grid
**Use Case:** Dashboard → Check "Not Synced" badge → Filter detail grid

### 🎯 Tình huống 4: Kiểm tra coverage MCP của tuyến
**Bước:** MCP → Báo cáo MCP → Filter theo NPP → Xem % Coverage
**Use Case:** MCP → Report → Filter by Distributor → View % Coverage

### 🎯 Tình huống 5: Xem lộ trình TDV cụ thể
**Bước:** Bản đồ → Chọn ngày → Nhập mã TDV → Click marker → Xem route
**Use Case:** Map → Select date → Enter salesman ID → Click marker → View route

---

## 11. FAQ

**Q: Sao dữ liệu hôm nay chưa cập nhật?**
A: Dữ liệu được sync từ app mobile. Kiểm tra TDV có kết nối mạng và đã mở app chưa. Thường có độ trễ 5-15 phút.

**Q: Why is today's data not updated?**
A: Data syncs from the mobile app. Check if the salesman has internet and has opened the app. Usually 5-15 minute delay.

---

**Q: Báo cáo báo lỗi "Không có dữ liệu"?**
A: Kiểm tra bộ lọc ngày — có thể chọn ngày không có dữ liệu. Thử ngày gần nhất có dữ liệu.

**Q: Report shows "No data"?**
A: Check the date filter — you may have selected a date with no data. Try the most recent date with data.

---

**Q: AI Assistant hiển thị "Offline"?**
A: Ollama chưa chạy trên máy chủ. Liên hệ IT để khởi động lại service Ollama.

**Q: AI Assistant shows "Offline"?**
A: Ollama is not running on the server. Contact IT to restart the Ollama service.

---

**Q: Không thể đăng nhập dù nhập đúng mật khẩu?**
A: Tài khoản có thể bị vô hiệu hóa. Liên hệ quản trị viên để kiểm tra trạng thái tài khoản.

**Q: Cannot login even with correct password?**
A: Account may be deactivated. Contact admin to check account status.

---

**Q: Phiên làm việc tự động đăng xuất?**
A: Hệ thống tự đăng xuất sau 2 giờ không hoạt động (ISO 27001). Đăng nhập lại bình thường.

**Q: Session auto logged out?**
A: System auto-logs out after 2 hours of inactivity (ISO 27001 compliance). Simply log in again.

---

*© 2026 DMSPRO Reporting | Phiên bản 1.0 | Hỗ trợ: IT Department*
