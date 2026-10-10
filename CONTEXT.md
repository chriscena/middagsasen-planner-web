# Middagsåsen Planner

Vaktplanlegging for skianlegget Middagsåsen: hvilke arrangementer som skal bemannes, hvor mange som trengs til hva, og hvem som tar vaktene.

## Språk

### Planlegging

**Vaktliste**:
Bemanningsplanen for ett arrangement i anlegget, f.eks. ordinær åpningstid en hverdagskveld, Diskokveld eller Skoleskidag. Hvert arrangement har nøyaktig én vaktliste, så arrangementet har ikke et eget begrep.
_Unngå_: Event, arrangement (som eget begrep)

**Åpningstid**:
Tidsrommet en vaktliste dekker.
_Unngå_: Driftstid

**Bemanningsbehov**:
Oppgavene en vaktliste eller mal består av, til sammen.
_Unngå_: Bemanning, behov

**Oppgave**:
En vakttype som skal dekkes i et gitt tidsrom på en vaktliste eller mal, med et bestemt antall vakter. Samme vakttype kan forekomme som flere oppgaver i én vaktliste, f.eks. storheis 18–20 og storheis 20–22.
_Unngå_: EventResource, ressurs, vakttype (om denne)

**Antall vakter**:
Hvor mange vakter en oppgave har. Det er et fast antall og ikke et minimum: når alle vaktene er bemannet, kan ingen flere ta vakt på oppgaven. Heter `ShiftCount` i koden (standardverdien per vakttype heter `DefaultShiftCount`), men databasekolonnene heter fortsatt `MinimumStaff` og `DefaultStaff`.
_Unngå_: Minimumsbemanning, MinimumStaff

**Mangler bemanning**:
En oppgave eller vaktliste som har minst én ledig vakt.
_Unngå_: Underbemannet

**Mal**:
En forhåndsdefinert vaktliste for et arrangement som går igjen, med kjent bemanningsbehov. Malen er bare et utgangspunkt: en vaktliste som er opprettet fra den, er uavhengig av malen.
_Unngå_: Template, vaktlistemal

### Vakter

**Vakttype**:
En type arbeid som må dekkes i anlegget, f.eks. storheis, barneheis, kiosk eller skiutleie. Arbeid som krever ulik kompetanse, er ulike vakttyper, selv om det ligner (storheis og barneheis er to vakttyper).
_Unngå_: Ressurs, ressurstype, Resource, heisvakt (som én vakttype)

**Vakt**:
Én persons plass i en oppgave. En vakt er enten ledig eller bemannet.
_Unngå_: Shift, plass

**Ledig vakt**:
En vakt som ingen har tatt ennå.
_Unngå_: Ledig plass, tom plass

**Bemannet vakt**:
En vakt som en bruker har tatt.
_Unngå_: Tatt plass

**Vaktpåminnelse**:
En SMS dagen før som lister vaktene en bruker har neste dag. Sendes bare til brukere som selv har slått den på, og høyst én per bruker per dag.
_Unngå_: Varsel, notifikasjon, reminder

**Bemanningsvarsel**:
En SMS til admin når en bruker trekker seg fra en vakt slik at oppgaven mangler bemanning, og vakta starter om kort tid (standard to dager eller mindre, konfigurerbart). Sendes bare til admin som selv har slått det på, og aldri til den som trakk seg. Heter `StaffingAlerts` i koden.
_Unngå_: Frafallsvarsel, varsel (uten presisering), notifikasjon

### Kvalifikasjoner

**Kompetanse**:
Noe en bruker kan, og som en vakt kan kreve. Det kan være formelt (snøskuterfører, førstehjelp) eller lært på stedet (kiosk, skiutleie, storheis). En brukers kompetanse må godkjennes av en opplærer for kompetansen eller av en admin, og kan ha utløpsdato.
_Unngå_: Sertifikat, kvalifikasjon, opplæring (om selve ferdigheten), godkjenner (som egen rolle)

**Opplæring**:
Måten en bruker får en kompetanse på, enten formelt kurs eller enkel innføring på stedet.
_Unngå_: Training, kurs

**Opplæringsbehov**:
At en bruker har meldt at de trenger opplæring i en kompetanse.
_Unngå_: Ønske om opplæring

**Opplærer**:
En bruker med opplæringsansvar for en bestemt kompetanse. Opplæreren får beskjed om opplæringsbehov og kan godkjenne kompetansen.
_Unngå_: Trainer, instruktør

**Vaktkrav**:
At minst et gitt antall av vaktene i en oppgave skal være bemannet av noen med en bestemt kompetanse. Kravet gjelder vakttypen, men oppfylles per oppgave, og det trenger ikke gjelde alle vaktene.
_Unngå_: Kompetansekrav (uten presisering), MinimumRequired

**Anleggskrav**:
At minst et gitt antall av de som er på vakt i anlegget skal ha en bestemt kompetanse på hvert tidspunkt i åpningstiden, uansett vakttype, f.eks. minst én snøskuterfører. Kravet settes per vaktliste og mal. Et brudd gir en advarsel, men hindrer ingenting.
_Unngå_: Kompetansekrav (uten presisering), beredskapskrav
