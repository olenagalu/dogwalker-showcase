const serviceList = document.querySelector('#service-list');
const serviceOwner = PrincessApi.user()?.role === 'Owner';
let services = [];
let serviceEditor = null;

loadServices();

async function loadServices() {
  try {
    services = await PrincessApi.request(`/api/services${serviceOwner ? '?includeInactive=true' : ''}`);
    renderServices();
  } catch (error) { serviceList.innerHTML = `<div class="empty-state">${escapeText(error.message)}</div>`; }
}

function renderServices() {
  serviceList.replaceChildren();
  if (serviceOwner) serviceList.append(ownerToolbar());
  if (!services.length) {
    const blank = document.createElement('div'); blank.className = 'empty-state'; blank.textContent = 'No services are published yet.'; serviceList.append(blank); return;
  }
  services.forEach((service, index) => serviceList.append(serviceRow(service, index)));
}

function ownerToolbar() {
  const toolbar = document.createElement('div'); toolbar.className = 'owner-inline-controls owner-service-toolbar';
  const add = ownerButton('Add service', () => openServiceEditor());
  const dashboard = document.createElement('a'); dashboard.className = 'owner-inline-edit'; dashboard.href = 'owner.html'; dashboard.textContent = 'Owner dashboard';
  toolbar.append(add, dashboard); return toolbar;
}

function serviceRow(service, index) {
  const row = document.createElement('article'); row.className = `service-row${service.isActive ? '' : ' owner-service-inactive'}`;
  const number = document.createElement('span'); number.className = 'service-number'; number.textContent = String(index + 1).padStart(2, '0');
  const copy = document.createElement('div'); const title = document.createElement('h3'); title.textContent = service.name;
  const description = document.createElement('p');
  const duration = service.isOvernightStay ? 'Multi-day overnight care' : `${service.durationMinutes} minutes`;
  description.textContent = `${service.description} · ${duration}`;
  if (!service.isActive) { const hidden = document.createElement('span'); hidden.className = 'status-badge status-cancelled'; hidden.textContent = 'Hidden from customers'; copy.append(title, hidden, description); }
  else copy.append(title, description);
  const price = document.createElement('span'); price.className = 'service-price'; price.textContent = `$${Number(service.price).toFixed(2)}${service.isOvernightStay ? ' / night' : ''}`;
  row.append(number, copy, price);
  if (serviceOwner) {
    const controls = document.createElement('div'); controls.className = 'owner-card-controls owner-service-controls';
    controls.append(ownerButton('Edit service', () => openServiceEditor(service)), ownerButton('Delete', () => removeService(service), true)); row.append(controls);
  }
  return row;
}

function ownerButton(text, handler, danger = false) {
  const button = document.createElement('button'); button.type = 'button'; button.className = `owner-inline-edit${danger ? ' danger' : ''}`; button.textContent = text; button.addEventListener('click', handler); return button;
}

function ensureServiceEditor() {
  if (serviceEditor) return;
  serviceEditor = document.createElement('dialog'); serviceEditor.className = 'owner-edit-dialog';
  serviceEditor.innerHTML = `<form class="panel-form owner-inline-form" id="inline-service-form">
    <input type="hidden" id="inline-service-id">
    <div class="owner-dialog-heading"><div><span class="eyebrow">Public services page</span><h2 id="inline-service-title">Add service</h2></div><button class="owner-dialog-close" type="button" aria-label="Close">×</button></div>
    <div class="field"><label for="inline-service-name">Name</label><input id="inline-service-name" name="name" maxlength="100" required></div>
    <div class="field"><label for="inline-service-description">Description</label><textarea id="inline-service-description" name="description" maxlength="1000" required></textarea></div>
    <div class="form-grid"><div class="field"><label for="inline-service-duration">Duration (minutes)</label><input type="number" id="inline-service-duration" name="durationMinutes" min="5" max="1440" required></div><div class="field"><label for="inline-service-price">Price</label><input type="number" id="inline-service-price" name="price" min="0.01" max="10000" step="0.01" required></div></div>
    <label class="check-field"><input type="checkbox" id="inline-service-active" checked> Visible to customers</label>
    <label class="check-field"><input type="checkbox" id="inline-service-overnight"> Overnight stay (price is per night)</label>
    <div class="owner-dialog-actions"><button class="button button-clay" type="submit">Save service</button><span class="form-status" role="status"></span></div>
  </form>`;
  document.body.append(serviceEditor);
  serviceEditor.querySelector('.owner-dialog-close').addEventListener('click', () => serviceEditor.close());
  serviceEditor.querySelector('form').addEventListener('submit', saveService);
}

function openServiceEditor(service = null) {
  ensureServiceEditor();
  serviceEditor.querySelector('#inline-service-id').value = service?.id ?? '';
  serviceEditor.querySelector('#inline-service-title').textContent = service ? `Edit ${service.name}` : 'Add service';
  serviceEditor.querySelector('#inline-service-name').value = service?.name ?? '';
  serviceEditor.querySelector('#inline-service-description').value = service?.description ?? '';
  serviceEditor.querySelector('#inline-service-duration').value = service?.durationMinutes ?? 30;
  serviceEditor.querySelector('#inline-service-price').value = service?.price ?? '';
  serviceEditor.querySelector('#inline-service-active').checked = service?.isActive ?? true;
  serviceEditor.querySelector('#inline-service-overnight').checked = service?.isOvernightStay ?? false;
  const status = serviceEditor.querySelector('.form-status'); status.textContent = ''; status.className = 'form-status';
  serviceEditor.showModal();
}

async function saveService(event) {
  event.preventDefault(); if (!event.currentTarget.reportValidity()) return;
  const id = serviceEditor.querySelector('#inline-service-id').value;
  const data = Object.fromEntries(new FormData(event.currentTarget));
  data.durationMinutes = Number(data.durationMinutes); data.price = Number(data.price);
  data.isActive = serviceEditor.querySelector('#inline-service-active').checked;
  data.isOvernightStay = serviceEditor.querySelector('#inline-service-overnight').checked;
  const status = serviceEditor.querySelector('.form-status');
  try {
    await PrincessApi.request(`/api/services${id ? `/${id}` : ''}`, { method:id ? 'PUT' : 'POST', body:JSON.stringify(data) });
    status.textContent = 'Saved.'; status.className = 'form-status success'; await loadServices(); setTimeout(() => serviceEditor.close(), 350);
  } catch (error) { status.textContent = error.message; status.className = 'form-status error'; }
}

async function removeService(service) {
  if (!confirm(`Delete ${service.name}? Services with booking history should be hidden instead.`)) return;
  try { await PrincessApi.request(`/api/services/${service.id}`, {method:'DELETE'}); await loadServices(); }
  catch (error) { alert(error.message); }
}

function escapeText(value) { const node = document.createElement('div'); node.textContent = value; return node.innerHTML; }
