# middagsasen-planner-db

SQL Server Database Project (Microsoft.Build.Sql) med skjemaet for Middagsåsen Planner. `dotnet build` gir en dacpac i `bin/`.

## Pre-deploy: normalisering av brukernavn

> **Viktig: databasen må deployes før API-et.**
>
> Ved push til `main` publiserer `.github/workflows/main_middagsasen-planner.yml` databasen (dacpac, jobben `database`) før API-et deployes, og feiler databasedeployen, deployes ikke API-et. Det nye API-et slår opp eksakt på normalisert brukernavn ved innlogging og engangskode, og regner med at eksisterende brukernavn allerede er normalisert og at den unike indeksen `IX_Users_UserName` finnes (den brukes til å oppdage samtidig opprettelse av samme bruker). Kommer API-et ut først, finner det ikke brukere som er lagret i et annet format (f.eks. «+47 …»), og OTP-innlogging oppretter da nye duplikatbrukere i stedet. Databaseendringen fungerer med API-et som kjører i produksjon i dag, så den kan trygt deployes først.

`Users.UserName` har en unik indeks (`IX_Users_UserName`), også for inaktive brukere. Brukernavnet er telefonnummeret normalisert med samme regel som `UserNameExtensions.ToNormalizedUserName` i API-et (f.eks. «+47 123 45 678» → «12345678»). Eldre data kan ha nummeret lagret i andre formater.

`Scripts/Script.PreDeployment.sql` kjøres automatisk før skjemaendringene ved deploy av dacpac-en, og normaliserer eksisterende brukernavn før indeksen opprettes. Bare norske numre normaliseres: 8 sifre, eller 47 fulgt av 8 sifre (ikke-sifre og ledende nuller ignoreres). Alt annet (utenlandske numre, feil antall sifre, «admin» osv.) står urørt og listes i en advarsel (`PRINT`) i deploy-loggen, men stopper ikke deployen. Skriptet er idempotent og gjør ingenting på en ny database.

Hvis normaliseringen ville gitt flere brukere med samme brukernavn, endrer skriptet ingenting og stopper deployen med en melding som lister de berørte brukerne.

### Før deploy

1. Kjør `Scripts/FinnBrukernavnKollisjoner.sql` mot databasen. Den endrer ingenting, og gir to resultatsett: brukere som kolliderer, med antall vakter, timeføringer, opplæringer og kompetanser for å hjelpe med valget av hvilken bruker som skal beholdes, og brukernavn som ikke kan normaliseres (bør ryddes, men stopper ikke deployen).
2. Slå sammen kolliderende brukere manuelt: flytt vakter, timeføringer, opplæringer osv. til brukeren som skal beholdes, og slett (eller gi et annet brukernavn til) de andre. For timeføringer må alle kolonnene som peker på brukeren flyttes, ikke bare eieren `UserId`: også `CreatedBy` (fylt ut med `UserId` ved deploy, se under), `ApprovedBy` og `ModifiedBy`. Ellers stopper slettingen av duplikatet på fremmednøklene `FK_WorkHours_Users_CreatedBy`, `FK_WorkHours_Users_ApprovedBy` og `FK_WorkHours_Users_ModifiedBy`.
3. Kjør spørringen på nytt og sjekk at det første resultatsettet (kollisjoner) er tomt.
4. Deploy databasen (dacpac), og sjekk deploy-loggen for advarselen om brukernavn som ikke kan normaliseres.
5. Merge endringen til `main`, så API-et deployes.

Normaliseringen i de to skriptene må holdes lik hverandre og `ToNormalizedUserName`. Den er dekket av `Database/PreDeploymentScriptTests.cs` i testprosjektet.

## Pre-deploy: duplikate vakter

`EventResourceUsers` har en unik indeks (`UQ_EventResourceUsers_EventResourceId_UserId`), så samme bruker bare kan stå én gang på samme oppgave. API-et (`ShiftRepository`) kjenner igjen brudd på indeksen ved navn og gir 400.

