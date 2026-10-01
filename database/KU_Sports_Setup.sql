-- Enable SSMS: Query > SQLCMD Mode. Run from database directory via sqlcmd.
:ON ERROR EXIT
:r 01_CreateDatabase.sql
:r 02_CreateTables.sql
:r 10_AddStudentAuth.sql
:r 03_InsertSampleData.sql
:r 04_CreateViews.sql
:r 05_CreateFunctions.sql
:r 08_CreateIndexes.sql
:r 06_CreateProcedures.sql
:r 07_CreateTriggers.sql
PRINT N'KU_Sports setup complete';
GO
