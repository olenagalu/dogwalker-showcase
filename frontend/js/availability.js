const serviceSelect = document.querySelector('#availability-service');
const calendarGrid = document.querySelector('#calendar-grid');
const periodHeading = document.querySelector('#calendar-period');
const viewLabel = document.querySelector('#calendar-view-label');
const schedulePanel = document.querySelector('#day-schedule');
const scheduleDate = document.querySelector('#schedule-date');
const scheduleService = document.querySelector('#schedule-service');
const timelineList = document.querySelector('#timeline-list');
const viewButtons = [...document.querySelectorAll('[data-calendar-view]')];
const calendarViewControl = document.querySelector('#calendar-view-control');
const calendarPanel = document.querySelector('#availability-calendar-panel');
const overnightRangeForm = document.querySelector('#overnight-range-form');
const overnightCheckIn = document.querySelector('#overnight-check-in');
const overnightCheckout = document.querySelector('#overnight-checkout');
const overnightRangeResult = document.querySelector('#overnight-range-result');
const today = new Date();
const localToday = formatIso(new Date(today.getFullYear(), today.getMonth(), today.getDate()));
const availabilityUser = PrincessApi.user();
let services = [];
let calendarDate = parseDate(localToday);
let selectedDate = null;
let calendarView = 'month';
let overnightAvailability = null;

overnightCheckIn.min = localToday;
overnightCheckout.min = localToday;

initializeCalendar();

async function initializeCalendar() {
  try {
    services = await PrincessApi.request('/api/services');
    serviceSelect.replaceChildren();
    if (!services.length) {
      serviceSelect.add(new Option('No services are currently available', ''));
      serviceSelect.disabled = true;
      calendarGrid.replaceChildren(empty('No services are currently available.'));
      return;
    }
    services.forEach(service => serviceSelect.add(new Option(`${service.name} · $${Number(service.price).toFixed(2)}`, service.id)));
    serviceSelect.value = String(services[0].id);
    toggleOvernightTools();
    await renderCalendar();
  } catch (error) {
    serviceSelect.replaceChildren(new Option('Services could not be loaded', ''));
    calendarGrid.replaceChildren(empty(error.message));
  }
}

serviceSelect.addEventListener('change', () => {
  selectedDate = null;
  overnightAvailability = null;
  toggleOvernightTools();
  renderCalendar();
});

overnightCheckIn.addEventListener('change', () => {
  overnightCheckout.min = overnightCheckIn.value || localToday;
  overnightAvailability = null;
  overnightRangeResult.hidden = true;
});

overnightRangeForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!event.currentTarget.reportValidity()) return;
  if (overnightCheckout.value <= overnightCheckIn.value) {
    showOvernightResult('Checkout must be after check-in.', false);
    return;
  }
  const service = selectedService();
  const button = event.currentTarget.querySelector('button[type="submit"]');
  button.disabled = true;
  showOvernightResult('Checking these dates…', null);
  try {
    overnightAvailability = await PrincessApi.request(`/api/availability/overnight?checkIn=${overnightCheckIn.value}&checkout=${overnightCheckout.value}&serviceId=${service.id}`);
    renderOvernightResult(service);
  } catch (error) {
    showOvernightResult(error.message, false);
  } finally {
    button.disabled = false;
  }
});

function toggleOvernightTools() {
  const overnight = Boolean(selectedService()?.isOvernightStay);
  overnightRangeForm.hidden = !overnight;
  calendarPanel.hidden = overnight;
  calendarViewControl.hidden = overnight;
  document.querySelector('#availability-service-hint').textContent = overnight
    ? 'Enter check-in and checkout dates to check the complete stay.'
    : 'Times are calculated for the selected service length.';
  if (overnight) {
    calendarView = 'month';
    viewButtons.forEach(button => {
      const active = button.dataset.calendarView === 'month';
      button.classList.toggle('active', active);
      button.setAttribute('aria-pressed', String(active));
    });
    if (!overnightCheckIn.value) overnightCheckIn.value = localToday;
    if (!overnightCheckout.value) {
      const next = parseDate(localToday);
      next.setDate(next.getDate() + 1);
      overnightCheckout.value = formatIso(next);
    }
  } else {
    overnightRangeResult.hidden = true;
  }
}

function showOvernightResult(message, available) {
  overnightRangeResult.hidden = false;
  overnightRangeResult.className = `overnight-range-result${available === true ? ' is-available' : available === false ? ' has-conflict' : ''}`;
  overnightRangeResult.replaceChildren(document.createTextNode(message));
}

