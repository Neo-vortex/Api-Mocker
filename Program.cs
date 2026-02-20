using ApiMocker;
using ApiMocker.Data;
using ApiMocker.Hubs;
using ApiMocker.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddHttpClient("proxy")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=apimocker.db"));

builder.Services.AddScoped<ProxyService>();
builder.Services.AddScoped<RouteSeederService>();
builder.Services.AddScoped<LuaScriptService>();
builder.Services.AddSingleton<RateLimiterService>();
builder.Services.AddHostedService<HealthCheckService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    var seeder = scope.ServiceProvider.GetRequiredService<RouteSeederService>();
    await seeder.SeedAsync();
}

app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<MockerMiddleware>();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapHub<RequestFeedHub>("/hubs/requestfeed");

app.Run();