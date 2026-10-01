const assistantUser = PrincessApi.requireUser('Assistant');
let ownerServices = [], ownerBookings = [], rules = [];
let ownerCalendarDate = new Date(), ownerOvernightDate = new Date();
let ownerCalendarView = 'week', ownerBookingsState = 'loading';
function prepareBlockDate(date) { location.href = `assistant.html?blockDate=${encodeURIComponent(date)}`; }
function ownerBookingCard(item) {
  const card = document.createElement('article');
  card.className = `appointment-card ${serviceColorClass(item.serviceId)}`;
  const title = document.createElement('h3'); title.textContent = `${item.dogName} · ${item.serviceName}`;
  const details = document.createElement('p');
  details.style.whiteSpace = 'pre-line';
  const when = item.isOvernightStay
    ? `${formatDate(item.date)}–${formatDate(item.endDate)}
Overnight: ${formatTime(item.overnightStartTime || item.startTime)}–${formatTime(item.overnightEndTime || item.endTime)}
Midday: ${formatTime(item.middayStartTime || '14:00')}–${formatTime(item.middayEndTime || '15:00')}`
    : `${formatDate(item.date)} · ${formatTime(item.startTime)}–${formatTime(item.endTime)}`;
  details.textContent = `${when}
${item.customerName} · ${item.customerPhone || item.customerEmail || 'Contact details not provided'}
${item.customerServiceAddress || 'Address not provided'}
${item.specialInstructions || ''}`;
  card.append(title, details); return card;
}
async function loadOwner() {
  const status = document.querySelector('#assistant-status');
  const refresh = document.querySelector('#refresh-calendar');
  refresh.disabled = true;
  try {
    const data = await Promise.all([
      PrincessApi.request('/api/assistants/me/bookings'),
      PrincessApi.request('/api/assistants/me/availability'),
      PrincessApi.request('/api/services')
    ]);
    [ownerBookings, rules, ownerServices] = data;
    rules = rules.filter(rule => !rule.isAvailable);
    ownerBookingsState = 'ready';
    status.textContent = '';
    document.querySelector('#assistant-workspace').hidden = false;
    renderOwnerCalendar(); renderOvernightCalendar();
    const list = document.querySelector('#owner-overnight-list'); list.replaceChildren();
    ownerBookings.filter(item => item.isOvernightStay && item.status === 'Confirmed')
      .forEach(item => list.append(ownerBookingCard(item)));
    if (!list.children.length) list.append(empty('No confirmed overnight stays.'));
    document.querySelector('#daily-event-details').replaceChildren();
  } catch (error) {
    ownerBookingsState = 'error';
    document.querySelector('#assistant-workspace').hidden = true;
    status.textContent = error.message;
    status.className = 'form-status error';
  } finally { refresh.disabled = false; }
}
document.querySelector('#refresh-calendar').addEventListener('click', loadOwner);
if (assistantUser) loadOwner();
function action(text,handler,extra=''){const button=document.createElement('button');button.className=`link-button ${extra}`;button.type='button';button.textContent=text;button.addEventListener('click',handler);return button;}function empty(text){const node=document.createElement('div');node.className='empty-state';node.textContent=text;return node;}function formatDate(value){return new Intl.DateTimeFormat('en-US',{month:'short',day:'numeric',year:'numeric',timeZone:'UTC'}).format(new Date(`${value}T00:00:00Z`));}function formatTime(value){const[h,m]=value.split(':');return new Intl.DateTimeFormat('en-US',{hour:'numeric',minute:'2-digit'}).format(new Date(2000,0,1,h,m));}function formatIso(value){return `${value.getFullYear()}-${String(value.getMonth()+1).padStart(2,'0')}-${String(value.getDate()).padStart(2,'0')}`;}function daysBetween(start,end){return Math.max(1,Math.round((new Date(`${end}T00:00:00Z`)-new Date(`${start}T00:00:00Z`))/86400000));}