`Scripts/Script.PreDeployment.sql` fjerner eksisterende duplikater før indeksen opprettes: den eldste raden (lavest `EventResourceUserId`) per (`EventResourceId`, `UserId`) beholdes. `WorkHours.ShiftId` settes til `NULL` på timeføringer som pekte på en slettet rad, siden fremmednøkkelen fortsatt finnes når pre-deploy kjører. Antallet fjernede rader skrives i deploy-loggen (`PRINT`). Skriptet er idempotent og gjør ingenting på en ny database. Endringen fungerer med både gammelt og nytt API, så rekkefølgen på deploy spiller ingen rolle.

Merk: når brukere slås sammen manuelt (se over), må vakter der begge brukerne står på samme oppgave slettes i stedet for å flyttes, ellers stopper den unike indeksen flyttingen.

Duplikatfjerningen er dekket av `Database/PreDeploymentScriptTests.cs`.

## Post-deploy: WorkHours.CreatedBy og CreatedTime

`WorkHours` har kolonnene `CreatedBy` (FK `FK_WorkHours_Users_CreatedBy` → `Users`) og `CreatedTime`: hvem som førte timene (ikke nødvendigvis eieren `UserId`, siden admin kan føre timer for andre) og når. Det nye API-et setter dem ved opprettelse.

Kolonnene er **nullable** i databasen, uten standardverdier. Databasen publiseres før API-et, og API-et som kjører i produksjon i mellomtiden kjenner ikke kolonnene og setter dem ikke ved insert. `NOT NULL` ville da stoppet timeføringen i det vinduet, og en standardverdi kan ikke hente `UserId`/`StartTime` fra samme rad.

`Scripts/Script.PostDeployment.sql` kjøres etter skjemaendringene ved hver deploy og setter `CreatedBy = UserId` og `CreatedTime = StartTime` der de mangler. Det fyller eksisterende rader ved første deploy, og rader som det gamle API-et rakk å lage mellom databasedeploy og API-deploy ved neste deploy. Antallet utfylte rader skrives i deploy-loggen (`PRINT`). Skriptet er idempotent og gjør ingenting på en ny database eller når ingenting mangler. Til en rad er fylt ut, returnerer API-et `createdBy`/`createdTime` som `null`. Kolonnene kan gjøres `NOT NULL` i en senere endring når alt er fylt ut.

Rekkefølge: databasen først, så API-et. Ved merge til `main` publiserer API-workflowen (`.github/workflows/main_middagsasen-planner.yml`) databasen før API-et deployes. Endringen fungerer med API-et som kjører i dag, så databasen kan også deployes manuelt i forkant.

Utfyllingen er dekket av `Database/PostDeploymentScriptTests.cs`.

## WorkHours.ShiftId

Timer registreres uten kobling til vakt, så fremmednøkkelen `FK_WorkHours_Users_ShiftId` er fjernet fra `Tables/WorkHours.sql` og droppes ved publish (ingen datatap). Kolonnen `ShiftId` står igjen ubrukt (API-et mapper den ikke) og kan fjernes senere.

## Deploy (GitHub Actions)

Ved push til `main` (endringer i API, databaseprosjekt eller deploy-workflowene) kjører `.github/workflows/main_middagsasen-planner.yml` build og test, deretter jobben `database`, som kaller `Database deploy` (`.github/workflows/db_deploy.yml`) med `publish`, og til slutt API-deployen. Feiler databasedeployen, deployes ikke API-et.

`Database deploy` kan også kjøres manuelt (workflow_dispatch): `script` (standard) genererer deploy-skript og DeployReport uten å endre noe, f.eks. for å se hva som vil endres før merge; `publish` oppdaterer databasen. Workflowen logger inn med managed identity (OIDC) og Entra-token, og åpner en midlertidig brannmurregel for runneren som slettes etterpå.

Engangsoppsett: identiteten trenger SQL Server Contributor på SQL-serveren, og en databasebruker. Sett `@ClientId` (clientId, ikke objectId) i `Scripts/OpprettDeployBruker.sql` og kjør skriptet som Entra-admin i applikasjonsdatabasen (ikke master). Production-miljøet i GitHub trenger variablene `SQL_RESOURCE_GROUP`, `SQL_SERVER_NAME` (uten `.database.windows.net`) og `SQL_DATABASE_NAME`.
