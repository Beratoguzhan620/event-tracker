using System.Text;
using EventTracker.Application.Calendar;
using EventTracker.Application.Common;
using Microsoft.Extensions.Time.Testing;

namespace EventTracker.Application.Tests;

public sealed class IcsBuilderTests
{
    private static IcsEvent Sample(string description = "Kısa açıklama") => new(
        Uid: "event-7@example.com",
        Title: "Seramik: ilk kupanı yap",
        Description: description,
        Location: "Cihangir, Toprak Atölye; 2. kat",
        StartsAtUtc: new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc),
        EndsAtUtc: new DateTime(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc),
        StampUtc: new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc),
        Url: "https://example.com/events/7");

    [Fact]
    public void Writes_a_calendar_with_utc_times_and_crlf_lines()
    {
        var ics = IcsBuilder.Build(Sample());

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
        Assert.Contains("\r\nDTSTART:20261019T080000Z\r\n", ics);
        Assert.Contains("\r\nDTEND:20261019T110000Z\r\n", ics);
        Assert.Contains("\r\nUID:event-7@example.com\r\n", ics);
        Assert.EndsWith("END:VEVENT\r\nEND:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("\n", ics.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Escapes_commas_semicolons_backslashes_and_line_breaks()
    {
        var ics = IcsBuilder.Build(Sample("Satır 1\nSatır 2 \\ son"));

        Assert.Contains("LOCATION:Cihangir\\, Toprak Atölye\\; 2. kat", ics);
        Assert.Contains("DESCRIPTION:Satır 1\\nSatır 2 \\\\ son", ics);
    }

    [Fact]
    public void Folds_long_lines_at_75_bytes_without_breaking_characters()
    {
        var description = string.Concat(Enumerable.Repeat("Çağrı ışığı görünce ", 12));
        var ics = IcsBuilder.Build(Sample(description));

        foreach (var line in ics.Split("\r\n"))
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"line too long: {line}");

        var unfolded = ics.Replace("\r\n ", string.Empty);
        Assert.Contains($"DESCRIPTION:{description}", unfolded);
    }

    [Fact]
    public void Clock_converts_istanbul_times_to_utc()
    {
        var clock = new EventClock(new FakeTimeProvider(), EventClock.FindZone("Europe/Istanbul"));

        Assert.Equal(new DateTime(2026, 10, 19, 8, 0, 0), clock.ToUtc(new DateTime(2026, 10, 19, 11, 0, 0)));
    }
}
