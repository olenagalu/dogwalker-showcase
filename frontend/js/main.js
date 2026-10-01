const currentUser = typeof PrincessApi !== 'undefined' ? PrincessApi.user() : null;
const currentPage = window.location.pathname.split('/').pop() || 'index.html';
const nav = document.querySelector('.nav-links');

initializePasswordToggles();

function initializePasswordToggles() {
  document.querySelectorAll('input[type="password"]').forEach(input => {
    if (input.parentElement?.classList.contains('password-input')) return;

    const wrapper = document.createElement('div');
    wrapper.className = 'password-input';
    input.parentNode.insertBefore(wrapper, input);
    wrapper.append(input);

    const toggle = document.createElement('button');
    toggle.className = 'password-toggle';
    toggle.type = 'button';
    toggle.setAttribute('aria-label', 'Show password');
    toggle.setAttribute('aria-controls', input.id);
    toggle.setAttribute('aria-pressed', 'false');
    toggle.innerHTML = `
      <svg class="password-icon password-icon-show" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z"></path>
        <circle cx="12" cy="12" r="2.75"></circle>
      </svg>
      <svg class="password-icon password-icon-hide" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M3 3l18 18"></path>
        <path d="M10.6 6.2A10.8 10.8 0 0 1 12 6c6 0 9.5 6 9.5 6a15.8 15.8 0 0 1-3.1 3.7M6.3 7.3A16 16 0 0 0 2.5 12s3.5 6 9.5 6c1.5 0 2.9-.4 4.1-1"></path>
      </svg>`;
    toggle.addEventListener('click', () => {
      const shouldShow = input.type === 'password';
      input.type = shouldShow ? 'text' : 'password';
      toggle.setAttribute('aria-label', shouldShow ? 'Hide password' : 'Show password');
      toggle.setAttribute('aria-pressed', String(shouldShow));
    });
    wrapper.append(toggle);
  });
}

if (nav) {
  if (currentUser?.role === 'Owner') {
    nav.innerHTML = `
      <a href="index.html">Home</a><a href="services.html">Services</a>
      <a href="availability.html">Availability</a><a href="team.html">Our Team</a>
      <a href="contact.html">Contact</a><a href="owner.html">Owner Profile</a>
      <button class="nav-signout" type="button">Sign out</button>`;
  } else if (currentUser?.role === 'Assistant') {
    nav.innerHTML = '<a href="index.html">Home</a><a href="assistant.html">Assistant dashboard</a><a href="assistant-calendar.html">Calendar</a><button class="nav-signout" type="button">Sign out</button>';
  } else {
    const accountLink = currentUser
      ? '<a href="dashboard.html">Profile</a><button class="nav-signout" type="button">Sign out</button>'
      : '<a href="auth.html">Sign in / Sign up</a>';
    nav.innerHTML = `
      <a href="index.html">Home</a><a href="services.html">Services</a>
      <a href="availability.html">Availability</a><a href="team.html">Our Team</a>
      <a href="contact.html">Contact</a>${accountLink}<a class="button" href="book.html">Book a service</a>`;
  }
  nav.querySelector(`a[href="${currentPage}"]`)?.setAttribute('aria-current', 'page');
  nav.querySelector('.nav-signout')?.addEventListener('click', () => PrincessApi.signOut());
}

if (currentUser?.role === 'Owner') {
  document.querySelectorAll('a[href="book.html"]').forEach(link => link.remove());
}

document.querySelectorAll('.brand').forEach(brand => {
  brand.innerHTML = '<img class="brand-logo" src="assets/demo-pet.svg?v=20260912-logo" alt=""><span>Princess Dog Walker</span>';
  brand.setAttribute('href', 'index.html');
  brand.setAttribute('aria-label', 'Princess Dog Walker home');
});

const toggle = document.querySelector('.nav-toggle');
toggle?.addEventListener('click', () => {
  const isOpen = nav.classList.toggle('open');
  toggle.setAttribute('aria-expanded', String(isOpen));
});

nav?.querySelectorAll('a').forEach(link => link.addEventListener('click', () => {
  nav.classList.remove('open');
  toggle?.setAttribute('aria-expanded', 'false');
}));

document.querySelectorAll('[data-year]').forEach(element => { element.textContent = new Date().getFullYear(); });
