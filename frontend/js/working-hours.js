(async function () {
  const owner = PrincessApi.user()?.role === 'Owner';
  const host = document.querySelector('.availability-page') || document.querySelector('[data-owner-panel-name="availability"]');
  if (!host) return;
  const panel = document.createElement('section');
  panel.className = 'panel-form';
  panel.style.marginBottom = '2rem';
  if (host.matches('.availability-page')) host.append(panel);
  else host.prepend(panel);
  const time = value => value.slice(0, 5);
  const pretty = value => new Date(`2000-01-01T${value}`).toLocaleTimeString('en-US', {hour:'numeric',minute:'2-digit'});
  try {
    let hours = await PrincessApi.request('/api/availability/hours');
    if (!owner) {
      const text = document.createElement('p');
      text.textContent = `Regular working hours: ${pretty(hours.start)}–${pretty(hours.end)} daily.`;
      panel.append(text);
      if (hours.emergencyEnabled) {
        const note = document.createElement('p');
        note.textContent = `Emergency requests: ${pretty(hours.emergencyStart)}–${pretty(hours.emergencyEnd)}. Extra charge: $${Number(hours.emergencySurcharge).toFixed(2)} per service, in addition to the service price. Subject to Demo Owner’s approval.`;
        panel.append(note);
        const link = document.createElement('a'); link.className='button'; link.href='emergency.html'; link.textContent='Request emergency care'; panel.append(link);
      }
      return;
    }
    panel.innerHTML = `<h2>Working hours & emergency care</h2><form id="hours-settings" class="panel-form">
      <label>Daily opening time<input name="start" type="time" required></label>
      <label>Daily closing time<input name="end" type="time" required></label>
      <label>Emergency requests start<input name="emergencyStart" type="time" required></label>
      <label>Emergency requests end<input name="emergencyEnd" type="time" required></label>
      <label>Extra charge per emergency service ($)<input name="emergencySurcharge" type="number" min="0" max="10000" step="0.01" required></label>
      <label>Accept emergency requests<select name="emergencyEnabled"><option value="false">No</option><option value="true">Yes</option></select></label>
      <button class="button">Save working hours</button></form>
      <h2>Block time</h2><p>Choose one date or a range covering days, weeks, or months. Blocks apply to each selected day. Existing bookings are not cancelled.</p>
      <form id="hours-block" class="panel-form">
      <label>First date<input name="from" type="date" required></label><label>Last date (inclusive)<input name="to" type="date" required></label>
      <label>Block duration<select name="duration"><option value="day">Entire days</option><option value="hours">Specific hours each day</option></select></label>
      <label>From<input name="start" type="time" value="07:00"></label><label>Until<input name="end" type="time" value="23:00"></label>
      <label>Private note<input name="notes" maxlength="300"></label><button class="button">Block time</button></form><p role="status" id="hours-feedback"></p>`;
    const form = panel.querySelector('#hours-settings');
    if(!document.querySelector('[data-owner-panel-name]')){
      const manage=document.createElement('a');manage.href='owner.html#availability';manage.textContent='Review, edit, or remove blocked dates in Owner Profile';panel.append(manage);
    }
    const requests = document.createElement('section');
    requests.className='panel-form';
    requests.innerHTML='<h2>Emergency & special requests</h2>';
    const requestHost=document.querySelector('[data-owner-panel-name="requests"]');
    if(requestHost){requestHost.append(requests);PrincessApi.request('/api/contact').then(messages=>{for(const message of messages){const card=document.createElement('article');card.className='mini-card';const title=document.createElement('h3');title.textContent=`${message.name} · ${message.email}`;const copy=document.createElement('p');copy.style.whiteSpace='pre-line';copy.textContent=message.message;card.append(title,copy);requests.append(card);}if(!messages.length)requests.append('No special requests yet.');}).catch(error=>requests.append(error.message));}
    for (const [key,value] of Object.entries(hours)) if(form.elements[key]) form.elements[key].value=typeof value==='string'?time(value):String(value);
    const feedback=message=>panel.querySelector('#hours-feedback').textContent=message;
    form.addEventListener('submit',async event=>{event.preventDefault();const data=Object.fromEntries(new FormData(form));data.emergencySurcharge=Number(data.emergencySurcharge);data.emergencyEnabled=data.emergencyEnabled==='true';try{await PrincessApi.request('/api/availability/hours',{method:'PUT',body:JSON.stringify(data)});feedback('Working hours saved.');if(typeof renderCalendar==='function')await renderCalendar();}catch(error){feedback(error.message);}});
    const block=panel.querySelector('#hours-block');
    const toggle=()=>{for(const name of ['start','end']){block.elements[name].disabled=block.elements.duration.value==='day';block.elements[name].parentElement.hidden=block.elements.duration.value==='day';}};
    block.elements.duration.addEventListener('change',toggle);toggle();
    block.addEventListener('submit',async event=>{event.preventDefault();const data=Object.fromEntries(new FormData(block));if(data.duration==='day'){data.start='00:00';data.end='23:59:59.9999999';}const button=block.querySelector('button');button.disabled=true;try{await PrincessApi.request('/api/availability/blocks',{method:'POST',body:JSON.stringify(data)});feedback('Time blocked. Existing bookings remain unchanged.');if(typeof loadRules==='function')await loadRules();if(typeof renderCalendar==='function')await renderCalendar();}catch(error){feedback(error.message);}finally{button.disabled=false;}});
  } catch(error) { panel.textContent=error.message; }
})();
