namespace EventTracker.Web.Models;

public record PagerViewModel(int Page, int PageSize, int TotalCount, int TotalPages);
