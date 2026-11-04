import assert from 'node:assert/strict';
import {randomInt} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {chromium} from '../frontend/node_modules/playwright/index.mjs';
const base=process.env.TEST_BASE_URL,db=process.env.TEST_SQL_DATABASE;
assert.ok(base&&new URL(base).port==='5082'&&db?.startsWith('KU_Sports_Regression_'),'Run only on port 5082 with an isolated regression database');
const browser=await chromium.launch({channel:'msedge',headless:true});
const pass='RegressionStudent!2026';let checks=0;
const plus=(d,n)=>new Date(new Date(d.slice(0,10)+'T12:00:00Z').getTime()+n*86400000).toISOString().slice(0,10);
try{
 const admin=await browser.newContext();
 assert.equal((await admin.request.post(base+'/api/admin/login',{data:{username:'admin',password:process.env.TEST_ADMIN_PASSWORD}})).status(),200);
 const dayData=await (await admin.request.get(base+'/api/today')).json();const today=dayData.schedule.today.slice(0,10),future=plus(today,2),closed=plus(today,3);
 for(const [date,open]of [[today,true],[future,true],[closed,false]])assert.ok((await admin.request.put(base+'/api/admin/schedule',{data:{scheduleDate:date,isCourtOpen:open,activityName:open?'':'Regression Event',description:''}})).ok());
 const snapshots=[];
 for(const width of [390,1440]){
  const context=await browser.newContext({viewport:{width,height:960}});const page=await context.newPage();const pageErrors=[];page.on('pageerror',e=>pageErrors.push(e.message));
  await page.goto(base,{waitUntil:'networkidle'});
  await page.locator(width<700?'.mobile-language':'nav .language-toggle').click();
  assert.equal(await page.locator('html').getAttribute('lang'),'en');
  const nav=async name=>{if(width<700)await page.getByRole('button',{name:'Menu',exact:true}).click();await page.locator('nav').getByRole('button',{name,exact:true}).click()};
  await nav('Book a court');await page.getByLabel('Choose a date',{exact:true}).fill(closed);
  await Promise.all([page.waitForResponse(r=>r.url().includes('/api/availability/')),page.getByRole('button',{name:'Show availability',exact:true}).click()]);
  await page.getByRole('heading',{name:'Regression Event',exact:true}).waitFor();
  await nav('Home');assert.match(await page.locator('.today-bar').innerText(),/Open for booking/);checks++;
  await nav('Book a court');await page.getByLabel('Choose a date',{exact:true}).fill(future);
  await Promise.all([page.waitForResponse(r=>r.url().includes('/api/availability/')),page.getByRole('button',{name:'Show availability',exact:true}).click()]);
  await page.locator('.select-courts button:enabled').first().click();await page.locator('.slot-grid button:enabled').first().click();
  assert.equal(await page.locator('.booking-panel .account-tabs').count(),1);checks++;
  await page.getByRole('button',{name:'Create account',exact:true}).click();
  const id=String(randomInt(1000000000,9999999999)),partner=String(randomInt(1000000000,9999999999));
  await page.getByLabel('Signed-in student ID',{exact:true}).fill(id);await page.getByLabel('Full name',{exact:true}).fill('Regression UI');await page.getByLabel('Password (at least 10 characters)',{exact:true}).fill(pass);
  await page.getByRole('button',{name:'Create account & sign in',exact:true}).click();
  await page.getByRole('button',{name:'Verify second student',exact:true}).waitFor();assert.match(await page.locator('.confirmation').innerText(),new RegExp(id));checks++;
  await page.getByRole('textbox',{name:'Second student ID',exact:true}).fill('0000000000');await page.getByRole('button',{name:'Verify second student',exact:true}).click();
  await page.locator('.alert.error').waitFor();assert.match(await page.locator('.alert.error').innerText(),/active account/);assert.deepEqual(pageErrors,[]);checks++;
  assert.ok((await admin.request.post(base+'/api/students/register',{data:{studentID:partner,fullName:'Regression Partner',password:pass}})).ok());
  await page.getByRole('textbox',{name:'Second student ID',exact:true}).fill(partner);await page.getByRole('button',{name:'Verify second student',exact:true}).click();
  await page.getByRole('button',{name:'Confirm booking',exact:true}).waitFor();await page.getByRole('button',{name:'Confirm booking',exact:true}).click();
  await page.getByRole('heading',{name:'Booking confirmed!',exact:true}).waitFor();checks++;
  const records=await (await context.request.get(base+'/api/bookings')).json();const booking=records.find(b=>b.status==='BOOKED');assert.equal(booking.studentID1,id);
  assert.equal((await context.request.get(base+'/api/admin/reports')).status(),403);checks++;
  const empty=await (await admin.request.get(base+'/api/admin/reports?from=2000-01-01&to=2000-01-01')).json();assert.ok(empty.courts.every(c=>c.bookingCount===0));assert.equal(empty.mostUsed.length,0);checks++;
  await page.getByRole('button',{name:'Support court maintenance (optional)',exact:true}).click();await page.getByRole('button',{name:/Save tip/}).click();
  await page.locator('[role="dialog"]').waitFor({state:'hidden'});
  assert.equal(await page.locator('.alert.error').count(),0,'No visible errors after a successful tip');const report=await (await admin.request.get(base+`/api/admin/reports?from=${future}&to=${future}`)).json();assert.equal(report.courts.reduce((s,c)=>s+c.bookingCount,0),report.daily[0].bookedCount);assert.ok(report.daily[0].tipTotal>=50);checks++;
  await nav('My bookings');await page.locator('.history-card').first().waitFor();assert.equal(await page.locator('.history-card .badge').first().innerText(),'Booked');
  await page.getByRole('button',{name:'Cancel booking',exact:true}).first().click();await page.getByRole('button',{name:'Confirm cancellation',exact:true}).click();
  await page.locator('[role="dialog"]').waitFor({state:'hidden'});await page.locator('.history-card .badge').filter({hasText:'Cancelled'}).first().waitFor();assert.equal(await page.locator('.history-card .badge').first().innerText(),'Cancelled');checks++;
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);assert.deepEqual(pageErrors,[]);checks++;
  snapshots.push({id,partner,bookingID:booking.bookingID});await context.close();
 }
 const user=await browser.newContext();await user.request.post(base+'/api/students/login',{data:{studentID:snapshots[0].id,password:pass}});
 execFileSync('sqlcmd',['-S','localhost','-E','-C','-I','-b','-d',db,'-Q',"UPDATE dbo.TIME_SLOTS SET StartTime='00:00',EndTime='01:00' WHERE SlotID=4;"],{stdio:'ignore'});
 const availability=await (await user.request.get(base+'/api/availability/'+today)).json();assert.ok(availability.slots.filter(s=>s.slotID===4).every(s=>s.availability==='CLOSED'));
 const past=await user.request.post(base+'/api/bookings',{headers:{'Accept-Language':'en'},data:{courtID:1,slotID:4,bookingDate:today,studentID2:snapshots[0].partner}});assert.equal(past.status(),409);assert.match((await past.json()).message,/already started/);checks++;
 const th=await user.request.post(base+'/api/students/register',{headers:{'Accept-Language':'th'},data:{studentID:'123',fullName:'x',password:pass}});assert.equal(th.status(),400);assert.match((await th.json()).message,/รหัสนิสิต/);checks++;
 const logs=await (await admin.request.get(base+'/api/admin/logs')).json();for(const x of snapshots){assert.ok(logs.some(l=>l.bookingID===x.bookingID&&l.actionType==='CREATED'));assert.ok(logs.some(l=>l.bookingID===x.bookingID&&l.newStatus==='CANCELLED'))}checks++;
 console.log(`PASS ${checks} presentation regression checks: inline signup/login resumes booking, partner error visible, correct today state, date-filtered charts, tip/cancel/audit, Thai validation, expired slots, role checks, 390px and 1440px.`);
}finally{await browser.close()}