function renderOvernightResult(service) {
  const available = overnightAvailability.isAvailable;
  overnightRangeResult.hidden = false;
  overnightRangeResult.className = `overnight-range-result ${available ? 'is-available' : 'has-conflict'}`;
  const heading = document.createElement('h3');
  heading.textContent = available ? 'These dates are available' : 'These dates need a special request';
  const copy = document.createElement('p');
  copy.textContent = available
    ? 'Demo Owner is available for the complete overnight stay.'
    : 'Some of these dates overlap Demo Owner’s existing bookings or unavailable time. She may be able to adapt or introduce someone she trusts.';
  const link = document.createElement('a');
  link.className = 'button button-clay';
  if (available) {
    link.href = bookingUrl(service.id, overnightAvailability.checkIn, '', overnightAvailability.checkout);
    link.textContent = availabilityUser?.role === 'Owner' ? 'Add a booking' : 'Continue to booking';
  } else {
    const params = new URLSearchParams({ serviceId: String(service.id), checkIn: overnightAvailability.checkIn, checkout: overnightAvailability.checkout });
    link.href = `request.html?${params}`;
    link.textContent = 'Make a special request';
  }
  overnightRangeResult.replaceChildren(heading, copy, link);
}

viewButtons.forEach(button => button.addEventListener('click', () => {
  calendarView = button.dataset.calendarView;
  viewButtons.forEach(option => {
    const active = option === button;
    option.classList.toggle('active', active);
    option.setAttribute('aria-pressed', String(active));
  });
  selectedDate = null;
  renderCalendar();
}));

document.querySelector('#public-calendar-prev').addEventListener('click', () => navigateCalendar(-1));
document.querySelector('#public-calendar-next').addEventListener('click', () => navigateCalendar(1));

function navigateCalendar(direction) {
  if (calendarView === 'month') calendarDate = new Date(calendarDate.getFullYear(), calendarDate.getMonth() + direction, 1);
  else calendarDate.setDate(calendarDate.getDate() + (7 * direction));
  selectedDate = null;
  renderCalendar();
}

async function renderCalendar() {
  const service = selectedService();
  if (!service) return;
  if (service.isOvernightStay) {
    schedulePanel.hidden = true;
    return;
  }
  schedulePanel.hidden = true;
  calendarGrid.innerHTML = '<div class="empty-state">Checking Demo Owner’s calendar…</div>';
  const range = getRange(calendarView, formatIso(calendarDate));
  periodHeading.textContent = range.label;
  viewLabel.textContent = calendarView === 'month' ? 'Month view' : 'Week view';
  updateNavigationLabels();

  try {
    const hours = await PrincessApi.request('/api/availability/hours');
    const emergencyNote = document.querySelector('#calendar-emergency-note');
    emergencyNote.hidden = !hours.emergencyEnabled;
    emergencyNote.textContent = hours.emergencyEnabled
      ? `Emergency requests: ${formatTime(hours.emergencyStart)}–${formatTime(hours.emergencyEnd)} daily · +$${Number(hours.emergencySurcharge).toFixed(2)} per service. Select a date to see emergency times. Subject to Demo Owner’s approval.` : '';
    if (calendarView === 'month') {
      const slots = await PrincessApi.request(`/api/availability/slots?from=${range.from}&to=${range.to}&serviceId=${service.id}`);
      const openDates = new Set(slots.map(slot => slot.date));
      calendarGrid.replaceChildren(renderMonthGrid(calendarDate.getFullYear(), calendarDate.getMonth(), openDates));
      return;
    }

    const dates = datesBetween(range.from, range.to);
    const schedules = await Promise.all(dates.map(date =>
      PrincessApi.request(`/api/availability/day?date=${date}&serviceId=${service.id}`)));
    renderWeekSchedule(dates, schedules, service);
  } catch (error) {
    calendarGrid.replaceChildren(empty(error.message));
  }
}

function updateNavigationLabels() {
  const period = calendarView === 'month' ? 'month' : 'week';
  document.querySelector('#public-calendar-prev').setAttribute('aria-label', `Previous ${period}`);
  document.querySelector('#public-calendar-next').setAttribute('aria-label', `Next ${period}`);
}

function renderMonthGrid(year, month, openDates) {
  const grid = calendarShell('public-month-calendar');
  const firstDay = new Date(year, month, 1).getDay();
  for (let blank = 0; blank < firstDay; blank += 1) grid.append(calendarBlank());
  const days = new Date(year, month + 1, 0).getDate();
  for (let day = 1; day <= days; day += 1) grid.append(dateButton(formatIso(new Date(year, month, day)), openDates));
  return grid;
}

