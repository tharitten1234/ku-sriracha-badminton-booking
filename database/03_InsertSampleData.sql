USE KU_Sports;
GO
-- Repeatable seeds preserve existing schedule and bookings.
IF NOT EXISTS(SELECT 1 FROM dbo.COURTS)
 INSERT dbo.COURTS(CourtName) VALUES('Court 1'),('Court 2'),('Court 3'),('Court 4'),('Court 5');
IF NOT EXISTS(SELECT 1 FROM dbo.TIME_SLOTS)
 INSERT dbo.TIME_SLOTS(StartTime,EndTime) VALUES('18:00','19:00'),('19:00','20:00'),('20:00','21:00'),('21:00','22:00');
-- Operational accounts and facility opening dates are not mock-seeded.
-- Students register themselves; Admin must enter the real schedule.
-- Missing Facility Schedule means CLOSED, which is fail-safe for real use.
-- The staff admin is created by the backend from a password stored locally, never here.
GO
