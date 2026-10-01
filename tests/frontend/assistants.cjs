// NODE_PATH=/path/to/playwright/node_modules node --test tests/frontend/assistants.cjs
const {test} = require('node:test');
const assert = require('node:assert/strict');
const {chromium} = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');

async function setup(t, role, pageName, frozen = false) {
  const browser = await chromium.launch({headless:true, channel:process.env.PLAYWRIGHT_CHANNEL || 'chrome'});
  t.after(() => browser.close());
  const page = await browser.newPage({viewport:{width:1100,height:850}});
  const errors = [], requests = [];
  let rules = [{id:1,dayOfWeek:'Monday',specificDate:null,startTime:'08:00:00',endTime:'17:00:00',isAvailable:true}];
  let status = 'Active';
  page.on('pageerror', e => errors.push(e.message));
  await page.addInitScript(role => {
    if(role && !sessionStorage.getItem('test-session-initialized')) { sessionStorage.setItem('test-session-initialized','1'); sessionStorage.setItem('dogwalkerShowcaseUser',JSON.stringify({id:'sitter',role,fullName:'Alex'})); sessionStorage.setItem('dogwalkerShowcaseToken','test'); }
  }, role);
  await page.route('**/*', async route => {
    const req = route.request(), url = new URL(req.url()), p = url.pathname;
    if(p.startsWith('/api/')) {
      requests.push({path:p,search:url.search,method:req.method(),data:req.postDataJSON?.()});
      if(frozen && (p.startsWith('/api/assistants/me') || p === '/api/assistants' || p === '/api/assistant-invitations')) return route.fulfill({status:401,json:{message:'Unavailable'}});
      if(p === '/api/assistants/sitter/status') {status = req.postDataJSON().status; return route.fulfill({status:204});}
      if(p === '/api/assistant-invitations' && req.method()==='POST') return route.fulfill({json:{message:'Invitation sent. It expires in 48 hours.'}});
      if(p === '/api/assistant-invitations/accept') return route.fulfill({json:{message:'Assistant account created. Sign in with your email and password.'}});
      if(p.endsWith('/availability/weekly')) return route.fulfill({status:204});
      if(p.endsWith('/availability/blocks')) { const d=req.postDataJSON();rules.push({id:2,specificDate:d.date,startTime:d.startTime||'00:00:00',endTime:d.endTime||'23:59:59.9999999',isAvailable:false});return route.fulfill({json:rules.at(-1)}); }
      if(p === '/api/bookings' && req.method()==='POST') return route.fulfill({json:{id:25}});
      const data = p === '/api/users/me' ? {id:'client',fullName:'Client',email:'client@example.test',phone:'',serviceArea:'Area',serviceAddress:'Address',role:'Customer',approvalStatus:'Approved'}
        : p === '/api/assistants/me' ? {id:'sitter',fullName:'Alex Sitter',email:'alex@example.test',status:'Active',hasProfilePhoto:false}
        : p === '/api/assistants' ? [{id:'sitter',fullName:'Alex Sitter',email:'alex@example.test',status,hasProfilePhoto:false}]
        : p === '/api/assistants/public' ? [{id:'sitter',fullName:'Alex Sitter',hasProfilePhoto:false}]
        : p.endsWith('/availability') && p.startsWith('/api/assistants/') ? rules
        : p === '/api/assistant-invitations' ? [{fullName:'Alex Invited',email:'new@example.test',expiresAt:'2026-10-01T12:00:00Z'}]
        : p === '/api/services' ? [{id:1,name:'Walk',durationMinutes:60,price:25,isActive:true,isOvernightStay:false},{id:4,name:'Overnight',durationMinutes:660,price:95,isActive:true,isOvernightStay:true}]
        : p === '/api/dogs' ? [{id:1,name:'Buddy'}]
        : p === '/api/availability/overnight' ? {isAvailable:!url.searchParams.get('assistantId')}
        : p === '/api/availability/slots' ? [{date:'2027-01-04',startTime:'09:00:00',endTime:'10:00:00'}]
        : p === '/api/availability/hours' ? {regularStart:'08:00',regularEnd:'17:00',emergencyStart:'17:00',emergencyEnd:'22:00'}
        : [];
      return route.fulfill({json:data});
    }
    if(url.hostname !== 'localhost') return route.abort();
    try {const file=path.resolve(__dirname,'../../frontend',p.slice(1));return route.fulfill({body:await fs.readFile(file),contentType:file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'text/html'});}
    catch {return route.fulfill({status:404});}
  });
  await page.goto(`http://localhost/${pageName}`);
  return {page,requests,errors};
}

