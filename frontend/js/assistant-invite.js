const invitationToken = new URLSearchParams(location.hash.slice(1)).get('token');
history.replaceState(null, '', location.pathname);
const invitationForm = document.querySelector('#assistant-setup');
const invitationStatus = document.querySelector('#invite-status');
if (!invitationToken) { invitationForm.hidden = true; invitationStatus.textContent = 'Open the invitation link from your email to set up your account.'; }
invitationForm.addEventListener('submit', async event => {
  event.preventDefault(); const button = invitationForm.querySelector('button'); button.disabled = true;
  try { const result = await PrincessApi.request('/api/assistant-invitations/accept', { method: 'POST', body: JSON.stringify({ ...Object.fromEntries(new FormData(invitationForm)), token: invitationToken }) }); invitationForm.hidden = true; invitationStatus.textContent = result.message; }
  catch (error) { invitationStatus.textContent = error.message; }
  finally { button.disabled = false; }
});
