// Visual smoke test: visits the main screens with the demo data, walks through creating, editing and
// deleting an event, saves desktop and 360px-wide screenshots and fails when a page is wider than the viewport.
import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";

const baseUrl = process.env.BASE_URL ?? "http://localhost:5080";
const out = new URL("./output/", import.meta.url).pathname;

const viewports = {
  desktop: { viewport: { width: 1440, height: 900 } },
  mobile: { viewport: { width: 360, height: 780 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
};

await mkdir(out, { recursive: true });
const browser = await chromium.launch();
const problems = [];

async function shot(page, name) {
  await page.waitForLoadState("networkidle").catch(() => {});
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  if (overflow > 1) problems.push(`${name}: page is ${overflow}px wider than the viewport`);
  await page.screenshot({ path: `${out}${name}.png`, fullPage: true });
  console.log(`saved ${name}.png`);
}

function expect(condition, message) {
  if (!condition) problems.push(message);
}

const submit = (page) => Promise.all([page.waitForNavigation(), page.click("form.form-card button[type=submit]")]);

for (const [device, options] of Object.entries(viewports)) {
  const context = await browser.newContext({ ...options, locale: "tr-TR" });
  const page = await context.newPage();

  await page.goto(`${baseUrl}/`);
  await shot(page, `${device}-events`);
  expect((await page.locator(".event-row").count()) > 5, `${device}: expected upcoming events on the home page`);

  await page.goto(`${baseUrl}/?scope=gecmis`);
  await shot(page, `${device}-events-past`);

  await page.goto(`${baseUrl}/?q=istanbul`);
  await shot(page, `${device}-events-search`);
  expect((await page.locator(".event-row").count()) >= 1, `${device}: searching "istanbul" should match "İstanbul"`);

  await page.goto(`${baseUrl}/?q=zzzz`);
  await shot(page, `${device}-events-empty`);

  await page.goto(`${baseUrl}/?category=atolye`);
  await shot(page, `${device}-events-category`);

  await page.locator(".event-title").first().click();
  await shot(page, `${device}-event`);

  await page.goto(`${baseUrl}/events/new`);
  await shot(page, `${device}-event-new`);

  if (device === "desktop") {
    // Empty form shows field errors on the client.
    await page.click("form.form-card button[type=submit]");
    await page.waitForTimeout(300);
    await shot(page, `${device}-event-validation`);
    expect(await page.locator("#Title-error, [data-valmsg-for=Title] span").count() > 0, "empty form should show a title error");

    // End before start is refused by the server.
    await page.fill("#Title", "Mahalle bostanı gönüllü günü");
    await page.selectOption("#CategoryId", { label: "Buluşma" });
    await page.fill("#Description", "Bostanda ekim, sulama ve kompost üzerine birlikte çalışıyoruz. Eldivenler bizden.");
    await page.fill("#Location", "Kuzguncuk · Bostan");
    await page.fill("#EndTime", "10:00");
    await page.fill("#StartTime", "11:00");
    await submit(page);
    await shot(page, `${device}-event-end-before-start`);
    expect(page.url().endsWith("/events/new"), "end before start should stay on the form");

    await page.fill("#EndTime", "13:00");
    await page.fill("#StartTime", "10:00");
    await page.fill("#Capacity", "2");
    await submit(page);
    await shot(page, `${device}-event-created`);
    expect(/\/events\/\d+$/.test(page.url()), "a valid event should open its detail page");

    // Lowering capacity below confirmed attendees is refused on an event with attendees.
    await page.goto(`${baseUrl}/?category=atolye`);
    await page.locator(".event-title", { hasText: "Seramik" }).click();
    const detailUrl = page.url();
    await page.goto(`${detailUrl}/edit`);
    await page.fill("#Capacity", "3");
    await submit(page);
    await shot(page, `${device}-event-capacity-too-low`);
    expect(page.url().endsWith("/edit"), "capacity under the confirmed count should be refused");

    // Raising it moves people off the waiting list.
    await page.fill("#Capacity", "12");
    await submit(page);
    await shot(page, `${device}-event-capacity-raised`);
    expect(/bekleme listesinden/i.test((await page.locator(".app-toast").textContent()) ?? ""), "raising capacity should promote the waiting list");

    // Delete asks first.
    await page.goto(`${baseUrl}/?q=bostan`);
    await page.locator(".event-title").first().click();
    await page.click("button:has-text('Sil')");
    await page.waitForTimeout(400);
    await shot(page, `${device}-event-delete-confirm`);
    await Promise.all([page.waitForNavigation(), page.click("[data-confirm-accept]")]);
    await shot(page, `${device}-event-deleted`);
    expect((await page.locator(".event-title", { hasText: "Mahalle bostanı" }).count()) === 0, "deleted event should be gone");
  }

  await page.goto(`${baseUrl}/calendar`);
  await shot(page, `${device}-calendar-month`);
  expect((await page.locator(".calendar-event").count()) > 3, `${device}: calendar should show this month's events`);

  await page.goto(`${baseUrl}/calendar?view=hafta`);
  await shot(page, `${device}-calendar-week`);

  if (device === "desktop") {
    // Register for an event with free seats.
    await page.goto(`${baseUrl}/?q=kitap`);
    await page.locator(".event-title").first().click();
    const eventUrl = page.url();
    await page.fill("#Registration_FullName", "Deniz Aksoy");
    await page.fill("#Registration_Email", "deniz.aksoy@example.com");
    await shot(page, `${device}-rsvp-form`);
    await Promise.all([page.waitForNavigation(), page.click("#kayit button[type=submit]")]);
    await shot(page, `${device}-rsvp-confirmed`);
    expect(page.url().includes("/registration/"), "registering should open the personal registration page");
    const personalUrl = page.url().split("?")[0];

    // The same address cannot register twice.
    await page.goto(eventUrl);
    await page.fill("#Registration_FullName", "Deniz A.");
    await page.fill("#Registration_Email", "DENIZ.AKSOY@example.com");
    await Promise.all([page.waitForNavigation(), page.click("#kayit button[type=submit]")]);
    await shot(page, `${device}-rsvp-duplicate`);
    expect((await page.locator("#kayit").textContent()).includes("zaten kayıtlısınız"), "a duplicate e-mail should be refused");

    // A full event puts people on the waiting list.
    await page.goto(`${baseUrl}/?q=belgrad`);
    await page.locator(".event-title").first().click();
    await page.fill("#Registration_FullName", "Kaan Er");
    await page.fill("#Registration_Email", "kaan.er@example.com");
    await Promise.all([page.waitForNavigation(), page.click("#kayit button[type=submit]")]);
    await shot(page, `${device}-rsvp-waitlisted`);
    expect((await page.locator(".status-line").textContent()).includes("Bekleme listesindesiniz"), "a full event should waitlist");

    // Cancelling from the personal link.
    await page.goto(personalUrl);
    await page.click("button:has-text('Kaydımı iptal et')");
    await page.waitForTimeout(400);
    await shot(page, `${device}-rsvp-cancel-confirm`);
    await Promise.all([page.waitForNavigation(), page.click("[data-confirm-accept]")]);
    await shot(page, `${device}-rsvp-cancelled`);
    expect((await page.locator(".status-line").textContent()).includes("iptal"), "cancelling should show the cancelled state");

    // Organiser view: cancelling a seat promotes the first person waiting.
    await page.goto(`${baseUrl}/?q=seramik`);
    await page.locator(".event-title").first().click();
    await page.click("a:has-text('Listeyi yönet')");
    await shot(page, `${device}-attendees`);
    await page.locator("tbody tr").first().locator("button:has-text('İptal et')").click();
    await page.waitForTimeout(400);
    await Promise.all([page.waitForNavigation(), page.click("[data-confirm-accept]")]);
    await shot(page, `${device}-attendees-after-cancel`);

    // Calendar file.
    const response = await page.request.get(`${eventUrl}/ics`);
    expect((response.headers()["content-type"] ?? "").startsWith("text/calendar"), "the .ics endpoint should return a calendar file");
    expect((await response.text()).includes("BEGIN:VEVENT"), "the .ics file should contain an event");
  }

  await page.goto(`${baseUrl}/events/99999`);
  await shot(page, `${device}-not-found`);

  await context.close();
}

await browser.close();

if (problems.length > 0) {
  console.error("\nProblems:\n" + problems.map((p) => `- ${p}`).join("\n"));
  process.exit(1);
}
console.log("\nAll screens rendered without horizontal overflow.");
