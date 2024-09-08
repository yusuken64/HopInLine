using HopInLine.Data.Line;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddScoped<LineService>();
builder.Services.AddSingleton<ParticipantFactory>();
builder.Services.AddSignalR();

builder.Services.AddSingleton<ILineRepository, InMemoryLineRepository>();
//builder.Services.AddScoped<ILineRepository, SQLLiteLineRepository>();
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<LineUpdatedNotifier>();
builder.Services.AddSingleton<LineAdvancementService>();

builder.Services.AddScoped<OverlayService>();

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

app.Run();
