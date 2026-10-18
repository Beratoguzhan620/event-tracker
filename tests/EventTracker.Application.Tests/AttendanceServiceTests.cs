using EventTracker.Application.Attendance;
using EventTracker.Application.Common;
using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Application.Tests;

public sealed class AttendanceServiceTests : IDisposable
{
    private readonly TestDatabase db = new();

    public void Dispose() => db.Dispose();

    [Fact]
    public async Task Gives_a_seat_while_there_is_room()
    {
        var entity = db.AddEvent(capacity: 2);

        var result = await db.Attendance().RegisterAsync(entity.Id, new("  Ayşe Yılmaz ", " Ayse@Example.com "));

        Assert.True(result.Success, result.Message);
        Assert.Equal(AttendanceStatus.Confirmed, result.Value!.Status);
        Assert.Null(result.Value.WaitlistPosition);
        var saved = await db.Fresh().Attendees.SingleAsync();
        Assert.Equal("Ayşe Yılmaz", saved.FullName);
        Assert.Equal("ayse@example.com", saved.Email);
        Assert.Matches("^[0-9a-f]{32}$", saved.AccessToken);
    }

    [Fact]
    public async Task Puts_people_on_the_waiting_list_when_full()
    {
        var entity = db.AddEvent(capacity: 1);
        db.AddAttendee(entity, "Ayşe Yılmaz");
        db.AddAttendee(entity, "Mehmet Kaya", AttendanceStatus.Waitlisted);

        var result = await db.Attendance().RegisterAsync(entity.Id, new("Elif Demir", "elif@example.com"));

        Assert.Equal(AttendanceStatus.Waitlisted, result.Value!.Status);
        Assert.Equal(2, result.Value.WaitlistPosition);
        Assert.Contains("2. sıradasınız", result.Message);
    }

    [Fact]
    public async Task Events_without_a_limit_always_confirm()
    {
        var entity = db.AddEvent(capacity: null);
        for (var i = 0; i < 5; i++)
            db.AddAttendee(entity, $"Kişi {i}");

        var result = await db.Attendance().RegisterAsync(entity.Id, new("Elif Demir", "elif@example.com"));

        Assert.Equal(AttendanceStatus.Confirmed, result.Value!.Status);
    }