function renderWeekSchedule(dates, schedules, service) {
  const scroller = document.createElement('div');
  scroller.className = 'public-week-scroll';
  const grid = document.createElement('div');
  grid.className = 'public-week-schedule';

  dates.forEach((date, index) => {
    const segments = schedules[index];
    const column = document.createElement('article');
    column.className = 'week-day-column';
    if (date === localToday) column.classList.add('is-today');
    if (date < localToday) column.classList.add('is-past');

    const header = document.createElement('button');
    header.type = 'button';
    header.className = 'week-day-heading';
    header.disabled = date < localToday;
    const dayName = document.createElement('span');
    dayName.textContent = new Intl.DateTimeFormat('en-US', { weekday: 'short', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`));
    const dayNumber = document.createElement('strong');
    dayNumber.textContent = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`));
    header.append(dayName, dayNumber);
    header.addEventListener('click', () => selectDate(date));

    const label = document.createElement('p');
    label.className = 'week-taken-label';
    label.textContent = 'Taken times';
    const takenList = document.createElement('div');
    takenList.className = 'week-taken-list';
    const ranges = summarizeTakenTimes(segments);
    if (!ranges.length) {
      const open = document.createElement('span');
      open.className = 'week-all-open';
      open.textContent = 'No times taken';
      takenList.append(open);
    } else {
      ranges.forEach(range => takenList.append(takenTime(range)));
    }

    const action = document.createElement('button');
    action.type = 'button';
    action.className = 'week-day-action';
    action.disabled = date < localToday;
    action.textContent = 'View & request times';
    action.addEventListener('click', () => selectDate(date));
    column.append(header, label, takenList);
    const emergencyRanges = summarizeRanges(segments.filter(segment => segment.status === 'Emergency'));
    emergencyRanges.forEach(range => {
      const item = takenTime(range);
      item.className = 'week-taken-time emergency';
      column.append(item);
    });
    column.append(action);
    grid.append(column);
  });

  scroller.append(grid);
  calendarGrid.replaceChildren(scroller);
}

function summarizeTakenTimes(segments) {
  const taken = segments.filter(segment => segment.status !== 'Available' && segment.status !== 'Emergency');
  return summarizeRanges(taken);
}

function summarizeRanges(segments) {
  const ranges = [];
  segments.forEach(segment => {
    const startMinutes = timeToMinutes(segment.startTime);
    const previous = ranges.at(-1);
    if (previous && previous.status === segment.status && previous.endMinutes === startMinutes) previous.endMinutes += 30;
    else ranges.push({ status: segment.status, startMinutes, endMinutes: startMinutes + 30 });
  });
  return ranges;
}

function takenTime(range) {
  const item = document.createElement('div');
  item.className = `week-taken-time ${range.status.toLowerCase()}`;
  const status = document.createElement('span');
  status.textContent = range.status === 'Emergency' ? 'Emergency requests' : range.status;
  const time = document.createElement('strong');
  time.textContent = `${formatMinutes(range.startMinutes)}–${formatMinutes(range.endMinutes)}`;
  item.append(status, time);
  return item;
}

function calendarShell(className) {
  const grid = document.createElement('div');
  grid.className = className;
  ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].forEach(day => {
    const label = document.createElement('div');
    label.className = 'calendar-weekday';
    label.textContent = day;
    grid.append(label);
  });
  return grid;
}

function calendarBlank() {
  const blank = document.createElement('div');
  blank.className = 'public-calendar-day is-empty';
  blank.setAttribute('aria-hidden', 'true');
  return blank;
}

function dateButton(date, openDates) {
  const value = parseDate(date);
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'public-calendar-day';
  button.dataset.date = date;
  const isPast = date < localToday;
  button.disabled = isPast;
  if (date === localToday) button.classList.add('is-today');
  if (date === selectedDate) button.classList.add('is-selected');
  if (openDates.has(date)) button.classList.add('has-openings');

  const number = document.createElement('strong');
  number.textContent = value.getDate();
  const status = document.createElement('span');
  status.textContent = isPast ? 'Past'
    : openDates.has(date) ? 'Open times' : 'View schedule';
  button.append(number, status);
  button.setAttribute('aria-label', `${formatLongDate(date)}${openDates.has(date) ? ', open times available' : ''}`);
  button.addEventListener('click', () => selectDate(date));
  return button;
}

async function selectDate(date) {
  selectedDate = date;
  document.querySelectorAll('.public-calendar-day[data-date]').forEach(button => {
    button.classList.toggle('is-selected', button.dataset.date === date);
  });
  schedulePanel.hidden = false;
  scheduleDate.textContent = formatLongDate(date);
  timelineList.innerHTML = '<div class="empty-state">Checking this day…</div>';
  const service = selectedService();
  scheduleService.textContent = `${service.name} · ${service.durationMinutes} minutes`;

  try {
    const segments = await PrincessApi.request(`/api/availability/day?date=${date}&serviceId=${service.id}`);
    renderTimeline(segments, date, service);
    schedulePanel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    timelineList.replaceChildren(empty(error.message));
  }
}

