# Data Dictionary (พจนานุกรมข้อมูล)

ฐานข้อมูล `KU_Sports` บน Microsoft SQL Server มีตารางหลัก 8 ตาราง รายการด้านล่างอ้างอิง `database/02_CreateTables.sql` และ `database/10_AddStudentAuth.sql` ชนิด `VARCHAR` ใช้กับข้อมูลตัวอักษรตาม schema; เครื่องฐานข้อมูลต้องตั้ง collation/code page ที่รองรับข้อความภาษาไทย

## 1. STUDENTS — บัญชีนิสิต

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| StudentID | VARCHAR(10) | No | PK; API รับตัวเลข 10 หลัก | รหัสบัญชีนิสิต |
| FullName | VARCHAR(100) | No | — | ชื่อและนามสกุล |
| Email | VARCHAR(100) | Yes | — | อีเมล (ไม่บังคับ) |
| PasswordHash | VARCHAR(255) | Yes | NULL ได้เพื่อรองรับ row เดิม; บัญชีใหม่เก็บ PBKDF2 hash | ค่าแฮชรหัสผ่าน ไม่เก็บ plaintext |
| IsActive | BIT | No | DEFAULT 1 | สถานะบัญชี; 1 ใช้งานได้ |

## 2. COURTS — สนาม

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| CourtID | INT | No | PK, IDENTITY | รหัสสนาม |
| CourtName | VARCHAR(30) | No | — | ชื่อที่แสดง เช่น Court 1 |
| CourtStatus | VARCHAR(20) | No | DEFAULT 'ACTIVE'; CHECK เป็น ACTIVE, MAINTENANCE หรือ CLOSED | สถานะพร้อมให้จอง |

## 3. TIME_SLOTS — ช่วงเวลา

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| SlotID | INT | No | PK, IDENTITY | รหัสช่วงเวลา |
| StartTime | TIME | No | CHECK StartTime < EndTime | เวลาเริ่ม |
| EndTime | TIME | No | CHECK StartTime < EndTime | เวลาสิ้นสุด |
| IsActive | BIT | No | DEFAULT 1 | สถานะเปิดใช้ช่วงเวลา |

## 4. FACILITY_SCHEDULE — ตารางเปิดปิดสถานที่

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| ScheduleID | INT | No | PK, IDENTITY | รหัสกำหนดการ |
| ScheduleDate | DATE | No | UNIQUE; หนึ่งรายการต่อวัน | วันที่มีผล |
| IsCourtOpen | BIT | No | — | 1 เปิด, 0 ปิด |
| ActivityName | VARCHAR(200) | Yes | ถ้ามีชื่อกิจกรรมถือว่าปิดการจอง | ชื่อกิจกรรมที่ใช้สถานที่ |
| Description | VARCHAR(500) | Yes | — | รายละเอียดเพิ่มเติม |

วันที่ไม่มี record ในตารางนี้ถือว่าปิดสนาม ไม่มีการสร้างวัน OPEN อัตโนมัติจาก sample data

## 5. BOOKINGS — รายการจอง

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| BookingID | INT | No | PK, IDENTITY | เลขที่จอง |
| CourtID | INT | No | FK → COURTS.CourtID | สนามที่เลือก |
| SlotID | INT | No | FK → TIME_SLOTS.SlotID | ช่วงเวลาที่เลือก |
| BookingDate | DATE | No | DEFAULT CAST(GETDATE() AS DATE); Procedure รับวันที่จองอย่างชัดเจน | วันที่เข้าใช้สนาม |
| StudentID1 | VARCHAR(10) | No | FK → STUDENTS.StudentID; CHECK ต่างจาก StudentID2 | ผู้จองหลักจาก session |
| StudentID2 | VARCHAR(10) | No | FK → STUDENTS.StudentID; CHECK ต่างจาก StudentID1 | รหัสนิสิตผู้เล่นร่วม |
| Status | VARCHAR(20) | No | DEFAULT 'BOOKED'; CHECK เป็น BOOKED หรือ CANCELLED | สถานะรายการ |
| CreatedAt | DATETIME | No | DEFAULT GETDATE() | วันเวลาที่สร้างโดย SQL Server |
| CancelledAt | DATETIME | Yes | — | วันเวลาที่ยกเลิก |

