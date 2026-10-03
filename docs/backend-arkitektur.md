# Backend arkitekturanalyse og forbedringsplan

## Kritiske funn (per 2026-03-31)

1. **God Object — EventsService** (~880 linjer, ~40 metoder, implementerer 3 interfaces)
2. **Ingen repository-lag** — alle services bruker PlannerDbContext direkte
3. **Forretningslogikk i controllers** — autorisering/audit i EventsController, ResourceTypesController
4. **Inkonsistent feilhåndtering** — null-retur ved feil, tomme try-catch, ingen custom exceptions
5. **Statiske/hardkodede avhengigheter** — API-nøkkel i WeatherDataCollector, statisk HttpClient i SmsSenderService, hardkodede JWT/OTP-utløpsverdier
6. **Sikkerhet** — ~~CORS AllowAnyOrigin, JWT uten issuer/audience-validering, stille catch i JwtMiddleware~~ **Løst:** CORS er kun på for opphav i `Cors:AllowedOrigins`; JWT valideres med signatur, issuer, audience og levetid (uten slingringsmonn); `JwtMiddleware` krever `Bearer`-skjema, logger ugyldige tokens og lar andre feil boble til `ExceptionHandlingMiddleware`; engangskoder lages med `RandomNumberGenerator`, og JWT/OTP-tider er konfigurerbare (se «Autentisering og CORS» under)

## Forbedringsplan (prioritert rekkefølge)

1. **Testprosjekt + infrastruktur** — xUnit + Moq, in-memory DB
2. **Repository-mønster** — `IRepository<T>`, erstatt direkte DbContext-bruk i services
3. **Splitt EventsService** → EventsService, ResourceTypesService, EventTemplatesService
4. **Flytt forretningslogikk ut av controllers** → service-lag
5. **Erstatt statiske avhengigheter** — IHttpClientFactory, `IOptions<T>`
6. **Strukturert feilhåndtering** — custom exceptions, global exception middleware

Koden er vanskelig å teste og vedlikeholde pga. tight coupling og mangel på abstraksjoner. Følg stegene i rekkefølge — steg 1 og 2 er grunnlaget for resten.

## Autentisering og CORS

All JWT-kunnskap (nøkkel fra `Infrastructure:Secret`, algoritme HS256, issuer, audience og levetid) ligger i `Authentication/SessionTokens.cs` (`ISessionTokens`, singleton): `Create(sessionId)` utsteder token, `ReadSessionId(token)` validerer og returnerer sesjons-id-en fra claim `id`, eller `null` for ugyldige/utløpte tokens. Tid hentes fra `TimeProvider`, også ved validering. Tokenet bærer bare sesjons-id-en; brukeren slås opp fra sesjonen i `JwtMiddleware`, så utlogging (sletting av sesjonen) gjør tokenet ubrukelig.

Seksjonen `Auth` (`AuthOptions`) er valgfri — standardverdiene står i koden:

```json
"Auth": {
  "Issuer": "middagsasen-planner",
  "Audience": "middagsasen-planner-web",
  "TokenLifetime": "7.00:00:00",
  "OtpThrottle": "00:05:00",
  "OtpLifetime": "00:30:00"
}
```

`Cors:AllowedOrigins` (string-array) styrer CORS. Frontend kaller API-et med relative URL-er (samme opphav via Static Web Apps / Vite-proxy), så lista er tom som standard, og da registreres ingen CORS-policy. Settes opphav, gis de `AllowAnyMethod`/`AllowAnyHeader`.

## Tilgangsregler (policy-mønsteret)

Tilgangsregler samles i rene, statiske policy-klasser per domene, f.eks. `WorkHourPolicy`, `ShiftRules` (vakter; gir både flagg og håndhevelse), `MessagePolicy` og `CompetencyPolicy` (opplæringsreglene ligger i `ShiftRules`). Referanseimplementasjonen er `Services/WorkHours/WorkHourPolicy.cs`.

- **Policyen er ren:** ingen I/O, ingen `HttpContext` og ingen DbContext. Innlogget bruker sendes inn som `Actor(UserId, IsAdmin)` (fra `CurrentUser.ToActor()`). Avgjørelser tas ut fra den **lagrede** entiteten der det finnes, ikke fra verdiene i forespørselen.
- **Servicen slår opp fakta** som krever database (er trener, er godkjenner) uten å kortslutte for admin eller eier, sender dem inn som `bool`, og kaster `ForbiddenAccessException` (403) når policyen sier nei.
- **Controllere har ingen tilgangslogikk** utover `[Authorize]` / `[Authorize(Role = Roles.Administrator)]`. `AuthorizeAttribute` kaster `NotAuthenticatedException` (401) og `ForbiddenAccessException` (403), slik at `ExceptionHandlingMiddleware` skriver svaret som ProblemDetails. 401/403 dokumenteres automatisk i OpenAPI av `AuthorizeProblemResponsesConvention`, og alle ProblemDetails-svar dokumenteres som `application/problem+json` av `ProblemDetailsContentTypeConvention`. `UnauthorizedAccessException` (som også kastes av I/O-feil) gir 500 og logges.
- **Hver policy har rene enhetstester** (uten database) med tillatt og avvist for hver regel, og hver service har integrasjonstester for avviste forespørsler.
