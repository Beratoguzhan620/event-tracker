using EventTracker.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventTracker.Controllers;

public class EventsController : Controller
{
    // Bellekte tutulacak liste
    private static List<EventModel> eventList = new()
    {
        new EventModel { Id = 1, Title = "Toplantı", Description = "Proje detayları", Date = DateTime.UtcNow.AddDays(7) },
        new EventModel { Id = 2, Title = "Konser", Description = "Yaz festivali", Date = DateTime.UtcNow.AddMonths(1) }
    };

    // 1. LISTELEME 
    [HttpGet]
    public IActionResult Index()
    {
        
        return View("List", eventList);
    }

    // 2. DETAYLAR 
    [HttpGet]
    public IActionResult Details(int id)
    {
        var @event = eventList.FirstOrDefault(e => e.Id == id);
        if (@event == null) return NotFound();
        return View(@event);
    }

    // 3. YENİ EKLEME Sayfayı aç
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // 4. YENİ EKLEME Veriyi kaydet
    [HttpPost]
    public IActionResult Create(EventModel newEvent)
    {
        
        newEvent.Id = eventList.Any() ? eventList.Max(e => e.Id) + 1 : 1;

        eventList.Add(newEvent);
        return RedirectToAction("Index");
    }

    // 5. DÜZENLEME Sayfayı aç
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var @event = eventList.FirstOrDefault(e => e.Id == id);
        if (@event == null) return NotFound();
        return View(@event);
    }

    // 6. DÜZENLEME Veriyi güncelle
    [HttpPost]
    public IActionResult Edit(EventModel updatedEvent)
    {
        var oldEvent = eventList.FirstOrDefault(e => e.Id == updatedEvent.Id);
        if (oldEvent != null)
        {
            oldEvent.Title = updatedEvent.Title;
            oldEvent.Description = updatedEvent.Description;
            oldEvent.Date = updatedEvent.Date;
        }
        return RedirectToAction("Index");
    }

    // 7. SİLME 
    [HttpGet]
    public IActionResult Delete(int id)
    {
        var @event = eventList.FirstOrDefault(e => e.Id == id);
        if (@event != null)
        {
            eventList.Remove(@event);
        }
        return RedirectToAction("Index");
    }
}