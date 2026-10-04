/*
 Oppretter managed identity-en som GitHub Actions bruker til databasedeploy (.github/workflows/db_deploy.yml)
 som contained Entra-bruker, og gir den db_owner.

 Kjøres manuelt av Entra-admin på SQL-serveren, i applikasjonsdatabasen (ikke master).
 Brukeren opprettes med SID utledet fra clientId, så SQL-serverens identitet trenger ikke Directory Readers.
 Skriptet er idempotent. Er ikke en del av databasemodellen (bygges ikke inn i dacpac).

 Alternativ hvis serveridentiteten har Directory Readers:
   CREATE USER [id-github-middagsasen-planner] FROM EXTERNAL PROVIDER;
*/
SET NOCOUNT ON;

DECLARE @Navn sysname = N'id-github-middagsasen-planner';
-- clientId (applikasjons-ID) til managed identity-en, IKKE objectId/principalId
DECLARE @ClientId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

IF @ClientId = '00000000-0000-0000-0000-000000000000'
    THROW 50000, N'Sett @ClientId til clientId for managed identity-en.', 1;

-- uniqueidentifier → varbinary gir samme byte-rekkefølge som Entra forventer i SID-en
DECLARE @Sid nvarchar(100) = CONVERT(nvarchar(100), CAST(@ClientId AS varbinary(16)), 1);
DECLARE @Sql nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @Navn)
BEGIN
    SET @Sql = N'CREATE USER ' + QUOTENAME(@Navn) + N' WITH SID = ' + @Sid + N', TYPE = E;';
    EXEC sys.sp_executesql @Sql;
    PRINT N'Opprettet bruker ' + @Navn + N'.';
END
ELSE
    PRINT N'Brukeren ' + @Navn + N' finnes allerede.';

SET @Sql = N'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@Navn) + N';';
EXEC sys.sp_executesql @Sql;
PRINT N'Brukeren ' + @Navn + N' er medlem av db_owner.';
