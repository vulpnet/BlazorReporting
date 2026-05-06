# BlazorReporting

> **Enterprise Reporting & Analytics Platform** — Blazor Server · .NET 8 · SQL Server · ML.NET · Ollama AI

A self-initiated full-stack reporting platform built from real-world experience optimizing reporting workflows for enterprise DMS/ERP clients. The system combines real-time pivot dashboards, multi-format export, ML.NET sales forecasting, and an integrated **local AI chatbot via Ollama** — all running on-premise with no cloud dependency.

---

## ✨ What's Inside

| Module | Status | Description |
|---|---|---|
| Pivot dashboards | 🟡 In progress | Dynamic client-side pivot with user-customizable layouts |
| Excel export | 🟡 In progress | ClosedXML — styled, multi-sheet workbooks |
| PDF export | 🟡 In progress | QuestPDF — print-ready report layouts |
| ML.NET forecasting | 🟡 In progress | SSA TimeSeries + Seasonal Factor + Strategy services |
| AI Chatbot (Ollama) | 🟡 In progress | Local LLM chatbot via HTTP — no API key, no cloud |
| Caching layer | ✅ Done | MemoryCache (default) + Redis (opt-in swap) |
| Auth system | ✅ Done | Config-based user/role auth, scoped per Blazor circuit |
| Data layer | ✅ Done | Dapper + SQL Server stored procedures |

---

## 🏗 Architecture

```
[ Blazor Server Pages + Components ]
            ↕  SignalR (real-time Blazor circuit)
    [ Service Layer ]
  ├── PivotService          → dynamic pivot aggregation
  ├── UserLayoutService     → persist user-customized report layouts
  ├── ExportService         → ClosedXML (Excel) + QuestPDF (PDF)
  ├── SalesForecastService  → ML.NET SSA (singleton, thread-safe)
  ├── SeasonalFactorService → seasonal adjustment calculations
  ├── SalesStrategyService  → strategy analysis over forecast output
  ├── ChatbotService        → HTTP client → Ollama local LLM
  ├── ChatHistoryService    → conversation state (scoped per session)
  ├── SurveyService         → user feedback collection
  └── AuthService           → config-based auth (scoped per circuit)
            ↕
  [ Cache Layer ]
  ICacheService abstraction → swap MemoryCache ↔ Redis via DI
  MemoryCacheService (default, in-process)
  RedisCacheService (opt-in, uncomment in Program.cs)
            ↕
  [ Data Layer ]
  IDataRepository / DataRepository
  Dapper + Microsoft.Data.SqlClient → SQL Server
            ↕
  [ AI Layer ]
  ChatbotService → HttpClient → Ollama @ localhost:11434
  ML.NET TimeSeries (SSA) → fully local, no external API
```

**Folder structure:**
```
BlazorReporting/
├── Pages/          # Blazor page components
├── Components/     # Reusable UI components
├── Services/       # All business services (see architecture above)
├── Data/           # IDataRepository, DataRepository (Dapper)
├── Models/         # DTOs and domain models
├── SQL/            # Stored procedures & schema scripts
├── Shared/         # Layout, NavMenu
├── wwwroot/        # Static assets (CSS, JS)
├── Program.cs      # DI registration, middleware pipeline
└── appsettings.json
```

---

## 🤖 Dual AI Integration

This project integrates AI at two levels — forecasting and conversational:

### 1. ML.NET Sales Forecasting (on-premise)

Three coordinated services handle the full forecasting pipeline:

- **`SalesForecastService`** — SSA TimeSeries core, registered as `Singleton` (MLContext is expensive to create and thread-safe)
- **`SeasonalFactorService`** — computes seasonal multipliers (weekly / monthly patterns) to adjust raw predictions
- **`SalesStrategyService`** — wraps forecast output with business strategy logic (growth targets, alerts, recommendations)

```csharp
// Singleton — MLContext created once, reused across all requests
builder.Services.AddSingleton<SalesForecastService>();
builder.Services.AddSingleton<SeasonalFactorService>();
builder.Services.AddSingleton<SalesStrategyService>();
```

Why `Singleton`? MLContext initialization is heavy (~200ms). Making it singleton means all users share one trained model in memory — appropriate for read-only forecasting.

### 2. Ollama Local Chatbot (no cloud, no API key)

`ChatbotService` communicates with a locally running Ollama instance via HTTP:

```json
// appsettings.json
"Ollama": {
  "BaseUrl": "http://localhost:11434"
}
```

`ChatHistoryService` (scoped per Blazor circuit) maintains conversation state for each user session independently.

**Why Ollama?** Enterprise environments often have strict data residency requirements. Running a local LLM means no user data leaves the server — suitable for DMS/ERP reporting contexts where sales data is sensitive.

---

## ⚡ Caching Design

