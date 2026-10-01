USE KU_Sports;
GO
CREATE OR ALTER VIEW dbo.vw_TodayCourtSchedule AS
SELECT c.CourtID,c.CourtName,c.CourtStatus,t.SlotID,CONVERT(VARCHAR(5),t.StartTime,108) StartTime,CONVERT(VARCHAR(5),t.EndTime,108) EndTime,d.BookingDate,
CASE WHEN s.ScheduleID IS NULL OR s.IsCourtOpen=0 OR NULLIF(LTRIM(RTRIM(s.ActivityName)),'') IS NOT NULL OR c.CourtStatus<>'ACTIVE' OR t.IsActive=0 THEN 'CLOSED'
WHEN b.BookingID IS NOT NULL THEN 'BOOKED' ELSE 'AVAILABLE' END Availability
FROM dbo.COURTS c CROSS JOIN dbo.TIME_SLOTS t
CROSS JOIN(SELECT CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS DATE) BookingDate)d
LEFT JOIN dbo.FACILITY_SCHEDULE s ON s.ScheduleDate=d.BookingDate
LEFT JOIN dbo.BOOKINGS b ON b.CourtID=c.CourtID AND b.SlotID=t.SlotID AND b.BookingDate=d.BookingDate AND b.Status='BOOKED';
GO
CREATE OR ALTER VIEW dbo.vw_BookingDetails AS
SELECT b.*,c.CourtName,CONVERT(VARCHAR(5),t.StartTime,108) StartTime,CONVERT(VARCHAR(5),t.EndTime,108) EndTime,s1.FullName StudentName1,s2.FullName StudentName2,
COALESCE((SELECT SUM(Amount) FROM dbo.DONATIONS d WHERE d.BookingID=b.BookingID),0) DonationTotal
FROM dbo.BOOKINGS b INNER JOIN dbo.COURTS c ON c.CourtID=b.CourtID INNER JOIN dbo.TIME_SLOTS t ON t.SlotID=b.SlotID
INNER JOIN dbo.STUDENTS s1 ON s1.StudentID=b.StudentID1 INNER JOIN dbo.STUDENTS s2 ON s2.StudentID=b.StudentID2;
GO
CREATE OR ALTER VIEW dbo.vw_DailyDonationSummary AS
SELECT b.BookingDate DonationDay,COUNT(*) DonationCount,SUM(d.Amount) TotalAmount
FROM dbo.DONATIONS d INNER JOIN dbo.BOOKINGS b ON b.BookingID=d.BookingID
GROUP BY b.BookingDate;
GO
