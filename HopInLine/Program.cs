using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<LineService>();
builder.Services.AddSingleton<ParticipantFactory>();
builder.Services.AddSignalR();

builder.Services.AddScoped<ILineRepository, SQLLineRepository>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<LineUpdatedNotifier>();
builder.Services.AddSingleton<LineAdvancementService>();

builder.Services.AddScoped<OverlayService>();

builder.Configuration
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapHub<LineHub>("/linehub");
app.MapFallbackToPage("/_Host");

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
	var pendingMigrations = db.Database.GetPendingMigrations();

	if (pendingMigrations.Any())
	{
		db.Database.Migrate();
	}
}

app.Run();
