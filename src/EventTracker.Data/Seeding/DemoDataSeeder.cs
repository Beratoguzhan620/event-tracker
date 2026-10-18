using EventTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTracker.Data.Seeding;

/// <summary>
/// Fills an empty database with events around the current date: a few from the last weeks and most in the
/// coming two months, with attendees, a couple of full events and their waiting lists.
/// </summary>
public static class DemoDataSeeder
{
    private sealed record Plan(
        string Title,
        int CategoryId,
        int DayOffset,
        int StartHour,
        int StartMinute,
        int DurationMinutes,
        string Location,
        int? Capacity,
        int Registrations,
        string Description);

    private static readonly Plan[] Plans =
    [
        new("Kadıköy kahve tadım atölyesi", 2, -24, 14, 0, 120, "Moda, Kadıköy · Kasa Kahve Atölyesi", 12, 12,
            "Üç farklı demleme yöntemiyle aynı çekirdeğin nasıl değiştiğini birlikte deneyeceğiz. Fincanlar ve çekirdekler bizden; meraklı bir damak sizden."),
        new("Frontend İstanbul buluşması #41", 4, -17, 19, 0, 150, "Levent · Workinton Kanyon", 80, 64,
            "İki kısa konuşma ve bol sohbet: tasarım sistemlerini kodla yönetmek ve erişilebilirlik testlerini CI'a almak. Konuşmaların ardından pizza ikramı var."),
        new("Sabah koşusu: Caddebostan sahil", 6, -9, 8, 0, 60, "Caddebostan Sahili · İskele önü", null, 23,
            "Tempo serbest, 5 ve 8 km iki grup halinde koşuyoruz. Koşu sonrası sahilde kahve molası."),
        new("Veri görselleştirmeye giriş", 3, -3, 18, 30, 90, "Çevrimiçi · Zoom", 150, 97,
            "Doğru grafiği seçmek, renkleri anlamlı kullanmak ve okuyucuyu yormamak üzerine pratik bir seminer. Kayıt olanlara bağlantı e-postayla gönderilir."),

        new("Seramik: ilk kupanı yap", 2, 1, 11, 0, 180, "Cihangir · Toprak Atölye", 10, 13,
            "Çark başında temel teknikler, sırlama ve fırınlama süreci. Kupalarınız fırından çıkınca iki hafta içinde teslim edilir. Önlük atölyede var."),
        new("Kitap kulübü: Tutunamayanlar, 1. bölüm", 4, 3, 19, 30, 120, "Beyoğlu · Minoa Pera", 25, 18,
            "Oğuz Atay'ın romanının ilk bölümünü konuşuyoruz. Okumanızı tamamlamış olmanız yeterli; not almak serbest."),
        new("Bulut maliyetleri: neden fatura büyüyor?", 3, 5, 19, 0, 90, "Maslak · İTÜ Arı Teknokent", 60, 41,
            "Gerçek faturalar üzerinden en sık yapılan beş hata ve bunları önleyen alışkanlıklar. Konuşmacı: altyapı ekibinden iki mühendis."),
        new("Akustik akşam: Yerel gruplar", 5, 7, 20, 30, 150, "Kadıköy · Arkaoda", 120, 88,
            "Üç yerel grup, kısa setler. Sahne sırası etkinlik günü duyurulur. Kapı açılışı 20.00."),
        new("Belgrad Ormanı doğa yürüyüşü", 6, 8, 9, 0, 240, "Sarıyer · Neşet Suyu girişi", 30, 30,
            "Yaklaşık 12 km, orta tempolu bir parkur. Rahat ayakkabı, su ve hafif bir atıştırmalık getirin. Yağmurda iptal edilir."),
        new(".NET 10 ile neler değişti?", 1, 12, 10, 0, 420, "Şişli · Lütfi Kırdar Kongre Merkezi", 400, 312,
            "Bir günlük konferans: performans iyileştirmeleri, yeni dil özellikleri, ASP.NET Core ve EF Core güncellemeleri. Öğle yemeği ve ara ikramları dahil."),
        new("Girişimciler için hukuk 101", 3, 14, 18, 30, 120, "Karaköy · Kolektif House", 40, 22,
            "Şirket kuruluşu, hisse sözleşmeleri ve fikri mülkiyet üzerine soru-cevap ağırlıklı bir seminer."),
        new("Fotoğraf yürüyüşü: Balat ve Fener", 4, 16, 10, 0, 180, "Balat · Ahrida Sinagogu önü", 20, 9,
            "Renkli sokaklarda ışığı ve kompozisyonu konuşarak yürüyoruz. Telefon kamerası da yeterli."),
        new("Ekşi maya ekmek atölyesi", 2, 20, 13, 0, 180, "Kadıköy · Fırın Mutfak", 8, 11,
            "Maya beslemekten şekillendirmeye kadar tüm süreç. Atölyeden kendi mayanızla ve bir somun ekmekle ayrılacaksınız."),
        new("Oyun geliştirici buluşması", 4, 23, 19, 0, 150, "Beşiktaş · Kolektif Akaretler", 70, 35,
            "Bağımsız oyun geliştiricileri için demo gecesi: beş ekip yapım aşamasındaki oyunlarını gösteriyor, sonra serbest sohbet."),
        new("Oda müziği konseri", 5, 28, 19, 30, 100, "Harbiye · Cemal Reşit Rey Konser Salonu", 200, 143,
            "Yaylı dörtlüden Haydn ve Borodin. Konser öncesi 19.00'da kısa bir tanıtım konuşması yapılacak."),
        new("Yapay zekâ ürünlerinde tasarım", 1, 34, 9, 30, 480, "Ataşehir · Novotel İstanbul Bosphorus", 250, 118,
            "Ürün tasarımcıları ve geliştiriciler için bir günlük konferans: arayüz kalıpları, hata durumları ve kullanıcı güveni."),
        new("Bisiklet turu: Adalar", 6, 41, 9, 0, 360, "Kabataş İskelesi · 08.30 vapuru", 25, 6,
            "Büyükada ve Heybeliada'da kolay bir tur. Bisiklet kiralama adada yapılır, ücreti katılımcıya aittir."),
        new("Yıl sonu gönüllü buluşması", 4, 52, 18, 0, 150, "Üsküdar · Nevmekan Sahil", null, 14,
            "Gönüllü projelerinin bir yılını konuşuyoruz ve gelecek yılın planını birlikte çıkarıyoruz. Herkes davetli."),
    ];

