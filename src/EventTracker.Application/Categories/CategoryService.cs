using EventTracker.Data;
using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Categories;

public sealed record CategoryItem(int Id, string Name, string Slug, string Icon, int UpcomingCount);

public sealed class CategoryService(EventTrackerDbContext db, Common.EventClock clock)
{
    public async Task<List<CategoryItem>> ListAsync()
    {
        var now = clock.Now;
        return await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CategoryItem(c.Id, c.Name, c.Slug, c.Icon, c.Events.Count(e => e.EndsAt >= now)))
            .ToListAsync();
    }

    public Task<Category?> FindBySlugAsync(string slug) =>
        db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug);
}
