const LOGIN_PATH = "/login";
const AUTH_ENDPOINT_PREFIX = "/api/authentication/";

// Kun relative stier innenfor appen er gyldige redirect-mål (hindrer open redirect).
export function isSafeRedirect(path) {
  return (
    typeof path === "string" &&
    path.startsWith("/") &&
    !path.startsWith("//") &&
    !path.startsWith("/\\")
  );
}

// Håndterer 401 fra API-et: rydder sesjonen og sender brukeren til innlogging.
// Avviser alltid med den opprinnelige feilen slik at kallere kan håndtere den.
export function handleUnauthorized(error, { authStore, router, notify }) {
  const isUnauthorized = error?.response?.status === 401;
  const url = error?.config?.url ?? "";
  const isAuthEndpoint = url.startsWith(AUTH_ENDPOINT_PREFIX);

  if (isUnauthorized && !isAuthEndpoint && authStore.user) {
    authStore.removeUserSession();

    const currentRoute = router.currentRoute.value;
    if (currentRoute.path !== LOGIN_PATH) {
      notify({ message: "Du er logget ut. Logg inn på nytt." });
      router.replace({
        path: LOGIN_PATH,
        query: { redirect: currentRoute.fullPath },
      });
    }
  }

  return Promise.reject(error);
}
