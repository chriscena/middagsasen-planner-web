import { getErrorResponse } from "src/shared/apiError";

const LOGIN_PATH = "/login";
const AUTH_ENDPOINT_PREFIX = "/api/authentication/";

// Minimal del av en axios-feil som håndtereren leser.
export interface UnauthorizedError {
  response?: { status?: number | undefined } | undefined;
  config?: { url?: string | undefined } | undefined;
}

// Strukturelle typer for avhengighetene, slik at tester kan sende inn fakes.
export interface UnauthorizedAuthStore {
  readonly user: unknown;
  removeUserSession(): void;
}

export interface UnauthorizedRouter {
  currentRoute: { value: { path: string; fullPath: string } };
  replace(to: { path: string; query: { redirect: string } }): unknown;
}

export type UnauthorizedNotify = (options: { message: string }) => void;

export interface UnauthorizedDeps {
  authStore: UnauthorizedAuthStore;
  router: UnauthorizedRouter;
  notify: UnauthorizedNotify;
}

// Kun relative stier innenfor appen er gyldige redirect-mål (hindrer open redirect).
export function isSafeRedirect(path: unknown): path is string {
  return (
    typeof path === "string" &&
    path.startsWith("/") &&
    !path.startsWith("//") &&
    !path.startsWith("/\\")
  );
}

function getUrl(error: object): string {
  if (!("config" in error)) return "";
  const config = error.config;
  if (typeof config !== "object" || config === null || !("url" in config)) {
    return "";
  }
  return typeof config.url === "string" ? config.url : "";
}

/**
 * Sant for 401 fra et vanlig API-endepunkt, dvs. utløpt/ugyldig sesjon.
 * Disse håndteres av `handleUnauthorized` (rydder sesjon, ett «logget ut»-
 * varsel), så kallere skal ikke vise et eget feilvarsel. 401 fra
 * `/api/authentication/` (f.eks. feil passord) regnes ikke med.
 */
export function isSessionExpiredError(error: unknown): boolean {
  const response = getErrorResponse(error);
  if (response?.status !== 401) return false;
  // `getErrorResponse` returnerer bare noe når `error` er et objekt.
  return !getUrl(error as object).startsWith(AUTH_ENDPOINT_PREFIX);
}

// Håndterer 401 fra API-et: rydder sesjonen og sender brukeren til innlogging.
// Avviser alltid med den opprinnelige feilen slik at kallere kan håndtere den.
export function handleUnauthorized(
  error: UnauthorizedError | null | undefined,
  { authStore, router, notify }: UnauthorizedDeps
): Promise<never> {
  if (isSessionExpiredError(error)) {
    // Rydd alltid sesjonen, også når ingen bruker er lastet, slik at et
    // ugyldig token ikke blir liggende igjen.
    const hadUser = !!authStore.user;
    authStore.removeUserSession();

    // Kun første 401 (mens bruker fortsatt var satt) varsler og navigerer,
    // slik at parallelle 401-svar ikke gir dobbel melding/navigering.
    const currentRoute = router.currentRoute.value;
    if (hadUser && currentRoute.path !== LOGIN_PATH) {
      notify({ message: "Du er logget ut. Logg inn på nytt." });
      router.replace({
        path: LOGIN_PATH,
        query: { redirect: currentRoute.fullPath },
      });
    }
  }

  return Promise.reject(error);
}
