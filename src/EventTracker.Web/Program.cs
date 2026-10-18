using System.Globalization;
using EventTracker.Application;
using EventTracker.Application.Common;
using EventTracker.Data;
using EventTracker.Web.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST form is checked for an antiforgery token.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustBeANumberAccessor(field => $"{field} bir sayı olmalı.");
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"\"{value}\" {field} için geçerli değil.");
    messages.SetValueIsInvalidAccessor(value => $"\"{value}\" geçerli bir değer değil.");
    messages.SetValueMustNotBeNullAccessor(_ => "Bu alan zorunlu.");
    messages.SetMissingBindRequiredValueAccessor(field => $"{field} zorunlu.");
    messages.SetUnknownValueIsInvalidAccessor(field => $"{field} için girilen değer geçerli değil.");
});

// SQLite needs no server: the database is a single file, created on first start.
var databasePath = Path.GetFullPath(
    builder.Configuration["Database:Path"] ?? "App_Data/eventtracker.db",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();

builder.Services.AddEventTrackerData(connectionString);
builder.Services.AddEventTrackerServices(builder.Configuration["Events:TimeZone"]);
builder.Services.AddScoped<CoverStorage>();
builder.Services.AddScoped<Lookups>();

// Keys that protect antiforgery tokens survive restarts (and container rebuilds when the folder is a volume).
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("EventTracker");

var app = builder.Build();

var clock = app.Services.GetRequiredService<EventClock>();
await app.Services.PrepareDatabaseAsync(app.Configuration.GetValue<bool>("Seed:DemoContent"), clock.Now);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

var turkish = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish],
});

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

// Covers uploaded at runtime are not part of the build-time static asset manifest.
app.UseStaticFiles();
app.UseRouting();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Events}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
