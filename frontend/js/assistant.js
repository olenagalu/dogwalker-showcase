const assistantUser = PrincessApi.requireUser('Assistant');
const assistantMessage = document.querySelector('#assistant-status');
let assistantPhotoUrl;
function assistantFeedback(message, type = 'error') {
  assistantMessage.textContent = message; assistantMessage.className = `form-status ${type}`;
}
async function assistantLoadProfile() {
  const me = await PrincessApi.request('/api/assistants/me');
  document.querySelector('#assistant-name').textContent = `${me.fullName} · ${me.email} · ${me.status}`;
  const image = document.querySelector('#assistant-photo'); image.hidden = !me.hasProfilePhoto;
  if (assistantPhotoUrl) URL.revokeObjectURL(assistantPhotoUrl);
  if (me.hasProfilePhoto) { assistantPhotoUrl = await PrincessApi.privateImageUrl('/api/users/me/photo'); image.src = assistantPhotoUrl; }
}
function assistantError(error) {
  if ([401,403].includes(error.status)) {
    document.querySelector('#assistant-workspace').hidden = true;
    assistantFeedback('Your session is unavailable or your account is frozen. Contact the owner, or sign in again after reactivation.');
  } else assistantFeedback(error.message);
}
if (assistantUser) (async () => {
  try { await assistantLoadProfile(); await AssistantAvailability(document.querySelector('#assistant-availability'), 'me', assistantFeedback);
    const blockDate = new URLSearchParams(location.search).get('blockDate');
    if (/^\d{4}-\d{2}-\d{2}$/.test(blockDate || '')) {
      const form = document.querySelector('.assistant-block');
      form.elements.date.value = blockDate;
      form.scrollIntoView({ block: 'center' });
    } }
  catch (error) { assistantError(error); }
})();
document.querySelector('#assistant-photo-form').addEventListener('submit', async event => {
  event.preventDefault();
  try { const data = new FormData(); data.append('photo', await PrincessApi.uploadReadyImage(document.querySelector('#photo').files[0])); await PrincessApi.request('/api/assistants/me/photo', { method: 'PUT', body: data }); await assistantLoadProfile(); assistantFeedback('Picture updated.', 'success'); }
  catch (error) { assistantError(error); }
});
document.querySelector('#remove-photo').addEventListener('click', async () => {
  try { await PrincessApi.request('/api/assistants/me/photo', { method: 'DELETE' }); await assistantLoadProfile(); assistantFeedback('Picture removed.', 'success'); }
  catch (error) { assistantError(error); }
});
