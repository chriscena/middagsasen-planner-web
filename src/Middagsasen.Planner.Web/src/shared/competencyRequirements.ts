// Anleggskrav i skjemaene (vaktliste og mal): skjemamodell, validering
// (speiler backend: minst 1, ikke samme kompetanse to ganger), valg av
// kompetanser og mapping til CompetencyRequirementRequest. I tillegg
// visningsteksten for brudd på anleggskrav i en vaktliste.
//
// Skjemaene sender alltid hele lista (tom liste fjerner alle anleggskrav);
// `null`/utelatt i requesten betyr «uendret» i backend og brukes ikke herfra.

import { newClientKey } from "@/shared/clientKey";
import { formatTimeRange } from "@/shared/time";
import type {
  CompetencyRequirementRequest,
  CompetencyRequirementResponse,
  CompetencyResponse,
  FacilityCompetencyWarningResponse,
} from "@/types";

// Ett anleggskrav i skjemaet.
export interface CompetencyRequirementDraft {
  // Stabil nøkkel for `:key` i lista (se newClientKey). Sendes ikke til API-et.
  clientKey: string;
  competencyId: number | null;
  // Navnet fra serveren, så et lagret krav vises selv om kompetansen er
  // inaktiv eller ikke finnes i lista over kompetanser.
  competencyName?: string | null | undefined;
  // q-input type="number" kan gi string når brukeren skriver.
  minimumRequired: number | string | null;
}

// Feil for ett anleggskrav; undefined = ingen feil i feltet.
export interface CompetencyRequirementError {
  competency?: string | undefined;
  minimumRequired?: string | undefined;
}

// Kompetanse som kan velges i en rad.
export type CompetencyOption = Pick<CompetencyResponse, "id" | "name">;

/** Anleggskravene fra serveren som skjemamodell. */
export function toCompetencyRequirementDrafts(
  requirements: CompetencyRequirementResponse[] | null | undefined
): CompetencyRequirementDraft[] {
  return (requirements ?? []).map((r) => ({
    // Anleggskrav har ingen egen id i API-et; nøkkelen lages når skjemaet
    // lastes og følger raden (også om kompetansen byttes).
    clientKey: newClientKey(),
    competencyId: r.competencyId,
    competencyName: r.competencyName,
    minimumRequired: r.minimumRequired,
  }));
}

/** Nytt, tomt anleggskrav (minst én). */
export function newCompetencyRequirementDraft(): CompetencyRequirementDraft {
  return {
    clientKey: newClientKey(),
    competencyId: null,
    competencyName: null,
    minimumRequired: 1,
  };
}

function isValidMinimum(value: number | string | null): boolean {
  if (value === null || value === "") return false;
  const n = Number(value);
  return Number.isInteger(n) && n >= 1;
}

/**
 * Feil per anleggskrav (nøkkel: clientKey). Rader uten feil er ikke med.
 * Andre (og senere) rad med samme kompetanse får feilen.
 */
export function competencyRequirementErrors(
  drafts: CompetencyRequirementDraft[]
): Record<string, CompetencyRequirementError> {
  const errors: Record<string, CompetencyRequirementError> = {};
  const seen = new Set<number>();
  for (const draft of drafts) {
    const error: CompetencyRequirementError = {};
    if (draft.competencyId === null) {
      error.competency = "Velg kompetanse";
    } else if (seen.has(draft.competencyId)) {
      error.competency = "Kompetansen er allerede valgt";
    } else {
      seen.add(draft.competencyId);
    }
    if (!isValidMinimum(draft.minimumRequired)) {
      error.minimumRequired = "Må være et heltall på minst 1";
    }
    if (error.competency || error.minimumRequired) {
      errors[draft.clientKey] = error;
    }
  }
  return errors;
}

/** Alle anleggskravene er gyldige (kan lagres). */
export function areValidCompetencyRequirements(
  drafts: CompetencyRequirementDraft[]
): boolean {
  return Object.keys(competencyRequirementErrors(drafts)).length === 0;
}

/**
 * Anleggskravene som request (hele lista). Kall
 * `areValidCompetencyRequirements` først; rader uten kompetanse utelates.
 */
export function toCompetencyRequirementRequests(
  drafts: CompetencyRequirementDraft[]
): CompetencyRequirementRequest[] {
  return drafts
    .filter(
      (d): d is CompetencyRequirementDraft & { competencyId: number } =>
        d.competencyId !== null
    )
    .map((d) => ({
      competencyId: d.competencyId,
      minimumRequired: Number(d.minimumRequired),
    }));
}

/**
 * Kompetansene som kan velges i en rad: aktive kompetanser som ikke er valgt
 * i en annen rad, pluss radens egen kompetanse (også om den er inaktiv eller
 * mangler i lista, da med navnet fra serveren).
 */
export function competencyOptionsFor(
  draft: CompetencyRequirementDraft,
  drafts: CompetencyRequirementDraft[],
  competencies: CompetencyResponse[]
): CompetencyOption[] {
  const takenByOthers = new Set(
    drafts
      .filter((d) => d.clientKey !== draft.clientKey)
      .map((d) => d.competencyId)
  );
  const options: CompetencyOption[] = competencies
    .filter(
      (c) =>
        c.id === draft.competencyId || (!c.inactive && !takenByOthers.has(c.id))
    )
    .map((c) => ({ id: c.id, name: c.name }));
  if (
    draft.competencyId !== null &&
    !options.some((o) => o.id === draft.competencyId)
  ) {
    options.push({
      id: draft.competencyId,
      name: draft.competencyName ?? `Kompetanse ${draft.competencyId}`,
    });
  }
  return options.sort((a, b) => a.name.localeCompare(b.name, "nb"));
}

/** Brudd på et anleggskrav, f.eks. "Snøskuterfører 19:00-21:00: 0 av 1". */
export function formatFacilityWarning(
  warning: FacilityCompetencyWarningResponse
): string {
  return `${warning.competencyName} ${formatTimeRange(
    warning.startTime,
    warning.endTime
  )}: ${warning.currentCount} av ${warning.minimumRequired}`;
}
