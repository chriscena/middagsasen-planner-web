# Admin-redigering av timeføringer

Admin skal kunne redigere timeføringer. Kun åpne føringer (uten status) kan redigeres — godkjente og avslåtte er låst. Samtidig strammes tilgangskontrollen inn i hele WorkHours-API-et, som i dag ikke håndhever noe i backend.

## Regler

Håndheves i backend via `WorkHourPolicy` (statisk, uten avhengigheter).

| Handling | Vanlig bruker | Admin |
|---|---|---|
| Redigere / slette | Egne åpne føringer | Alle åpne føringer |
| Godkjenne / avslå | Nei | Kun åpne føringer |
| «Ingen status» (låse opp) | Nei | Godkjente og avslåtte |
| Redigere låst føring | Nei | Nei (må låse opp først) |
| Opprette | Kun for seg selv | Kun for seg selv |
| Lese | Egne føringer og summer | Alt |

- Redigerbare felter: starttid, sluttid, beskrivelse. **Eier kan aldri endres.**
- Manglende tilgang → `403 Forbidden`. Føringen er låst → `409 Conflict`.
- «Ingen status» beholdes som bevisst angrevei: admin låser opp, retter og godkjenner på nytt.
- Status er enumen `ApprovalStatus` (`Approved = 1`, `Rejected = 2`, null = åpen); heltallsverdiene lagres i databasen. Udefinerte verdier gir `400`.
- **Flagg i svaret:** `WorkHourResponse` har `canEdit`, `canDelete`, `canApprove` (godkjenne/avslå) og `canResetStatus` («Ingen status») for innlogget bruker, beregnet av `WorkHourPolicy.GetPermissions` — samme regler som håndheves. Frontend skal lese flaggene i stedet for å tolke status selv.
- Listefilteret `approved` er enumen `ApprovalFilter` (`All = 0`, `Approved = 1`, `Rejected = 2`, `Pending = 3`).

## Backend

### Skjema
- `WorkHours`: nye kolonner `ModifiedBy INT NULL` (FK → `Users`) og `ModifiedTime DATETIME NULL`.
- Oppdater `WorkHours.sql`, entiteten `WorkHour` og mapping i `PlannerDbContext`.

### Arkitektur
- Migreres til repository-mønsteret: `IWorkHourRepository` → `WorkHourRepository` → `WorkHoursService` → `WorkHoursController` (kompetansesystemet er referanse).
- Service injiserer `ICurrentUserService` (som `CompetencyService`/`EventsService`) og kaster eksisterende `EntityNotFoundException` (404) / `ForbiddenAccessException` (403), samt ny `EntityLockedException` som mappes til 409 i `ExceptionHandlingMiddleware`. Controller blir tynn.

### API

| Endepunkt | Endring |
|---|---|
| `POST /api/WorkHours` | `UserId` fjernes fra request — eier er alltid innlogget bruker |
| `PATCH /api/WorkHours/{id}` | Eneste redigeringsendepunkt. Body `{ startTime?, endTime?, description?, approvalStatus? }` — kun medsendte felter endres. Innhold og status lagres i én operasjon |
| `PATCH /api/WorkHours/{id}/ApprovedBy` | Tar kun `{ approvalStatus }`. Brukes til masse-godkjenning og «Ingen status» |
| `PUT /api/WorkHours/{id}` | **Fjernes** |
| `PATCH /api/WorkHours/{id}/EndTime` | **Fjernes** (ubrukt) |
| `GET /api/WorkHours/User/{id}/EndTime` | **Fjernes** (ubrukt) |
| `DELETE /api/WorkHours/{id}` | Følger policy (kun åpne, eier eller admin) |
| `GET /`, `GET Sum` uten `userId`, `GET Sum/All` | Kun admin |
| `GET /{id}`, `GET User/{userId}`, `GET Sum?userId=` | Admin eller eier, ellers `403` |

- `ApprovedBy` og `ApprovedTime` settes **alltid** av server fra innlogget bruker — aldri fra klient.
- `EndTimeRequest` / `EndTimeResponse` slettes.
- `WorkHourResponse` utvides med `modifiedBy`, `modifiedTime`, `modifiedByName`, `approvedByName` (navn fra backend, siden vanlige brukere ikke har tilgang til brukerlisten).

### ModifiedBy-regler
- Settes kun ved **faktisk endring** av innhold (starttid, sluttid, beskrivelse) — gamle og nye verdier sammenlignes.
- Statusendringer setter ikke `ModifiedBy` (spores allerede via `ApprovedBy`).
- Settes kun når en **annen enn eier** endrer. Blir stående selv om eier redigerer senere.

### Samtidighet
Klienten sender kun felter som faktisk er endret. «Godkjenn» uten redigering sender kun `approvalStatus`, slik at en samtidig endring fra eier ikke overskrives. Ingen rowversion — siste skriving vinner ved samtidig redigering av samme felt.

## Frontend

- **`HoursApprovalPage`**
  - Åpen rad → `TimeTrackingForm` med «Godkjenn»/«Avslå» for admin.
  - Låst rad → dagens dialog, read-only, med status-menyen. Inline beskrivelsesredigering fjernes.
  - Masse-godkjenning: fortsetter ved feil, viser én oppsummering (f.eks. «8 godkjent, 2 var allerede behandlet»), laster tabellen på nytt.
  - Rett ødelagt notify `("errorOccurred", { error: e })`.
- **`TimeTrackingForm`**
  - Sender kun endrede felter via `PATCH`, aldri `userId`.
  - Slett kun for åpne føringer.
  - `409` → «Føringen er allerede behandlet og kan ikke endres lenger», lukk og last listen på nytt. `403` → generell tilgangsmelding.
  - Viser «Endret av [navn]» når satt.
- **`HoursLogPage`**: viser «Endret av [navn]»; `:key="index"` → `hours.workHourId`.
- **`WorkHourStore`**: tilpasses nytt API.

## Tester

- `WorkHourPolicyTests`: hele regeltabellen som `[Theory]`.
- `WorkHoursServiceTests` (in-memory DB): delvis PATCH, ModifiedBy-regler, `ApprovedBy` fra server, redigering + godkjenning i én lagring, eier ved opprettelse.
- Vitest for `TimeTrackingForm`: kun endrede felter sendes; godkjenn uten redigering sender kun `approvalStatus`.

## Rekkefølge

1. Backend: skjema, repository, policy, service, controller, tester. Leverer endelig API-kontrakt.
2. Frontend: basert på kontrakten fra backend.
