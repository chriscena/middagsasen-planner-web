/*
 Post-deploy: fyller ut WorkHours.CreatedBy og WorkHours.CreatedTime der de mangler.

 Kolonnene er nullable i databasen, fordi API-versjonen fra før kolonnene fantes ikke setter dem ved insert,
 og databasen deployes før det nye API-et. Manglende verdier settes til samme regel som for gamle rader:
   CreatedBy   = UserId     (eieren antas å ha ført timene selv)
   CreatedTime = StartTime
 Skriptet kjøres etter skjemaendringene ved hver deploy, så det fyller både eksisterende rader ved første deploy
 og rader som det gamle API-et rakk å lage mellom databasedeploy og API-deploy (ved neste deploy).

 Skriptet er idempotent, og gjør ingenting hvis tabellen eller kolonnene ikke finnes, eller ingen rader mangler verdier
 (f.eks. på en ny database). Kolonnene refereres via sp_executesql, så batchen kompilerer også uten dem.
*/
IF OBJECT_ID(N'dbo.WorkHours', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.WorkHours', N'CreatedBy') IS NOT NULL
    AND COL_LENGTH(N'dbo.WorkHours', N'CreatedTime') IS NOT NULL
BEGIN
    SET NOCOUNT ON;

    DECLARE @AntallCreatedBy int, @AntallCreatedTime int;

    EXEC sp_executesql N'
        UPDATE dbo.WorkHours SET CreatedBy = UserId WHERE CreatedBy IS NULL;
        SET @AntallCreatedBy = @@ROWCOUNT;
        UPDATE dbo.WorkHours SET CreatedTime = StartTime WHERE CreatedTime IS NULL;
        SET @AntallCreatedTime = @@ROWCOUNT;',
        N'@AntallCreatedBy int OUTPUT, @AntallCreatedTime int OUTPUT',
        @AntallCreatedBy = @AntallCreatedBy OUTPUT,
        @AntallCreatedTime = @AntallCreatedTime OUTPUT;

    IF @AntallCreatedBy > 0 OR @AntallCreatedTime > 0
        PRINT N'Fylte ut WorkHours: CreatedBy på ' + CAST(@AntallCreatedBy AS nvarchar(20))
            + N' rader, CreatedTime på ' + CAST(@AntallCreatedTime AS nvarchar(20)) + N' rader.';
END;
