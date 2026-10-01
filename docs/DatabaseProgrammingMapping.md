# เนื้อหาวิชา → การใช้งานจริง

| Object / Concept | API / Flow | ประโยชน์ |
|---|---|---|
| vw_TodayCourtSchedule | GET /api/today → Home วันนี้ | CROSS JOIN 5×4 + LEFT JOIN booking และ schedule; CASE ให้ AVAILABLE/BOOKED/CLOSED |
| Facility Schedule + Booking Date | GET /api/availability/{date} / POST /api/bookings | เลือกวันวันนี้ถึง 30 วันหน้า; API และ Procedure ตรวจวันเปิดสนาม/วันจำกัดซ้ำที่ database |
| vw_BookingDetails | GET /api/bookings; ผล create/cancel | INNER JOIN Court, Slot, Students 2 คน; รวมยอด donation |
| vw_DailyDonationSummary | GET /api/admin/reports → เงินรายวัน | CAST วัน + COUNT + SUM + GROUP BY |
| fn_IsStudentEligible | POST /api/students/partner และ sp_CreateBooking | คืน BIT จาก STUDENTS.IsActive; partner ต้องเป็นบัญชี active |
| STUDENTS.PasswordHash / PBKDF2 | POST /api/students/register + /login | ลงทะเบียนทันที, hash salt ต่อ user, HttpOnly session ผูก StudentID หลัก |
| Session role authorization | Student/Admin API | Admin route group ต้องการ role ADMIN; primary StudentID ของ booking อ่านจาก session เท่านั้น |
| fn_IsCourtAvailable | sp_CreateBooking ก่อน INSERT | ตรวจ schedule/court/slot/active booking |
| sp_CreateBooking | POST /api/bookings → Confirm → Success | Validate, BEGIN TRAN, TRY/CATCH, COMMIT/ROLLBACK |
| sp_CancelBooking | POST /api/bookings/{id}/cancel → History/Admin | เปลี่ยน BOOKED เป็น CANCELLED และ CancelledAt |
| sp_AddDonation | POST /api/bookings/{id}/donations → Optional Tip | ตรวจจำนวนเงิน/booking และ insert DONATIONS |
| trg_BookingAudit | create/cancel → BOOKING_LOGS → Admin logs | AFTER INSERT/UPDATE แบบ set-based |
| trg_PreventBookingDelete | SSMS DELETE demo in 09_TestQueries.sql | INSTEAD OF DELETE แปลงเป็น cancellation; audit trigger บันทึกด้วย |
| UX_Bookings_ActiveSlot | ทุก INSERT/UPDATE BOOKINGS | filtered UNIQUE Court+Date+Slot WHERE BOOKED; CANCELLED จองใหม่ได้ |
| IX_Bookings_Lookup | procedure indexed range lock; Home lookups | รองรับคำค้นหาคอร์ด/วัน/เวลา |
| IX_Donations_Date | donation/report queries | date index พร้อม Amount/BookingID |
| DECLARE/SET/IF/BEGIN/END | procedures/functions/seeds | ตัวแปร + กฎธุรกิจ + flow control |
| GETDATE/CAST/CONVERT | timestamp/date/time displays | วันปัจจุบันและเวลาแบบ HH:mm |
| SELECT/WHERE/ORDER BY | ประวัติ รายงาน logs | จำกัดและเรียงข้อมูล |
| INNER JOIN | booking details / most-used court report | แสดงข้อมูลที่สัมพันธ์กัน |
| COUNT/SUM/GROUP BY/HAVING | Admin reports | จำนวนจอง ยอดเงิน และคอร์ดที่มีการใช้งานอย่างน้อย 1 รายการ |
| Constraints/Defaults | ทุก write path | PK/FK, student mismatch CHECK, amount CHECK, status CHECK |

## Concurrency
Availability จาก frontend เป็น snapshot จึงต้องตรวจใหม่ที่ server ใช้ transaction ใน sp_CreateBooking และ SELECT BOOKINGS WITH(UPDLOCK,HOLDLOCK,INDEX(IX_Bookings_Lookup)) เพื่อ lock ช่วง key ของคอร์ด/วัน/slot จน commit ผู้จองพร้อมกันต้องรอและเห็นการจองของผู้ชนะ ไม่ใช่ check-then-insert ที่ไม่มี lock มี filtered unique index เป็นแนวป้องกันสุดท้ายแม้มี direct SQL write

HOLDLOCK บน schedule/court/slot/student รักษาข้อมูลตรวจสิทธิ์ให้คงที่ระหว่าง transaction การแก้ schedule ผ่าน admin ใช้ serializable upsert เมื่อ SQL เกิด deadlock จะ rollback และ API แจ้งให้ลองใหม่ ไม่มีการยืนยันว่าจองสำเร็จหาก transaction ล้มเหลว

Functions ใช้เพื่อกฎธุรกิจ แต่ตัว function ไม่ได้แทน locking; ทั้งสองชั้นมีหน้าที่ต่างกัน การยกเลิกไม่ลบ record จึงไม่ทำลาย referential integrity ของ DONATIONS และ LOGS

## ACID / Recovery
- Atomicity: สร้าง booking และ audit log commit พร้อมกัน หรือ rollback พร้อมกัน
- Consistency: PK/FK/CHECK/UNIQUE รักษากฎข้อมูล
- Isolation: range lock ป้องกันการจอง slot เดียวกันพร้อมกัน
- Durability: หลัง commit SQL Server ดูแล transaction log; ไม่ต้องเขียน log recovery เองในเว็บ

เอกสาร 09 แสดง DELETE ภายใน transaction แล้ว ROLLBACK เพื่อสาธิตการคืนข้อมูล UNDO/REDO/checkpoint เป็นกลไก DBMS อธิบายในการนำเสนอได้ แต่ไม่กล่าวอ้างว่าเว็บทำ checkpoint หรือ restore อัตโนมัติ ตัวอย่าง backup ด้วย SSMS → Tasks → Back Up และ restore ไปฐานข้อมูลใหม่เพื่อการสาธิต โดยไม่เปลี่ยนฐานข้อมูลเดโมระหว่างนำเสนอ
