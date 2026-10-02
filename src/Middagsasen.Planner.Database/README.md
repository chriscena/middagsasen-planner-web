# middagsasen-planner-db

SQL Server Database Project (Microsoft.Build.Sql) med skjemaet for Middagsåsen Planner. `dotnet build` gir en dacpac i `bin/`.

## Pre-deploy: normalisering av brukernavn

`Users.UserName` har en unik indeks (`IX_Users_UserName`), også for inaktive brukere. Brukernavnet er telefonnummeret normalisert med samme regel som `UserNameExtensions.ToNormalizedUserName` i API-et (f.eks. «+47 123 45 678» → «12345678»). Eldre data kan ha nummeret lagret i andre formater.

`Scripts/Script.PreDeployment.sql` kjøres automatisk før skjemaendringene ved deploy av dacpac-en, og normaliserer eksisterende brukernavn før indeksen opprettes. Verdier som ikke er gyldige telefonnumre (f.eks. «admin») står urørt. Skriptet er idempotent og gjør ingenting på en ny database.

Hvis normaliseringen ville gitt flere brukere med samme brukernavn, endrer skriptet ingenting og stopper deployen med en melding som lister de berørte brukerne.

### Før deploy

1. Kjør `Scripts/FinnBrukernavnKollisjoner.sql` mot databasen. Den endrer ingenting, og viser brukere som kolliderer, med antall vakter, timeføringer, opplæringer og kompetanser for å hjelpe med valget av hvilken bruker som skal beholdes.
2. Slå sammen kolliderende brukere manuelt: flytt vakter, timeføringer, opplæringer osv. til brukeren som skal beholdes, og slett (eller gi et annet brukernavn til) de andre.
3. Kjør spørringen på nytt og sjekk at den ikke gir rader.
4. Deploy.

Normaliseringen i de to skriptene må holdes lik hverandre og `ToNormalizedUserName`. Den er dekket av `Database/PreDeploymentScriptTests.cs` i testprosjektet.
