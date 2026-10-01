USE KU_Sports;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CreateBooking @CourtID INT,@SlotID INT,@BookingDate DATE,@StudentID1 VARCHAR(10),@StudentID2 VARCHAR(10)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Today DATE=CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE),@BookingID INT;
 BEGIN TRY
  BEGIN TRANSACTION;
  -- Shared range lock protects schedule against admin closure until commit.
  IF @BookingDate<@Today OR @BookingDate>DATEADD(DAY,30,@Today)
   THROW 50009,N'เลือกวันจองได้ตั้งแต่วันนี้ถึง 30 วันข้างหน้า',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.FACILITY_SCHEDULE WITH(HOLDLOCK) WHERE ScheduleDate=@BookingDate AND IsCourtOpen=1 AND NULLIF(LTRIM(RTRIM(ActivityName)),'') IS NULL)
   THROW 50001,N'วันที่เลือกสนามปิดให้บริการ กรุณาตรวจสอบกิจกรรม',1;
  IF @StudentID1=@StudentID2 THROW 50002,N'กรุณาใช้รหัสนิสิตคนละคน',1;
  -- Keep eligibility and court/slot state stable through the transaction.
  DECLARE @ActiveCount INT;
  SELECT @ActiveCount=COUNT(*) FROM dbo.STUDENTS WITH(HOLDLOCK) WHERE StudentID IN(@StudentID1,@StudentID2) AND IsActive=1;
  IF @ActiveCount<>2 OR dbo.fn_IsStudentEligible(@StudentID1)=0 OR dbo.fn_IsStudentEligible(@StudentID2)=0
   THROW 50003,N'ไม่พบรหัสนิสิตหรือรหัสนิสิตไม่มีสิทธิ์ใช้งาน',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.COURTS WITH(HOLDLOCK) WHERE CourtID=@CourtID AND CourtStatus='ACTIVE')
   OR NOT EXISTS(SELECT 1 FROM dbo.TIME_SLOTS WITH(HOLDLOCK) WHERE SlotID=@SlotID AND IsActive=1)
   THROW 50004,N'คอร์ดหรือช่วงเวลาปิดให้บริการ',1;
  -- UPDLOCK + HOLDLOCK obtains a serializable range lock on the indexed slot key.
  IF EXISTS(SELECT 1 FROM dbo.BOOKINGS WITH(UPDLOCK,HOLDLOCK,INDEX(IX_Bookings_Lookup)) WHERE CourtID=@CourtID AND BookingDate=@BookingDate AND SlotID=@SlotID AND Status='BOOKED')
   THROW 50005,N'ช่วงเวลานี้ถูกจองแล้ว กรุณาเลือกเวลาอื่น',1;
  IF dbo.fn_IsCourtAvailable(@CourtID,@BookingDate,@SlotID)=0 THROW 50005,N'ช่วงเวลานี้ไม่ว่าง',1;
  INSERT dbo.BOOKINGS(CourtID,SlotID,BookingDate,StudentID1,StudentID2) VALUES(@CourtID,@SlotID,@BookingDate,@StudentID1,@StudentID2);
  SET @BookingID=SCOPE_IDENTITY();
  COMMIT TRANSACTION;
  SELECT * FROM dbo.vw_BookingDetails WHERE BookingID=@BookingID;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  IF ERROR_NUMBER() IN(2601,2627) THROW 50005,N'ช่วงเวลานี้ถูกจองแล้ว กรุณาเลือกเวลาอื่น',1;
  THROW;
 END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CancelBooking @BookingID INT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  IF NOT EXISTS(SELECT 1 FROM dbo.BOOKINGS WITH(UPDLOCK,HOLDLOCK) WHERE BookingID=@BookingID) THROW 50006,N'ไม่พบการจอง',1;
  UPDATE dbo.BOOKINGS SET Status='CANCELLED',CancelledAt=GETDATE() WHERE BookingID=@BookingID AND Status='BOOKED';
  COMMIT;
  SELECT * FROM dbo.vw_BookingDetails WHERE BookingID=@BookingID;
 END TRY BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AddDonation @BookingID INT,@Amount DECIMAL(10,2)
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  IF @Amount IS NULL OR @Amount<=0 OR @Amount>100000 THROW 50007,N'จำนวนเงินต้องมากกว่า 0 และไม่เกิน 100,000 บาท',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.BOOKINGS WITH(HOLDLOCK) WHERE BookingID=@BookingID AND Status='BOOKED') THROW 50008,N'เพิ่มเงินสนับสนุนได้เฉพาะการจองที่ยังไม่ยกเลิก',1;
  INSERT dbo.DONATIONS(BookingID,Amount) VALUES(@BookingID,@Amount);
  DECLARE @ID INT=SCOPE_IDENTITY();
  COMMIT;
  SELECT * FROM dbo.DONATIONS WHERE DonationID=@ID;
 END TRY BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH
END;
GO
