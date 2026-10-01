// Run with Playwright installed: node --test tests/frontend/owner-calendar.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');

const stay = {
  id: 1, serviceId: 4, serviceName: 'Overnight stay', isOvernightStay: true,
  date: '2026-09-29', endDate: '2026-10-02', startTime: '22:00', endTime: '09:00',
  status: 'Confirmed', dogName: 'Buddy', customerName: 'Sam', customerEmail: 'sam@example.test',
  price: 285, createdAt: '2026-09-01T12:00:00Z'
};

async function setup(t, failures = new Set(), bookings = [stay]) {
  const browser = await chromium.launch({ headless: true, ...(process.env.PLAYWRIGHT_CHANNEL ? { channel: process.env.PLAYWRIGHT_CHANNEL } : {}) });
  t.after(() => browser.close());
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    sessionStorage.setItem('dogwalkerShowcaseUser', JSON.stringify({ role: 'Owner', fullName: 'Owner' }));
    sessionStorage.setItem('dogwalkerShowcaseToken', 'test-token');
  });
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.pathname.startsWith('/api/')) {
      if (failures.has(url.pathname)) return route.fulfill({ status: 500, json: { message: 'Test request failure' } });
      const data = url.pathname === '/api/bookings/admin' ? bookings
        : url.pathname === '/api/services' ? [{ id: 4, name: 'Overnight stay', price: 95, isActive: true, isOvernightStay: true }]
        : url.pathname === '/api/availability/hours' ? { regularStart: '09:00', regularEnd: '18:00', emergencyStart: '18:00', emergencyEnd: '22:00', emergencySurcharge: 10 }
        : [];
      return route.fulfill({ json: data });
    }
    if (url.hostname !== 'localhost') return route.abort();
    try {
      const file = path.resolve(__dirname, '../../frontend', url.pathname.slice(1));
      const type = file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : file.endsWith('.html') ? 'text/html' : 'application/octet-stream';
      await route.fulfill({ body: await fs.readFile(file), contentType: type });
    } catch { await route.fulfill({ status: 404 }); }
  });
  await page.goto('http://localhost/owner.html#overnight');
  await page.evaluate(() => { ownerOvernightDate = new Date(2026, 8, 1); renderOvernightCalendar(); });
  return { page, errors };
}

test('unrelated request failures do not block overnight bookings or month navigation', async t => {
  const { page, errors } = await setup(t, new Set(['/api/availability', '/api/users/customers']));
  await page.waitForFunction(() => document.querySelectorAll('#owner-overnight-calendar .overnight-event').length === 2);
  assert.match(await page.locator('#owner-status').innerText(), /Availability:/);
  await page.locator('#owner-overnight-next').click();
  assert.match(await page.locator('#owner-overnight-month').innerText(), /October 2026/);
  assert.equal(await page.locator('#owner-overnight-calendar .overnight-event').count(), 1);
  await page.locator('#owner-overnight-prev').click();
  assert.equal(await page.locator('#owner-overnight-calendar .overnight-event').count(), 2);
  assert.deepEqual(errors, []);
});

test('booking failures replace loading with a retry state, and retry recovers', async t => {
  const failures = new Set(['/api/bookings/admin']);
  const { page, errors } = await setup(t, failures);
  await page.locator('#owner-overnight-calendar button').waitFor();
  assert.match(await page.locator('#owner-overnight-calendar').innerText(), /could not be loaded/);
  await page.locator('#owner-overnight-next').click();
  assert.equal(await page.locator('#owner-overnight-calendar .owner-calendar-day').count(), 0);
  failures.clear();
  await page.locator('#owner-overnight-calendar button').click();
  await page.waitForFunction(() => document.querySelectorAll('#owner-overnight-calendar .overnight-event').length === 1);
  assert.equal(await page.locator('#owner-status').innerText(), '');
  assert.deepEqual(errors, []);
});

test('an empty successful response renders a usable month', async t => {
  const { page, errors } = await setup(t, new Set(), []);
  await page.waitForFunction(() => document.querySelectorAll('#owner-overnight-calendar .calendar-day-number').length === 30);
  assert.equal(await page.locator('#owner-overnight-calendar .overnight-event').count(), 0);
  assert.match(await page.locator('#owner-overnight-list').innerText(), /No confirmed overnight stays/);
  assert.deepEqual(errors, []);
});

test('booking history includes canceled services and owns the history totals', async t => {
  const bookings = ['Completed', 'Completed', 'Cancelled', 'Declined', 'Confirmed', 'Pending']
    .map((status, index) => ({ ...stay, id: index + 1, status }));
  const { page, errors } = await setup(t, new Set(), bookings);
  await page.locator('[data-owner-panel="history"]').click();
  await page.waitForFunction(() => document.querySelector('#owner-history-total-count').textContent === '4');
  assert.equal(await page.locator('#owner-completed-count').innerText(), '2');
  assert.equal(await page.locator('#owner-cancelled-count').innerText(), '1');
  assert.equal(await page.locator('#owner-declined-count').innerText(), '1');
  assert.equal(await page.locator('#owner-booking-history .appointment-card').count(), 4);
  assert.equal(await page.locator('#owner-booking-history [data-status="Cancelled"]').count(), 1);
  assert.equal(await page.locator('#owner-overview-history-count').count(), 0);
  assert.equal(await page.locator('#owner-overview-completed-count').textContent(), '2');
  assert.equal(await page.locator('[data-owner-panel-name="overview"] .owner-stat-card').count(), 4);
  assert.deepEqual(errors, []);
});
