using HopInLine.Data;
using HopInLine.Data.Line;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Re-added last so it wins over the auto-loaded User Secrets during local debugging.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
}

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Database:Provider picks the database. SQLite needs no server, so it's the default in the
// Docker image; SQL Server suits larger or multi-instance deployments.
var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useSqlite = dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
if (useSqlite)
{
    builder.Services.AddDbContextFactory<ApplicationDbContext, SqliteDbContextFactory>(options =>
        options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? "Data Source=hopinline.db" : connectionString));
}
else if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    throw new InvalidOperationException($"Unknown Database:Provider '{dbProvider}'. Use 'Sqlite' or 'SqlServer'.");
}

// Keeps login-free browser sessions valid across restarts when keys live on a mounted volume.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddScoped<ILineRepository, SQLLineRepository>();
builder.Services.AddScoped<ILineService, LineService>();
builder.Services.AddSingleton<ILineChangeNotifier, LineChangeNotifier>();
builder.Services.AddSingleton<IActionRateLimiter, InMemoryActionRateLimiter>();
builder.Services.AddScoped<ClientContext>();
builder.Services.AddHostedService<LineAdvancementBackgroundService>();
builder.Services.AddHostedService<LineCleanupBackgroundService>();

builder.Services.AddScoped<OverlayService>();
builder.Services.AddScoped<ConfirmService>();

// Detect if HTTPS is configured
var httpsUrls = builder.Configuration["ASPNETCORE_HTTPS_PORTS"];
var aspnetcoreUrls = builder.Configuration["ASPNETCORE_URLS"];
var hasHttps = (!string.IsNullOrEmpty(httpsUrls) && httpsUrls.Contains("https")) ||
               (!string.IsNullOrEmpty(aspnetcoreUrls) && aspnetcoreUrls.Contains("https"));

// Forwarded headers are only honored from trusted proxies, otherwise every client would share
// the proxy's IP for rate limiting. Loopback is trusted by default; add your load balancer or
// ingress under ForwardedHeaders:KnownProxies (IPs) or ForwardedHeaders:KnownNetworks (CIDRs).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // For hosts like Azure App Service whose front-end IPs aren't known in advance. Only safe when
    // the app can't be reached except through that proxy. ForwardLimit stays 1, so only the
    // address the proxy itself appended is used.
    if (builder.Configuration.GetValue("ForwardedHeaders:TrustAllProxies", false))
    {
        o.KnownProxies.Clear();
        o.KnownIPNetworks.Clear();
    }

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()?.Where(s => !string.IsNullOrWhiteSpace(s)) ?? [])
    {
        o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }

    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()?.Where(s => !string.IsNullOrWhiteSpace(s)) ?? [])
    {
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// Per-IP limit on every HTTP request that reaches routing: page loads and Blazor connections.
// Static files are served before routing and are not counted. In-app actions are limited in LineService.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            }));
});

var app = builder.Build();

// UseForwardedHeaders must be the first middleware
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    if (hasHttps)
    {
        app.UseHsts();
    }
}

if (hasHttps)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

// Probes from the orchestrator shouldn't eat into a client's request budget
app.MapHealthChecks("/healthz").DisableRateLimiting();
// Serves framework assets like blazor.server.js, which publish no longer copies into wwwroot
app.MapStaticAssets().DisableRateLimiting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Convenient for a single instance. With several instances, set Database:MigrateOnStartup=false
// and apply migrations as a separate deployment step so instances don't race each other.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
	using var scope = app.Services.CreateScope();
	var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
	using var db = factory.CreateDbContext();
	db.Database.Migrate();
	if (useSqlite)
	{
		// Write-ahead logging lets readers keep going while the auto-advance sweep writes.
		db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
	}
}

app.Run();
