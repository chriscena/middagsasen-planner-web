---
status: proposed
---

# Opplæring erstattes av kompetanse

I dag finnes to overlappende systemer for hva som kreves for å ta en vakt: opplæring per vakttype (`ResourceTypeTraining`/`ResourceTypeTrainer`) og frittstående kompetanser med godkjenning, utløpsdato og krav (`Competency` m.fl.). Forslaget er å la kompetanse dekke begge deler: alt en vakt kan kreve, fra formelle sertifikater (snøskuter, førstehjelp) til det man lærer på stedet (kiosk, storheis), blir en kompetanse. Opplæring blir bare måten man får en kompetanse på. Funksjoner som i dag bare finnes for opplæring, f.eks. at en bruker melder opplæringsbehov og at opplæreren får beskjed, flyttes over til kompetanse. Begrunnelsen er at kompetansesystemet allerede håndterer krav bedre, og at to parallelle begreper for «kan gjøre denne vakten» skaper forvirring.

## Konsekvenser

- Opplæringsansvar knyttes til en kompetanse, ikke til en vakttype. En opplærer kan godkjenne sin kompetanse, og en admin kan godkjenne alle.
- Eksisterende opplæringsdata må migreres til brukerkompetanser, og hver vakttype med opplæring i dag får en tilsvarende kompetanse.
- Den egne godkjennerlisten (`CompetencyApprover`) blir i praksis en opplærerliste.
