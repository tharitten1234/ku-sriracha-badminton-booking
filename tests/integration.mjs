import assert from 'node:assert/strict';
import fs from 'node:fs';
import {randomInt} from 'node:crypto';
import {execFileSync} from 'node:child_process';

const base=process.env.TEST_BASE_URL||'http://localhost:5080';
const password=process.env.TEST_ADMIN_PASSWORD||fs.readFileSync(new URL('../docs/DemoCredentials.local.md',import.meta.url),'utf8').match(/Password: ([A-Za-z0-9_-]+)/)?.[1];
function client(){let cookie='';return async(path,method='GET',body)=>{const r=await fetch(base+'/api'+path,{method,headers:{...(cookie?{Cookie:cookie}:{}),...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});const c=r.headers.get('set-cookie');if(c)cookie=c.split(';')[0];const raw=await r.text();let data;try{data=JSON.parse(raw)}catch{data={raw:raw.slice(0,200)}}return{status:r.status,data}}}
const admin=client(),a=client(),b=client(),stranger=client();
const check=(r,status=200)=>{assert.equal(r.status,status,JSON.stringify(r.data));return r.data};
const plusDays=(s,n)=>{const[y,m,d]=s.slice(0,10).split('-').map(Number);return new Date(Date.UTC(y,m-1,d+n)).toISOString().slice(0,10)};
const ids=Array.from({length:4},()=>String(randomInt(1000000000,9999999999)));
const pass='CourseProject!2026';
let created=[];
check(await admin('/admin/login','POST',{username:'admin',password}));
const today=check(await admin('/today'));const day=plusDays(today.schedule.today,1);
const previous=(check(await admin('/admin/schedule'))).find(s=>s.scheduleDate.slice(0,10)===day);
try{
 check(await a('/students/register','POST',{studentID:'12345',fullName:'Invalid ID',password:pass}),400);
 check(await a('/students/register','POST',{studentID:ids[0],fullName:'Invalid password',password:'short'}),400);
 // Create four valid accounts on their own sessions.
 const acc=[a,b,client(),stranger];
 for(let i=0;i<4;i++)check(await acc[i]('/students/register','POST',{studentID:ids[i],fullName:`Student Test ${i+1}`,password:pass}));
 check(await client()('/students/register','POST',{studentID:ids[0],fullName:'Duplicate ID',password:pass}),409);
 // Each account must sign in; the account that books remains the primary student.
 check(await a('/students/login','POST',{studentID:ids[0],password:pass}));
 check(await b('/students/login','POST',{studentID:ids[2],password:pass}));
 check(await stranger('/students/login','POST',{studentID:ids[3],password:pass}));
 check(await client()('/students/login','POST',{studentID:ids[0],password:'incorrect-password'}),401);
 assert.equal(check(await a('/session')).studentId,ids[0]);
 check(await a('/students/partner','POST',{studentID2:ids[0]}),400);
 check(await a('/students/partner','POST',{studentID2:'0000000000'}),400);
 check(await a('/students/partner','POST',{studentID2:ids[1]}));
 check(await b('/students/partner','POST',{studentID2:ids[3]}));
 check(await a('/students/partner','POST',{studentID2:ids[2]}));
 check(await admin('/admin/schedule','PUT',{scheduleDate:day,isCourtOpen:true,activityName:'',description:'Open for advance-booking test'}));
 const availability=check(await a('/availability/'+day));assert.ok(availability.slots.some(s=>s.availability==='AVAILABLE'));
 check(await a('/availability/2099-01-01'),400);
 const court=1,slot=2;
 const occupied=check(await admin('/bookings')).find(x=>x.courtID===court&&x.slotID===slot&&x.bookingDate.slice(0,10)===day&&x.status==='BOOKED');
 if(occupied)throw new Error('Court 1 / 19:00 is already booked tomorrow. Cancel it manually before the test; the test never cancels pre-existing bookings.');
 check(await client()('/bookings','POST',{courtID:court,slotID:slot,bookingDate:day,studentID2:ids[1]}),401);
 const race=await Promise.all([
  a('/bookings','POST',{courtID:court,slotID:slot,bookingDate:day,studentID1:'9999999999',studentID2:ids[1]}),
  b('/bookings','POST',{courtID:court,slotID:slot,bookingDate:day,studentID2:ids[3]})
 ]);
 assert.deepEqual(race.map(r=>r.status).sort(),[200,409]);
 const winner=race.find(r=>r.status===200).data;created.push(winner.bookingID);
 const owner=race[0].status===200?a:b,loser=race[0].status===200?b:a;
 assert.equal(winner.studentID1,race[0].status===200?ids[0]:ids[2]);
 check(await owner('/bookings','POST',{courtID:court,slotID:slot,bookingDate:day,studentID2:ids[1]}),409);
 assert.ok(check(await owner('/bookings')).some(x=>x.bookingID===winner.bookingID));
 check(await loser(`/bookings/${winner.bookingID}/cancel`,'POST'),404);
 check(await loser(`/bookings/${winner.bookingID}/donations`,'POST',{amount:50}),404);
 check(await owner('/admin/reports'),403);
 assert.ok(check(await admin('/admin/logs')).some(x=>x.bookingID===winner.bookingID&&x.actionType==='CREATED'));
 const before=check(await admin(`/admin/reports?from=${day}&to=${day}`));
 assert.equal(before.daily.length,1);assert.equal(before.daily[0].bookedCount,1);assert.equal(before.daily[0].courtCount,1);
 check(await owner(`/bookings/${winner.bookingID}/donations`,'POST',{amount:0}),400);
 check(await owner(`/bookings/${winner.bookingID}/donations`,'POST',{amount:50}));
 const after=check(await admin(`/admin/reports?from=${day}&to=${day}`));assert.equal(after.daily[0].tipTotal-before.daily[0].tipTotal,50);assert.equal(after.daily[0].tipCount-before.daily[0].tipCount,1);
 check(await owner(`/bookings/${winner.bookingID}/cancel`,'POST'));
 const cancelled=check(await admin(`/admin/reports?from=${day}&to=${day}`));assert.equal(cancelled.daily[0].bookedCount,before.daily[0].bookedCount-1);assert.equal(cancelled.daily[0].cancelledCount,before.daily[0].cancelledCount+1);
 assert.ok(check(await admin('/admin/logs')).some(x=>x.bookingID===winner.bookingID&&x.oldStatus==='BOOKED'&&x.newStatus==='CANCELLED'));
 assert.equal(check(await a('/availability/'+day)).slots.find(x=>x.courtID===court&&x.slotID===slot).availability,'AVAILABLE');
 const again=check(await a('/bookings','POST',{courtID:court,slotID:slot,bookingDate:day,studentID2:ids[1]}));created.push(again.bookingID);
 check(await a(`/bookings/${again.bookingID}/cancel`,'POST'));
 check(await admin('/admin/schedule','PUT',{scheduleDate:day,isCourtOpen:false,activityName:'University sports event',description:'Closed test'}));
 assert.ok(check(await a('/availability/'+day)).slots.every(s=>s.availability==='CLOSED'));
 check(await a('/bookings','POST',{courtID:court,slotID:slot,bookingDate:day,studentID2:ids[1]}),409);
 check(await admin('/admin/schedule','PUT',{scheduleDate:day,isCourtOpen:true,activityName:'Event',description:''}),400);
 console.log('PASS: account registration and sign-in, account-bound primary ID, partner confirmation, advance booking, race protection, ownership, daily court/tip reports, cancellation and closed facility.');
}finally{
 for(const id of created)await admin(`/bookings/${id}/cancel`,'POST');
 if(previous)await admin('/admin/schedule','PUT',{scheduleDate:day,isCourtOpen:previous.isCourtOpen,activityName:previous.activityName||'',description:previous.description||''});
 else execFileSync('sqlcmd',['-S',process.env.TEST_SQL_SERVER||'localhost','-E','-C','-d','KU_Sports','-Q',`SET QUOTED_IDENTIFIER ON; DELETE dbo.FACILITY_SCHEDULE WHERE ScheduleDate='${day}' AND NOT EXISTS(SELECT 1 FROM dbo.BOOKINGS WHERE BookingDate='${day}' AND Status='BOOKED')`,'-b'],{stdio:'ignore'});
}
