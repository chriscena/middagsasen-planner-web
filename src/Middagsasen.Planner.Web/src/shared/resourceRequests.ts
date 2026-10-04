// Vakter fra skjemaene (vaktliste og mal) til request: hvilke som sendes,
// validering av tidene og mapping til ResourceRequest (vaktliste) og
// ResourceTemplateRequest (mal). Vaktlista sender i tillegg
// `originalMinimumStaff`, så backend endrer bemanningen relativt (#151).

import type {
  ResourceRequest,
  ResourceTemplateRequest,
  ResourceTypeResponse,
} from "@/types";
import { isValidTime, toTimeWire } from "@/shared/time";

// Det skjemaene (ResourceFormModel og EventPage sin vaktmodell) har felles.
export interface ResourceDraft {
  id?: number | null | undefined;
  resourceType: Pick<ResourceTypeResponse, "id"> | null;
  startTime: string | null;
  endTime: string | null;
  minimumStaff: number | string | null;
  isDeleted?: boolean | undefined;
}

// Vakt i vaktlisteskjemaet (EventForm/EventPage).
export interface EventResourceDraft extends ResourceDraft {
  // Bemanningen vakta ble lastet med fra serveren. Endres ikke av
  // vaktdialogene; backend legger differansen til fersk verdi, så ledige
  // plasser andre har lagt til eller fjernet imens (#142), beholdes.
  originalMinimumStaff?: number | null | undefined;
}

/**
 * Vaktene som ikke er slettet: de som vises i lista, og de som kreves for å
 * lagre (en mal eller vaktliste med bare slettede vakter kan ikke lagres).
 */
export function visibleResources<T extends Pick<ResourceDraft, "isDeleted">>(
  resources: T[]
): T[] {
  return resources.filter((r) => !r.isDeleted);
}

/**
 * Første vakt som ikke er slettet og har ugyldig start- eller sluttid, ellers
 * undefined. Slettede vakter sjekkes ikke: de sendes med de opprinnelige
 * tidene fra serveren (sletting forkaster endringer i vaktdialogen).
 */
export function findInvalidResource<T extends ResourceDraft>(
  resources: T[]
): T | undefined {
  return resources.find(
    (r) =>
      !r.isDeleted && (!isValidTime(r.startTime) || !isValidTime(r.endTime))
  );
}

// Nye vakter (uten id) som er slettet, finnes ikke på serveren og sendes ikke.
function isSent(r: ResourceDraft): boolean {
  return !(r.isDeleted && !r.id);
}

function toTemplateRequest(r: ResourceDraft): ResourceTemplateRequest {
  return {
    id: r.id ?? null,
    // Vaktdialogene krever vakttype før lagring (canAdd).
    resourceTypeId: r.resourceType!.id,
    // Bare klokkeslett; backend legger vakta på riktig døgn.
    startTime: toTimeWire(r.startTime),
    endTime: toTimeWire(r.endTime),
    // q-input type="number" kan gi string; Number() sender et tall.
    minimumStaff: Number(r.minimumStaff),
    isDeleted: r.isDeleted ?? false,
  };
}

/**
 * Vaktene i en mal som request (uten `originalMinimumStaff`). Nye vakter som
 * er slettet, utelates. Kaster RangeError for ugyldige tider, så kall
 * `findInvalidResource` først.
 */
export function toResourceTemplateRequests(
  resources: ResourceDraft[]
): ResourceTemplateRequest[] {
  return resources.filter(isSent).map(toTemplateRequest);
}

/**
 * Vaktene i en vaktliste som request. Eksisterende vakter sender
 * `originalMinimumStaff` (relativ endring); nye sender null (absolutt verdi).
 * Nye vakter som er slettet, utelates. Kaster RangeError for ugyldige tider,
 * så kall `findInvalidResource` først.
 */
export function toResourceRequests(
  resources: EventResourceDraft[]
): ResourceRequest[] {
  return resources.filter(isSent).map((r) => ({
    ...toTemplateRequest(r),
    originalMinimumStaff: r.id ? (r.originalMinimumStaff ?? null) : null,
  }));
}
