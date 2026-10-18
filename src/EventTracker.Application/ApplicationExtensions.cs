using EventTracker.Application.Categories;
using EventTracker.Application.Common;
using EventTracker.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventTracker.Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddEventTrackerServices(this IServiceCollection services, string? timeZoneId)
    {
        var zone = EventClock.FindZone(timeZoneId);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new EventClock(provider.GetRequiredService<TimeProvider>(), zone));
        services.AddScoped<EventService>();
        services.AddScoped<CategoryService>();
        return services;
    }
}
