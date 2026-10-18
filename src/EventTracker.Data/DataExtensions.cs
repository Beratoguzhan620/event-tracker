using EventTracker.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventTracker.Data;

public static class DataExtensions
{
    public static IServiceCollection AddEventTrackerData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EventTrackerDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }

    /// <summary>Applies pending migrations and, when asked, fills an empty database with demo events.</summary>
    public static async Task PrepareDatabaseAsync(this IServiceProvider services, bool seedDemoData, DateTime localNow)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventTrackerDbContext>();

        await context.Database.MigrateAsync();

        if (seedDemoData)
            await DemoDataSeeder.SeedAsync(context, localNow);
    }
}
