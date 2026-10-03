# middagsasen-planner-db

SQL Server Database Project (Microsoft.Build.Sql) med skjemaet for Middagsåsen Planner. `dotnet build` gir en dacpac i `bin/`.

## Pre-deploy: normalisering av brukernavn

> **Viktig: deploy databasen FØR endringen merges til `main`.**
>
> API-et deployes automatisk ved push til `main` (`.github/workflows/main_middagsasen-planner.yml`), mens databasen (dacpac) deployes manuelt. Det nye API-et slår opp eksakt på normalisert brukernavn ved innlogging og engangskode, og regner med at eksisterende brukernavn allerede er normalisert og at den unike indeksen `IX_Users_UserName` finnes (den brukes til å oppdage samtidig opprettelse av samme bruker). Kommer API-et ut først, finner det ikke brukere som er lagret i et annet format (f.eks. «+47 …»), og OTP-innlogging oppretter da nye duplikatbrukere i stedet. Databaseendringen fungerer med API-et som kjører i produksjon i dag, så den kan trygt deployes først.

`Users.UserName` har en unik indeks (`IX_Users_UserName`), også for inaktive brukere. Brukernavnet er telefonnummeret normalisert med samme regel som `UserNameExtensions.ToNormalizedUserName` i API-et (f.eks. «+47 123 45 678» → «12345678»). Eldre data kan ha nummeret lagret i andre formater.

`Scripts/Script.PreDeployment.sql` kjøres automatisk før skjemaendringene ved deploy av dacpac-en, og normaliserer eksisterende brukernavn før indeksen opprettes. Bare norske numre normaliseres: 8 sifre, eller 47 fulgt av 8 sifre (ikke-sifre og ledende nuller ignoreres). Alt annet (utenlandske numre, feil antall sifre, «admin» osv.) står urørt og listes i en advarsel (`PRINT`) i deploy-loggen, men stopper ikke deployen. Skriptet er idempotent og gjør ingenting på en ny database.

Hvis normaliseringen ville gitt flere brukere med samme brukernavn, endrer skriptet ingenting og stopper deployen med en melding som lister de berørte brukerne.

### Før deploy

1. Kjør `Scripts/FinnBrukernavnKollisjoner.sql` mot databasen. Den endrer ingenting, og gir to resultatsett: brukere som kolliderer, med antall vakter, timeføringer, opplæringer og kompetanser for å hjelpe med valget av hvilken bruker som skal beholdes, og brukernavn som ikke kan normaliseres (bør ryddes, men stopper ikke deployen).
2. Slå sammen kolliderende brukere manuelt: flytt vakter, timeføringer, opplæringer osv. til brukeren som skal beholdes, og slett (eller gi et annet brukernavn til) de andre.
3. Kjør spørringen på nytt og sjekk at det første resultatsettet (kollisjoner) er tomt.
4. Deploy databasen (dacpac), og sjekk deploy-loggen for advarselen om brukernavn som ikke kan normaliseres.
5. Merge endringen til `main`, så API-et deployes.

Normaliseringen i de to skriptene må holdes lik hverandre og `ToNormalizedUserName`. Den er dekket av `Database/PreDeploymentScriptTests.cs` i testprosjektet.

## Pre-deploy: duplikate vakter

`EventResourceUsers` har en unik indeks (`UQ_EventResourceUsers_EventResourceId_UserId`), så samme bruker bare kan stå én gang på samme ressurs. API-et (`ShiftRepository`) kjenner igjen brudd på indeksen ved navn og gir 400.

`Scripts/Script.PreDeployment.sql` fjerner eksisterende duplikater før indeksen opprettes: den eldste raden (lavest `EventResourceUserId`) per (`EventResourceId`, `UserId`) beholdes, og timeføringer (`WorkHours.ShiftId`) som pekte på en slettet rad flyttes til raden som beholdes. Antallet fjernede rader skrives i deploy-loggen (`PRINT`). Skriptet er idempotent og gjør ingenting på en ny database. Endringen fungerer med både gammelt og nytt API, så rekkefølgen på deploy spiller ingen rolle.

Merk: når brukere slås sammen manuelt (se over), må vakter der begge brukerne står på samme ressurs slettes i stedet for å flyttes, ellers stopper den unike indeksen flyttingen.

Duplikatfjerningen er dekket av `Database/PreDeploymentScriptTests.cs`.