test('assistant edits own weekly schedule and full-day blocks', async t => {
  const {page,requests,errors}=await setup(t,'Assistant','assistant.html');
  await page.waitForSelector('.assistant-day');
  assert.equal(await page.locator('.assistant-days input[type=checkbox]').count(),7);
  await page.getByRole('button',{name:'Save weekly schedule'}).click();
  await page.waitForFunction(()=>document.querySelector('#assistant-status').textContent==='Saved.');
  const weekly=requests.find(r=>r.path.endsWith('/weekly'));
  assert.equal(weekly.path,'/api/assistants/me/availability/weekly');assert.equal(weekly.data.length,7);
  await page.locator('.assistant-block input[name=date]').fill('2027-01-04');
  await page.getByRole('button',{name:'Save block',exact:true}).click();
  await page.getByRole('button',{name:'Edit',exact:true}).waitFor();
  assert.equal(requests.find(r=>r.path.endsWith('/blocks')).data.startTime,null);
  await page.getByRole('button',{name:'Edit',exact:true}).click();
  assert.equal(await page.locator('.assistant-block input[name=startTime]').inputValue(),'');
  await page.screenshot({path:'/tmp/assistant-dashboard.png',fullPage:true});
  await page.setViewportSize({width:375,height:812});
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  assert.equal(await page.locator('#assistant-photo').isVisible(),false);
  assert.deepEqual(errors,[]);
});

test('frozen dashboard hides all assistant actions',async t=>{
  const {page,errors}=await setup(t,'Assistant','assistant.html',true);
  await page.waitForURL('**/auth.html?sessionExpired=1&returnTo=assistant.html');
  assert.match(await page.locator('#auth-status').innerText(),/sign in again/);
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('dogwalkerShowcaseToken')),null);
  assert.deepEqual(errors,[]);
});

