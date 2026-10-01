# KU Sriracha Badminton Court Booking Management System

เว็บไซต์จองสนามแบดมินตันสำหรับวิชา Database System — React/Vite + ASP.NET Core 10 + Microsoft SQL Server 2019 ขึ้นไป ใช้ฐานข้อมูลจริง ไม่มีฐานข้อมูลจำลองใน frontend

## เริ่มใช้งานบนเครื่องนี้

เปิด PowerShell ที่โฟลเดอร์โปรเจกต์ แล้วรัน:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-Demo.ps1
```

เปิด http://localhost:5080 บัญชี `admin` ใช้รหัสผ่านใน `docs/DemoCredentials.local.md` (ไฟล์นี้ไม่อยู่ใน git) ถ้าไม่มีตารางเวลา ให้ Admin บันทึกตารางจริงของวันที่ต้องการเปิดก่อน เพราะไม่มี Facility Schedule หมายถึงปิดสนาม สมัคร Student ด้วยรหัสนิสิต 10 หลักและรหัสผ่านอย่างน้อย 10 ตัวอักษร บัญชีใช้ได้ทันที จากนั้นเลือกระหว่างวันนี้ถึง 30 วันข้างหน้า ใช้ ID ของบัญชีที่เข้าสู่ระบบเป็นผู้จองหลัก และระบุ ID ของผู้เล่นคนที่สอง กด Ctrl+C เพื่อหยุด

ถ้า build และติดตั้งฐานข้อมูลแล้ว:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-Demo.ps1 -SkipBuild -SkipDatabase
```

ต้องมี .NET SDK 10, Node.js 22+, SQL Server 2019+ และ sqlcmd/SSMS ใช้ Windows Authentication ที่มีสิทธิ์สร้างฐานข้อมูลเฉพาะตอน setup `Start-Demo.ps1 -Server '.\SQLEXPRESS'` สำหรับ named instance สคริปต์ไม่ลบฐานข้อมูลหรือประวัติเก่า

## ติดตั้งแยกขั้นตอน / SSMS

1. เปิด SQLCMD Mode ใน SSMS (Query → SQLCMD Mode) ตั้ง working directory ให้ไฟล์ `:r` resolve ไปที่ `database/` หรือเปลี่ยน `:r` เป็น absolute path แล้วรัน `KU_Sports_Setup.sql`
2. อีกทาง: เปิด terminal ที่ `database/` แล้ว `sqlcmd -S localhost -E -C -I -i KU_Sports_Setup.sql -b`
3. ถ้ารันทีละไฟล์ ลำดับคือ **01 → 02 → 10 → 03 → 04 → 05 → 08 → 06 → 07** (10 เพิ่ม PasswordHash ให้ผู้สมัคร; 08 ก่อน procedure เพราะ procedure ใช้ชื่อ index) แล้วใช้ 09 เพื่อดูผล/สาธิต
4. ใน backend: `dotnet restore --configfile ../NuGet.Config` แล้ว `dotnet build`
5. ใน frontend: `npm ci` แล้ว `npm run build` ผลลัพธ์ไป `backend/wwwroot` เสิร์ฟทั้ง API/เว็บจาก host เดียว
6. ตั้ง environment ใน PowerShell แล้วเริ่ม backend:

```powershell
$env:ConnectionStrings__SqlServer='Server=localhost;Database=KU_Sports;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$env:Demo__AdminPassword='<choose-your-own-password-at-least-12-characters>'
# เฉพาะเมื่อต้องการ reset password ของ admin ที่มีอยู่แล้ว:
$env:Demo__ResetAdminPassword='true'
$env:ASPNETCORE_URLS='http://localhost:5080'
dotnet run --project backend --no-launch-profile
```

ถ้าเริ่มฐานข้อมูลใหม่ด้วย `dotnet run` ต้องกำหนด `Demo__AdminPassword` เพื่อสร้างบัญชี Admin; ถ้าไม่กำหนด แอปจะไม่สร้างรหัสผ่านเริ่มต้นให้ `Start-Demo.ps1` สร้างรหัสผ่านสุ่มไว้ในไฟล์ local และตั้งค่าให้ hash ใหม่เมื่อเริ่มระบบ รหัสผ่านเก็บด้วย PBKDF2-SHA256 210,000 iterations และ random salt.

อีกทาง copy `backend/appsettings.Local.example.json` เป็น `backend/appsettings.Local.json` และแก้ connection string ไฟล์ Local และ `.env` ถูก ignore ตัวอย่าง `.env.example` ใช้เป็นแนวทางตั้ง environment เท่านั้น .NET ไม่โหลด `.env` อัตโนมัติ

Frontend development: เปิด backend ที่ 5080 และรัน `npm run dev` ใน frontend แล้วเปิด http://localhost:5173 Vite proxy `/api` ไป backend

## ใช้บน Smartphone

UI รองรับ 390px และ desktop หากต้องการเปิดจากโทรศัพท์จริง ให้อุปกรณ์อยู่ Wi-Fi เดียวกัน ตั้ง `ASPNETCORE_URLS=http://0.0.0.0:5080` และเปิด `http://<LAN-IP-of-computer>:5080` ต้องอนุญาต firewall เฉพาะเครือข่ายส่วนตัวตามการตั้งค่าเครื่อง ใช้ HTTPS เมื่อ deploy จริง

## Flow / สิทธิ์

