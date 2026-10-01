const homeServices = document.querySelector('#home-services');
PrincessApi.request('/api/services').then(services => {
  homeServices.replaceChildren();
  services.slice(0, 4).forEach((service, index) => {
    const card = document.createElement('article');
    card.className = 'card';
    card.innerHTML = `<div class="card-icon">${String(index + 1).padStart(2, '0')}</div><h3></h3><p></p><span class="price"></span>`;
    card.querySelector('h3').textContent = service.name;
    card.querySelector('p').textContent = service.description;
    card.querySelector('.price').textContent = service.isOvernightStay
      ? `$${Number(service.price).toFixed(2)} / night`
      : `$${Number(service.price).toFixed(2)} · ${service.durationMinutes} min`;
    homeServices.append(card);
  });
}).catch(() => { homeServices.innerHTML = '<div class="empty-state">Services will appear here when the booking service is running.</div>'; });

const ownerPhoto = document.querySelector('.about-owner-photo');
if (ownerPhoto) {
  const showOwnerPhoto = () => {
    ownerPhoto.hidden = false;
    ownerPhoto.parentElement.classList.add('has-photo');
  };
  const hideOwnerPhoto = () => {
    ownerPhoto.hidden = true;
    ownerPhoto.parentElement.classList.remove('has-photo');
  };
  ownerPhoto.addEventListener('load', showOwnerPhoto);
  ownerPhoto.addEventListener('error', hideOwnerPhoto);
  if (ownerPhoto.complete) ownerPhoto.naturalWidth > 0 ? showOwnerPhoto() : hideOwnerPhoto();
}

let homeOwnerProfile = null;
function applyHomeOwnerProfile(profile) {
  homeOwnerProfile = profile;
  document.querySelector('#home-owner-section-label').textContent = profile.sectionLabel;
  document.querySelector('#home-owner-section-title').textContent = profile.sectionTitle;
  document.querySelector('#home-owner-greeting').textContent = profile.greeting;
  document.querySelector('#home-owner-headline').textContent = profile.headline;
  const biography = document.querySelector('#home-owner-biography');
  biography.replaceChildren();
  profile.biography.split(/\n\s*\n/).filter(Boolean).forEach(text => {
    const paragraph = document.createElement('p');
    paragraph.textContent = text;
    biography.append(paragraph);
  });
  document.querySelector('#home-owner-phone').textContent = profile.phone;
  document.querySelector('#home-owner-phone-link').href = `tel:${profile.phone.replace(/[^+\d]/g, '')}`;
  document.querySelector('#home-owner-email').textContent = profile.email;
  document.querySelector('#home-owner-email-link').href = `mailto:${profile.email}`;
  document.querySelector('#home-owner-instagram').textContent = profile.instagramLabel;
  document.querySelector('#home-owner-instagram-link').href = profile.instagramUrl;
  document.querySelector('#home-owner-area').textContent = profile.serviceArea;
  document.querySelector('#home-owner-map-link').href = profile.mapUrl;
  document.querySelector('#home-owner-contact-button').textContent = profile.contactButtonText;
}

PrincessApi.request('/api/site-content/owner-profile').then(profile => {
  applyHomeOwnerProfile(profile);
  if (PrincessApi.user()?.role === 'Owner') initializeHomeOwnerEditor();
}).catch(() => {});

function initializeHomeOwnerEditor() {
  const container = document.querySelector('.home-owner-section .container');
  const controls = document.createElement('div');
  controls.className = 'owner-inline-controls';
  const edit = inlineButton('Edit text & contact', openHomeOwnerEditor);
  const photo = inlineButton('Change photo', () => photoInput.click());
  const dashboard = document.createElement('a');
  dashboard.className = 'owner-inline-edit';
  dashboard.href = 'owner.html';
  dashboard.textContent = 'Owner dashboard';
  const photoInput = document.createElement('input');
  photoInput.type = 'file';
  photoInput.accept = 'image/jpeg,image/png,image/webp';
  photoInput.hidden = true;
  photoInput.addEventListener('change', updateHomeOwnerPhoto);
  controls.append(edit, photo, dashboard, photoInput);
  container.prepend(controls);
  buildHomeOwnerDialog();
}

function inlineButton(label, handler) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'owner-inline-edit';
  button.textContent = label;
  button.addEventListener('click', handler);
  return button;
}