test('owner invites, freezes, reactivates and manages a scoped schedule',async t=>{
  const {page,requests,errors}=await setup(t,'Owner','owner.html#assistants');
  const sections = await page.locator('[data-owner-panel]').evaluateAll(buttons => buttons.map(b => b.dataset.ownerPanel));
  assert.equal(sections.at(-2),'assistants'); assert.equal(sections.at(-1),'history');
  const assistantLabel = page.locator('[data-owner-panel=assistants] strong');
  const historyLabel = page.locator('[data-owner-panel=history] strong');
  assert.equal(await assistantLabel.evaluate(n=>getComputedStyle(n).fontSize),await historyLabel.evaluate(n=>getComputedStyle(n).fontSize));
  await page.getByRole('button',{name:'Freeze account',exact:true}).click();
  await page.getByRole('button',{name:'Reactivate account',exact:true}).click();
  await page.getByRole('button',{name:'Freeze account',exact:true}).waitFor();
  await page.locator('#assistant-name').fill('Alex Invited');
  await page.locator('#assistant-email').fill('invite@example.test');
  await page.getByRole('button',{name:'Invite Assistant',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('#owner-assistant-status').textContent.includes('Invitation sent'));
  assert.match(await page.locator('#owner-assistant-invitations').innerText(),/Alex Invited · new@example.test/);
  assert.equal(requests.find(r=>r.path==='/api/assistant-invitations'&&r.method==='POST').data.fullName,'Alex Invited');
  await page.getByText('View / manage availability', {exact:true}).click();
  await page.getByRole('button',{name:'Save weekly schedule'}).waitFor();
  assert.ok(requests.some(r=>r.path==='/api/assistants/sitter/availability'));
  assert.deepEqual(requests.filter(r=>r.path.endsWith('/status')).map(r=>r.data.status),['Frozen','Active']);
  assert.deepEqual(errors,[]);
});

test('invitation fragment is removed and submitted only in the setup POST',async t=>{
  const {page,requests,errors}=await setup(t,null,'assistant-invite.html#token=secret-test');
  assert.equal(new URL(page.url()).hash,'');
  await page.locator('#name').fill('New Assistant');await page.locator('#email').fill('new@example.test');await page.locator('#password').fill('password123');
  await page.getByRole('button',{name:'Create assistant account'}).click();
  await page.waitForFunction(()=>document.querySelector('#assistant-setup').hidden);
  assert.equal(requests.find(r=>r.path.endsWith('/accept')).data.token,'secret-test');assert.deepEqual(errors,[]);
});

test('customer selects sitter and sends their identity with booking',async t=>{
  const {page,requests,errors}=await setup(t,'Customer','book.html');
  await page.getByLabel('Alex Sitter').check();
  await page.locator('#book-service').selectOption('1');await page.locator('#book-dog').selectOption('1');await page.locator('#book-date').fill('2027-01-04');
  await page.waitForFunction(()=>!document.querySelector('#book-time').disabled);
  await page.locator('#book-time').selectOption('09:00:00');
  await page.getByRole('button',{name:'Review booking'}).click();
  assert.match(await page.locator('#review-details').innerText(),/Alex Sitter/);
  await page.getByRole('button',{name:'Submit booking request'}).click();
  await page.waitForURL('**/dashboard.html?booked=25');
  assert.equal(requests.find(r=>r.path==='/api/bookings'&&r.method==='POST').data.assistantId,'sitter');
  assert.ok(requests.some(r=>r.path==='/api/availability/slots'&&r.search.includes('assistantId=sitter')));
  // Dashboard APIs are intentionally not modeled by this booking test.
  assert.deepEqual(errors,[]);
});

test('overnight review requires availability for the selected sitter',async t=>{
  const {page,requests,errors}=await setup(t,'Customer','book.html');
  await page.locator('#book-service').selectOption('4');
  await page.locator('#book-date').fill('2027-01-04');
  await page.locator('#book-end-date').fill('2027-01-06');
  await page.waitForFunction(()=>document.querySelector('#booking-status').textContent.includes('is available'));
  await page.getByLabel('Alex Sitter').check();
  await page.waitForFunction(()=>document.querySelector('#booking-status').textContent.includes('is unavailable'));
  assert.equal(await page.getByRole('button',{name:'Review booking'}).isDisabled(),true);
  assert.ok(requests.some(r=>r.path==='/api/availability/overnight'&&r.search.includes('assistantId=sitter')));
  assert.deepEqual(errors,[]);
});

test('expired owner session returns to sign-in and preserves the assistants section',async t=>{
  const {page,errors}=await setup(t,'Owner','owner.html#assistants',true);
  await page.waitForURL('**/auth.html?sessionExpired=1&returnTo=owner.html%23assistants');
  assert.match(await page.locator('#auth-status').innerText(),/sign in again/);
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('dogwalkerShowcaseUser')),null);
  await page.route('**/api/auth/login',route=>route.fulfill({status:401,json:{message:'Email or password is incorrect.'}}));
  await page.locator('#login-panel input[name=email]').fill('owner@example.test');
  await page.locator('#login-panel input[name=password]').fill('wrong-password');
  await page.locator('#login-panel button[type=submit]').click();
  await page.waitForFunction(()=>document.querySelector('#auth-status').textContent==='Email or password is incorrect.');
  assert.ok(page.url().includes('auth.html'));
  await page.route('**/api/auth/login',route=>route.fulfill({json:{token:'fresh-token',user:{id:'owner',role:'Owner',fullName:'Owner'}}}));
  // Make the destination load successfully after reauthentication.
  await page.route('**/api/assistants',route=>route.fulfill({json:[]}));
  await page.route('**/api/assistant-invitations',route=>route.fulfill({json:[]}));
  await page.locator('#login-panel button[type=submit]').click();
  await page.waitForURL('**/owner.html#assistants');
  await page.getByRole('button',{name:'Invite Assistant',exact:true}).waitFor();
  assert.deepEqual(errors,[]);
});
