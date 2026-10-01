if (PrincessApi.user()?.role === 'Assistant') window.location.replace('assistant.html');
const dashboardUser = PrincessApi.requireUser('Customer');
const dashboardStatus = document.querySelector('#dashboard-status');
let currentProfile = null;
let dogs = [];
let bookings = [];

initializePanels();
document.querySelector('#dog-photo').nextElementSibling.textContent = 'Large photos are resized automatically. For a new dog, save first and then edit to add the photo.';
if (dashboardUser) {
  document.querySelector('#dashboard-greeting').textContent = `Welcome back, ${dashboardUser.fullName.split(' ')[0]}.`;
  loadAll();
}

function initializePanels() {
  document.querySelectorAll('[data-profile-panel]').forEach(button => button.addEventListener('click', () => showPanel(button.dataset.profilePanel)));
  const requested = location.hash.replace('#', '');
  const aliases = { profile: 'account' };
  showPanel(document.querySelector(`[data-panel-name="${aliases[requested] || requested}"]`) ? (aliases[requested] || requested) : 'overview', false);
}
function showPanel(name, updateHash = true) {
  document.querySelectorAll('[data-panel-name]').forEach(panel => { panel.hidden = panel.dataset.panelName !== name; });
  document.querySelectorAll('[data-profile-panel]').forEach(button => button.classList.toggle('active', button.dataset.profilePanel === name));
  if (updateHash) history.replaceState(null, '', `#${name}`);
  window.scrollTo({ top: 0, behavior: 'smooth' });
}
async function loadAll() {
  await Promise.all([loadProfile(), loadDogs(), loadBookings()]);
  if (new URLSearchParams(location.search).has('booked')) {
    showPanel('bookings');
    feedback('Your booking request was submitted and is pending confirmation.', 'success');
  }
}
async function loadProfile() {
  currentProfile = await PrincessApi.request('/api/users/me');
  document.querySelector('#profile-name').value = currentProfile.fullName;
  document.querySelector('#profile-email').value = currentProfile.email;
  document.querySelector('#profile-phone').value = currentProfile.phone;
  document.querySelector('#profile-area').value = currentProfile.serviceArea;
  document.querySelector('#profile-address').value = currentProfile.serviceAddress;
  document.querySelector('#security-email').textContent = currentProfile.email;
  document.querySelector('#profile-summary-initial').textContent = currentProfile.fullName.trim().charAt(0).toUpperCase() || 'P';
  document.querySelector('#profile-summary-details').textContent = `${currentProfile.email} · ${currentProfile.serviceArea || 'Service area not added'}`;
  const banner = document.querySelector('#approval-banner');
  const book = document.querySelector('#dashboard-book-button');
  banner.hidden = currentProfile.approvalStatus !== 'Declined';
  book.hidden = currentProfile.approvalStatus === 'Declined';
  if (!banner.hidden) {
    banner.className = 'approval-banner declined';
    banner.textContent = 'Booking is not available for this service address. Update your area/address or contact Demo Owner.';
  }
  if (currentProfile.hasProfilePhoto) {
    const url = await PrincessApi.privateImageUrl('/api/users/me/photo');
    const preview = document.querySelector('#profile-photo-preview');
    const summary = document.querySelector('#profile-summary-photo');
    preview.src = url; preview.hidden = false;
    summary.src = url; summary.hidden = false;
    document.querySelector('#profile-summary-initial').hidden = true;
  }
}
async function loadDogs() {
  dogs = await PrincessApi.request('/api/dogs');
  const list = document.querySelector('#dog-list');
  const overview = document.querySelector('#overview-dogs');
  list.replaceChildren(); overview.replaceChildren();
  if (!dogs.length) {
    list.append(empty('No dogs saved yet. Add your first companion.'));
    const prompt = empty('No dogs saved yet.');
    prompt.append(action('Add your first dog', () => showPanel('dogs')));
    overview.append(prompt); return;
  }
  dogs.forEach(dog => {
    list.append(dogCard(dog, true));
    overview.append(dogCard(dog, false));
  });
}
function dogCard(dog, editable) {
  const card = document.createElement('article'); card.className = 'mini-card dog-profile-card';
  if (dog.hasPhoto) {
    const photo = document.createElement('img'); photo.className = 'dog-profile-photo'; photo.alt = dog.name;
    PrincessApi.privateImageUrl(`/api/dogs/${dog.id}/photo`).then(url => photo.src = url).catch(() => photo.remove()); card.append(photo);
  }
  const title = document.createElement('h3'); title.textContent = dog.name;
  const details = document.createElement('p'); details.textContent = `${dog.breed || 'Breed not specified'}${dog.age != null ? ` · Age ${dog.age}` : ''}`;
  card.append(title, details);
  if (editable) card.append(action('Edit', () => editDog(dog)), action('Delete', () => deleteDog(dog.id), 'danger'));
  return card;
}
async function loadBookings() {
  bookings = await PrincessApi.request('/api/bookings');
  const today = new Date().toISOString().split('T')[0];
  const upcoming = bookings.filter(item => (item.endDate || item.date) >= today && !['Completed', 'Cancelled', 'Declined'].includes(item.status));
  renderBookings(document.querySelector('#upcoming-bookings'), upcoming, true);
  renderBookings(document.querySelector('#previous-bookings'), bookings.filter(item => (item.endDate || item.date) < today || ['Completed', 'Cancelled', 'Declined'].includes(item.status)), false);
  renderBookings(document.querySelector('#overview-booking'), upcoming.slice(0, 1), false);
}
function renderBookings(container, items, canCancel) {
  container.replaceChildren(); if (!items.length) return container.append(empty('Nothing here yet.'));
  items.forEach(item => {
    const card = document.createElement('article'); card.className = 'appointment-card'; card.dataset.status = item.status;
    const title = document.createElement('h3'); title.textContent = `${item.dogName} · ${item.serviceName}${item.assistantName ? ` · Sitter: ${item.assistantName}` : ''}`;
    const meta = document.createElement('p'); const nights = item.isOvernightStay ? daysBetween(item.date, item.endDate) : 1;
    meta.textContent = item.isOvernightStay ? `${formatDate(item.date)}–${formatDate(item.endDate)} · ${nights} night${nights === 1 ? '' : 's'} · Total $${Number(item.price).toFixed(2)}` : `${formatDate(item.date)} at ${formatTime(item.startTime)} · $${Number(item.price).toFixed(2)}`;
    const badge = document.createElement('span'); badge.className = `status-badge status-${item.status.toLowerCase()}`; badge.textContent = item.status;
    card.append(title, meta, badge);
    if (item.assistantId) {
      const image = document.createElement('img'); image.className = 'assistant-avatar'; image.alt = item.assistantName || 'Pet sitter';
      PrincessApi.privateImageUrl(`/api/bookings/${item.id}/assistant-photo`).then(url => { image.src = url; card.prepend(image); }).catch(() => {});
    }
    if (item.specialInstructions) { const notes = document.createElement('p'); notes.textContent = item.specialInstructions; card.append(notes); }
    if (canCancel && ['Pending', 'Confirmed'].includes(item.status)) card.append(action('Cancel booking', () => cancelBooking(item.id), 'danger'));
    container.append(card);
  });
}