    private static readonly string[] FirstNames =
    [
        "Ayşe", "Mehmet", "Zeynep", "Ahmet", "Elif", "Mustafa", "Defne", "Can", "Ece", "Burak", "Selin", "Emre",
        "Deniz", "Kerem", "İrem", "Onur", "Melis", "Barış", "Gizem", "Tolga", "Nazlı", "Serkan", "Pınar", "Oğuz",
    ];

    private static readonly string[] LastNames =
    [
        "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Aydın", "Öztürk", "Arslan", "Doğan", "Kılıç",
        "Aslan", "Koç", "Kurt", "Özdemir", "Polat", "Erdoğan", "Güneş", "Tekin", "Ünal",
    ];

    public static async Task SeedAsync(EventTrackerDbContext context, DateTime localNow)
    {
        if (await context.Events.AnyAsync())
            return;

        var random = new Random(41);
        var today = localNow.Date;

        foreach (var plan in Plans)
        {
            var startsAt = today.AddDays(plan.DayOffset).AddHours(plan.StartHour).AddMinutes(plan.StartMinute);
            var created = startsAt.AddDays(-Math.Max(14, Math.Abs(plan.DayOffset) + 10));
            if (created > localNow) created = localNow.AddDays(-1);

            var entity = new Event
            {
                Title = plan.Title,
                Description = plan.Description,
                CategoryId = plan.CategoryId,
                StartsAt = startsAt,
                EndsAt = startsAt.AddMinutes(plan.DurationMinutes),
                Location = plan.Location,
                Capacity = plan.Capacity,
                SearchText = SearchFolding.Fold($"{plan.Title} {plan.Location} {plan.Description}"),
                CreatedAt = created,
                UpdatedAt = created,
            };

            var emails = new HashSet<string>();
            var registeredAt = created.AddHours(2);
            var latest = startsAt < localNow ? startsAt.AddHours(-1) : localNow.AddHours(-1);
            var step = (latest - registeredAt) / Math.Max(1, plan.Registrations);

            for (var i = 0; i < plan.Registrations; i++)
            {
                string first, last, email;
                do
                {
                    first = FirstNames[random.Next(FirstNames.Length)];
                    last = LastNames[random.Next(LastNames.Length)];
                    email = $"{Ascii(first)}.{Ascii(last)}{(emails.Count > 20 ? random.Next(2, 99).ToString() : "")}@example.com";
                }
                while (!emails.Add(email));

                var confirmed = plan.Capacity is null || entity.Attendees.Count(a => a.Status == AttendanceStatus.Confirmed) < plan.Capacity;
                entity.Attendees.Add(new Attendee
                {
                    FullName = $"{first} {last}",
                    Email = email,
                    Status = confirmed ? AttendanceStatus.Confirmed : AttendanceStatus.Waitlisted,
                    RegisteredAt = registeredAt + step * i,
                    AccessToken = Tokens.Create(),
                });
            }

            context.Events.Add(entity);
        }

        await context.SaveChangesAsync();
    }

    private static string Ascii(string value) => SearchFolding.Fold(value);
}
