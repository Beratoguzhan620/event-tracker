// Regenerates the README images (docs/screenshots) from the demo data.
// Needs the app running with Seed:DemoContent=true on a fresh database. The walkthrough video is saved as
// output/media/walkthrough.webm and turned into docs/screenshots/demo.gif with ffmpeg.
import { chromium } from "playwright";
import { mkdir, readdir, rename } from "node:fs/promises";

const baseUrl = process.env.BASE_URL ?? "http://localhost:5080";
const out = new URL("./output/media/", import.meta.url).pathname;
const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

await mkdir(out, { recursive: true });
const browser = await chromium.launch();

async function shot(page, name, fullPage = false) {
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.screenshot({ path: `${out}${name}.png`, fullPage });
  console.log(`saved ${name}.png`);
}

async function openEvent(page, query) {
  await page.goto(`${baseUrl}/?q=${encodeURIComponent(query)}`);
  await page.locator(".event-title").first().click();
  return page.url();
}

{
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, locale: "tr-TR" });
  const page = await context.newPage();

  await page.goto(`${baseUrl}/`);
  await shot(page, "events");
  await page.goto(`${baseUrl}/calendar`);
  await shot(page, "calendar");
  await page.goto(`${baseUrl}/calendar?view=hafta`);
  await shot(page, "calendar-week");
  const seramik = await openEvent(page, "seramik");
  await shot(page, "event");
  await page.goto(`${seramik}/attendees`);
  await shot(page, "attendees");
  await page.goto(`${baseUrl}/events/new`);
  await shot(page, "event-form");

  await context.close();
}

{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true, locale: "tr-TR" });
  const page = await context.newPage();
  await page.goto(`${baseUrl}/`);
  await shot(page, "mobile-events");
  await page.goto(`${baseUrl}/calendar`);
  await shot(page, "mobile-calendar");
  await openEvent(page, "kitap");
  await shot(page, "mobile-event");
  await context.close();
}

// Walkthrough: list → event → register → personal page → calendar.
{
  const context = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    locale: "tr-TR",
    recordVideo: { dir: out, size: { width: 1280, height: 800 } },
  });
  const page = await context.newPage();

  await page.goto(`${baseUrl}/`);
  await pause(1800);
  await page.mouse.wheel(0, 500);
  await pause(1000);
  await page.mouse.wheel(0, -500);
  await pause(600);

  await page.fill("input[name=q]", "");
  await page.locator("input[name=q]").pressSequentially("bulut", { delay: 90 });
  await pause(400);
  await Promise.all([page.waitForNavigation(), page.click("button:has-text('Filtrele')")]);
  await pause(1200);

  await Promise.all([page.waitForNavigation(), page.locator(".event-title").first().click()]);
  await pause(1600);
  await page.locator("#Registration_FullName").pressSequentially("Selin Arı", { delay: 70 });
  await page.locator("#Registration_Email").pressSequentially("selin.ari@example.com", { delay: 45 });
  await pause(500);
  await Promise.all([page.waitForNavigation(), page.click("#kayit button[type=submit]")]);
  await pause(2400);

  await Promise.all([page.waitForNavigation(), page.click(".top-link:has-text('Takvim')")]);
  await pause(1800);
  await Promise.all([page.waitForNavigation(), page.click("a:has-text('Hafta')")]);
  await pause(1800);

  await context.close();
  const video = (await readdir(out)).find((name) => name.endsWith(".webm") && name !== "walkthrough.webm");
  if (video) await rename(`${out}${video}`, `${out}walkthrough.webm`);
  console.log("saved walkthrough.webm");
}

await browser.close();
