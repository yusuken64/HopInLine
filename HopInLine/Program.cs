using HopInLine.Data.Line;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ILineRepository, SQLLineRepository>();
builder.Services.AddScoped<ILineService, LineService>();
builder.Services.AddSingleton<ILineChangeNotifier, LineChangeNotifier>();
builder.Services.AddHostedService<LineAdvancementBackgroundService>();

builder.Services.AddScoped<OverlayService>();

// Detect if HTTPS is configured
var httpsUrls = builder.Configuration["ASPNETCORE_HTTPS_PORTS"];
var aspnetcoreUrls = builder.Configuration["ASPNETCORE_URLS"];
var hasHttps = (!string.IsNullOrEmpty(httpsUrls) && httpsUrls.Contains("https")) ||
               (!string.IsNullOrEmpty(aspnetcoreUrls) && aspnetcoreUrls.Contains("https"));

// Configure ForwardedHeaders middleware
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

using (var scope = app.Services.CreateScope())
{
	var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
	using var db = factory.CreateDbContext();
	db.Database.Migrate();
}

app.Run();
