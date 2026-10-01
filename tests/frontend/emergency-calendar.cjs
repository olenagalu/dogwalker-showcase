const { test } = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');

test('customer calendar shows emergency requests and carries the selected time into the form', async t => {
  const browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHANNEL } : {}) });
  t.after(() => browser.close());
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.pathname.startsWith('/api/')) {
      const data = url.pathname === '/api/services' ? [{ id: 1, name: 'Visit', price: 22, durationMinutes: 30 }]
        : url.pathname === '/api/availability/hours' ? { start: '07:00', end: '23:00', emergencyStart: '23:00', emergencyEnd: '07:00', emergencyEnabled: true, emergencySurcharge: 45 }
        : url.pathname === '/api/availability/day' ? [{ startTime: '00:00', status: 'Emergency', isBookable: false }, { startTime: '07:00', status: 'Available', isBookable: true }, { startTime: '23:00', status: 'Emergency', isBookable: false }]
        : [];
      return route.fulfill({ json: data });
    }
    if (url.hostname !== 'localhost') return route.abort();
    try {
      const file = path.resolve(__dirname, '../../frontend', url.pathname.slice(1));
      await route.fulfill({ body: await fs.readFile(file), contentType: file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : file.endsWith('.html') ? 'text/html' : 'application/octet-stream' });
    } catch { await route.fulfill({ status: 404 }); }
  });
  await page.goto('http://localhost/availability.html');
  await page.locator('.public-calendar-day[data-date]:not([disabled])').first().waitFor();
  assert.match(await page.locator('#calendar-emergency-note').innerText(), /11:00 PM–7:00 AM.*\+\$45.00/);
  await page.locator('.public-calendar-day[data-date]:not([disabled])').first().click();
  await page.locator('a.timeline-slot.emergency').first().waitFor();
  assert.equal(await page.locator('a.timeline-slot.emergency').count(), 2);
  const target = await page.locator('a.timeline-slot.emergency').last().getAttribute('href');
  const selected = new URL(target, 'http://localhost');
  await page.locator('[data-calendar-view="week"]').click();
  await page.locator('.week-taken-time.emergency').first().waitFor();
  assert.doesNotMatch(await page.locator('.week-taken-list').first().innerText(), /Emergency/);
  await page.goto(selected.href);
  await page.waitForFunction(() => document.querySelector('[name="time"]').value === '23:00');
  assert.equal(await page.locator('[name="date"]').inputValue(), selected.searchParams.get('date'));
  assert.equal(await page.locator('[name="serviceId"]').inputValue(), '1');
  assert.deepEqual(errors, []);
});
