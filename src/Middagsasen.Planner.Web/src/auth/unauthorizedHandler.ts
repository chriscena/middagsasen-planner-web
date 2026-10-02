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

// Håndterer 401 fra API-et: rydder sesjonen og sender brukeren til innlogging.
// Avviser alltid med den opprinnelige feilen slik at kallere kan håndtere den.
export function handleUnauthorized(
  error: UnauthorizedError | null | undefined,
  { authStore, router, notify }: UnauthorizedDeps
): Promise<never> {
  const isUnauthorized = error?.response?.status === 401;
  const url = error?.config?.url ?? "";
  const isAuthEndpoint = url.startsWith(AUTH_ENDPOINT_PREFIX);

  if (isUnauthorized && !isAuthEndpoint) {
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
