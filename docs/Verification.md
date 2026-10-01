# ผลการตรวจสอบ

ทดสอบล่าสุดวันที่ 1 ตุลาคม 2569 กับเว็บไซต์ที่เชื่อม Microsoft SQL Server จริง ไม่มี mock database ในหน้าเว็บ

ล้างข้อมูลจำลอง/ทดสอบจากฐาน `KU_Sports` แล้ว และรัน setup ซ้ำเพื่อยืนยันว่าไม่ใส่ข้อมูลตัวอย่างกลับมา: STUDENTS, BOOKINGS, DONATIONS, BOOKING_LOGS และ FACILITY_SCHEDULE = 0 แถว; เก็บ COURTS 5 แถว, TIME_SLOTS 4 แถว และบัญชี admin 1 บัญชีไว้ การทดสอบหลังล้าง: health endpoint ผ่าน, admin login ผ่าน, สมัครบัญชีนิสิตผ่าน แล้วลบบัญชีตรวจสอบชั่วคราวออกทันที

| Check | ผล |
|---|---|
| Backend build | ผ่าน 0 warnings / 0 errors (`dotnet build backend --no-restore -p:UseAppHost=false`) |
| Frontend production build | ผ่าน; Vite build จบโดยไม่มี warning หลังแก้ JSX booking step |
| SQL view update | ผ่าน; `vw_DailyDonationSummary` สรุป donation ตามวันจองและถูกเรียกจาก Admin Reports |
| Account registration and sign-in | ผ่าน; ID ไม่ครบ/เกิน 10 หลักและ password สั้นถูกปฏิเสธ, ID ซ้ำถูกปฏิเสธ, บัญชีใหม่ใช้งานได้ทันที |
| Booking identity and partner | ผ่าน; StudentID หลักมาจาก session แม้ request ส่งค่าอื่น, คู่จองต้องเป็นอีกบัญชี active |
| Advance booking | ผ่าน; เลือกวันที่พรุ่งนี้, ตรวจช่วง 30 วัน และทดสอบ facility ปิด |
| Double booking / concurrency | ผ่าน; สอง session พร้อมกันได้ 200 หนึ่งคำขอและ 409 หนึ่งคำขอ |
| History / ownership / cancel / audit | ผ่าน; ประวัติจำกัดตามบัญชี, ผู้ใช้อื่นแก้รายการไม่ได้, ยกเลิกและปล่อย slot พร้อม log |
| Tip and daily reports | ผ่าน; เพิ่ม 50 บาทแล้วรายงานรายวันนับยอดตามวันที่จอง; ทดสอบเทียบกับข้อมูลประวัติที่มีอยู่ |
| Student UI | ผ่านที่ 390px และ 1440px: สมัคร, สลับภาษา, จองวันล่วงหน้า, ยืนยันผู้เล่น, tip, history, cancel |
| Admin UI and roles | ผ่าน; login, Reports, Facility Schedule, Courts, Audit; student เรียก admin report ไม่ได้ |
| Admin responsive layout | ผ่านที่ 320 / 390 / 768 / 1440px ไม่มี horizontal overflow ระดับหน้า; ตารางยาวเลื่อนภายในกรอบของตารางได้ |

รันทดสอบได้ด้วย `node tests/integration.mjs`, `node tests/ui.mjs`, `node tests/admin-ui.mjs` โดยเปิด server ก่อน คำสั่งทดสอบสร้าง Student ID สุ่มและ booking ทดสอบ จากนั้นยกเลิกรายการเพื่อคืน slot; audit, cancellation และ donation ยังคงอยู่เป็นประวัติตามการออกแบบ

ข้อจำกัดเดโม: สมัครตรวจรูปแบบ Student ID 10 หลักแต่ไม่เชื่อมทะเบียน KU/Nontri; Tip เป็นข้อมูลจำลอง; เลือกจองได้ภายใน 30 วัน แต่ยังไม่ตรวจว่าเวลา slot ของวันนั้นผ่านไปแล้วหรือไม่; ทดสอบ responsive ผ่าน browser viewport ยังไม่ได้ทดสอบบนโทรศัพท์จริงหรือ deploy สาธารณะ
