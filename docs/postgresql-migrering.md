# PostgreSQL-migrering — Middagsåsen Planner

## Kontekst

Middagsåsen bruker i dag MSSQL (SQL Azure) som database. Målet er å migrere til PostgreSQL for portabilitet og uavhengighet fra Azure. Vi har 89 integrasjonstester med Testcontainers som gir oss et sikkerhetsnett. Planen dekker tre parallelle spor: EF Core-migrering, datamigrering og fremtidig databasehåndtering.

---

## Fase 1: EF Core provider-bytte (kode)

### 1.1 NuGet-pakker

**API-prosjekt** (`src/Middagsasen.Planner.Api/Middagsasen.Planner.Api.csproj`):
- Fjern: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.Data.SqlClient`
- Legg til: `Npgsql.EntityFrameworkCore.PostgreSQL` (8.0.x)

**Test-prosjekt** (`src/Middagsasen.Planner.Api.Tests/Middagsasen.Planner.Api.Tests.csproj`):
- Fjern: `Microsoft.EntityFrameworkCore.SqlServer`, `Testcontainers.MsSql`
- Legg til: `Npgsql.EntityFrameworkCore.PostgreSQL` (8.0.x), `Testcontainers.PostgreSql`

### 1.2 Program.cs

Endre:
```csharp
// Fra:
options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
// Til:
options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
```

### 1.3 PlannerDbContext.cs — fjern SQL Server-spesifikke typer

**datetime → timestamp without time zone:**
Fjern alle `.HasColumnType("datetime")` (16 stk). Npgsql mapper `DateTime` til `timestamp without time zone` automatisk.

**nvarchar(max) → text:**
Endre `.HasColumnType("nvarchar(max)")` til `.HasColumnType("text")` (1 stk, Competency.Description). Alternativt: fjern helt — Npgsql mapper uavgrensede strenger til `text` automatisk.

**decimal(15,5):**
Endre `.HasColumnType("decimal(15, 5)")` til `.HasPrecision(15, 5)` (database-agnostisk). Npgsql støtter `decimal` → `numeric`.

**HasConstraintName:**
Alle `HasConstraintName()`-kall kan beholdes — Npgsql respekterer dem.

### 1.4 TestPlannerDbContext (DatabaseFixture.cs)

`TestPlannerDbContext` overrider `OnModelCreating` for å endre cascade-oppførsel pga. SQL Servers begrensninger med multiple cascade paths. **PostgreSQL har ikke denne begrensningen.** Vi kan forenkle — men beholder override for sikkerhet, og fjerner den gradvis etter at testene bekrefter at det fungerer.

### 1.5 DatabaseFixture.cs — bytt container

```csharp
// Fra:
using Testcontainers.MsSql;
private readonly MsSqlContainer _container = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .Build();

