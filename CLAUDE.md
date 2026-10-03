# Middagsåsen Planner

Vaktplanleggingssystem for skianlegget Middagsåsen. Domene: event/vakt-planlegging med ressurstyper, vakter (shifts), maler (templates), arbeidstimer, værovervåking, treningssporing, kompetanser og Hall of Fame.

## Tech stack

- **Frontend:** TypeScript (strict), Vue 3 + Pinia + Quasar 2 (Vite), vue-router 4, axios, vuelidate, chart.js, vue-i18n
- **Backend:** ASP.NET Core 10 Web API, EF Core 10, JWT-autentisering (custom middleware), Argon2 passord-hashing, Serilog, innebygd OpenAPI (`Microsoft.AspNetCore.OpenApi`) + Swagger UI
- **Database:** MSSQL (SQL Azure), 20 tabeller + 2 views, SQL Server Database Project
- **Infrastruktur:** Azure (App Insights, Blob Storage), SMS-integrasjon
- **Test:** xUnit + NSubstitute + Testcontainers (MSSQL) (backend), Vitest (frontend)

## Prosjektstruktur

```
Directory.Packages.props            # Sentrale NuGet-versjoner (Central Package Management)
global.json                         # Låser .NET SDK 10
docs/                                # Design- og planleggingsdokumenter
src/
  Middagsasen.Planner.Api/          # .NET 10 Web API
    Controllers/                     # 10 controllers (Auth, Events, Competencies, etc.)
    Data/                            # Entity-klasser + PlannerDbContext
    Services/                        # Domene-organisert (Authentication, Competencies, Events, etc.)
    Authentication/                  # JwtMiddleware, AuthorizeAttribute
    Core/OpenApi/                    # OpenAPI-transformere (required, tall, Bearer)
    openapi/openapi.json             # Generert ved build — sjekkes inn
  Middagsasen.Planner.Api.Tests/    # xUnit tester
  Middagsasen.Planner.Database/     # SQL Server Database Project (Microsoft.Build.Sql 2.x)
  Middagsasen.Planner.Migration/    # Konsollapp for datamigrering MSSQL → PostgreSQL
  Middagsasen.Planner.Web/          # Vue 3/Quasar frontend (TypeScript)
    src/pages/                       # 13 sider
    src/components/                  # 10 komponenter
    src/stores/                      # 7 Pinia stores
    src/types/                       # API-typer generert fra openapi.json + håndskrevne typer
    src/router/                      # Vue Router config
    src/boot/                        # axios, i18n, notify-defaults, etc.
```

## Dokumentasjon

- [docs/kompetansesystem.md](docs/kompetansesystem.md) — design for kompetansesystemet
- [docs/backend-arkitektur.md](docs/backend-arkitektur.md) — arkitekturanalyse og 6-stegs forbedringsplan
- [docs/postgresql-migrering.md](docs/postgresql-migrering.md) — plan for migrering MSSQL → PostgreSQL
- [docs/timeforing-redigering.md](docs/timeforing-redigering.md) — admin-redigering av timeføringer, låsing og tilgangsregler

## Kommandoer

### Frontend (`src/Middagsasen.Planner.Web/`)
```bash
npm run dev          # Start dev server
npm run build        # Produksjonsbygg
npm run lint         # ESLint
npm run typecheck    # vue-tsc --noEmit
npm run gen:api      # Generer src/types/ fra backendens openapi.json
npm run format       # Prettier
npm run test         # Vitest
npm run test:watch   # Vitest watch-modus
```

### Backend (`src/Middagsasen.Planner.Api/`)
```bash
dotnet build         # Regenererer også openapi/openapi.json
dotnet run
dotnet test ../Middagsasen.Planner.Api.Tests/
```

## Arkitektur og mønstre

### Backend
- **Nye features** skal bruke repository-mønster: `IRepository` -> `Repository` -> `IService` -> `Service` -> `Controller`
- Kompetansesystemet er referanseimplementasjonen for dette mønsteret
- Eldre kode bruker services direkte mot `PlannerDbContext` (skal gradvis migreres)
- DTOs brukes for request/response, ikke entities
- Pakkeversjoner legges i `Directory.Packages.props`, ikke i csproj
- Annoter endepunkter med korrekt `[ProducesResponseType]` (eller `ActionResult<T>`) — OpenAPI-dokumentet og frontend-typene genereres fra dem. Ikke-nullable DTO-egenskaper blir `required`.
- Prioriter database-agnostiske og container-vennlige løsninger; unngå nye Azure-spesifikke avhengigheter
- Innlogget bruker er en `Actor` (`UserId`, `IsAdmin`) som `JwtMiddleware` legger i `HttpContext.Items["User"]`. Les den via `ICurrentUserService` (`UserId`, `IsAdmin`, `ToActor()` til policyene) — ikke direkte fra `HttpContext.Items`. `[Authorize]` returnerer allerede 401 hvis bruker mangler, så ikke dupliser null-sjekk (`UserId` kaster `NotAuthenticatedException` uansett). Trenger du brukerdata (navn, opplæringer osv.), hent dem via `IUserService`.

### Frontend
- All kode er TypeScript: `.ts` og `<script setup lang="ts">` (strict, inkl. `noUncheckedIndexedAccess` og `exactOptionalPropertyTypes`)
- API-typer importeres fra `src/types` (`import type { EventResponse } from "src/types"`). `src/types/api.d.ts`, `index.ts` og `enums.ts` er generert — ikke rediger; kjør `dotnet build` i backend og `npm run gen:api` når DTO-er endres, og sjekk inn begge
- Type-baserte `defineProps<{ ... }>()` / `defineEmits<{ ... }>()`
- Pinia stores i options-stil med typet state-interface, en per domene
- Axios med interceptors (`boot/axios.ts`) for API-kall
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
