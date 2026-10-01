# Data List (รายการข้อมูลของระบบ)

เอกสารนี้แยกข้อมูลที่รับเข้าระบบ ข้อมูลที่ระบบอ่าน/คำนวณ และข้อมูลที่ส่งออกให้ผู้ใช้ โดยอ้างอิงตารางจริง 8 ตาราง

## 1. ข้อมูลนำเข้า (Input Data)

| กลุ่มข้อมูล | รายการข้อมูล | ผู้ให้ข้อมูล / ที่มา | ใช้ในกระบวนการ |
|---|---|---|---|
| สมัครนิสิต | รหัสนิสิต 10 หลัก, ชื่อ, รหัสผ่าน | นิสิต | สมัครบัญชีและสร้าง password hash |
| เข้าสู่ระบบ | รหัสนิสิตและรหัสผ่าน | นิสิต | ตรวจบัญชีและสร้าง session |
| ค้นหาวันจอง | วันที่ตั้งแต่วันนี้ถึง 30 วันข้างหน้า | นิสิต | อ่านตารางสนามและสถานะว่างของวัน |
| สร้างการจอง | CourtID, SlotID, BookingDate, StudentID2 | นิสิตที่ login แล้ว; StudentID1 มาจาก session | ตรวจสิทธิ์และสร้างรายการจอง |
| ยกเลิกการจอง | BookingID | เจ้าของรายการหรือ Admin | เปลี่ยนสถานะเป็น CANCELLED |
| เงินสนับสนุน | BookingID, Amount | นิสิต (ไม่บังคับ) | เพิ่มรายการ Donation จำลอง |
| ตารางสถานที่ | วันที่, เปิด/ปิด, ชื่อกิจกรรม, รายละเอียด | Admin | กำหนดวันที่เปิดจองหรือปิดเพราะกิจกรรม |
| สถานะคอร์ด | CourtID, CourtStatus | Admin | เปิดใช้งาน/ปิด/ซ่อมบำรุงรายคอร์ด |
| เข้าสู่ระบบแอดมิน | Username, Password | Admin | ตรวจสิทธิ์ role ADMIN |
| เงื่อนไขรายงาน | วันที่เริ่มต้นและสิ้นสุด | Admin | จำกัดช่วงข้อมูลรายงาน |

## 2. ข้อมูลที่จัดเก็บ (Stored Data)

| Data Store | ข้อมูลสำคัญ | การสร้าง/เปลี่ยนแปลง |
|---|---|---|
| `STUDENTS` | StudentID, FullName, Email, PasswordHash, IsActive | นิสิตสมัคร; บัญชีไม่มีขั้นอนุมัติ |
| `COURTS` | CourtID, CourtName, CourtStatus | setup และ Admin ปรับสถานะ |
| `TIME_SLOTS` | SlotID, StartTime, EndTime, IsActive | setup/ผู้ดูแลฐานข้อมูล |
| `FACILITY_SCHEDULE` | ScheduleID, ScheduleDate, IsCourtOpen, ActivityName, Description | Admin เพิ่มหรือแก้ไขรายวัน |
| `BOOKINGS` | BookingID, CourtID, SlotID, BookingDate, StudentID1, StudentID2, Status, timestamps | Stored Procedure สร้าง/ยกเลิก |
| `DONATIONS` | DonationID, BookingID, Amount, PaymentMethod, DonationDate | Stored Procedure บันทึกทิปจำลอง |
| `BOOKING_LOGS` | LogID, BookingID, ActionType, OldStatus, NewStatus, ChangedAt | Trigger บันทึกเมื่อสร้างหรือเปลี่ยนสถานะการจอง |
| `STAFF_USERS` | StaffID, Username, PasswordHash, Role, IsActive | setup/backend จัดเตรียมบัญชีแอดมิน |

## 3. ข้อมูลประมวลผลและผลลัพธ์ (Process / Output Data)

| ข้อมูลผลลัพธ์ | แหล่ง/วิธีประมวลผล | ผู้รับ / จุดใช้งาน |
|---|---|---|
| ผลสมัครและสถานะ session | ตรวจรูปแบบ Student ID, hash password และข้อมูล `STUDENTS` | หน้าสมัคร / login |
| ตารางความพร้อมรายวัน | JOIN `COURTS`, `TIME_SLOTS`, `FACILITY_SCHEDULE`, `BOOKINGS`; สถานะ CLOSED/BOOKED/AVAILABLE | หน้าเลือกวันและจอง |
| รายละเอียดการจอง | `vw_BookingDetails` JOIN คอร์ด เวลา และชื่อนิสิต | หน้ายืนยัน, success, ประวัติ, จัดการจอง |
| ผลตรวจสิทธิ์ผู้เล่น | `fn_IsStudentEligible` และกฎใน `sp_CreateBooking` | ก่อนบันทึกการจอง |
| ผลการจอง/สาเหตุที่ไม่สำเร็จ | `sp_CreateBooking` ตรวจวันเปิด, สมาชิก, คอร์ด, เวลา, รายการซ้ำใน transaction | หน้า Confirm |
| ประวัติและผลยกเลิก | `BOOKINGS` + `BOOKING_LOGS`; ยกเลิกโดยเปลี่ยนสถานะ | Student History / Admin |
| ยอดทิปรายวัน | `vw_DailyDonationSummary` และรายงานตามวันจอง | Admin Reports |
| สถิติการจอง | COUNT, GROUP BY และช่วงวันที่จาก `BOOKINGS` | Admin Reports |
| Audit trail | `trg_BookingAudit` บันทึก CREATED/STATUS_CHANGED | Admin Logs / ตรวจสอบ |

## 4. การจำแนกข้อมูลตามผู้ใช้

- **นิสิต:** จัดการบัญชีของตน ดูสถานะสนามและวันที่จอง สร้าง/ดู/ยกเลิกการจองที่เกี่ยวข้อง และให้ทิปแบบสมัครใจ
- **Admin:** จัดการ Facility Schedule และสถานะ Court ดู booking ทั้งหมด ดู audit log และรายงาน
- **ระบบ:** สร้าง BookingID/เวลา, ตรวจเงื่อนไข, คุมการจองซ้ำ, สร้าง audit log และคำนวณผลสรุป

ไม่มีข้อมูลนักศึกษาเดโมถูก seed โดยสคริปต์ปัจจุบัน และไม่มี Facility Schedule ตัวอย่าง การไม่มี schedule ของวันที่เลือกหมายถึงสนามปิด; Student ID ที่กรอกตรวจรูปแบบ 10 หลักและบัญชีในระบบ แต่ไม่ได้ตรวจทะเบียน KU/SSO
