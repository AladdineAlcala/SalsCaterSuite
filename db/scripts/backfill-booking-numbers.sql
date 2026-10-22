/* ============================================================================
   Sal's CaterSuite — BookingNumber backfill (DEC-016 + DEC-021 clarification)

   PURPOSE
   -------
   Populates the human-readable business identifier on any Booking row that does
   not already have one, using the contractual format:

       BK-{transdate:yyyyMMdd}-{trn_Id padded to 5 digits}
       e.g. trn_Id 42 on 2026-10-18  ->  BK-20261018-00042

   SCOPE / WHEN TO RUN
   -------------------
   This script is ONLY for a database whose schema was created OUTSIDE the EF Core
   migrations (for example an ad-hoc database created with `EnsureCreated`, or a
   hand-provisioned schema). On a database created by `dotnet ef database update`
   the Booking table starts empty, so this script is a guaranteed no-op.

   Run it BEFORE adding the unique index `IX_Booking_BookingNumber`, because
   `CREATE UNIQUE INDEX` fails when two rows share a NULL/empty value.

   SAFETY
   ------
   * Idempotent: it only touches rows where BookingNumber IS NULL or empty, so
     running it repeatedly is harmless and it never rewrites an issued number.
   * It performs NO schema change (no DDL), only a targeted UPDATE.
   * It exits immediately if the Booking table does not exist.
   * This script is NOT part of the EF migration pipeline and MUST NOT be applied
     to any database that was migrated by EF Core. It was NOT run against the
     LocalDB `SalsCaterSuiteDb` dev database (that database was created empty by
     the InitialCreate migration).

   CAVEAT
   ------
   The 5-character suffix is the zero-padded trn_Id. If a trn_Id ever exceeds
   99999 the suffix intentionally keeps the last 5 digits of the id, which is
   still unique because the id itself is unique and the date part carries the
   remaining information.
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[Booking]', N'U') IS NULL
BEGIN
    PRINT 'Booking table not found. Nothing to do.';
    RETURN;
END;

BEGIN TRANSACTION;

UPDATE [dbo].[Booking]
SET [BookingNumber] = 'BK-'
    + FORMAT([transdate], 'yyyyMMdd')
    + '-'
    + RIGHT('00000' + CAST([trn_Id] AS varchar(10)), 5)
WHERE [BookingNumber] IS NULL
   OR LTRIM(RTRIM([BookingNumber])) = '';

DECLARE @rowsUpdated int = @@ROWCOUNT;

COMMIT TRANSACTION;

PRINT CONCAT('Backfilled BookingNumber for ', @rowsUpdated, ' booking row(s).');

-- Verification: must return zero rows before the unique index is created.
SELECT [trn_Id], [transdate], [BookingNumber]
FROM [dbo].[Booking]
WHERE [BookingNumber] IS NULL
   OR LTRIM(RTRIM([BookingNumber])) = '';
