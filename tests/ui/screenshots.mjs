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
