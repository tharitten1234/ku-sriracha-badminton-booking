USE KU_Sports;
GO
CREATE OR ALTER TRIGGER dbo.trg_BookingAudit ON dbo.BOOKINGS AFTER INSERT,UPDATE
AS BEGIN
 SET NOCOUNT ON;
 -- Set-based: supports multi-row inserts and updates.
 INSERT dbo.BOOKING_LOGS(BookingID,ActionType,OldStatus,NewStatus)
 SELECT i.BookingID,CASE WHEN d.BookingID IS NULL THEN 'CREATED' ELSE 'STATUS_CHANGED' END,d.Status,i.Status
 FROM inserted i LEFT JOIN deleted d ON d.BookingID=i.BookingID WHERE d.BookingID IS NULL OR d.Status<>i.Status;
END;
GO
CREATE OR ALTER TRIGGER dbo.trg_PreventBookingDelete ON dbo.BOOKINGS INSTEAD OF DELETE
AS BEGIN
 SET NOCOUNT ON;
 UPDATE b SET Status='CANCELLED',CancelledAt=GETDATE() FROM dbo.BOOKINGS b INNER JOIN deleted d ON d.BookingID=b.BookingID WHERE b.Status='BOOKED';
END;
GO
