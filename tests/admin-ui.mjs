import fs from 'node:fs';
import {expect} from '../frontend/node_modules/@playwright/test/index.mjs';
import {chromium} from '../frontend/node_modules/playwright/index.mjs';
const password=fs.readFileSync(new URL('../docs/DemoCredentials.local.md',import.meta.url),'utf8').match(/Password: ([A-Za-z0-9_-]+)/)?.[1];
if(!password)throw new Error('Demo admin password was not found in ignored local credentials file.');
const browser=await chromium.launch({channel:'msedge',headless:true}),context=await browser.newContext({viewport:{width:390,height:900}}),page=await context.newPage();
try {
 await page.goto('http://localhost:5080');
 await page.getByRole('button',{name:'เมนู',exact:true}).click();
 await page.locator('.nav-wrap nav button').filter({hasText:/^สำหรับเจ้าหน้าที่$/}).click();
 await page.getByLabel('รหัสผ่าน (อย่างน้อย 10 ตัวอักษร)',{exact:true}).fill(password);
 await page.getByRole('button',{name:'เข้าสู่ระบบ',exact:true}).click();
 await expect(page.getByText('การจองสะสม',{exact:true})).toBeVisible();
 await page.getByRole('button',{name:'รายงาน',exact:true}).click();
 await expect(page.getByRole('heading',{name:'รายงานรายวัน'}).first()).toBeVisible();
 await expect(page.getByText('คอร์ดที่จอง',{exact:true})).toBeVisible();
 for(const width of [320,390,768,1440]) {await page.setViewportSize({width,height:900});const scroll=await page.evaluate(()=>document.documentElement.scrollWidth);expect(scroll<=width).toBeTruthy();}
 await page.getByRole('button',{name:'ตารางสนาม',exact:true}).click();
 await expect(page.getByRole('heading',{name:'จัดการตารางสนาม'})).toBeVisible();
 await page.getByRole('button',{name:'จัดการคอร์ด',exact:true}).click();
 await expect(page.getByRole('heading',{name:'จัดการคอร์ด'})).toBeVisible();
 await page.getByRole('button',{name:'ประวัติระบบ',exact:true}).click();
 await expect(page.getByRole('heading',{name:'ประวัติระบบ'})).toBeVisible();
 console.log('PASS: admin login and reports, schedule/court/audit screens, responsive 320/390/768/1440px.');
} finally {await browser.close();}

