function ownerAssistantFeedback(message, type = 'error') {
  const status = document.querySelector('#owner-assistant-status'); status.textContent = message; status.className = `form-status ${type}`;
}
async function loadOwnerAssistants() {
  try {
    const [assistants, invitations] = await Promise.all([PrincessApi.request('/api/assistants'), PrincessApi.request('/api/assistant-invitations')]);
    const list = document.querySelector('#owner-assistant-list'); list.replaceChildren();
    assistants.forEach(assistant => {
      const card = document.createElement('article'); card.className = 'profile-panel';
      const title = document.createElement('h3'); title.textContent = `${assistant.fullName} — ${assistant.status}`;
      const email = document.createElement('p'); email.textContent = assistant.email;
      const toggle = document.createElement('button'); toggle.type = 'button'; toggle.className = 'button'; toggle.textContent = assistant.status === 'Active' ? 'Freeze account' : 'Reactivate account';
      toggle.onclick = async () => { toggle.disabled = true; try { await PrincessApi.request(`/api/assistants/${encodeURIComponent(assistant.id)}/status`, { method:'PUT',body:JSON.stringify({status:assistant.status==='Active'?'Frozen':'Active'}) }); await loadOwnerAssistants(); ownerAssistantFeedback('Account status updated. Existing availability and bookings are preserved.', 'success'); } catch (error) { ownerAssistantFeedback(error.message); toggle.disabled = false; } };
      const availability = document.createElement('details'); const summary = document.createElement('summary'); summary.textContent = 'View / manage availability'; const editor = document.createElement('div'); availability.append(summary, editor);
      availability.addEventListener('toggle', async () => { if (availability.open && !editor.dataset.loaded) { try { await AssistantAvailability(editor, assistant.id, ownerAssistantFeedback); editor.dataset.loaded = 'true'; } catch (error) { ownerAssistantFeedback(error.message); } } });
      card.append(title, email, toggle, availability); list.append(card);
    });
    if (!assistants.length) list.textContent = 'No assistant accounts yet.';
    const pending = document.querySelector('#owner-assistant-invitations'); pending.replaceChildren();
    invitations.forEach(invitation => { const row = document.createElement('p'); row.textContent = `${invitation.fullName ? `${invitation.fullName} · ` : ''}${invitation.email} — pending, expires ${new Date(invitation.expiresAt).toLocaleString()}`; pending.append(row); });
    if (!invitations.length) pending.textContent = 'No pending invitations.';
  } catch (error) { ownerAssistantFeedback(error.message); }
}
document.querySelector('#invite-assistant-form').addEventListener('submit', async event => {
  event.preventDefault(); const form = event.currentTarget; const button = form.querySelector('button'); button.disabled = true;
  try { const result = await PrincessApi.request('/api/assistant-invitations', { method:'POST',body:JSON.stringify(Object.fromEntries(new FormData(form))) }); form.reset(); await loadOwnerAssistants(); ownerAssistantFeedback(result.message, 'success'); }
  catch (error) { ownerAssistantFeedback(error.message); }
  finally { button.disabled = false; }
});
document.querySelector('#refresh-owner').addEventListener('click', loadOwnerAssistants);
if (PrincessApi.user()?.role === 'Owner') loadOwnerAssistants();