document.querySelector('#profile-form').addEventListener('submit', async event => {
  event.preventDefault(); try { const user = await PrincessApi.request('/api/users/me', { method:'PUT', body:JSON.stringify(Object.fromEntries(new FormData(event.currentTarget))) }); sessionStorage.setItem('princessDogWalkerUser', JSON.stringify(user)); await loadProfile(); feedback('Personal information saved.', 'success'); } catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#profile-photo-form').addEventListener('submit', async event => {
  event.preventDefault(); const data = new FormData();
  try { feedback('Preparing photo…', ''); data.append('photo', await PrincessApi.uploadReadyImage(document.querySelector('#profile-photo').files[0])); await PrincessApi.request('/api/users/me/photo', { method:'PUT', body:data }); event.currentTarget.reset(); await loadProfile(); feedback('Profile photo updated.', 'success'); } catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#request-code-form').addEventListener('submit', async event => { event.preventDefault(); await requestResetCode(); });
document.querySelector('#resend-reset-code').addEventListener('click', requestResetCode);
async function requestResetCode() {
  try {
    const response = await PrincessApi.request('/api/auth/forgot-password', { method:'POST', body:JSON.stringify({ email:currentProfile.email }) });
    const form = document.querySelector('#reset-password-form'); form.hidden = false;
    if (response.resetCode) document.querySelector('#dashboard-reset-code').value = response.resetCode;
    feedback('Check your email for the 6-digit verification code. It expires in 15 minutes.', 'success');
  } catch (error) { feedback(error.message, 'error'); }
}
document.querySelector('#reset-password-form').addEventListener('submit', async event => {
  event.preventDefault(); const data = Object.fromEntries(new FormData(event.currentTarget)); data.email = currentProfile.email;
  try { const response = await PrincessApi.request('/api/auth/reset-password', { method:'POST', body:JSON.stringify(data) }); event.currentTarget.reset(); feedback(response.message, 'success'); } catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#dog-form').addEventListener('submit', async event => {
  event.preventDefault(); const id = document.querySelector('#dog-id').value; const data = Object.fromEntries(new FormData(event.currentTarget)); data.age = data.age ? Number(data.age) : null;
  try { const dog = await PrincessApi.request(`/api/dogs${id ? `/${id}` : ''}`, { method:id ? 'PUT' : 'POST', body:JSON.stringify(data) }); const photo = document.querySelector('#dog-photo').files[0]; if (photo) { feedback('Preparing dog photo…', ''); const upload = new FormData(); upload.append('photo', await PrincessApi.uploadReadyImage(photo)); await PrincessApi.request(`/api/dogs/${dog.id}/photo`, { method:'PUT', body:upload }); } clearDogForm(); await loadDogs(); feedback('Dog profile saved.', 'success'); } catch (error) { feedback(error.message, 'error'); }
});
document.querySelector('#clear-dog-form').addEventListener('click', clearDogForm);
function editDog(dog) { showPanel('dogs'); document.querySelector('#dog-id').value = dog.id; document.querySelector('#dog-form-title').textContent = `Edit ${dog.name}`; document.querySelector('#dog-name').value = dog.name; document.querySelector('#dog-breed').value = dog.breed; document.querySelector('#dog-age').value = dog.age ?? ''; document.querySelector('#dog-care').value = dog.careInstructions; document.querySelector('#dog-behavior').value = dog.behavioralNotes; document.querySelector('#dog-medical').value = dog.medicalNotes; toggleDogPassword(false); document.querySelector('#dog-form').scrollIntoView({behavior:'smooth'}); }
function clearDogForm() { document.querySelector('#dog-form').reset(); document.querySelector('#dog-id').value = ''; document.querySelector('#dog-form-title').textContent = 'Register a dog'; toggleDogPassword(true); }
function toggleDogPassword(required) { document.querySelector('#dog-password-field').hidden = !required; document.querySelector('#dog-password').required = required; if (!required) document.querySelector('#dog-password').value = ''; }
async function deleteDog(id) { if (!confirm('Delete this dog profile?')) return; try { await PrincessApi.request(`/api/dogs/${id}`, {method:'DELETE'}); await loadDogs(); } catch (error) { feedback(error.message, 'error'); } }
async function cancelBooking(id) { if (!confirm('Cancel this upcoming booking?')) return; try { await PrincessApi.request(`/api/bookings/${id}/cancel`, {method:'PUT'}); await loadBookings(); feedback('Booking cancelled.', 'success'); } catch (error) { feedback(error.message, 'error'); } }
function action(text, handler, extra = '') { const button = document.createElement('button'); button.className = `link-button ${extra}`; button.type = 'button'; button.textContent = text; button.addEventListener('click', handler); return button; }
function feedback(text, type) { dashboardStatus.textContent = text; dashboardStatus.className = `form-status ${type}`; dashboardStatus.scrollIntoView({behavior:'smooth'}); }
function empty(text) { const node = document.createElement('div'); node.className = 'empty-state'; node.textContent = text; return node; }
function formatDate(value) { return new Intl.DateTimeFormat('en-US', {month:'short', day:'numeric', year:'numeric', timeZone:'UTC'}).format(new Date(`${value}T00:00:00Z`)); }
function formatTime(value) { const [h, m] = value.split(':'); return new Intl.DateTimeFormat('en-US', {hour:'numeric', minute:'2-digit'}).format(new Date(2000, 0, 1, h, m)); }
function daysBetween(start, end) { return Math.max(1, Math.round((new Date(`${end}T00:00:00Z`) - new Date(`${start}T00:00:00Z`)) / 86400000)); }
