using EventTracker.Application.Categories;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventTracker.Web.Infrastructure;

/// <summary>Select lists shared by several views.</summary>
public sealed class Lookups(CategoryService categories)
{
    public async Task<List<SelectListItem>> CategoriesAsync(int? selected = null) =>
        (await categories.ListAsync())
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == selected))
            .ToList();
}