function renderTimeline(segments, date, service) {
  timelineList.replaceChildren();
  const groups = [
    { label: 'Overnight', from: 0, to: 5 },
    { label: 'Morning', from: 5, to: 12 },
    { label: 'Afternoon', from: 12, to: 17 },
    { label: 'Evening', from: 17, to: 24 }
  ];
  groups.forEach(group => {
    const section = document.createElement('section');
    section.className = 'schedule-period';
    const heading = document.createElement('h3');
    heading.textContent = group.label;
    const grid = document.createElement('div');
    grid.className = 'timeline-grid';
    segments.filter(segment => hourOf(segment.startTime) >= group.from && hourOf(segment.startTime) < group.to)
      .forEach(segment => grid.append(scheduleSlot(segment, date, service)));
    section.append(heading, grid);
    timelineList.append(section);
  });
}

function scheduleSlot(segment, date, service) {
  const emergency = segment.status === 'Emergency' && date >= localToday;
  const bookable = segment.isBookable || emergency;
  const node = document.createElement(bookable ? 'a' : 'div');
  node.className = `timeline-slot ${segment.status.toLowerCase()}${bookable ? ' is-bookable' : ''}`;
  const time = document.createElement('strong');
  time.textContent = formatTime(segment.startTime);
  const status = document.createElement('span');
  status.textContent = emergency ? 'Request emergency care' : bookable ? (availabilityUser?.role === 'Owner' ? 'Add a booking' : 'Request this time') : segment.status === 'Available' ? `Doesn’t fit ${service.durationMinutes} min` : segment.status;
  node.append(time, status);
  if (bookable) {
    node.href = emergency
      ? `emergency.html?${new URLSearchParams({ serviceId: String(service.id), date, time: segment.startTime.slice(0, 5) })}`
      : bookingUrl(service.id, date, segment.startTime);
    const action = emergency ? 'Request emergency care for' : availabilityUser?.role === 'Owner' ? 'Add a booking for' : 'Request';
    node.setAttribute('aria-label', `${action} ${service.name} on ${formatLongDate(date)} at ${formatTime(segment.startTime)}`);
  }
  return node;
}

function bookingUrl(serviceId, date, time = '', endDate = '') {
  const params = new URLSearchParams({ serviceId: String(serviceId), date });
  if (time) params.set('time', time);
  if (endDate) params.set('endDate', endDate);
  if (availabilityUser?.role === 'Owner') return `owner.html?${params}#schedule`;
  return `book.html?${params}`;
}

function getRange(view, value) {
  const selected = parseDate(value);
  let from = new Date(selected);
  let to = new Date(selected);
  if (view === 'week') {
    from.setDate(selected.getDate() - selected.getDay());
    to = new Date(from);
    to.setDate(from.getDate() + 6);
  } else {
    from = new Date(selected.getFullYear(), selected.getMonth(), 1);
    to = new Date(selected.getFullYear(), selected.getMonth() + 1, 0);
  }
  const label = view === 'month'
    ? new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(from)
    : `${formatShortDate(formatIso(from))} – ${formatShortDate(formatIso(to))}`;
  return { from: formatIso(from), to: formatIso(to), label };
}

function datesBetween(from, to) {
  const dates = [];
  const end = parseDate(to);
  for (let value = parseDate(from); value <= end; value.setDate(value.getDate() + 1)) dates.push(formatIso(value));
  return dates;
}

function selectedService() { return services.find(item => String(item.id) === serviceSelect.value); }
function empty(text) { const node = document.createElement('div'); node.className = 'empty-state'; node.textContent = text; return node; }
function parseDate(value) { const [year, month, day] = value.split('-').map(Number); return new Date(year, month - 1, day); }
function formatIso(value) { const year = value.getFullYear(); const month = String(value.getMonth() + 1).padStart(2, '0'); const day = String(value.getDate()).padStart(2, '0'); return `${year}-${month}-${day}`; }
function formatLongDate(value) { return new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatShortDate(value) { return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`)); }
function formatTime(value) { const [hours, minutes] = value.split(':'); return new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit' }).format(new Date(2000, 0, 1, hours, minutes)); }
function formatMinutes(value) { const normalized = value % (24 * 60); return formatTime(`${String(Math.floor(normalized / 60)).padStart(2, '0')}:${String(normalized % 60).padStart(2, '0')}`); }
function timeToMinutes(value) { const [hours, minutes] = value.split(':').map(Number); return (hours * 60) + minutes; }
function hourOf(value) { return Number(value.split(':')[0]); }
