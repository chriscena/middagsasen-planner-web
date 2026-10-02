/*
 Viser brukere som ville fått samme brukernavn når Users.UserName normaliseres av
 Script.PreDeployment.sql. Disse må slås sammen manuelt før deploy, ellers stopper deployen.

 Ren SELECT – endrer ingenting. Er ikke en del av databasemodellen (bygges ikke inn i dacpac).
 Normaliseringen må holdes lik den i Script.PreDeployment.sql (og UserNameExtensions.ToNormalizedUserName i API-et).

 Antall vakter, timeføringer, opplæringer og kompetanser per bruker er tatt med som hjelp til å
 velge hvilken bruker som skal beholdes.
*/
WITH Posisjoner AS (
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
),
Normalisert AS (
    SELECT
        u.UserId,
        u.UserName,
        n.Normalisert,
        COALESCE(n.Normalisert, u.UserName) AS NyttBrukernavn,
        u.Inactive,
        u.FirstName,
        u.LastName,
        u.Created
    FROM dbo.Users u
    LEFT JOIN Tall t ON t.UserId = u.UserId
    CROSS APPLY (
        SELECT CASE
            WHEN t.Nummer IS NULL OR t.Nummer < 10000000 THEN NULL
            WHEN t.Nummer <= 99999999 THEN SUBSTRING(CAST(t.Nummer + 4700000000 AS nvarchar(20)), 3, 20)
            ELSE SUBSTRING(CAST(t.Nummer AS nvarchar(20)), 3, 20)
        END AS Normalisert
    ) n
),
Kollisjoner AS (
    SELECT nb.*, COUNT(*) OVER (PARTITION BY nb.NyttBrukernavn) AS AntallMedSammeBrukernavn
    FROM Normalisert nb
)
SELECT
    k.NyttBrukernavn,
    k.UserId,
    k.UserName,
    k.Normalisert,
    k.Inactive,
    k.FirstName,
    k.LastName,
    k.Created,
    (SELECT COUNT(*) FROM dbo.EventResourceUsers s WHERE s.UserId = k.UserId) AS Vakter,
    (SELECT COUNT(*) FROM dbo.WorkHours w WHERE w.UserId = k.UserId) AS Timeforinger,
    (SELECT COUNT(*) FROM dbo.ResourceTypeTrainings t WHERE t.UserId = k.UserId) AS Opplaeringer,
    (SELECT COUNT(*) FROM dbo.UserCompetencies c WHERE c.UserId = k.UserId) AS Kompetanser,
    (SELECT MAX(us.Created) FROM dbo.UserSessions us WHERE us.UserId = k.UserId) AS SisteInnlogging
FROM Kollisjoner k
WHERE k.AntallMedSammeBrukernavn > 1
ORDER BY k.NyttBrukernavn, k.Inactive, k.UserId;
