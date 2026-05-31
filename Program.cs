using BlazorReporting.Data;
using BlazorReporting.Services;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// HTTP context for user identity
builder.Services.AddHttpContextAccessor();

// Data layer
builder.Services.AddScoped<IDataRepository, DataRepository>();

// Application services
builder.Services.AddScoped<IPivotService, PivotService>();
builder.Services.AddScoped<IUserLayoutService, UserLayoutService>();
builder.Services.AddScoped<IExportService, ExportService>();

// Auth (scoped = per Blazor circuit / session)
builder.Services.AddScoped<AuthService>();

// Localization — scoped = moi user co ngon ngu rieng
builder.Services.AddScoped<LocalizationService>();

// S1: Session timeout 2h (scoped = moi user co 1 instance rieng)
builder.Services.AddScoped<SessionSecurityService>();

// S2: Data masking KPI/NPP (scoped = biet user hien tai la ai)
builder.Services.AddScoped<DataMaskingService>();

// S3: Audit log (singleton — fire-and-forget, khong block UI)
builder.Services.AddSingleton<AuditLogService>();

// S4: Multi-device control max 3 (singleton — shared state across circuits)
builder.Services.AddSingleton<DeviceSessionService>();

// User & Role management
builder.Services.AddScoped<UserManagementService>();

// Dashboard
builder.Services.AddScoped<DashboardService>();

// Config
builder.Services.AddScoped<ConfigService>();

// Distribution
builder.Services.AddScoped<DistributionService>();

// MCP
builder.Services.AddScoped<MCPService>();

// Tracking
builder.Services.AddScoped<TrackingService>();

// Reports
builder.Services.AddScoped<ReportService>();

// Issues
builder.Services.AddScoped<IssuesService>();

// Nav menu (singleton — reflection scan once)
builder.Services.AddSingleton<NavMenuService>();

// ML.NET Sales Forecasting (singleton — MLContext is thread-safe, expensive to create)
builder.Services.AddSingleton<SalesForecastService>();
builder.Services.AddSingleton<SeasonalFactorService>();
builder.Services.AddSingleton<SalesStrategyService>();

// Chat history & Survey (scoped)
builder.Services.AddScoped<ChatHistoryService>();
builder.Services.AddScoped<SurveyService>();

// Chatbot — Ollama local
builder.Services.AddHttpClient<ChatbotService>();

// H4-FIX: MemoryCache voi compaction — bo SizeLimit vi cac entry khong set Size
// SizeLimit yeu cau moi entry phai co SetSize() -> loi runtime
// Dung ExpirationScanFrequency de don dep entry het han nhanh hon
builder.Services.AddMemoryCache(o =>
{
    o.CompactionPercentage    = 0.25;                        // giai phong 25% khi can
    o.ExpirationScanFrequency = TimeSpan.FromMinutes(1);     // quet entry het han moi 1p
});
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
// builder.Services.AddStackExchangeRedisCache(o =>
//     o.Configuration = builder.Configuration.GetConnectionString("Redis"));
// builder.Services.AddSingleton<ICacheService, RedisCacheService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
