// Shared by the assistant dashboard and owner management; the API enforces scope.
window.AssistantAvailability = async function (host, id, report) {
  const base = `/api/assistants/${encodeURIComponent(id)}/availability`;
  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  let rules = await PrincessApi.request(base);
  host.innerHTML = `<h3>Weekly availability</h3><p>Days marked unavailable cannot be booked. Times use the business’s local time.</p><form class="assistant-weekly"><div class="assistant-days"></div><button class="button" type="submit">Save weekly schedule</button></form><h3>Specific date / time blocks</h3><p>Leave both times blank to block whole days. The end date is inclusive; leave it blank for one day. Times apply on each date in the range. Add as many separate blocks as needed. Customers only see whether a time is available.</p><form class="assistant-block form-grid"><input type="hidden" name="blockId"><div class="field"><label>Start date<input type="date" name="date" required></label></div><div class="field"><label>End date (optional)<input type="date" name="endDate"></label></div><div class="field"><label>Start (optional)<input type="time" name="startTime"></label></div><div class="field"><label>End (optional)<input type="time" name="endTime"></label></div><button class="button" type="submit">Save block</button><button class="link-button" type="reset">Clear</button></form><div class="assistant-blocks"></div>`;
  const weekly = host.querySelector('.assistant-weekly');
  days.forEach((day, index) => {
    const saved = rules.filter(r => r.dayOfWeek === day && r.isAvailable).sort((a, b) => a.startTime.localeCompare(b.startTime));
    const row = document.createElement('div'); row.className = 'assistant-day';
    row.innerHTML = `<label><input type="checkbox" name="available-${index}"> ${day}</label><div class="assistant-intervals"></div><button class="link-button" type="button">Add time interval</button>`;
    const checkbox = row.querySelector('input[type=checkbox]');
    const intervals = row.querySelector('.assistant-intervals');
    const add = row.querySelector('button');
    checkbox.checked = saved.length > 0;
    function toggle() {
      intervals.querySelectorAll('input, button').forEach(input => input.disabled = !checkbox.checked);
    }
    function addInterval(start = '08:00', end = '17:00') {
      const interval = document.createElement('div'); interval.className = 'assistant-interval';
      interval.innerHTML = '<label>From <input type="time" class="interval-start" required></label><label>To <input type="time" class="interval-end" required></label><button class="link-button" type="button">Remove</button>';
      interval.querySelector('.interval-start').value = start.slice(0, 5);
      interval.querySelector('.interval-end').value = end.slice(0, 5);
      interval.querySelector('.interval-start').setAttribute('aria-label', `${day} interval start`);
      interval.querySelector('.interval-end').setAttribute('aria-label', `${day} interval end`);
      interval.querySelector('button').onclick = () => {
        interval.remove();
        if (!intervals.children.length) { checkbox.checked = false; addInterval(); toggle(); }
      };
      intervals.append(interval);
    }
    if (saved.length) saved.forEach(rule => addInterval(rule.startTime, rule.endTime));
    else addInterval();
    add.setAttribute('aria-label', `Add time interval for ${day}`);
    add.onclick = () => { if (checkbox.checked) addInterval(); checkbox.checked = true; toggle(); };
    checkbox.addEventListener('change', toggle); toggle(); weekly.querySelector('.assistant-days').append(row);
  });
  async function run(action) { try { await action(); report('Saved.', 'success'); } catch (error) { report(error.message, 'error'); } }
  weekly.addEventListener('submit', event => {
    event.preventDefault();
    const payload = days.flatMap((day, i) => {
      const row = weekly.querySelectorAll('.assistant-day')[i];
      if (!weekly.elements[`available-${i}`].checked)
        return [{ dayOfWeek: day, isAvailable: false, startTime: '08:00', endTime: '17:00' }];
      return [...row.querySelectorAll('.assistant-interval')].map(interval => ({
        dayOfWeek: day, isAvailable: true,
        startTime: interval.querySelector('.interval-start').value,
        endTime: interval.querySelector('.interval-end').value
      }));
    });
    run(() => PrincessApi.request(`${base}/weekly`, { method: 'PUT', body: JSON.stringify(payload) }));
  });
  const form = host.querySelector('.assistant-block');
  async function refreshBlocks() { rules = await PrincessApi.request(base); renderBlocks(); }
  function renderBlocks() {
    const list = host.querySelector('.assistant-blocks'); list.replaceChildren();
    rules.filter(r => r.specificDate).sort((a, b) => a.specificDate.localeCompare(b.specificDate) || a.startTime.localeCompare(b.startTime)).forEach(rule => {
      const row = document.createElement('div'); row.className = 'assistant-day';
      const whole = rule.startTime === '00:00:00' && rule.endTime.startsWith('23:59:59');
      const label = document.createElement('span');
      label.textContent = `${rule.specificDate}${rule.endDate && rule.endDate !== rule.specificDate ? ' – ' + rule.endDate : ''}: ${whole ? 'All day' : rule.startTime.slice(0,5) + '–' + rule.endTime.slice(0,5)}`;
      const edit = document.createElement('button'); edit.type = 'button'; edit.className = 'link-button'; edit.textContent = 'Edit';
      edit.onclick = () => { form.elements.blockId.value = rule.id; form.elements.date.value = rule.specificDate; form.elements.endDate.value = rule.endDate || ''; form.elements.startTime.value = whole ? '' : rule.startTime.slice(0,5); form.elements.endTime.value = whole ? '' : rule.endTime.slice(0,5); form.elements.date.focus(); };
      const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'link-button'; remove.textContent = 'Remove';
      remove.onclick = () => run(async () => { await PrincessApi.request(`${base}/blocks/${rule.id}`, { method: 'DELETE' }); await refreshBlocks(); });
      row.append(label, edit, remove); list.append(row);
    });
  }
  form.addEventListener('reset', () => { form.elements.blockId.value = ''; });
  form.addEventListener('submit', event => {
    event.preventDefault(); const data = Object.fromEntries(new FormData(form));
    run(async () => { await PrincessApi.request(`${base}/blocks${data.blockId ? `/${data.blockId}` : ''}`, { method: data.blockId ? 'PUT' : 'POST', body: JSON.stringify({date:data.date,endDate:data.endDate||null,startTime:data.startTime||null,endTime:data.endTime||null}) }); form.reset(); await refreshBlocks(); });
  });
  renderBlocks();
};
