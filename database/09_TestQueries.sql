USE KU_Sports;
GO
SELECT * FROM dbo.vw_TodayCourtSchedule ORDER BY CourtID,SlotID;
SELECT * FROM dbo.vw_BookingDetails WHERE BookingDate=CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE) ORDER BY CreatedAt DESC;
SELECT StudentID,dbo.fn_IsStudentEligible(StudentID) Eligible,dbo.fn_IsCourtAvailable(1,CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE),2) Available FROM dbo.STUDENTS WHERE PasswordHash IS NOT NULL;
SELECT c.CourtName,COUNT(b.BookingID) BookingCount FROM dbo.COURTS c LEFT JOIN dbo.BOOKINGS b ON b.CourtID=c.CourtID AND b.Status='BOOKED' GROUP BY c.CourtName ORDER BY BookingCount DESC;
SELECT c.CourtName,COUNT(*) BookingCount FROM dbo.BOOKINGS b INNER JOIN dbo.COURTS c ON c.CourtID=b.CourtID WHERE b.Status='BOOKED' GROUP BY c.CourtName HAVING COUNT(*)>=1 ORDER BY BookingCount DESC;
SELECT * FROM dbo.vw_DailyDonationSummary ORDER BY DonationDay DESC;
SELECT * FROM dbo.BOOKING_LOGS ORDER BY LogID DESC;
-- Full API/concurrency regression: tests/integration.mjs (see README).
-- Demonstrate delete conversion without persisting changes:
BEGIN TRANSACTION;
DELETE FROM dbo.BOOKINGS WHERE BookingID=(SELECT MIN(BookingID) FROM dbo.BOOKINGS WHERE Status='BOOKED');
SELECT * FROM dbo.BOOKING_LOGS ORDER BY LogID DESC;
ROLLBACK TRANSACTION;
GO
