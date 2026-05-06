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

---

## 🤖 ML Forecasting Engine — How It Works

The forecasting pipeline consists of **3 coordinated services** working in sequence:

```
SalesForecastService          →  raw SSA forecast (qty + amount)
      ↓
SeasonalFactorService         →  seasonal multiplier per (Province × Product × Month)
      ↓
SalesStrategyService          →  adjusted growth rate → Import / Export / Maintain
```

---

### 1. SalesForecastService — SSA + Linear Fallback

**Primary algorithm: Singular Spectrum Analysis (ML.NET SSA)**

```
Input  : ProductTrendPoint[] — monthly (Year, Month, TotalQty, TotalAmount)
Output : ForecastResult      — labels, qty series, amount series, confidence bands
```

**Window size formula (adaptive):**
```
windowSize = max(2, min(n / 2, 12))

Where n = number of historical data points
- n = 4  → windowSize = 2
- n = 12 → windowSize = 6
- n = 24 → windowSize = 12  (capped)
```

**Confidence bands: 90%**
```
SSA outputs 3 arrays per forecast:
  Forecast[t]  — point prediction
  Lower[t]     — 90% lower bound  = max(0, lower)
  Upper[t]     — 90% upper bound
```

**Fallback: Linear Regression (when n < 4 or SSA returns NaN/Inf)**
```
Ordinary Least Squares:
  slope (s) = (n·Σxy − Σx·Σy) / (n·Σx² − (Σx)²)
  intercept (b) = (Σy − s·Σx) / n
  pred(t) = max(0, b + s·t)

Confidence bands (±20%):
  Lower[t] = pred[t] × 0.80
  Upper[t] = pred[t] × 1.20
```

**Algorithm label exposed to UI:**
```
IsSSA = true  → "ML.NET SSA (window=W, n=N)"
IsSSA = false → "Linear Regression (n=N — cần ≥4 điểm để dùng SSA)"
```

---

### 2. SeasonalFactorService — Multi-Factor Seasonal Adjustment

Computes a **seasonal multiplier** for each (Province × Product × Month) combination by multiplying independent factors:

```
SeasonalFactor = f_holiday × f_midautumn × f_summer × f_national × f_climate
```

**Holiday factor (f_holiday):**
```
Month 1–2 (Tết):
  Confectionery → ×2.8
  Condiment     → ×2.0
  Beverage      → ×1.8
  InstantFood   → ×1.5
  Others        → ×1.3

Month 12 (pre-Tết stocking):
  Confectionery → ×1.8
  Condiment     → ×1.5
  Beverage      → ×1.4
  Others        → ×1.2
```

**Mid-Autumn factor (f_midautumn):**
```
Month 8–9:
  Confectionery → ×1.8
  Others        → ×1.0
```

**Summer factor (f_summer):**
```
Month 6–8:
  Beverage    → ×1.5
  FrozenFood  → ×1.6
  PersonalCare→ ×1.1
```

**Climate factor (f_climate) by region:**
```
North (Miền Bắc):
  Month 11–2 (winter):  Beverage    → ×0.75  |  InstantFood/Condiment → ×1.15
  Month 6–8  (summer):  Household   → ×1.10

Central (Miền Trung):
  Month 9–12 (flood):   All         → ×0.82
  Month 6–8  (hot/dry): Beverage    → ×1.60

South (Miền Nam):
  Month 5–10 (rainy):   Beverage    → ×1.20  |  Others → ×0.95
  Month 11–4 (dry):     Beverage    → ×1.35  |  Others → ×1.05
```

**Province → Climate Region mapping:**
```
North   : Hà Nội, Hải Phòng, Thanh Hóa, Nghệ An, Hà Tĩnh, ...
Central : Đà Nẵng, Huế, Quảng Nam, Khánh Hòa, Lâm Đồng, ...
South   : (default) TP.HCM, Cần Thơ, ...
```

**Product → Category auto-detection (keyword matching):**
```
"bia", "rượu", "nước ngọt" ... → Beverage
"bánh", "kẹo", "mứt"       ... → Confectionery
"nước mắm", "dầu ăn"       ... → Condiment
"mì gói", "phở", "bún"     ... → InstantFood
"kem", "đông lạnh"          ... → FrozenFood
"dầu gội", "sữa tắm"       ... → PersonalCare
"bột giặt", "nước rửa chén" ... → Household
```

**Inventory signal (from purchase history):**
```
zeros_ratio = count(qty == 0) / n
  > 25% → PossibleStockout

velocity_change = (avg_last_3M - avg_prev_3M) / avg_prev_3M
  > 35% → SurgeBuying
  > 12% → Increasing
  < -35%→ SharpDecline
  < -12%→ Slowing
  else  → Stable
```

---

### 3. SalesStrategyService — Final Adjusted Growth & Action

Combines SSA forecast + seasonal factor + inventory signal into a single **AdjustedGrowthRate**, then maps to an action:

**Adjusted growth formula:**
```
rawGrowth     = (predQtyAvg - avgQty3M) / avgQty3M       ← from SSA/Linear
seasonAdj     = (SeasonalFactor - 1.0) × 0.4             ← 40% weight on seasonal
inventoryAdj  = SurgeBuying      → +0.08
                PossibleStockout → +0.10
                SharpDecline     → -0.08
                others           → 0.0

AdjustedGrowthRate = rawGrowth + seasonAdj + inventoryAdj
```

**Action thresholds:**
```
AdjustedGrowthRate > +20% → "Nhập nhiều"  (Import Strong)
AdjustedGrowthRate > + 7% → "Nhập thêm"  (Import)
AdjustedGrowthRate < -20% → "Xuất gấp"   (Export Strong)
AdjustedGrowthRate < - 7% → "Xuất bớt"   (Export)
otherwise                 → "Duy trì"    (Maintain)
```

**Confidence score:**
```
confidence = min(1.0, n/12) × ssaWeight × signalWeight

ssaWeight    = 1.0  if UsedSSA else 0.65
signalWeight = 0.9  if InventorySignal == Unknown else 1.0
```

**Parallel processing:** All (Province × Product) pairs processed concurrently via `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = max(2, CPU_cores - 1)`.

---

### Full Pipeline Summary

```
History data (Province × Product × Month)
        ↓
[SalesForecastService]
  SSA (n≥4): windowSize = max(2, min(n/2, 12)), confidence=90%
  Fallback:  OLS linear regression ± 20% bands
        ↓
[SeasonalFactorService]
  Factor = f_holiday × f_midautumn × f_summer × f_national × f_climate
  Province → Region (North/Central/South)
  Product  → Category (8 types, keyword detection)
  History  → InventorySignal (velocity analysis)
        ↓
[SalesStrategyService]
  AdjustedGrowth = rawGrowth + (SF-1)×0.4 + inventoryAdj
  Action = Import Strong / Import / Maintain / Export / Export Strong
  Confidence = f(n, SSA, signal)
        ↓
StrategyResult → TopImport / TopExport / ProvinceSummaries
```


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
