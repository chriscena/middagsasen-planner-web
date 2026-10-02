// Skjemamodell for vakttyper (ResourceTypesPage): kopi av API-dataene som kan
// endres uten å røre store-staten, og mapping tilbake til request.
import { newClientKey } from "src/shared/clientKey";
import type {
  FileInfoResponse,
  ResourceTypeRequest,
  ResourceTypeResponse,
  ResourceTypeTrainerResponse,
} from "src/types";

// Trener i skjemaet: fra ResourceTypeResponse, evt. markert som slettet.
// Nye trenere (id 0) får fullName fra UserResponse, der den er valgfri.
export type EditableTrainer = Omit<ResourceTypeTrainerResponse, "fullName"> & {
  // Stabil nøkkel for `:key` i lista (se newClientKey). Sendes ikke til API-et.
  clientKey: string;
  fullName?: null | string | undefined;
  isDeleted?: boolean;
};

// Enten en ny vakttype (uten id/navn/filer) eller en kopi av en
// ResourceTypeResponse.
export interface EditableResourceType {
  id: number | null;
  name: string | null;
  // q-input type="number" kan gi string når brukeren skriver.
  defaultStaff: number | string;
  notificationMessage?: null | string;
  trainers: EditableTrainer[];
  files?: FileInfoResponse[];
}

/**
 * Eksplisitt kopi (nye objekter på alle nivåer), slik at en avbrutt dialog
 * ikke endrer store-staten. Erstatter `structuredClone(toRaw(...))`, som kun
 * pakker ut ytterste proxy og kaster DataCloneError på nestede proxies.
 */
export function toEditableResourceType(
  resourceType: ResourceTypeResponse
): EditableResourceType {
  return {
    id: resourceType.id,
    name: resourceType.name,
    defaultStaff: resourceType.defaultStaff,
    notificationMessage: resourceType.notificationMessage ?? null,
    trainers: resourceType.trainers.map((t) => ({
      ...t,
      clientKey: newClientKey(t.id),
      isDeleted: false,
    })),
    files: resourceType.files.map((f) => ({ ...f })),
  };
}

/** Ny trener i skjemaet (id 0), med egen klientnøkkel. */
export function newEditableTrainer(user: {
  id: number;
  fullName?: string | null | undefined;
  phoneNo: string;
}): EditableTrainer {
  return {
    id: 0,
    clientKey: newClientKey(),
    userId: user.id,
    fullName: user.fullName,
    phoneNo: user.phoneNo,
    isDeleted: false,
  };
}

/**
 * Request til API-et. `name` kan være null fra skjemaet; backend avviser
 * manglende navn med 400 (som før). Klientnøkler o.l. sendes ikke.
 */
export function toResourceTypeRequest(
  resource: EditableResourceType
): ResourceTypeRequest {
  return {
    name: resource.name as string,
    defaultStaff: Number(resource.defaultStaff),
    notificationMessage: resource.notificationMessage ?? null,
    trainers: resource.trainers.map((t) => ({
      id: t.id,
      userId: t.userId,
      isDeleted: t.isDeleted ?? false,
    })),
  };
}