// Til:
using Testcontainers.PostgreSql;
private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();
```

Endre `UseSqlServer(ConnectionString)` til `UseNpgsql(ConnectionString)` i `CreateContext()`.

### 1.6 Views (HallOfFame, EventStatuses)

To views brukes via `ToView()` i DbContext. De opprettes IKKE av EF Core `EnsureCreated()`. Vi har to alternativer:

**Alternativ A (anbefalt):** Lag PostgreSQL-kompatible view-definisjoner og kjør dem som del av databaseoppsettet.
- `GETDATE()` → `NOW()`
- `CAST(... as date)` → `::date`
- Resten av syntaksen (CTE, COALESCE, CASE WHEN, JOIN) er kompatibel

**Alternativ B:** Omskriv view-logikken til EF Core LINQ-queries i servicen (fjern avhengighet på views helt).

For integrasjonstester: Views må opprettes manuelt etter `EnsureCreatedAsync()`. Legg til en metode i DatabaseFixture som kjører SQL for å opprette views.

---

## Fase 2: Integrasjonstester grønne

### 2.1 Kjør testene mot PostgreSQL-container

Etter endringene i fase 1, kjør `dotnet test`. Fiks eventuelle feil:
- DateTime-håndtering (PostgreSQL er strengere på tidssoner)
- Streng-sammenligninger (PostgreSQL er case-sensitive som standard)
- Cascade delete (kan fungere bedre i PG, evt. forenkle TestPlannerDbContext)

### 2.2 Verifiser alle 89 tester passerer

Dette er vår gatekeeper — ingen produksjonsmigrering uten grønne tester.

---

## Fase 3: Fremtidig databasehåndtering (EF Core Migrations)

### 3.1 Innfør EF Core Migrations

I dag brukes SQL Server Database Project (SQLPROJ) for skjemaendringer. Denne er SQL Server-spesifikk og bør erstattes med EF Core Migrations.

```bash
dotnet ef migrations add InitialCreate --project src/Middagsasen.Planner.Api
```

### 3.2 Migrasjonsflyt for fremtidige endringer

1. Endre entity/DbContext-konfigurasjon
2. `dotnet ef migrations add <BeskrivelsAvEndring>`
3. Kjør integrasjonstester (fanger opp problemer)
4. Deploy: `dotnet ef database update` eller kjør migrasjoner ved oppstart

### 3.3 Vurder å fjerne SQL Server Database Project

`src/Middagsasen.Planner.Database/` blir overflødig når EF Core Migrations tar over. Behold som referanse/dokumentasjon inntil migreringen er fullført og verifisert.

---

## Fase 4: Datamigrering DEV (MSSQL → PostgreSQL)

Migrering til DEV-miljø først for å verifisere at alt fungerer før produksjon.

### 4.1 Migreringsverktøy (C# konsollapp)

~~Opprinnelig plan var å bruke **pgloader**, men det fungerer ikke med Azure SQL Database (TDS-protokoll-inkompatibilitet).~~

I stedet bruker vi et eget C#-konsollprogram (`Middagsasen.Planner.Migration`) som:
- Gjenbruker eksisterende EF Core entities og DbContext
- Leser fra MSSQL med SqlServer-provider, skriver til PostgreSQL med Npgsql-provider
- Setter inn data i riktig FK-rekkefølge (3 nivåer)
- Resetter PostgreSQL-sekvenser etter innsetting
- Håndterer store tabeller med batching (1000 rader per batch)

**Prosjekt:** `src/Middagsasen.Planner.Migration/`

**Innsettingsrekkefølge:**
| Nivå | Tabeller |
|------|----------|
| 0 | Users, Competencies, Events, EventTemplates, ResourceTypes, WeatherLocations, WeatherMeasurements |
| 1 | UserSessions, EventResources, ResourceTemplates, ResourceTypeTrainers, ResourceTypeTrainings, ResourceTypeFiles, CompetencyApprovers, ResourceTypeCompetencies, WorkHours, WeatherMeasurementValues |
| 2 | EventResourceUsers (Shifts), EventResourceMessages (Messages), UserCompetencies |

Views (HallOfFame, EventStatuses) migreres **ikke** — de er beregnede views som opprettes separat.

### 4.2 Opprett DEV PostgreSQL-database

Sett opp en PostgreSQL-instans for DEV (f.eks. lokal Docker-container):
```bash
docker run -d --name middagsasen-pg-dev \
  -e POSTGRES_USER=planner -e POSTGRES_PASSWORD=<passord> -e POSTGRES_DB=middagsasen \
  -p 5432:5432 postgres:16-alpine
```

La EF Core opprette skjema:
```bash
dotnet ef database update --project src/Middagsasen.Planner.Api --connection "Host=localhost;Database=middagsasen;Username=planner;Password=<passord>"
```

Opprett views manuelt (PostgreSQL-versjoner fra fase 1.6).

### 4.3 Kjør migreringsverktøyet

```bash
cd C:\Source\ChrisCena\Repos\middagsasen-planner-web
dotnet run --project src/Middagsasen.Planner.Migration/ -- \
  --source "Server=tcp:<mssql-host>;Database=<db>;User ID=<user>;Password=<pass>;Encrypt=True;TrustServerCertificate=False;" \
  --target "Host=localhost;Database=middagsasen;Username=planner;Password=<passord>"
