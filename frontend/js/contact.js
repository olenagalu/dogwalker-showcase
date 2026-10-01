document.querySelector('#contact-form').addEventListener('submit', async event => {
  event.preventDefault(); const form = event.currentTarget; const status = document.querySelector('#contact-status');
  if (!form.reportValidity()) return;
  try { const result = await PrincessApi.request('/api/contact', { method:'POST', body:JSON.stringify(Object.fromEntries(new FormData(form))) }); status.textContent = result.message; status.className = 'form-status success'; form.reset(); }
  catch (error) { status.textContent = error.message; status.className = 'form-status error'; }
});
