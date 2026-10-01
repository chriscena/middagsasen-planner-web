# Backend arkitekturanalyse og forbedringsplan

## Kritiske funn (per 2026-03-31)

1. **God Object — EventsService** (~880 linjer, ~40 metoder, implementerer 3 interfaces)
2. **Ingen repository-lag** — alle services bruker PlannerDbContext direkte
3. **Forretningslogikk i controllers** — autorisering/audit i EventsController, ResourceTypesController
4. **Inkonsistent feilhåndtering** — null-retur ved feil, tomme try-catch, ingen custom exceptions
5. **Statiske/hardkodede avhengigheter** — API-nøkkel i WeatherDataCollector, statisk HttpClient i SmsSenderService, hardkodede JWT/OTP-utløpsverdier
6. **Sikkerhet** — CORS AllowAnyOrigin, JWT uten issuer/audience-validering, stille catch i JwtMiddleware

## Forbedringsplan (prioritert rekkefølge)

1. **Testprosjekt + infrastruktur** — xUnit + Moq, in-memory DB
2. **Repository-mønster** — `IRepository<T>`, erstatt direkte DbContext-bruk i services
3. **Splitt EventsService** → EventsService, ResourceTypesService, EventTemplatesService
4. **Flytt forretningslogikk ut av controllers** → service-lag
5. **Erstatt statiske avhengigheter** — IHttpClientFactory, `IOptions<T>`
6. **Strukturert feilhåndtering** — custom exceptions, global exception middleware

Koden er vanskelig å teste og vedlikeholde pga. tight coupling og mangel på abstraksjoner. Følg stegene i rekkefølge — steg 1 og 2 er grunnlaget for resten.
