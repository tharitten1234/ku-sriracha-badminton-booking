USE KU_Sports;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
-- Final protection applies even to concurrent direct INSERTs outside the procedure.
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='UX_Bookings_ActiveSlot' AND object_id=OBJECT_ID('dbo.BOOKINGS'))
 CREATE UNIQUE INDEX UX_Bookings_ActiveSlot ON dbo.BOOKINGS(CourtID,BookingDate,SlotID) WHERE Status='BOOKED';
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Bookings_Lookup' AND object_id=OBJECT_ID('dbo.BOOKINGS'))
 CREATE INDEX IX_Bookings_Lookup ON dbo.BOOKINGS(CourtID,BookingDate,SlotID) INCLUDE(Status,StudentID1,StudentID2);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Donations_Date' AND object_id=OBJECT_ID('dbo.DONATIONS'))
 CREATE INDEX IX_Donations_Date ON dbo.DONATIONS(DonationDate) INCLUDE(Amount,BookingID);
GO
