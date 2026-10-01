# Entity–Relationship Diagram (ERD)

ERD นี้อ้างอิงโครงสร้างจริงใน `database/02_CreateTables.sql` และการเพิ่ม `PasswordHash` ใน `database/10_AddStudentAuth.sql` ระบบมี 8 ตารางหลัก ใช้ SQL Server และเก็บการจองที่ยกเลิกไว้เพื่อรักษาประวัติ

```mermaid
erDiagram
    STUDENTS ||--o{ BOOKINGS : "ผู้จองหลัก StudentID1"
    STUDENTS ||--o{ BOOKINGS : "ผู้เล่นร่วม StudentID2"
    COURTS ||--o{ BOOKINGS : reserves
    TIME_SLOTS ||--o{ BOOKINGS : uses
    BOOKINGS ||--o{ DONATIONS : receives
    BOOKINGS ||--o{ BOOKING_LOGS : audited_by

    STUDENTS {
        varchar StudentID PK "10 digits"
        varchar FullName "NOT NULL"
        varchar Email "NULL"
        varchar PasswordHash "NULL for legacy rows"
        bit IsActive "NOT NULL, default 1"
    }
    COURTS {
        int CourtID PK "IDENTITY"
        varchar CourtName "NOT NULL"
        varchar CourtStatus "ACTIVE / MAINTENANCE / CLOSED"
    }
    TIME_SLOTS {
        int SlotID PK "IDENTITY"
        time StartTime "NOT NULL"
        time EndTime "NOT NULL; StartTime < EndTime"
        bit IsActive "NOT NULL, default 1"
    }
    FACILITY_SCHEDULE {
        int ScheduleID PK "IDENTITY"
        date ScheduleDate UK "one schedule per date"
        bit IsCourtOpen "NOT NULL"
        varchar ActivityName "NULL"
        varchar Description "NULL"
    }
    BOOKINGS {
        int BookingID PK "IDENTITY"
        int CourtID FK
        int SlotID FK
        date BookingDate "NOT NULL"
        varchar StudentID1 FK "NOT NULL"
        varchar StudentID2 FK "NOT NULL; must differ from StudentID1"
        varchar Status "BOOKED / CANCELLED"
        datetime CreatedAt "NOT NULL"
        datetime CancelledAt "NULL"
    }
    DONATIONS {
        int DonationID PK "IDENTITY"
        int BookingID FK
        decimal Amount "must be > 0"
        varchar PaymentMethod "default PROMPTPAY_DEMO"
        datetime DonationDate "NOT NULL"
    }
    BOOKING_LOGS {
        int LogID PK "IDENTITY"
        int BookingID FK
        varchar ActionType "CREATED / STATUS_CHANGED"
        varchar OldStatus "NULL on creation"
        varchar NewStatus "NULL"
        datetime ChangedAt "NOT NULL"
    }
    STAFF_USERS {
        int StaffID PK "IDENTITY"
        varchar Username UK "NOT NULL"
        varchar PasswordHash "NOT NULL"
        varchar Role "default ADMIN"
        bit IsActive "NOT NULL, default 1"
    }
```

## กฎความสัมพันธ์และข้อกำหนด

- `BOOKINGS` อ้างถึง `STUDENTS` สองครั้งเพื่อระบุผู้จองหลักและผู้เล่นร่วม ผู้จองหลักมาจากบัญชีที่เข้าสู่ระบบ ส่วนผู้เล่นร่วมต้องเป็นนิสิตที่ active เช่นกัน
- `FACILITY_SCHEDULE.ScheduleDate` ไม่ได้เป็น Foreign Key ของ `BOOKINGS.BookingDate`; ระบบตรวจว่ามีตารางเปิดสนามสำหรับวันที่นั้นภายใน View/Function/Stored Procedure ก่อนสร้างรายการจอง วันที่ไม่มี schedule ถือว่าปิด
- `DONATIONS` และ `BOOKING_LOGS` เป็นข้อมูลลูกของการจอง การยกเลิกปรับ `BOOKINGS.Status` เป็น `CANCELLED` และเก็บ audit ไว้ ไม่มีการลบรายการจองทิ้งจาก flow ปกติ
- `STAFF_USERS` เป็นบัญชีแอดมินแยกจาก `STUDENTS` ไม่มีความสัมพันธ์กับรายการจอง
- Filtered unique index บน `(CourtID, BookingDate, SlotID)` เฉพาะ `Status='BOOKED'` ทำให้คอร์ดเดียวกันในวันและช่วงเวลาเดียวกันมีรายการ active ได้เพียงรายการเดียว

## การออกแบบเชิงสัมพันธ์

แยกนิสิต คอร์ด ช่วงเวลา การจอง และเงินสนับสนุนออกจากกันเพื่อลดข้อมูลซ้ำ ชื่อคอร์ด ช่วงเวลา และชื่อนิสิตดึงด้วย JOIN เมื่อต้องแสดงผล ผู้เล่นสองคนเก็บในคอลัมน์บทบาท `StudentID1` และ `StudentID2` ตามขอบเขตระบบที่กำหนดให้มีสองคนต่อการจอง
