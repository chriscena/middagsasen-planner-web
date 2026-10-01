# Middagsåsen Planner

Vaktplanleggingssystem for skianlegget Middagsåsen. Domene: event/vakt-planlegging med ressurstyper, vakter (shifts), maler (templates), arbeidstimer, værovervåking, treningssporing, kompetanser og Hall of Fame.

## Tech stack

- **Frontend:** Vue 3 + Pinia + Quasar 2 (Vite), vue-router 4, axios, vuelidate, chart.js, vue-i18n
- **Backend:** ASP.NET Core 8.0 Web API, EF Core, JWT-autentisering (custom middleware), Argon2 passord-hashing, Serilog, Swagger
- **Database:** MSSQL (SQL Azure), 20 tabeller + 2 views, SQL Server Database Project
- **Infrastruktur:** Azure (App Insights, Blob Storage), SMS-integrasjon
- **Test:** xUnit + Moq (backend), Vitest (frontend)

## Prosjektstruktur

```
docs/                                # Design- og planleggingsdokumenter
src/
  Middagsasen.Planner.Api/          # .NET 8 Web API
    Controllers/                     # 9 controllers (Auth, Events, Competencies, etc.)
    Entities/                        # 30+ entity-klasser + PlannerDbContext
    Services/                        # Domene-organisert (Authentication, Competencies, Events, etc.)
    Authentication/                  # JwtMiddleware, AuthorizeAttribute
  Middagsasen.Planner.Api.Tests/    # xUnit tester
  Middagsasen.Planner.Database/     # SQL Server Database Project (tabeller, views)
  Middagsasen.Planner.Migration/    # Konsollapp for datamigrering MSSQL → PostgreSQL
  Middagsasen.Planner.Web/          # Vue 3/Quasar frontend
    src/pages/                       # 12 sider
    src/components/                  # 15 komponenter
    src/stores/                      # 6 Pinia stores
    src/router/                      # Vue Router config
    src/boot/                        # axios, i18n, notify-defaults, etc.
```

## Dokumentasjon

- [docs/kompetansesystem.md](docs/kompetansesystem.md) — design for kompetansesystemet
- [docs/backend-arkitektur.md](docs/backend-arkitektur.md) — arkitekturanalyse og 6-stegs forbedringsplan
- [docs/postgresql-migrering.md](docs/postgresql-migrering.md) — plan for migrering MSSQL → PostgreSQL

## Kommandoer

### Frontend (`src/Middagsasen.Planner.Web/`)
```bash
npm run dev          # Start dev server
npm run build        # Produksjonsbygg
npm run lint         # ESLint
npm run format       # Prettier
npm run test         # Vitest
npm run test:watch   # Vitest watch-modus
```

### Backend (`src/Middagsasen.Planner.Api/`)
```bash
dotnet build
dotnet run
dotnet test ../Middagsasen.Planner.Api.Tests/
```

## Arkitektur og mønstre

### Backend
- **Nye features** skal bruke repository-mønster: `IRepository` -> `Repository` -> `IService` -> `Service` -> `Controller`
- Kompetansesystemet er referanseimplementasjonen for dette mønsteret
- Eldre kode bruker services direkte mot `PlannerDbContext` (skal gradvis migreres)
- DTOs brukes for request/response, ikke entities
- Prioriter database-agnostiske og container-vennlige løsninger; unngå nye Azure-spesifikke avhengigheter
- `[Authorize]` returnerer allerede 401 hvis bruker mangler — ikke dupliser null-sjekk. Bruk `var user = (UserResponse)HttpContext.Items["User"]!;`

### Frontend
- Pinia stores for state management, en per domene
- Axios med interceptors (boot/axios.js) for API-kall
- Quasar-komponenter for UI
- vue-i18n for oversettelser (`src/i18n/`)
- Bruk alltid stabil ID fra datamodellen som `:key` i `v-for` — aldri array-index (gir feil DOM-gjenbruk i lister med inputs/sletting/sortering)

## Arbeidsflyt

- Kommuniser på **norsk**
- **Deleger kodeendringer til agenter** — ikke skriv kode direkte:
  - Frontend-agent for Vue/Quasar/Pinia (`src/Middagsasen.Planner.Web/`)
  - Backend-agent for .NET/C#/SQL (`src/Middagsasen.Planner.Api/`, `src/Middagsasen.Planner.Database/`)
- Hovedagenten koordinerer og designer, agenter implementerer. Gi agentene detaljerte instruksjoner med filstier, eksisterende mønstre og konkret hva som skal endres.

## Pågående arbeid

- **Kompetansesystem**: Frittstående kompetanser, godkjenningsflyt, utløpsdato og MinimumRequired per ressurstype. Backend og frontend delvis implementert.
- **Backend-forbedring**: 6-stegs plan for bedre arkitektur (se docs/backend-arkitektur.md)
- **PostgreSQL-migrering**: se docs/postgresql-migrering.md. Docker-deploy planlagt for portabilitet bort fra Azure.
