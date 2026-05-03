# BlazorReporting

> **Enterprise Reporting & Analytics Platform** built with Blazor Server (.NET 8), SQL Server, and ML.NET.

A self-initiated project born from real-world experience optimizing reporting workflows for enterprise DMS/ERP clients (distribution management systems serving companies like VINATABA, Pinaco, TH Food Chain). This platform brings together fast data access, multi-format export, and ML-powered sales forecasting in a single Blazor Server application.

---

## ✨ Features

| Feature | Status | Details |
|---|---|---|
| Real-time dashboards | 🟡 In progress | Sales by date, region, employee — live Blazor components |
| Excel export | 🟡 In progress | ClosedXML — styled, multi-sheet workbooks |
| PDF export | 🟡 In progress | QuestPDF — print-ready report layouts |
| Two-tier caching | ✅ Done | MemoryCache (L1) + Redis (L2) for large dataset performance |
| SQL Server data layer | ✅ Done | Dapper + stored procedures, optimized for million-row datasets |
| ML.NET sales forecasting | 🔵 Planned | TimeSeries prediction overlaid on dashboards |
| Auth & multi-tenant | 🔵 Planned | Role-based access, per-client data isolation |

---

## 🏗 Architecture

```
[ Blazor Server Pages + Components ]
            ↕
    [ Service Layer ]
  ReportService · ForecastService · ExportService
            ↕
  [ Cache Layer ] MemoryCache → Redis
            ↕
  [ Data Layer ]  Dapper + Microsoft.Data.SqlClient → SQL Server
            ↕
  [ ML Layer ]    Microsoft.ML + ML.TimeSeries
            ↕
  [ Export ]      ClosedXML (Excel) · QuestPDF (PDF)
```

**Folder structure:**
```
BlazorReporting/
├── Pages/          # Blazor page components
├── Components/     # Reusable UI components
├── Services/       # Business logic & orchestration
├── Data/           # Dapper repositories & DB access
├── Models/         # DTOs and domain models
├── SQL/            # Stored procedures & schema scripts
├── Shared/         # Layout, NavMenu, shared components
└── wwwroot/        # Static assets
```

---

## 🛠 Tech Stack

- **Framework:** Blazor Server on .NET 8
- **Database:** Microsoft SQL Server via Dapper 2.1 + Microsoft.Data.SqlClient 5.2
- **Caching:** Microsoft.Extensions.Caching.Memory + StackExchange.Redis
- **Export:** ClosedXML 0.102 (Excel) · QuestPDF 2024.10 (PDF)
- **ML / Analytics:** Microsoft.ML 3.0 + Microsoft.ML.TimeSeries 3.0
- **Serialization:** System.Text.Json 8.0

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (local or remote)
- Redis (optional — falls back to MemoryCache if not configured)

### Setup

```bash
git clone https://github.com/vulpnet/BlazorReporting.git
cd BlazorReporting
```

Update `appsettings.json` with your connection strings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=BlazorReporting;Trusted_Connection=True;",
    "Redis": "localhost:6379"
  }
}
```

Run the SQL scripts in `/SQL` to create the required schema, then:

```bash
dotnet run
```

Navigate to `https://localhost:5001`.

---

## 💡 Motivation

After years of building and optimizing reporting pipelines for enterprise clients at **DMSpro** and **CJ Gemadept** — where generating accurate, performant sales reports was a daily challenge — this project applies those lessons in a modern, maintainable Blazor stack. The ML.NET forecasting layer is the differentiating feature: bringing predictive analytics into the reporting UI without relying on external AI services.

---

## 📄 License

MIT

---

*Built by [Ly Phuc Vu](mailto:vulp.net@gmail.com) · Ho Chi Minh City, Vietnam*
