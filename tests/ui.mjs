import assert from 'node:assert/strict';
import {randomInt} from 'node:crypto';
import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
import {expect} from '../frontend/node_modules/@playwright/test/index.mjs';
import {chromium} from '../frontend/node_modules/playwright/index.mjs';

const base='http://localhost:5080';
const adminPassword=fs.readFileSync(new URL('../docs/DemoCredentials.local.md',import.meta.url),'utf8').match(/Password: ([A-Za-z0-9_-]+)/)?.[1];
const addDays=(s,n)=>{const [y,m,d]=s.slice(0,10).split('-').map(Number);return new Date(Date.UTC(y,m-1,d+n)).toISOString().slice(0,10)};
const browser=await chromium.launch({channel:'msedge',headless:true});
try {
 for(const width of [390,1440]) {
  const context=await browser.newContext({viewport:{width,height:900}}),page=await context.newPage();
  const studentID=String(randomInt(1000000000,9999999999)),partnerID=String(randomInt(1000000000,9999999999)),fullName=`UI Student ${studentID}`;
  const password='CourseProject!2026';
  let previousSchedule=null;
  const today=await (await page.request.get(base+'/api/today')).json();
  const bookingDate=addDays(today.schedule.today,2);
  try {
   const adminLogin=await page.request.post(base+'/api/admin/login',{data:{username:'admin',password:adminPassword}});
   expect(adminLogin.ok()).toBeTruthy();
   const schedules=await (await page.request.get(base+'/api/admin/schedule')).json();
   previousSchedule=schedules.find(s=>s.scheduleDate.slice(0,10)===bookingDate)||null;
   const scheduleSave=await page.request.put(base+'/api/admin/schedule',{data:{scheduleDate:bookingDate,isCourtOpen:true,activityName:'',description:'UI test schedule'}});
   expect(scheduleSave.ok()).toBeTruthy();
   await page.request.post(base+'/api/logout');
   const availability=await (await page.request.get(`${base}/api/availability/${bookingDate}`)).json();
   const slot=availability.slots.find(s=>s.courtID===1&&s.availability==='AVAILABLE');
   assert.ok(slot,'Court 1 should be available two days ahead for UI test');
   await page.goto(base);
   await expect(page.getByRole('button',{name:'จองสนาม',exact:true}).first()).toBeVisible();
   const langButton=width<700?page.locator('button.mobile-language'):page.locator('.nav-wrap nav .language-toggle');
   await langButton.click();
   await expect(page.locator('html')).toHaveAttribute('lang','en');
   if(width<700)await page.getByRole('button',{name:'Menu',exact:true}).click();
   await expect(page.locator('.nav-wrap nav button').filter({hasText:/^Home$/})).toBeVisible();
   await langButton.click();
   await expect(page.locator('html')).toHaveAttribute('lang','th');
   if(width<700)await page.getByRole('button',{name:'เมนู',exact:true}).click();
   const nav=async label=>{if(width<700)await page.getByRole('button',{name:'เมนู',exact:true}).click();await page.locator('.nav-wrap nav button').filter({hasText:new RegExp(`^${label}$`)}).click();};
   await nav('บัญชีนิสิต');
   await page.getByRole('button',{name:'สมัครบัญชี',exact:true}).click();
   const studentField=page.getByLabel('รหัสนิสิตในบัญชี');
   await studentField.fill('12345678901');
   assert.equal(await studentField.evaluate(e=>e.validity.patternMismatch),true,'IDs longer than 10 digits must fail browser validation');
   await studentField.fill(studentID);
   await page.getByLabel('ชื่อ–นามสกุล').fill(fullName);
   await page.getByLabel('รหัสผ่าน (อย่างน้อย 10 ตัวอักษร)').fill(password);
   await page.getByRole('button',{name:'สมัครและเข้าสู่ระบบ',exact:true}).click();
   await expect(page.getByText(new RegExp(studentID))).toBeVisible();
   const partner=await page.request.post(base+'/api/students/register',{data:{studentID:partnerID,fullName:`Partner ${partnerID}`,password}});
   expect(partner.ok()).toBeTruthy();
   await nav('จองสนาม');
   await page.locator('.calendar-step input[type=date]').fill(bookingDate);
   await page.getByRole('button',{name:'ดูคอร์ดว่าง',exact:true}).click();
   await page.locator('.select-courts button').first().click();
   await page.getByRole('button').filter({hasText:`${slot.startTime}–${slot.endTime}`}).first().click();
   await expect(page.getByText(new RegExp(studentID))).toBeVisible();
   await page.getByLabel('รหัสนิสิตผู้เล่นอีกคน').fill(partnerID);
   await page.getByRole('button',{name:'ตรวจสอบคู่จอง',exact:true}).click();
   await expect(page.getByText(`Court 1 · ${slot.startTime}–${slot.endTime}`,{exact:true})).toBeVisible();
   await page.getByRole('button',{name:'ยืนยันการจอง',exact:true}).click();
   await expect(page.getByRole('heading',{name:'จองสนามสำเร็จ!'})).toBeVisible();
   await page.getByRole('button',{name:'สนับสนุนค่าบำรุงสนาม (ไม่บังคับ)'}).click();
   await page.getByRole('button',{name:'฿50',exact:true}).click();
   await page.locator('.modal form button').click();
   await expect(page.getByRole('dialog')).toHaveCount(0);
   await page.getByRole('button',{name:'ดูการจองของฉัน'}).click();
   const card=page.locator('.history-card').filter({hasText:fullName});
   await expect(card).toBeVisible();
   await card.getByRole('button',{name:'ยกเลิกการจอง',exact:true}).click();
   await page.getByRole('button',{name:'ยืนยันยกเลิก',exact:true}).click();
   await expect(page.getByRole('dialog')).toHaveCount(0);
   expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
   console.log(`PASS: student registration, bilingual toggle, advance booking, second-player confirmation, tip, history and cancel at ${width}px`);
  } finally {
   if(previousSchedule){await page.request.post(base+'/api/admin/login',{data:{username:'admin',password:adminPassword}});await page.request.put(base+'/api/admin/schedule',{data:{scheduleDate:bookingDate,isCourtOpen:previousSchedule.isCourtOpen,activityName:previousSchedule.activityName||'',description:previousSchedule.description||''}});}
   else execFileSync('sqlcmd',['-S',process.env.TEST_SQL_SERVER||'localhost','-E','-C','-d','KU_Sports','-Q',`SET QUOTED_IDENTIFIER ON; DELETE dbo.FACILITY_SCHEDULE WHERE ScheduleDate='${bookingDate}' AND NOT EXISTS(SELECT 1 FROM dbo.BOOKINGS WHERE BookingDate='${bookingDate}' AND Status='BOOKED')`,'-b'],{stdio:'ignore'});
   await context.close();
  }
 }
} finally {await browser.close();}
