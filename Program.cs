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

// Caching — swap to Redis by uncommenting below
builder.Services.AddMemoryCache();
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
