// Oppgaver fra skjemaene (vaktliste og mal) til request: hvilke som sendes,
// validering av tidene og mapping til ResourceRequest (vaktliste) og
// ResourceTemplateRequest (mal). Vaktlista sender i tillegg
// `originalShiftCount`, så backend kan oppdage at antall vakter er endret av
// andre siden skjemaet ble lastet (#151).

import type {
  ResourceRequest,
  ResourceTemplateRequest,
  ResourceTypeResponse,
} from "@/types";
import { isValidTime, toTimeWire } from "@/shared/time";

// Det skjemaene (ResourceFormModel i EventForm og TemplateForm) har felles.
export interface ResourceDraft {
  id?: number | null | undefined;
  resourceType: Pick<ResourceTypeResponse, "id"> | null;
  startTime: string | null;
  endTime: string | null;
  shiftCount: number | string | null;
  isDeleted?: boolean | undefined;
}

// Oppgave i vaktlisteskjemaet (EventForm).
export interface EventResourceDraft extends ResourceDraft {
  // Antall vakter oppgaven ble lastet med fra serveren. Endres ikke av
  // oppgavedialogen. Backend sammenligner med lagret verdi: er den lik, settes
  // `shiftCount`; har andre endret den imens (f.eks. ledige vakter, #142),
  // svarer backend 409 med norsk `detail`, og skjemaet blir stående åpent.
  originalShiftCount?: number | null | undefined;
}

/**
 * Oppgavene som ikke er slettet: de som vises i lista, og de som kreves for å
 * lagre (en mal eller vaktliste med bare slettede oppgaver kan ikke lagres).
 */
export function visibleResources<T extends Pick<ResourceDraft, "isDeleted">>(
  resources: T[]
): T[] {
  return resources.filter((r) => !r.isDeleted);
}

/**
 * Første oppgave som ikke er slettet og har ugyldig start- eller sluttid, ellers
 * undefined. Slettede oppgaver sjekkes ikke: de sendes med de opprinnelige
 * tidene fra serveren (sletting forkaster endringer i oppgavedialogen).
 */
export function findInvalidResource<T extends ResourceDraft>(
  resources: T[]
): T | undefined {
  return resources.find(
    (r) =>
      !r.isDeleted && (!isValidTime(r.startTime) || !isValidTime(r.endTime))
  );
}

// Nye oppgaver (uten id) som er slettet, finnes ikke på serveren og sendes ikke.
function isSent(r: ResourceDraft): boolean {
  return !(r.isDeleted && !r.id);
}

function toTemplateRequest(r: ResourceDraft): ResourceTemplateRequest {
  return {
    id: r.id ?? null,
    // Oppgavedialogen krever vakttype før lagring (canAdd).
    resourceTypeId: r.resourceType!.id,
    // Bare klokkeslett; backend legger oppgaven på riktig døgn.
    startTime: toTimeWire(r.startTime),
    endTime: toTimeWire(r.endTime),
    // q-input type="number" kan gi string; Number() sender et tall.
    shiftCount: Number(r.shiftCount),
    isDeleted: r.isDeleted ?? false,
  };
}

/**
 * Oppgavene i en mal som request (uten `originalShiftCount`). Nye oppgaver som
 * er slettet, utelates. Kaster RangeError for ugyldige tider, så kall
 * `findInvalidResource` først.
 */
export function toResourceTemplateRequests(
  resources: ResourceDraft[]
): ResourceTemplateRequest[] {
  return resources.filter(isSent).map(toTemplateRequest);
}

/**
 * Oppgavene i en vaktliste som request. Eksisterende oppgaver sender
 * `originalShiftCount` (konfliktsjekk); nye sender null (ingen sjekk).
 * Nye oppgaver som er slettet, utelates. Kaster RangeError for ugyldige tider,
 * så kall `findInvalidResource` først.
 */
export function toResourceRequests(
  resources: EventResourceDraft[]
): ResourceRequest[] {
  return resources.filter(isSent).map((r) => ({
    ...toTemplateRequest(r),
    originalShiftCount: r.id ? (r.originalShiftCount ?? null) : null,
  }));
}
