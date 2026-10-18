using System.Text;

namespace EventTracker.Application.Calendar;

public sealed record IcsEvent(
    string Uid,
    string Title,
    string Description,
    string Location,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime StampUtc,
    string? Url);

/// <summary>Writes a single-event iCalendar file (RFC 5545) that calendar apps can import.</summary>
public static class IcsBuilder
{
    public static string Build(IcsEvent item)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//EventTracker//TR",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{item.Uid}",
            $"DTSTAMP:{Utc(item.StampUtc)}",
            $"DTSTART:{Utc(item.StartsAtUtc)}",
            $"DTEND:{Utc(item.EndsAtUtc)}",
            $"SUMMARY:{Escape(item.Title)}",
            $"LOCATION:{Escape(item.Location)}",
            $"DESCRIPTION:{Escape(item.Description)}",
        };
        if (item.Url is not null)
            lines.Add($"URL:{item.Url}");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        var builder = new StringBuilder();
        foreach (var line in lines)
            Fold(builder, line);
        return builder.ToString();
    }

    internal static string Utc(DateTime value) => value.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Escapes text values: backslash, semicolon, comma and line breaks.</summary>
    internal static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");

    /// <summary>Lines longer than 75 octets continue on the next line after a space, without splitting a character.</summary>
    internal static void Fold(StringBuilder builder, string line)
    {
        var limit = 75;
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                builder.Append("\r\n ");
                octets = 1;
                limit = 75;
            }
            builder.Append(rune.ToString());
            octets += size;
        }
        builder.Append("\r\n");
    }
}
