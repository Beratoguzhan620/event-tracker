using System.Globalization;

namespace EventTracker.Web.Infrastructure;

/// <summary>Turkish date and time phrases used across the views.</summary>
public static class Format
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public static string Day(DateTime value) => value.ToString("d MMMM yyyy, dddd", Turkish);

    public static string ShortDay(DateTime value) => value.ToString("d MMM ddd", Turkish);

    public static string MonthShort(DateTime value) => value.ToString("MMM", Turkish).ToUpper(Turkish);

    public static string Time(DateTime value) => value.ToString("HH:mm", Turkish);

    /// <summary>"19:00–21:30" on one day, or "12 Kas 19:00 – 13 Kas 02:00" across days.</summary>
    public static string TimeRange(DateTime start, DateTime end) =>
        start.Date == end.Date
            ? $"{Time(start)}–{Time(end)}"
            : $"{start.ToString("d MMM HH:mm", Turkish)} – {end.ToString("d MMM HH:mm", Turkish)}";

    public static string Duration(DateTime start, DateTime end)
    {
        var span = end - start;
        if (span.TotalDays >= 1)
            return $"{Math.Ceiling(span.TotalDays):0} gün";
        var hours = (int)span.TotalHours;
        return (hours, span.Minutes) switch
        {
            (0, var m) => $"{m} dk",
            (var h, 0) => $"{h} sa",
            (var h, var m) => $"{h} sa {m} dk",
        };
    }

    /// <summary>"Bugün", "Yarın", "3 gün sonra", "2 hafta önce" style relative label.</summary>
    public static string Relative(DateTime start, DateTime end, DateTime now)
    {
        if (start <= now && now < end)
            return "Şu an sürüyor";

        var days = (start.Date - now.Date).Days;
        return days switch
        {
            0 => start > now ? "Bugün" : "Bugün sona erdi",
            1 => "Yarın",
            -1 => "Dün",
            > 1 and < 14 => $"{days} gün sonra",
            >= 14 => $"{days / 7} hafta sonra",
            < -1 and > -14 => $"{-days} gün önce",
            _ => $"{-days / 7} hafta önce",
        };
    }
}
