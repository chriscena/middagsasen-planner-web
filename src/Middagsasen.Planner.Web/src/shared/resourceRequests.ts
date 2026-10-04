// Vakter fra skjemaene (vaktliste og mal) til request: hvilke som sendes,
// validering av tidene og mapping til ResourceRequest/ResourceTemplateRequest
// (samme form).

import type { ResourceRequest, ResourceTypeResponse } from "@/types";
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

/**
 * Vaktene som request. Nye vakter (uten id) som er slettet, utelates; de
 * finnes ikke på serveren. Kaster RangeError for ugyldige tider, så kall
 * `findInvalidResource` først.
 */
export function toResourceRequests(
  resources: ResourceDraft[]
): ResourceRequest[] {
  return resources
    .filter((r) => !(r.isDeleted && !r.id))
    .map((r) => ({
      id: r.id ?? null,
      // Vaktdialogene krever vakttype før lagring (canAdd).
      resourceTypeId: r.resourceType!.id,
      // Bare klokkeslett; backend legger vakta på riktig døgn.
      startTime: toTimeWire(r.startTime),
      endTime: toTimeWire(r.endTime),
      // q-input type="number" kan gi string; Number() sender et tall.
      minimumStaff: Number(r.minimumStaff),
      isDeleted: r.isDeleted ?? false,
    }));
}
