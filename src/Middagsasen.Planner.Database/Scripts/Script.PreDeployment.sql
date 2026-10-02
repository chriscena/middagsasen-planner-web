/*
 Pre-deploy: normaliserer Users.UserName før den unike indeksen IX_Users_UserName opprettes.

 Normaliseringen er den samme som UserNameExtensions.ToNormalizedUserName i API-et:
   1. Fjern alle tegn som ikke er sifre (0-9).
   2. Tolk resten som et tall (bigint). Tomt, eller for stort for bigint, regnes som ugyldig.
   3. Under 10000000 er ugyldig. Til og med 99999999 (8 siffer) legges 4700000000 til.
   4. Resultatet er tallet som tekst uten de to første tegnene (landskoden).
 Ugyldige verdier (f.eks. «admin») står urørt.

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
    Tall AS (
        SELECT UserId, TRY_CAST(Sifre AS bigint) AS Nummer
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
    LEFT JOIN Tall t ON t.UserId = u.UserId
    CROSS APPLY (
        SELECT CASE
            WHEN t.Nummer IS NULL OR t.Nummer < 10000000 THEN NULL
            WHEN t.Nummer <= 99999999 THEN SUBSTRING(CAST(t.Nummer + 4700000000 AS nvarchar(20)), 3, 20)
            ELSE SUBSTRING(CAST(t.Nummer AS nvarchar(20)), 3, 20)
        END AS Normalisert
    ) n;

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