    [Fact]
    public async Task The_same_email_cannot_register_twice_whatever_the_case()
    {
        var entity = db.AddEvent();
        await db.Attendance().RegisterAsync(entity.Id, new("Ayşe Yılmaz", "ayse@example.com"));

        var result = await db.Attendance().RegisterAsync(entity.Id, new("Ayşe Y.", "AYSE@example.com"));

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(nameof(RegistrationInput.Email), result.Field);
        Assert.Equal(1, await db.Fresh().Attendees.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ayse")]
    [InlineData("ayse@")]
    [InlineData("ayse@example")]
    [InlineData("Ayşe <ayse@example.com>")]
    public async Task Refuses_invalid_email_addresses(string email)
    {
        var entity = db.AddEvent();

        var result = await db.Attendance().RegisterAsync(entity.Id, new("Ayşe Yılmaz", email));

        Assert.Equal(nameof(RegistrationInput.Email), result.Field);
    }

    [Fact]
    public async Task Refuses_a_missing_name()
    {
        var entity = db.AddEvent();

        var result = await db.Attendance().RegisterAsync(entity.Id, new(" ", "ayse@example.com"));

        Assert.Equal(nameof(RegistrationInput.FullName), result.Field);
    }

    [Fact]
    public async Task Finished_events_take_no_registrations_but_running_ones_do()
    {
        var finished = db.AddEvent("Bitti", db.Now.AddHours(-3), durationHours: 2);
        var running = db.AddEvent("Sürüyor", db.Now.AddHours(-1), durationHours: 2);

        var late = await db.Attendance().RegisterAsync(finished.Id, new("Ayşe Yılmaz", "ayse@example.com"));
        var inTime = await db.Attendance().RegisterAsync(running.Id, new("Ayşe Yılmaz", "ayse@example.com"));

        Assert.Equal(ResultStatus.Invalid, late.Status);
        Assert.True(inTime.Success);
    }

    [Fact]
    public async Task Unknown_event_is_not_found()
    {
        var result = await db.Attendance().RegisterAsync(404, new("Ayşe Yılmaz", "ayse@example.com"));

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Cancelling_a_seat_gives_it_to_the_first_person_waiting()
    {
        var entity = db.AddEvent(capacity: 1);
        var seat = db.AddAttendee(entity, "Ayşe Yılmaz", minutesAgo: 300);
        var first = db.AddAttendee(entity, "Mehmet Kaya", AttendanceStatus.Waitlisted, minutesAgo: 200);
        var second = db.AddAttendee(entity, "Elif Demir", AttendanceStatus.Waitlisted, minutesAgo: 100);

        var result = await db.Attendance().CancelByTokenAsync(seat.AccessToken);

        Assert.True(result.Success, result.Message);
        var fresh = db.Fresh();
        var cancelled = (await fresh.Attendees.FindAsync(seat.Id))!;
        Assert.Equal(AttendanceStatus.Cancelled, cancelled.Status);
        Assert.Equal(db.Now, cancelled.CancelledAt);
        Assert.Equal(AttendanceStatus.Confirmed, (await fresh.Attendees.FindAsync(first.Id))!.Status);
        Assert.Equal(AttendanceStatus.Waitlisted, (await fresh.Attendees.FindAsync(second.Id))!.Status);
    }

    [Fact]
    public async Task Leaving_the_waiting_list_promotes_nobody()
    {
        var entity = db.AddEvent(capacity: 1);
        db.AddAttendee(entity, "Ayşe Yılmaz");
        var waiting = db.AddAttendee(entity, "Mehmet Kaya", AttendanceStatus.Waitlisted, minutesAgo: 50);
        var last = db.AddAttendee(entity, "Elif Demir", AttendanceStatus.Waitlisted, minutesAgo: 10);

        await db.Attendance().CancelByIdAsync(entity.Id, waiting.Id);

        Assert.Equal(AttendanceStatus.Waitlisted, (await db.Fresh().Attendees.FindAsync(last.Id))!.Status);
        Assert.Equal(1, (await db.Attendance().GetByTokenAsync(last.AccessToken))!.WaitlistPosition);
    }

    [Fact]
    public async Task A_registration_cannot_be_cancelled_twice_or_from_another_event()
    {
        var entity = db.AddEvent();
        var other = db.AddEvent("Başka");
        var attendee = db.AddAttendee(entity, "Ayşe Yılmaz");

        var wrongEvent = await db.Attendance().CancelByIdAsync(other.Id, attendee.Id);
        var first = await db.Attendance().CancelByIdAsync(entity.Id, attendee.Id);
        var again = await db.Attendance().CancelByIdAsync(entity.Id, attendee.Id);

        Assert.Equal(ResultStatus.NotFound, wrongEvent.Status);
        Assert.True(first.Success);
        Assert.Equal(ResultStatus.Invalid, again.Status);
    }

    [Fact]
    public async Task Finished_events_cannot_be_cancelled()
    {
        var entity = db.AddEvent(startsAt: db.Now.AddDays(-2));
        var attendee = db.AddAttendee(entity, "Ayşe Yılmaz", minutesAgo: 60 * 24 * 3);

        var result = await db.Attendance().CancelByTokenAsync(attendee.AccessToken);

        Assert.Equal(ResultStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task Registering_again_after_cancelling_goes_to_the_back_of_the_queue()
    {
        var entity = db.AddEvent(capacity: 1);
        var person = db.AddAttendee(entity, "Ayşe Yılmaz", minutesAgo: 300);
        db.AddAttendee(entity, "Mehmet Kaya", AttendanceStatus.Waitlisted, minutesAgo: 200);
        var oldToken = person.AccessToken;
        await db.Attendance().CancelByIdAsync(entity.Id, person.Id);

        var result = await db.Attendance().RegisterAsync(entity.Id, new("Ayşe Yılmaz", person.Email));

        Assert.True(result.Success, result.Message);
        Assert.Equal(AttendanceStatus.Waitlisted, result.Value!.Status);
        Assert.NotEqual(oldToken, result.Value.AccessToken);
        Assert.Equal(2, await db.Fresh().Attendees.CountAsync());
    }

    [Fact]
    public async Task A_personal_link_shows_the_registration()
    {
        var entity = db.AddEvent("Seramik", capacity: 1);
        db.AddAttendee(entity, "Ayşe Yılmaz");
        var registration = await db.Attendance().RegisterAsync(entity.Id, new("Elif Demir", "elif@example.com"));

        var view = await db.Attendance().GetByTokenAsync(registration.Value!.AccessToken);

        Assert.Equal("Seramik", view!.EventTitle);
        Assert.Equal(AttendanceStatus.Waitlisted, view.Status);
        Assert.Equal(1, view.WaitlistPosition);
        Assert.Null(await db.Attendance().GetByTokenAsync(new string('0', 32)));
    }

    [Fact]
    public async Task Attendee_list_splits_by_status_in_arrival_order()
    {
        var entity = db.AddEvent(capacity: 2);
        db.AddAttendee(entity, "Birinci", minutesAgo: 50);
        db.AddAttendee(entity, "İkinci", minutesAgo: 40);
        db.AddAttendee(entity, "Bekleyen", AttendanceStatus.Waitlisted, minutesAgo: 30);
        db.AddAttendee(entity, "Vazgeçen", AttendanceStatus.Cancelled, minutesAgo: 20);

        var list = await db.Attendance().ListAsync(entity.Id);

        Assert.Equal(new[] { "Birinci", "İkinci" }, list!.Confirmed.Select(a => a.FullName));
        Assert.Equal("Bekleyen", Assert.Single(list.Waitlist).FullName);
        Assert.Equal("Vazgeçen", Assert.Single(list.Cancelled).FullName);
    }
}