Cache is abstracted behind `ICacheService` — swapping between MemoryCache and Redis requires changing **one line** in `Program.cs`:

```csharp
// Default: in-process MemoryCache
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Opt-in: Redis (uncomment to swap)
// builder.Services.AddStackExchangeRedisCache(o =>
//     o.Configuration = builder.Configuration.GetConnectionString("Redis"));
// builder.Services.AddSingleton<ICacheService, RedisCacheService>();
```

**Cache config (appsettings.json):**
```json
"Caching": {
  "SlidingExpirationMinutes": 5,
  "AbsoluteExpirationMinutes": 30,
  "MaxRowsForClientPivot": 50000
}
```

`MaxRowsForClientPivot` caps the dataset sent to the client-side pivot engine — prevents browser memory issues on large reports.

---

## 📊 Pivot & Layout System

`PivotService` handles dynamic aggregation server-side before sending to the client pivot component. `UserLayoutService` persists each user's preferred report layout (columns, groupings, filters) — so the dashboard remembers their configuration across sessions.

---

## 🔐 Auth

Config-based authentication — users and roles defined in `appsettings.json`, no database dependency for auth:

```json
"Auth": {
  "Users": [
    { "Username": "admin", "Password": "...", "FullName": "Administrator", "Role": "Admin" },
    { "Username": "report", "Password": "...", "FullName": "Report User", "Role": "User" }
  ]
}
```

`AuthService` is registered as `Scoped` — isolated per Blazor circuit (per browser tab/session).

---

## 🛠 Tech Stack

| Layer | Package | Version |
|---|---|---|
| Framework | Blazor Server / .NET | 8.0 |
| Database | Dapper | 2.1.35 |
| Database | Microsoft.Data.SqlClient | 5.2.1 |
| Export | ClosedXML | 0.102.3 |
| Export | QuestPDF | 2024.10.0 |
| ML | Microsoft.ML | 3.0.1 |
| ML | Microsoft.ML.TimeSeries | 3.0.1 |
| Cache | Microsoft.Extensions.Caching.Memory | 8.0.0 |
| Cache | Microsoft.Extensions.Caching.StackExchangeRedis | 8.0.0 |
| Serialization | System.Text.Json | 8.0.4 |
| AI | Ollama (local) | any model |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (local or remote)
- [Ollama](https://ollama.ai) *(optional — for chatbot feature)*
- Redis *(optional — MemoryCache is the default)*

### Setup

```bash
git clone https://github.com/vulpnet/BlazorReporting.git
cd BlazorReporting
```

Configure `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=BlazorReporting;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=True;",
    "Redis": "localhost:6379"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434"
  },
  "Caching": {
    "SlidingExpirationMinutes": 5,
    "AbsoluteExpirationMinutes": 30,
    "MaxRowsForClientPivot": 50000
  }
}
```

Run the SQL scripts in `/SQL` to initialize the schema, then:

```bash
dotnet run
```

Navigate to `https://localhost:5001`.

### Optional: Enable Ollama chatbot

```bash
# Install Ollama from https://ollama.ai, then pull a model:
ollama pull llama3
ollama serve
```

The chatbot will be available once Ollama is running on `localhost:11434`.

### Optional: Switch to Redis cache

Uncomment the Redis lines in `Program.cs` and comment out the MemoryCache lines — no other changes needed.

---

## 🗺 Roadmap

- [x] Solution architecture & service registration (Program.cs)
- [x] SQL Server data layer (Dapper + IDataRepository)
- [x] Cache abstraction (ICacheService — MemoryCache & Redis)
- [x] Config-based auth (AuthService, scoped per circuit)
- [x] ML.NET forecasting pipeline (Forecast + Seasonal + Strategy)
- [x] Ollama chatbot integration (ChatbotService + ChatHistoryService)
- [ ] Core pivot dashboards (PivotService + client-side pivot UI)
- [ ] User layout persistence (UserLayoutService)
- [ ] Excel export (ExportService via ClosedXML)
- [ ] PDF export (ExportService via QuestPDF)
- [ ] Survey/feedback module (SurveyService)
- [ ] Role-based UI restrictions (Admin vs User)

---

## 💡 Motivation

After years of building reporting pipelines for enterprise DMS clients — where generating fast, accurate sales reports for companies like VINATABA, Pinaco, TH Food Chain was a daily operational requirement — this project consolidates those lessons into a modern, self-contained Blazor stack. The dual AI layer (ML.NET for structured forecasting + Ollama for conversational queries) reflects the direction enterprise reporting is heading: not just dashboards, but intelligent, interactive data exploration without cloud dependencies.

---

## 📄 License

MIT

---

*Built by [Ly Phuc Vu](mailto:vulp.net@gmail.com) · Ho Chi Minh City, Vietnam · [github.com/vulpnet](https://github.com/vulpnet)*