- เปิดหน้าแรก → เข้าสู่ระบบ/สมัครบัญชี → เลือกวัน (วันนี้ถึง 30 วัน) → Court → Slot → ยืนยันผู้เล่นคนที่สอง → ยืนยัน → Success → Tip (ข้ามได้)
- หน้าแรกแสดงสถานะของวันนี้ ส่วนการจองรองรับวันนี้ถึง 30 วันล่วงหน้า โดยวันที่และขอบเขตวันอิง **Asia/Bangkok** ผ่าน `AT TIME ZONE 'SE Asia Standard Time'`.
- ไม่มี Facility Schedule = CLOSED; ไม่มีการ seed วันเปิดหรือกิจกรรมตัวอย่าง ให้ Admin เพิ่มตารางจริงก่อนเปิดจอง มี ActivityName = CLOSED, คอร์ดซ่อมบำรุง = CLOSED
- Tip เป็นรายการจำลอง `PROMPTPAY_DEMO` ไม่มีการชำระเงินจริง และยังอยู่ในรายงานแม้ภายหลังยกเลิกการจอง
- หน้าประวัติตรวจรหัสคู่เดิมก่อนดูรายการ ยกเลิกได้เฉพาะรายการที่มีนิสิตใน session หรือแอดมิน
- การตรวจ student ID เป็น **ตัวแทนสิทธิ์สำหรับเดโม** ไม่ใช่การพิสูจน์ตัวตนจริง ผู้รู้รหัสนิสิตสามารถเข้า session เดโมได้ ไม่มี KU/Nontri integration
- สมัคร/เข้าสู่ระบบด้วย Student ID 10 หลักและ password hash PBKDF2 ไม่มีขั้นอนุมัติ ผู้จองหลักมาจาก session เท่านั้น ผู้จองลำดับสองต้องมีบัญชี active แล้ว
- Login แอดมินจำกัดคำขอต่อ IP, cookie HttpOnly/SameSite Strict, ตรวจ Origin ของ write requests, ใช้ SQL parameters ทุก query
- ปุ่มแอดมิน “เปิดสนามวันนี้” / “ปิดสนามวันนี้ / Event” เปลี่ยน Facility Schedule ของวันนี้จริงโดยไม่เปลี่ยน OS date ไม่ยกเลิกรายการเดิมอัตโนมัติ

## ทดสอบ

เริ่ม server ก่อน ต้องให้ Court 1 / 19:00 ในวันที่เลือกว่าง (ยกเลิกด้วยหน้าแอดมินถ้ามีการจองเดโมเก่า)

```powershell
node tests/integration.mjs
node tests/ui.mjs
node tests/admin-ui.mjs
```

UI tests ใช้ Microsoft Edge ที่ติดตั้งอยู่ผ่าน Playwright (`channel: msedge`) รันทีละ viewport หากไม่มี Edge ใช้ Chromium ที่ Playwright รองรับแล้วปรับ browser channel ในสคริปต์ ทดสอบสร้างบัญชีสุ่ม จองวันล่วงหน้า สนับสนุน 50 บาท และยกเลิกท้ายการทดสอบ รายการประวัติ/audit/donation ยังอยู่ตามการออกแบบ ไม่ใช้ hard delete; การทดสอบ admin คืนค่า schedule และ court status เดิม

Concurrency test ยิง create booking สอง session พร้อมกัน ต้องได้ **200 หนึ่งรายการ + 409 หนึ่งรายการ** ตรวจ audit, ยอดสนับสนุน, ownership, cancel, rebook, CLOSED และ inactive students รายงานผลล่าสุดอยู่ใน `docs/Verification.md`

## โครงสร้าง

| Folder | หน้าที่ |
|---|---|
| backend | ASP.NET API, cookie auth, parameterized SQL + stored procedures |
| frontend | React mobile-first SPA / responsive CSS |
| database | 10 SQL scripts รวม master entry point |
| docs | รายงานและเอกสารออกแบบฐานข้อมูล |
| tests | Real SQL API integration + responsive UI tests |

SQL programming mapping อยู่ใน `docs/DatabaseProgrammingMapping.md` การจองไม่ได้พึ่งการตรวจความว่างฝั่ง UI เพียงอย่างเดียว: transaction, range lock, function validation และ filtered unique index ป้องกันทั้ง API และ direct insert

เอกสารประกอบรายงาน:

- [ER Diagram](docs/ERD.md) — Entity, Primary Key, Foreign Key และความสัมพันธ์ของทั้ง 8 ตาราง
- [Data Flow Diagram](docs/DFD.md) — Context Diagram และ Level 1 ครอบคลุม Student, Admin และ data stores
- [Data List](docs/DataList.md) — ข้อมูลนำเข้า ข้อมูลจัดเก็บ การประมวลผล และผลลัพธ์
- [Data Dictionary](docs/DataDictionary.md) — ชนิดข้อมูล Null, Key, Default, Constraint และความหมายทุก field
- [Technology Stack](docs/TechStack.md) — เทคโนโลยีที่ใช้ เหตุผล และภาพรวมสถาปัตยกรรม

## ข้อจำกัดเดโม

ไม่มี payment gateway / KU SSO / แจ้งเตือน การสมัครตรวจรูปแบบรหัส 10 หลักและสร้างบัญชีทันที แต่ไม่ยืนยันตัวตนกับทะเบียน KU รหัสคู่จองต้องมีบัญชี active ผู้ใช้จองวันในช่วง 30 วัน และระบบยังไม่บังคับว่า slot ของวันที่เลือกต้องเริ่มในอนาคต Tip เป็นข้อมูลจำลอง ใช้ฐานข้อมูล KU_Sports ใหม่สำหรับเดโมก่อนส่งอาจารย์ เพื่อไม่ปนข้อมูลการทดสอบ และใช้ SQL Server backup/restore เพื่อสาธิต Recovery ห้ามลบ booking เพื่อ reset เพราะ trigger จะเปลี่ยนเป็น cancellation

แหล่งอ้างอิงเทคนิค: [Microsoft SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/get-started-sqlclient-driver), [ASP.NET minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api), [SQL Server locking](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table)
