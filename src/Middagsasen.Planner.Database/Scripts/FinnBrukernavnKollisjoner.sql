/*
 Viser brukere som må ryddes manuelt før Users.UserName normaliseres av Script.PreDeployment.sql.

 Gir to resultatsett:
   1. Kollisjoner: brukere som ville fått samme brukernavn etter normaliseringen. Disse må slås sammen
      manuelt før deploy, ellers stopper deployen.
   2. Ugyldige: brukernavn som ikke er gyldige norske telefonnumre og derfor ikke normaliseres (f.eks.
      utenlandske numre, feil antall sifre eller «admin»). De stopper ikke deployen, men brukerne kan ikke
      logge inn med engangskode og bør ryddes.

 Endrer ingenting i databasen (bruker bare en midlertidig tabell). Er ikke en del av databasemodellen (bygges ikke inn i dacpac).
 Normaliseringen må holdes lik den i Script.PreDeployment.sql (og UserNameExtensions.ToNormalizedUserName i API-et).

 Antall vakter, timeføringer, opplæringer og kompetanser per bruker er tatt med som hjelp til å
 velge hvilken bruker som skal beholdes.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'tempdb..#NormaliserteBrukernavn') IS NOT NULL
    DROP TABLE #NormaliserteBrukernavn;

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
UtenLedendeNuller AS (
    -- PATINDEX finner første siffer som ikke er 0. Bare nuller gir en tom streng.
    SELECT UserId, SUBSTRING(Sifre, PATINDEX(N'%[^0]%', Sifre + N'x'), 200) AS Sifre
    FROM Sifre
)
SELECT
    u.UserId,
    u.UserName,
    n.Normalisert,
    COALESCE(n.Normalisert, u.UserName) AS NyttBrukernavn,
    u.Inactive,
    u.FirstName,
    u.LastName,
    u.Created,
    (SELECT COUNT(*) FROM dbo.EventResourceUsers s WHERE s.UserId = u.UserId) AS Vakter,
    (SELECT COUNT(*) FROM dbo.WorkHours w WHERE w.UserId = u.UserId) AS Timeforinger,
    (SELECT COUNT(*) FROM dbo.ResourceTypeTrainings t WHERE t.UserId = u.UserId) AS Opplaeringer,
    (SELECT COUNT(*) FROM dbo.UserCompetencies c WHERE c.UserId = u.UserId) AS Kompetanser,
    (SELECT MAX(us.Created) FROM dbo.UserSessions us WHERE us.UserId = u.UserId) AS SisteInnlogging
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

-- 1. Kollisjoner
WITH Kollisjoner AS (
    SELECT nb.*, COUNT(*) OVER (PARTITION BY nb.NyttBrukernavn) AS AntallMedSammeBrukernavn
    FROM #NormaliserteBrukernavn nb
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
    k.Vakter,
    k.Timeforinger,
    k.Opplaeringer,
    k.Kompetanser,
    k.SisteInnlogging
FROM Kollisjoner k
WHERE k.AntallMedSammeBrukernavn > 1
ORDER BY k.NyttBrukernavn, k.Inactive, k.UserId;

-- 2. Brukernavn som ikke kan normaliseres
SELECT
    nb.UserId,
    nb.UserName,
    nb.Inactive,
    nb.FirstName,
    nb.LastName,
    nb.Created,
    nb.Vakter,
    nb.Timeforinger,
    nb.Opplaeringer,
    nb.Kompetanser,
    nb.SisteInnlogging
FROM #NormaliserteBrukernavn nb
WHERE nb.Normalisert IS NULL
ORDER BY nb.Inactive, nb.UserId;

DROP TABLE #NormaliserteBrukernavn;