let homeOwnerDialog;
function buildHomeOwnerDialog() {
  homeOwnerDialog = document.createElement('dialog');
  homeOwnerDialog.className = 'owner-edit-dialog';
  homeOwnerDialog.innerHTML = `<form class="panel-form owner-inline-form" id="home-owner-edit-form">
    <div class="owner-dialog-heading"><div><span class="eyebrow">Visible on the homepage</span><h2>Edit Demo Owner’s section</h2></div><button class="owner-dialog-close" type="button" aria-label="Close">×</button></div>
    <div class="form-grid">
      <div class="field"><label for="inline-section-label">Small heading</label><input id="inline-section-label" maxlength="120" required></div>
      <div class="field"><label for="inline-section-title">Section title</label><input id="inline-section-title" maxlength="180" required></div>
      <div class="field"><label for="inline-greeting">Introduction label</label><input id="inline-greeting" maxlength="100" required></div>
      <div class="field"><label for="inline-headline">Main headline</label><input id="inline-headline" maxlength="180" required></div>
      <div class="field field-full"><label for="inline-biography">About Demo Owner</label><textarea id="inline-biography" maxlength="3000" rows="9" required></textarea><span class="hint">Leave a blank line between paragraphs.</span></div>
      <div class="field"><label for="inline-phone">Phone</label><input type="tel" id="inline-phone" maxlength="30" required></div>
      <div class="field"><label for="inline-email">Email</label><input type="email" id="inline-email" maxlength="254" required></div>
      <div class="field"><label for="inline-instagram-label">Instagram name</label><input id="inline-instagram-label" maxlength="120" required></div>
      <div class="field"><label for="inline-instagram-url">Instagram link</label><input type="url" id="inline-instagram-url" maxlength="500" required></div>
      <div class="field"><label for="inline-service-area">Service area</label><input id="inline-service-area" maxlength="160" required></div>
      <div class="field"><label for="inline-map-url">Map link</label><input type="url" id="inline-map-url" maxlength="500" required></div>
      <div class="field"><label for="inline-contact-button">Contact button text</label><input id="inline-contact-button" maxlength="100" required></div>
    </div>
    <div class="owner-dialog-actions"><button class="button button-clay" type="submit">Save changes</button><span class="form-status" role="status"></span></div>
  </form>`;
  document.body.append(homeOwnerDialog);
  homeOwnerDialog.querySelector('.owner-dialog-close').addEventListener('click', () => homeOwnerDialog.close());
  homeOwnerDialog.querySelector('form').addEventListener('submit', saveHomeOwnerProfile);
}

function openHomeOwnerEditor() {
  if (!homeOwnerProfile) return;
  const values = {
    '#inline-section-label': homeOwnerProfile.sectionLabel, '#inline-section-title': homeOwnerProfile.sectionTitle,
    '#inline-greeting': homeOwnerProfile.greeting, '#inline-headline': homeOwnerProfile.headline,
    '#inline-biography': homeOwnerProfile.biography, '#inline-phone': homeOwnerProfile.phone,
    '#inline-email': homeOwnerProfile.email, '#inline-instagram-label': homeOwnerProfile.instagramLabel,
    '#inline-instagram-url': homeOwnerProfile.instagramUrl, '#inline-service-area': homeOwnerProfile.serviceArea,
    '#inline-map-url': homeOwnerProfile.mapUrl, '#inline-contact-button': homeOwnerProfile.contactButtonText
  };
  Object.entries(values).forEach(([selector, value]) => { homeOwnerDialog.querySelector(selector).value = value; });
  homeOwnerDialog.showModal();
}

async function saveHomeOwnerProfile(event) {
  event.preventDefault();
  if (!event.currentTarget.reportValidity()) return;
  const value = selector => homeOwnerDialog.querySelector(selector).value;
  const data = {
    sectionLabel:value('#inline-section-label'), sectionTitle:value('#inline-section-title'), greeting:value('#inline-greeting'),
    headline:value('#inline-headline'), biography:value('#inline-biography'), phone:value('#inline-phone'),
    email:value('#inline-email'), instagramLabel:value('#inline-instagram-label'), instagramUrl:value('#inline-instagram-url'),
    serviceArea:value('#inline-service-area'), mapUrl:value('#inline-map-url'), contactButtonText:value('#inline-contact-button')
  };
  const status = homeOwnerDialog.querySelector('.form-status');
  try {
    const updated = await PrincessApi.request('/api/site-content/owner-profile',{method:'PUT',body:JSON.stringify(data)});
    applyHomeOwnerProfile(updated);
    status.textContent = 'Saved.'; status.className = 'form-status success';
    setTimeout(() => homeOwnerDialog.close(), 500);
  } catch (error) { status.textContent = error.message; status.className = 'form-status error'; }
}

async function updateHomeOwnerPhoto(event) {
  const file = event.currentTarget.files[0];
  if (!file) return;
  const data = new FormData();
  const controls = event.currentTarget.closest('.owner-inline-controls');
  try {
    controls.dataset.message = 'Preparing photo…';
    data.append('photo', await PrincessApi.uploadReadyImage(file));
    const result = await PrincessApi.request('/api/site-content/about-photo',{method:'PUT',body:data});
    ownerPhoto.src = `api/site-content/about-photo?v=${encodeURIComponent(result.updatedAt)}`;
    ownerPhoto.hidden = false;
    controls.dataset.message = 'Photo updated';
  } catch (error) { controls.dataset.message = error.message; }
  event.currentTarget.value = '';
}
