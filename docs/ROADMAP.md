# EventTracker — Yol Haritası

Etkinlik oluşturma, takvim görünümü ve katılım (RSVP) takibi yapan etkinlik planlayıcı.

- **Önerilen repo adı:** `event-tracker`
- **Teknoloji:** ASP.NET Core MVC (.NET 10), EF Core, SQL Server, Bootstrap 5 + özel SCSS, xUnit
- **Kurallar:** `CONTRIBUTING.md` · **Tasarım:** `docs/DESIGN.md`

Her aşama ayrı bir branch ve PR olarak yapılır (`stage/<no>-<kisa-ad>`). Bir aşama ancak tüm maddeleri bitince, build/testler geçince ve ekran görüntüleri eklenince tamamlanmış sayılır. Biten maddeler `[x]` yapılır.

## Aşama 1 — Kalıcı veri ve tasarım
- [x] Bellekteki statik liste yerine EF Core + SQLite (kurulumsuz çalışsın) + migrations + seed
- [x] Model: Event (başlık, açıklama, tarih-saat, konum, kapasite, kategori, kapak görseli), Attendee
- [x] `docs/DESIGN.md` token'larıyla yeni layout; validation mesajları

## Aşama 2 — Özellikler, test, yayın
- [ ] Liste + **takvim görünümü** (ay/hafta), yaklaşan/geçmiş filtreleri, arama
- [ ] Katılım (RSVP) formu, kapasite dolunca bekleme listesi, katılımcı listesi
- [ ] .ics dosyası indir (takvime ekle)
- [ ] xUnit testleri + GitHub Actions; canlı demo; ekran görüntüleri; README (TR + EN)
