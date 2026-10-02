# middagsasen-planner-api

# Middagsåsen Bemanning (middagsasen-planner-web)

Planlegger for Middagsåsen

## Install the dependencies

```bash
yarn
# or
npm install
```

### Start the app in development mode (hot-code reloading, error reporting, etc.)

```bash
quasar dev
```

### Lint the files

```bash
yarn lint
# or
npm run lint
```

### Format the files

```bash
yarn format
# or
npm run format
```

### API-typer

TypeScript-typene for API-et genereres fra backendens OpenAPI-dokument (`src/Middagsasen.Planner.Api/openapi/openapi.json`) til `src/types/api.d.ts` og `src/types/index.ts` (lesbare aliaser, f.eks. `ShiftResponse`). Disse filene skal ikke redigeres for hånd. Enum-verdier (`AuthStatus`, `OtpStatus`) står håndskrevet i `src/types/enums.ts`.

Når DTO-er endres i backend:

1. Kjør `dotnet build` i `src/Middagsasen.Planner.Api` (oppdaterer `openapi.json`).
2. Kjør `npm run gen:api` her i frontend.
3. Sjekk inn både `openapi.json` og `src/types/`.

CI (`web_pr.yml`) feiler hvis `src/types/` ikke er i synk med `openapi.json`. Får backend en ny enum, feiler `npm run typecheck` til den er lagt til i `src/types/enums.ts`.

### Build the app for production

```bash
quasar build
```

### Customize the configuration

See [Configuring quasar.config.js](https://v2.quasar.dev/quasar-cli-vite/quasar-config-js).