```

**Hva verktøyet gjør:**
1. Verifiserer tilkobling til begge databaser
2. Leser alle rader fra MSSQL med `AsNoTracking()` (ingen endringssporing)
3. Setter inn i PostgreSQL i riktig FK-rekkefølge (nivå 0 → 1 → 2)
4. Store tabeller (WeatherMeasurementValues) batches i grupper på 1000
5. Resetter alle PostgreSQL-sekvenser (neste auto-ID = MAX(id) + 1)
6. Skriver oppsummering med radtall per tabell og eventuelle feil

**Forberedelser før kjøring:**
- Azure SQL må være tilgjengelig (brannmurregel for din IP)
- PostgreSQL-skjema må være opprettet FØR kjøring (via EF Core migrations)
- `Users.UserName` må være normalisert i MSSQL først (pre-deploy-skriptet i databaseprosjektet, se `src/Middagsasen.Planner.Database/README.md`). EF-modellen har en unik indeks på `UserName` (`IX_Users_UserName`, uten filter), som kommer med i PostgreSQL-skjemaet via migrations. Duplikater gjør at innsettingen av `Users` feiler, og unormaliserte brukernavn blir ikke funnet ved innlogging
- Views (HallOfFame, EventStatuses) må opprettes manuelt i PostgreSQL
- Stopp applikasjonen mot MSSQL under migrering for å unngå data-drift
- Anbefalt: ta backup av MSSQL før migrering

### 4.4 Verifiser DEV-data

- Sjekk radtall per tabell: `SELECT schemaname, relname, n_live_tup FROM pg_stat_user_tables;`
- Sammenlign med MSSQL: `SELECT t.name, SUM(p.rows) FROM sys.tables t JOIN sys.partitions p ON t.object_id = p.object_id WHERE p.index_id < 2 GROUP BY t.name;`
- Sjekk sekvenser: `SELECT sequencename, last_value FROM pg_sequences;`
- Verifiser at views returnerer data
- **Kjør applikasjonen mot DEV PG og test manuelt** — logg inn, opprett vakt, sjekk kompetanser, etc.

### 4.5 Gate: DEV fungerer

Alle funksjoner må fungere mot DEV PostgreSQL før vi går videre til produksjon.

---

## Fase 5: Produksjonsmigrering

### 5.1 Forberedelser

1. Sett opp PostgreSQL produksjonsserver (managed PG, Docker, eller annet)
2. Planlegg vedlikeholdsvindu — applikasjonen må være nede under migrering
3. Informer brukere om planlagt nedetid
4. Ta full backup av MSSQL produksjonsdatabase

### 5.2 Migrering

1. Stopp API-en (forhindre nye skrivninger til MSSQL)
2. Opprett skjema via EF Core migrations mot produksjons-PG
3. Opprett views manuelt
4. Kjør migreringsverktøyet med PROD connection strings (som testet i DEV)
5. Verifiser radtall og sekvenser
6. Oppdater connection string i produksjonskonfigurasjon
7. Deploy ny versjon av API
8. Verifiser at alt fungerer (smoke test)
9. Åpne for brukere

### 5.3 Rollback-plan

Behold MSSQL-databasen uendret under overgangen. Hvis noe feiler:
- Bytt connection string tilbake til MSSQL
- Deploy forrige versjon (med SqlServer-provider — ha den klar som rollback-artifact)
- MSSQL er uberørt og inneholder all data

### 5.4 Etterarbeid

- Behold MSSQL som backup i 2-4 uker
- Overvåk logger for PG-spesifikke feil
- Når alt er stabilt: deaktiver/slett MSSQL

---

## Gjennomføringsrekkefølge

| Steg | Beskrivelse | Risiko |
|------|-------------|--------|
| 1 | EF Core provider-bytte + DbContext-endringer | Lav (lokalt) |
| 2 | Testcontainers PG + kjør tester | Lav (lokalt) |
| 3 | PostgreSQL views i tester | Lav (lokalt) |
| 4 | Alle 89 tester grønne mot PG | **Gate** |
| 5 | Innfør EF Core Migrations | Medium |
| 6 | Opprett DEV PG + migrer data med migreringsverktøy | Medium |
| 7 | Verifiser DEV manuelt | **Gate** |
| 8 | Opprett PROD PG + migrer data med migreringsverktøy | Høy |
| 9 | Deploy til produksjon + verifiser | Høy (rollback klar) |

## Filer som endres

- `src/Middagsasen.Planner.Api/Middagsasen.Planner.Api.csproj` — pakker
- `src/Middagsasen.Planner.Api/Program.cs` — UseNpgsql
- `src/Middagsasen.Planner.Api/Data/PlannerDbContext.cs` — fjern SQL Server-typer
- `src/Middagsasen.Planner.Api.Tests/Middagsasen.Planner.Api.Tests.csproj` — pakker
- `src/Middagsasen.Planner.Api.Tests/Infrastructure/DatabaseFixture.cs` — PG-container + views
- `src/Middagsasen.Planner.Migration/Middagsasen.Planner.Migration.csproj` — **nytt** migreringsverktøy
- `src/Middagsasen.Planner.Migration/Program.cs` — **nytt** migreringslogikk

## Verifisering

```bash
dotnet test src/Middagsasen.Planner.Api.Tests/ --verbosity normal
```

Alle 89 tester må passere mot PostgreSQL-containeren.
