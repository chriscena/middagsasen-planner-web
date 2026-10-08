# Kompetansesystem — design

## Designbeslutninger (per 2026-04-01)

- **Datamodell:** 4 nye tabeller — Competencies, CompetencyApprovers, UserCompetencies, ResourceTypeCompetencies
- **Koeksistens:** Beholder eksisterende ResourceTypeTraining-systemet, nytt system lever ved siden av
- **Status:** Kun godkjent/ikke godkjent, ingen nivåer
- **Utløp:** Valgfri utløpsdato per bruker-kompetanse (styrt av HasExpiry på kompetansetypen)
- **Advarsel, ikke blokkering:** Vakter viser advarsel ved manglende kompetanse, men blokkerer ikke tilmelding
- **Godkjennere:** Utpekte godkjennere per kompetansetype via CompetencyApprovers (ikke bare admin)
- **Selvregistrering:** Brukere kan registrere at de har en kompetanse (Approved=false), må godkjennes
- **Fokus:** Testbar, ren arkitektur — repository-mønster, interface-basert DI

## Kompetansekrav (MinimumRequired på ResourceTypeCompetencies)

- `MinimumRequired` (int, default 1) — antall påmeldte som minimum må ha kompetansen
- Eksempel for "Skiheis" med 4 vakter: Heisfører MinimumRequired=2, Snøskuter MinimumRequired=1
- Advarsel vises på ressursen hvis antall påmeldte med godkjent, ikke-utløpt kompetanse < MinimumRequired

## Anleggskrav (#155)

- Minst N av de som er på vakt i anlegget skal ha en kompetanse på hvert tidspunkt i åpningstiden (vaktlistens
  `[StartTime, EndTime)`), uansett vakttype. Eksempel: minst én snøskuterfører.
- Settes per vaktliste (`EventCompetencyRequirements`) og mal (`EventTemplateCompetencyRequirements`). Kopieres fra mal til
  vaktliste ved opprettelse fra mal, og fra vaktliste til mal ved «lag mal fra vaktliste».
- API: `competencyRequirements` på `EventRequest`/`EventTemplateRequest` erstatter hele settet; `null` = uendret, tom liste =
  fjern alle. Validering (400): antall >= 1, ingen duplikat kompetanse, kompetansen må finnes.
- Beregning: `FacilityRequirementRules.FindBreaches` (ren). Alle bemannede vakter der brukeren har gyldig kompetanse
  (`CompetencyRules.IsValid`) teller; vaktens tider, ellers oppgavens; klippet til åpningstiden; distinkte brukere per
  tidsrom. `EventResponse.competencyWarnings` har én advarsel per sammenhengende tidsrom med samme antall under kravet.
- Bare advarsel — blokkerer ikke påmelding.

## Datamodell

```
Competencies (masterliste: Name, Description, HasExpiry, Inactive)
├── CompetencyApprovers (CompetencyId, UserId — hvem kan godkjenne)
├── UserCompetencies (UserId, CompetencyId, Approved, ApprovedDate, ApprovedBy, ExpiryDate, Created)
├── ResourceTypeCompetencies (ResourceTypeId, CompetencyId, MinimumRequired) — vaktkrav
├── EventCompetencyRequirements (EventId, CompetencyId, MinimumRequired) — anleggskrav på vaktliste
└── EventTemplateCompetencyRequirements (EventTemplateId, CompetencyId, MinimumRequired) — anleggskrav på mal
```

## Arkitektur

- `ICompetencyRepository` → `CompetencyRepository` (abstraherer DB)
- `ICompetencyService` → `CompetencyService` (forretningslogikk)
- `CompetenciesController` (HTTP-lag)
- Mønster som senere kan rulles ut til resten av kodebasen

## Implementeringsrekkefølge

Backend først (DB → entities → repository → service → controller), deretter frontend. Ikke endre eksisterende treningssystem.
