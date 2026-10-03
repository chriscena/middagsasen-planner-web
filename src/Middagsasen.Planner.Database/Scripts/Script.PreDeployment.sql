/*
 Pre-deploy: normaliserer Users.UserName før den unike indeksen IX_Users_UserName opprettes.

 Normaliseringen er den samme som UserNameExtensions.ToNormalizedUserName i API-et:
   1. Fjern alle tegn som ikke er sifre (0-9), og ledende nuller (så «0047 …» fungerer).
   2. Nøyaktig 8 sifre (10000000-99999999) brukes som de er.
   3. 47 fulgt av nøyaktig 8 sifre (4710000000-4799999999) blir de 8 sifrene uten landskoden.
   4. Alt annet er ugyldig, f.eks. utenlandske numre («+46 92345678»), for mange eller for få sifre, og «admin».
 Ugyldige verdier står urørt (de kuttes aldri til 8 sifre, da kunne de treffe en annen persons nummer).
 De listes i en PRINT-advarsel så de kan ryddes manuelt, men stopper ikke deployen.

 Hvis normaliseringen ville gitt to eller flere rader med samme brukernavn (aktive eller inaktive),
 endres ingenting, og deployen stoppes. Brukerne må da slås sammen manuelt først; se
 Scripts/FinnBrukernavnKollisjoner.sql og README.md.

 Skriptet er idempotent, og gjør ingenting hvis tabellen Users ikke finnes ennå (ny database).
*/
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'tempdb..#NormaliserteBrukernavn') IS NOT NULL
        DROP TABLE #NormaliserteBrukernavn;

    WITH Posisjoner AS (
        -- 1..100, nok for UserName NVARCHAR(100)
        SELECT TOP (100) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Pos
        FROM (VALUES (0), (0), (0), (0), (0), (0), (0), (0), (0), (0)) AS a (x)
        CROSS JOIN (VALUES (0), (0), (0), (0), (0), (0), (0), (0), (0), (0)) AS b (x)
    ),
    Sifre AS (
        -- Sifrene (0-9) i brukernavnet, i rekkefølge. Brukernavn uten sifre får ingen rad.
        SELECT u.UserId, STRING_AGG(SUBSTRING(u.UserName, p.Pos, 1), N'') WITHIN GROUP (ORDER BY p.Pos) AS Sifre
        FROM dbo.Users u
        JOIN Posisjoner p
            -- DATALENGTH i stedet for LEN, som ignorerer etterfølgende mellomrom
            ON p.Pos <= DATALENGTH(u.UserName) / 2
           AND UNICODE(SUBSTRING(u.UserName, p.Pos, 1)) BETWEEN 48 AND 57
        GROUP BY u.UserId
    ),
    UtenLedendeNuller AS (
        -- PATINDEX finner første siffer som ikke er 0. Bare nuller gir en tom streng.
        SELECT UserId, SUBSTRING(Sifre, PATINDEX(N'%[^0]%', Sifre + N'x'), 200) AS Sifre
        FROM Sifre
    )
    SELECT
        u.UserId,
        u.UserName,
        u.Inactive,
        n.Normalisert,
        COALESCE(n.Normalisert, u.UserName) AS NyttBrukernavn
    INTO #NormaliserteBrukernavn
    FROM dbo.Users u
    LEFT JOIN UtenLedendeNuller s ON s.UserId = u.UserId
    CROSS APPLY (
        SELECT CASE
            WHEN LEN(s.Sifre) = 8 THEN s.Sifre
            WHEN LEN(s.Sifre) = 10 AND LEFT(s.Sifre, 2) = N'47' AND SUBSTRING(s.Sifre, 3, 1) <> N'0' THEN SUBSTRING(s.Sifre, 3, 8)
            ELSE NULL
        END AS Normalisert
    ) n;

    -- Advarsel om brukernavn som ikke kan normaliseres. De står urørt og må ryddes manuelt.
    DECLARE @Ugyldige nvarchar(max) = (
        SELECT STRING_AGG(CAST(N'UserId ' + CAST(nb.UserId AS nvarchar(20)) + N' («' + nb.UserName + N'»)' AS nvarchar(max)), N', ')
            WITHIN GROUP (ORDER BY nb.UserId)
        FROM #NormaliserteBrukernavn nb
        WHERE nb.Normalisert IS NULL
    );
    IF @Ugyldige IS NOT NULL
        PRINT N'Advarsel: disse brukernavnene er ikke gyldige norske telefonnumre og er ikke normalisert '
            + N'(se Scripts/FinnBrukernavnKollisjoner.sql): ' + LEFT(@Ugyldige, 3500)
            + CASE WHEN LEN(@Ugyldige) > 3500 THEN N' ...' ELSE N'' END;

    -- Kollisjoner: flere rader som ville fått samme brukernavn etter normaliseringen.
    -- Dette dekker også normaliserte verdier som kolliderer med en allerede lagret verdi.
    DECLARE @Kollisjoner nvarchar(max) = (
        SELECT STRING_AGG(CAST(k.Beskrivelse AS nvarchar(max)), N'; ')
        FROM (
            SELECT
                N'«' + nb.NyttBrukernavn + N'»: UserId ' + STRING_AGG(
                    CAST(nb.UserId AS nvarchar(max)) + N' («' + nb.UserName + N'»'
                        + CASE WHEN nb.Inactive = 1 THEN N', inaktiv' ELSE N'' END + N')',
                    N', ') WITHIN GROUP (ORDER BY nb.UserId) AS Beskrivelse
            FROM #NormaliserteBrukernavn nb
            GROUP BY nb.NyttBrukernavn
            HAVING COUNT(*) > 1
        ) k
    );

    IF @Kollisjoner IS NOT NULL
    BEGIN
        DECLARE @Melding nvarchar(2048) =
            N'Kan ikke normalisere Users.UserName: flere brukere får samme brukernavn. '
            + N'Ingenting er endret. Slå sammen brukerne manuelt (se Scripts/FinnBrukernavnKollisjoner.sql) og deploy på nytt. '
            + N'Kollisjoner: ' + LEFT(@Kollisjoner, 1700)
            + CASE WHEN LEN(@Kollisjoner) > 1700 THEN N' ...' ELSE N'' END;
        DROP TABLE #NormaliserteBrukernavn;
        THROW 50000, @Melding, 1;
    END;

    UPDATE u
    SET u.UserName = nb.Normalisert
    FROM dbo.Users u
    JOIN #NormaliserteBrukernavn nb ON nb.UserId = u.UserId
    WHERE nb.Normalisert IS NOT NULL
      -- Binær sammenligning, siden vanlig sammenligning ignorerer etterfølgende mellomrom («12345678 »).
      AND CAST(u.UserName AS varbinary(200)) <> CAST(nb.Normalisert AS varbinary(200));

    DECLARE @Antall int = @@ROWCOUNT;
    IF @Antall > 0
        PRINT N'Normaliserte ' + CAST(@Antall AS nvarchar(20)) + N' brukernavn i Users.';

    DROP TABLE #NormaliserteBrukernavn;
