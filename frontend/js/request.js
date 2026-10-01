const requestForm = document.querySelector('#special-request-form');
const requestStatus = document.querySelector('#special-request-status');
const requestQuery = new URLSearchParams(location.search);
const requestUser = PrincessApi.user();
const requestCheckIn = document.querySelector('#request-check-in');
const requestCheckout = document.querySelector('#request-checkout');
const requestToday = new Date();
const requestTodayIso = new Date(requestToday.getTime() - requestToday.getTimezoneOffset() * 60000).toISOString().split('T')[0];
requestCheckIn.min = requestTodayIso;
requestCheckout.min = requestTodayIso;
requestCheckIn.value = requestQuery.get('checkIn') || '';
requestCheckout.value = requestQuery.get('checkout') || '';
if (requestUser) {
  document.querySelector('#request-name').value = requestUser.fullName || '';
  document.querySelector('#request-email').value = requestUser.email || '';
}
requestCheckIn.addEventListener('change', () => { requestCheckout.min = requestCheckIn.value || requestTodayIso; });
requestForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!requestForm.reportValidity()) return;
  const data = Object.fromEntries(new FormData(requestForm));
  if (data.checkout <= data.checkIn) {
    feedbackRequest('Checkout must be after check-in.', 'error');
    return;
  }
  const button = requestForm.querySelector('button[type="submit"]');
  button.disabled = true;
  const message = `Special overnight request\nDates: ${data.checkIn} through ${data.checkout}\nPhone: ${data.phone}\nDog: ${data.dogName}\nNotes: ${data.notes || 'None'}`;
  try {
    await PrincessApi.request('/api/contact', { method: 'POST', body: JSON.stringify({ name: data.name, email: data.email, message }) });
    requestForm.reset();
    feedbackRequest('Your request was sent to Demo Owner. She’ll contact you personally about the options.', 'success');
  } catch (error) {
    feedbackRequest(error.message, 'error');
  } finally {
    button.disabled = false;
  }
});
function feedbackRequest(message, type) { requestStatus.textContent = message; requestStatus.className = `form-status ${type}`; }
