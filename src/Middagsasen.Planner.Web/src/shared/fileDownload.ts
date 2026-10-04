// Nedlasting av filer som krever innlogging.
//
// En vanlig `<a href>` sender ikke Bearer-token, så filen hentes via `api`
// (interceptoren legger på tokenet) som en Blob, og lagres så lokalt.
import { api } from "@/boot/axios";
import { getErrorResponse } from "@/shared/apiError";
import { notifyApiError } from "@/shared/notifyApiError";
import type { FileInfoResponse } from "@/types";

// Hvor lenge object-URL-en lever etter klikket. Noen nettlesere (Firefox)
// starter nedlastingen asynkront, så vi venter litt før vi frigjør den.
export const REVOKE_DELAY_MS = 1000;

// Filer kan være store og nettet tregt, så nedlasting får en mye rausere
// grense enn standard-timeouten på 10 s (se `shared/requestDefaults.ts`).
export const DOWNLOAD_TIMEOUT_MS = 120_000;

export const DOWNLOAD_ERROR_FALLBACK = "Klarte ikke å hente filen.";

/**
 * Med `responseType: "blob"` blir også feil-bodyen en Blob. Gjør den om til
 * JSON (ProblemDetails) eller tekst, slik at `notifyApiError` kan lese den.
 */
async function unwrapBlobError(error: unknown): Promise<void> {
  const response = getErrorResponse(error);
  if (!response || !(response.data instanceof Blob)) return;
  try {
    const text = await response.data.text();
    try {
      response.data = JSON.parse(text) as unknown;
    } catch {
      response.data = text;
    }
  } catch {
    response.data = undefined;
  }
}

/**
 * Lagrer en Blob som fil via en midlertidig `<a download>` + `click()`.
 *
 * Valgt fremfor `window.open`: backend svarer med
 * `Content-Disposition: attachment`, så den gamle lenken lastet ned filen
 * uten å åpne ny fane. Et programmatisk klikk på en `download`-lenke gir
 * samme opplevelse og blir ikke stoppet av popup-blokkering, selv om det
 * skjer etter en `await`.
 */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.style.display = "none";
  document.body.appendChild(link);
  try {
    link.click();
  } finally {
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS);
  }
}

/**
 * Henter en fil for en vakttype (med innlogging) og laster den ned med
 * riktig filnavn. Kaster videre ved feil; komponenter bruker
 * `downloadResourceTypeFileOrNotify`.
 */
export async function downloadResourceTypeFile(
  file: Pick<FileInfoResponse, "id" | "resourceTypeId" | "fileName">
): Promise<void> {
  let blob: Blob;
  try {
    const response = await api.get<Blob>(
      `/api/resourcetypes/${file.resourceTypeId}/files/${file.id}`,
      {
        responseType: "blob",
        signal: AbortSignal.timeout(DOWNLOAD_TIMEOUT_MS),
      }
    );
    blob = response.data;
  } catch (error) {
    await unwrapBlobError(error);
    throw error;
  }
  saveBlob(blob, file.fileName);
}

/**
 * Som `downloadResourceTypeFile`, men viser feilen til brukeren via
 * `notifyApiError` i stedet for å kaste. Brukes direkte fra klikk-handlere.
 */
export async function downloadResourceTypeFileOrNotify(
  file: Pick<FileInfoResponse, "id" | "resourceTypeId" | "fileName">
): Promise<void> {
  try {
    await downloadResourceTypeFile(file);
  } catch (error) {
    notifyApiError(error, DOWNLOAD_ERROR_FALLBACK);
  }
}