END;


/*
 Pre-deploy: fjerner duplikate vakter før den unike indeksen UQ_EventResourceUsers_EventResourceId_UserId opprettes.

 En duplikat er flere rader i EventResourceUsers med samme (EventResourceId, UserId), altså samme bruker
 to ganger på samme ressurs. Den eldste raden (lavest EventResourceUserId) beholdes, de andre slettes.
 Timeføringer (WorkHours.ShiftId) som peker på en rad som slettes, flyttes til raden som beholdes.

 Skriptet er idempotent, og gjør ingenting hvis tabellen EventResourceUsers ikke finnes ennå (ny database).
*/
IF OBJECT_ID(N'dbo.EventResourceUsers', N'U') IS NOT NULL
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'tempdb..#DuplikateVakter') IS NOT NULL
        DROP TABLE #DuplikateVakter;

    SELECT d.EventResourceUserId, d.Beholdes
    INTO #DuplikateVakter
    FROM (
        SELECT
            EventResourceUserId,
            MIN(EventResourceUserId) OVER (PARTITION BY EventResourceId, UserId) AS Beholdes
        FROM dbo.EventResourceUsers
    ) d
    WHERE d.EventResourceUserId <> d.Beholdes;

    IF EXISTS (SELECT 1 FROM #DuplikateVakter)
    BEGIN
        BEGIN TRANSACTION;

        IF OBJECT_ID(N'dbo.WorkHours', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.WorkHours', N'ShiftId') IS NOT NULL
            EXEC sp_executesql N'
                UPDATE w
                SET w.ShiftId = dv.Beholdes
                FROM dbo.WorkHours w
                JOIN #DuplikateVakter dv ON dv.EventResourceUserId = w.ShiftId;';

        DELETE eru
        FROM dbo.EventResourceUsers eru
        JOIN #DuplikateVakter dv ON dv.EventResourceUserId = eru.EventResourceUserId;

        DECLARE @AntallDuplikater int = @@ROWCOUNT;

        COMMIT TRANSACTION;

        PRINT N'Fjernet ' + CAST(@AntallDuplikater AS nvarchar(20)) + N' duplikate vakter i EventResourceUsers.';
    END;

    DROP TABLE #DuplikateVakter;
END;