Filtered unique index `UX_Bookings_ActiveSlot` บังคับไม่ให้มีรายการ `BOOKED` ซ้ำใน `(CourtID, BookingDate, SlotID)`; การจองที่ยกเลิกแล้วไม่ล็อกช่วงเวลานั้น

## 6. DONATIONS — เงินสนับสนุน/ทิป

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| DonationID | INT | No | PK, IDENTITY | รหัสรายการทิป |
| BookingID | INT | No | FK → BOOKINGS.BookingID | รายการจองที่เกี่ยวข้อง |
| Amount | DECIMAL(10,2) | No | CHECK Amount > 0; Procedure/API จำกัดไม่เกิน 100,000 | จำนวนเงิน (บาท) |
| PaymentMethod | VARCHAR(30) | No | DEFAULT 'PROMPTPAY_DEMO' | ช่องทางจำลอง ไม่ได้ตัดเงินจริง |
| DonationDate | DATETIME | No | DEFAULT GETDATE() | วันเวลาที่บันทึก |

## 7. BOOKING_LOGS — ประวัติการเปลี่ยนแปลง

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| LogID | INT | No | PK, IDENTITY | ลำดับรายการ audit |
| BookingID | INT | No | FK → BOOKINGS.BookingID | รายการจองที่ถูกเปลี่ยน |
| ActionType | VARCHAR(30) | No | Trigger สร้าง CREATED หรือ STATUS_CHANGED | ประเภทเหตุการณ์ |
| OldStatus | VARCHAR(20) | Yes | NULL เมื่อสร้างรายการใหม่ | สถานะก่อนเปลี่ยน |
| NewStatus | VARCHAR(20) | Yes | — | สถานะหลังเปลี่ยน |
| ChangedAt | DATETIME | No | DEFAULT GETDATE() | วันเวลาที่ Trigger บันทึก |

## 8. STAFF_USERS — บัญชีเจ้าหน้าที่

| Field | SQL Type | Null | Key / Default / Rule | คำอธิบาย |
|---|---|---|---|---|
| StaffID | INT | No | PK, IDENTITY | รหัสเจ้าหน้าที่ |
| Username | VARCHAR(50) | No | UNIQUE | ชื่อเข้าใช้ |
| PasswordHash | VARCHAR(255) | No | — | PBKDF2-SHA256 hash พร้อม salt |
| Role | VARCHAR(20) | No | DEFAULT 'ADMIN' | บทบาทเจ้าหน้าที่ |
| IsActive | BIT | No | DEFAULT 1 | 1 อนุญาตให้เข้าใช้ |

## ข้อมูลคำนวณ/ข้อมูลแสดงผล (ไม่ได้เป็นตารางจัดเก็บแยก)

| ชื่อข้อมูล | แหล่งที่มา / นิยาม |
|---|---|
| Availability | สถานะ CLOSED เมื่อไม่มีตารางเปิด/มีกิจกรรม/สนามหรือ slot ปิด; BOOKED เมื่อมีรายการ active; กรณีอื่น AVAILABLE |
| BookingDetails | `vw_BookingDetails`: Booking รวมชื่อสนาม ช่วงเวลา ชื่อนิสิตสองคน และ DonationTotal |
| DonationDay, DonationCount, TotalAmount | `vw_DailyDonationSummary`: สรุปทิปโดยวันจอง (BookingDate) |
| TodayBookingCount | จำนวนรายการ BOOKED ที่ BookingDate ตรงกับวันปัจจุบันตามเวลา Asia/Bangkok |

รหัสผ่านจริงและข้อมูลล็อกอินไม่อยู่ในเอกสารนี้; บัญชี Admin สำหรับเครื่องเดโมเก็บแยกไว้ในไฟล์ local ที่ถูก ignore จาก Git
