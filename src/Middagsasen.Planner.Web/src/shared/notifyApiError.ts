// Felles feilvarsel for API-kall som feiler.
//
// Ligger utenfor `apiError.ts` slik at den forblir fri for Quasar- og
// auth-avhengigheter.
import { Notify } from "quasar";
import { isSessionExpiredError } from "src/auth/unauthorizedHandler";
import { getApiErrorMessage } from "src/shared/apiError";

/**
 * Logger feilen og viser et rødt varsel med meldingen fra API-et, eller
 * `fallback` når API-et ikke gir en brukervennlig melding.
 *
 * Utløpt sesjon (401) varsles ikke: axios-interceptoren har allerede vist
 * «Du er logget ut» og sendt brukeren til innlogging.
 */
export function notifyApiError(error: unknown, fallback: string): void {
  if (isSessionExpiredError(error)) return;
  console.error(error);
  Notify.create({
    type: "negative",
    message: getApiErrorMessage(error, fallback),
  });
}
