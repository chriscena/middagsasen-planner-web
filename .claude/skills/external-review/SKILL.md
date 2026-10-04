---
name: external-review
description: Run /code-review in a separate, fresh Claude Code process (headless subprocess), read its findings back into the current session and act on them. Use when the user wants a code review "in a new session", "in a separate process", "with fresh eyes", or without polluting the current context. Accepts an optional effort level (default high) and an optional review target (PR number, branch, path).
---

Kjør `/code-review` i en **ny, uavhengig Claude-prosess** slik at reviewen ikke ser (eller forurenses av) konteksten i den nåværende sesjonen. Les resultatet tilbake og ager på det her.

## Argumenter

- Effort-nivå: `low|medium|high|xhigh|max` — standard `high`.
- Valgfritt mål: PR-nummer, branch eller sti. Uten mål reviewes nåværende diff (staged + unstaged + untracked mot HEAD/base).
- `--fix`-lignende ønsker («fiks alt», «bare rapporter») tolkes fra brukerens ord — se steg 4.

## 1. Forbered

- Sjekk at det finnes noe å reviewe: `git status --short` (og `git diff --stat`). Er diffen tom og intet mål er gitt, si fra og stopp.
- Velg en output-fil i scratchpad-katalogen for sesjonen, f.eks. `<scratchpad>/external-review-<timestamp>.json`.

## 2. Start review-prosessen i bakgrunnen

Kjør med **Bash**-verktøyet og `run_in_background: true` (reviewen tar typisk flere minutter; timeout gjerne 1800000 ms). Kjør fra repo-roten:

```bash
claude -p "/code-review <effort> <mål>" \
  --output-format json \
  --permission-mode plan \
  --allowedTools "Read" "Grep" "Glob" "Agent" "Bash(git diff:*)" "Bash(git log:*)" "Bash(git show:*)" "Bash(git status:*)" "Bash(git rev-parse:*)" "Bash(git merge-base:*)" "Bash(git ls-files:*)" "Bash(gh pr view:*)" "Bash(gh pr diff:*)" \
  --no-session-persistence \
  < /dev/null > "<outfil>" 2> "<outfil>.err"
```

Viktig:
- **Aldri** `--fix` eller `--comment` til underprosessen — den skal kun lese og rapportere. Endringer gjøres i *denne* sesjonen (steg 4).
- `--permission-mode plan` + eksplisitt read-only allowlist gjør at underprosessen ikke kan endre filer.
- `< /dev/null` hindrer at `claude -p` venter på stdin.
- Si kort til brukeren at reviewen kjører i bakgrunnen, og fortsett gjerne med annet arbeid. Ikke poll — du blir varslet når prosessen er ferdig.

## 3. Les resultatet

Når bakgrunnsjobben er ferdig:
- Les `<outfil>`. Det er et JSON-objekt; review-teksten ligger i feltet `result`. Sjekk `is_error`/`subtype` — ved feil, vis innholdet i `<outfil>.err` og stopp.
- Trekk ut funnene (fil, linje, beskrivelse, alvorlighet). Underprosessen kan ha sett ting du ikke har kontekst på — **verifiser hvert funn selv** ved å lese den aktuelle koden før du stoler på det. Marker funn som falske positive hvis koden motbeviser dem.

## 4. Ager på funnene

- Presenter de verifiserte funnene kort, mest alvorlige først, med `fil:linje`-referanser. Nevn eventuelle forkastede funn og hvorfor (én linje hver).
- Hvis brukeren ba om å fikse («og fiks», «ager på det»): fiks de verifiserte funnene i denne sesjonen, i tråd med prosjektets arbeidsflyt (f.eks. deleger til frontend-/backend-agent hvis CLAUDE.md sier det). Kjør relevante tester/typecheck etterpå.
- Hvis brukeren ikke sa noe om fiksing: spør hvilke funn som skal fikses (AskUserQuestion med multiSelect er fint), med anbefaling.
- Uklare/designmessige funn: ikke fiks på eget initiativ — løft dem til brukeren.
- Aldri commit eller push som del av denne skillen med mindre brukeren ber om det.
