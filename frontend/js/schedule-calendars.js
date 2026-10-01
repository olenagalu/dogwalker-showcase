// Shared day and overnight calendar views for owner and assistant dashboards.
document.querySelectorAll('[data-owner-calendar-view]').forEach(button=>button.addEventListener('click',()=>setOwnerCalendarView(button.dataset.ownerCalendarView)));
document.querySelector('#owner-calendar-prev').addEventListener('click',()=>navigateOwnerCalendar(-1));
document.querySelector('#owner-calendar-next').addEventListener('click',()=>navigateOwnerCalendar(1));
document.querySelector('#owner-overnight-prev').addEventListener('click',()=>{ownerOvernightDate=new Date(ownerOvernightDate.getFullYear(),ownerOvernightDate.getMonth()-1,1);renderOvernightCalendar();});
document.querySelector('#owner-overnight-next').addEventListener('click',()=>{ownerOvernightDate=new Date(ownerOvernightDate.getFullYear(),ownerOvernightDate.getMonth()+1,1);renderOvernightCalendar();});
function setOwnerCalendarView(view){ownerCalendarView=view;document.querySelectorAll('[data-owner-calendar-view]').forEach(button=>{const active=button.dataset.ownerCalendarView===view;button.classList.toggle('active',active);button.setAttribute('aria-pressed',String(active));});renderOwnerCalendar();}
function navigateOwnerCalendar(direction){if(ownerCalendarView==='month')ownerCalendarDate=new Date(ownerCalendarDate.getFullYear(),ownerCalendarDate.getMonth()+direction,1);else ownerCalendarDate=new Date(ownerCalendarDate.getFullYear(),ownerCalendarDate.getMonth(),ownerCalendarDate.getDate()+(ownerVisibleDays()*direction));renderOwnerCalendar();}
function serviceColorClass(serviceId){const index=ownerServices.findIndex(service=>service.id===serviceId);return `booking-color-${(index<0?Number(serviceId):index)%6}`;}
function bookingWindows(item){if(!item.isOvernightStay||!item.endDate)return[{date:item.date,start:item.startTime,end:item.endTime,label:''}];const windows=[];let date=new Date(`${item.date}T00:00:00`);const end=new Date(`${item.endDate}T00:00:00`);while(date<end){windows.push({date:formatIso(date),start:'00:00',end:'23:59',label:'Overnight'});date.setDate(date.getDate()+1);}return windows;}
function activeDayBookings(){return ownerBookings.filter(item=>!item.isOvernightStay&&item.status==='Confirmed');}
function rulesForDate(date){const parsed=new Date(`${date}T00:00:00`);return rules.filter(rule=>(rule.specificDate<=date&&(rule.endDate||rule.specificDate)>=date)||(rule.dayOfWeek!==null&&rule.dayOfWeek!==undefined&&(Number(rule.dayOfWeek)===parsed.getDay()||rule.dayOfWeek===['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'][parsed.getDay()])));}
function renderOwnerCalendar(){
  const calendar=document.querySelector('#owner-calendar-grid');
  const legend=document.querySelector('#booking-legend');
  if(!calendar||!legend)return;
  document.querySelector('#owner-calendar-view-label').textContent=ownerCalendarView==='month'?'Month view':(ownerVisibleDays()===2?'Two-day hourly view':'Weekly hourly view');
  const period=ownerCalendarView==='month'?'month':(ownerVisibleDays()===2?'two days':'week');
  document.querySelector('#owner-calendar-prev').setAttribute('aria-label',`Previous ${period}`);
  document.querySelector('#owner-calendar-next').setAttribute('aria-label',`Next ${period}`);
  if(ownerCalendarView==='week')renderOwnerWeek(calendar);else renderOwnerMonth(calendar);
  document.querySelectorAll('[data-owner-calendar-view]').forEach(button=>{const active=button.dataset.ownerCalendarView===ownerCalendarView;button.classList.toggle('active',active);button.setAttribute('aria-pressed',String(active));});
  renderOwnerLegend(legend);
}
function renderOwnerMonth(calendar){
  const year=ownerCalendarDate.getFullYear();const month=ownerCalendarDate.getMonth();
  document.querySelector('#owner-calendar-month').textContent=new Intl.DateTimeFormat('en-US',{month:'long',year:'numeric'}).format(new Date(year,month,1));
  calendar.className='owner-month-calendar';calendar.replaceChildren();appendWeekdayLabels(calendar);
  for(let blank=0;blank<new Date(year,month,1).getDay();blank+=1){const cell=document.createElement('div');cell.className='owner-calendar-day is-empty';calendar.append(cell);}
  const active=activeDayBookings();const daysInMonth=new Date(year,month+1,0).getDate();
  for(let day=1;day<=daysInMonth;day+=1){
    const date=formatIso(new Date(year,month,day));const cell=document.createElement('div');cell.className='owner-calendar-day owner-calendar-zoom';cell.tabIndex=0;cell.setAttribute('role','button');cell.setAttribute('aria-label',`Open week of ${formatDate(date)}`);
    const number=document.createElement('span');number.className='calendar-day-number';number.textContent=day;cell.append(number);
    active.forEach(item=>bookingWindows(item).filter(window=>window.date===date).forEach(window=>cell.append(ownerCalendarEvent(item,window))));
    rulesForDate(date).forEach(rule=>{const block=document.createElement('div');block.className='owner-calendar-block';block.textContent=(rule.startTime.startsWith('00:00')&&rule.endTime.startsWith('23:59'))?'Blocked all day':`Blocked ${formatTime(rule.startTime)}–${formatTime(rule.endTime)}`;cell.append(block);});
    const open=()=>openOwnerWeek(date);cell.addEventListener('click',open);cell.addEventListener('keydown',event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();open();}});calendar.append(cell);
  }
}
function ownerVisibleDays(){return window.matchMedia('(max-width: 720px)').matches?2:7;}
window.matchMedia('(max-width: 720px)').addEventListener('change',()=>renderOwnerCalendar());
function renderOwnerWeek(calendar){
  const count=ownerVisibleDays();
  const start=count===2?new Date(ownerCalendarDate):startOfWeek(ownerCalendarDate);
  const end=new Date(start);end.setDate(start.getDate()+count-1);
  document.querySelector('#owner-calendar-month').textContent=`${formatDate(formatIso(start))} – ${formatDate(formatIso(end))}`;
  calendar.className='owner-hourly-calendar';calendar.replaceChildren();
  calendar.style.setProperty('--day-count',count);
  const header=document.createElement('div');header.className='hourly-header';
  header.append(document.createElement('span'));
  const body=document.createElement('div');body.className='hourly-body';
  const axis=document.createElement('div');axis.className='hourly-axis';
  for(let hour=0;hour<24;hour++){const label=document.createElement('span');label.textContent=formatTime(String(hour).padStart(2,'0')+':00');axis.append(label);}
  body.append(axis);
  const minutes=value=>{const [h,m]=value.split(':').map(Number);return h*60+m;};
  for(let offset=0;offset<count;offset++){
    const current=new Date(start);current.setDate(start.getDate()+offset);const date=formatIso(current);
    const heading=document.createElement('button');heading.type='button';heading.textContent=current.toLocaleDateString('en-US',{weekday:'short',month:'short',day:'numeric'});heading.title='Block time on this date';heading.addEventListener('click',()=>prepareBlockDate(date));header.append(heading);
    const column=document.createElement('div');column.className='hourly-day';column.setAttribute('aria-label',formatDate(date));
    for(const rule of rulesForDate(date)){const block=document.createElement('div');block.className='hourly-block';block.style.top=minutes(rule.startTime)/1440*100+'%';block.style.height=(minutes(rule.endTime)-minutes(rule.startTime))/1440*100+'%';block.textContent='Unavailable';column.append(block);}
    for(const item of activeDayBookings().filter(item=>item.date===date)){
      const event=document.createElement('button');event.type='button';event.className='hourly-event '+serviceColorClass(item.serviceId);
      event.style.top=minutes(item.startTime)/1440*100+'%';event.style.height=Math.max(30,minutes(item.endTime)-minutes(item.startTime))/1440*100+'%';
      event.textContent=`${formatTime(item.startTime)} · ${item.dogName} · ${item.serviceName}${item.assistantName ? ` · Sitter: ${item.assistantName}` : ''}`;
      event.title=`${formatTime(item.startTime)}–${formatTime(item.endTime)} · ${item.customerName}`;
      event.addEventListener('click',()=>{const details=document.querySelector('#daily-event-details');details.replaceChildren(ownerBookingCard(item,true));details.scrollIntoView({behavior:'smooth',block:'center'});});
      column.append(event);
    }
    body.append(column);
  }
  const scroller=document.createElement('div');scroller.className='hourly-scroll';scroller.append(body);
  calendar.append(header,scroller);scroller.scrollTop=7*64;
}
function ownerCalendarEvent(item,window,detailed=false){const event=document.createElement('div');event.className=`owner-calendar-event ${serviceColorClass(item.serviceId)}`;const time=window.label||`${formatTime(window.start)}–${formatTime(window.end)}`;event.textContent=detailed?`${time} · ${item.dogName} · ${item.serviceName}${item.assistantName ? ` · Sitter: ${item.assistantName}` : ''}`:`${window.label||formatTime(window.start)} · ${item.dogName}`;event.title=`${item.serviceName} · ${item.customerName} · ${item.status}`;return event;}
function renderOwnerLegend(legend){const visible=new Map();activeDayBookings().forEach(item=>visible.set(item.serviceId,item.serviceName));legend.replaceChildren();visible.forEach((name,id)=>{const entry=document.createElement('span');entry.className=`legend-item ${serviceColorClass(id)}`;const dot=document.createElement('span');dot.className='legend-dot';entry.append(dot,document.createTextNode(name));legend.append(entry);});const blocked=document.createElement('span');blocked.className='legend-item';const blockedDot=document.createElement('span');blockedDot.className='legend-dot blocked-dot';blocked.append(blockedDot,document.createTextNode('Unavailable'));legend.append(blocked);}
function appendWeekdayLabels(calendar){['Sun','Mon','Tue','Wed','Thu','Fri','Sat'].forEach(day=>{const label=document.createElement('div');label.className='calendar-weekday';label.textContent=day;calendar.append(label);});}
function startOfWeek(value){const date=new Date(value.getFullYear(),value.getMonth(),value.getDate());date.setDate(date.getDate()-date.getDay());return date;}
function openOwnerWeek(date){ownerCalendarDate=new Date(`${date}T00:00:00`);setOwnerCalendarView('week');}
function renderOvernightCalendar(){
  const calendar=document.querySelector('#owner-overnight-calendar');if(!calendar)return;
  const year=ownerOvernightDate.getFullYear();const month=ownerOvernightDate.getMonth();
  document.querySelector('#owner-overnight-month').textContent=new Intl.DateTimeFormat('en-US',{month:'long',year:'numeric'}).format(new Date(year,month,1));
  if(ownerBookingsState!=='ready'){
    const message=empty(ownerBookingsState==='error'?'Overnight stays could not be loaded. Refresh the owner profile to try again.':'Loading overnight stays…');
    message.style.gridColumn='1 / -1';
    message.setAttribute('role','status');
    if(ownerBookingsState==='error')message.append(action('Retry',loadOwner));
    calendar.replaceChildren(message);return;
  }
  calendar.replaceChildren();appendWeekdayLabels(calendar);
  for(let blank=0;blank<new Date(year,month,1).getDay();blank+=1){const cell=document.createElement('div');cell.className='owner-calendar-day is-empty';calendar.append(cell);}
  const overnight=ownerBookings.filter(item=>item.isOvernightStay&&item.endDate&&item.status==='Confirmed');const days=new Date(year,month+1,0).getDate();
  for(let day=1;day<=days;day+=1){const date=formatIso(new Date(year,month,day));const cell=document.createElement('div');cell.className='owner-calendar-day';const number=document.createElement('span');number.className='calendar-day-number';number.textContent=day;cell.append(number);overnight.filter(item=>item.date<=date&&item.endDate>date).forEach(item=>{const stay=document.createElement('div');stay.className=`owner-calendar-event overnight-event ${serviceColorClass(item.serviceId)}`;stay.textContent=`${item.dogName} · ${item.customerName}`;stay.title=`${item.serviceName} · ${formatDate(item.date)}–${formatDate(item.endDate)} · ${item.status}`;cell.append(stay);});calendar.append(cell);}
}
