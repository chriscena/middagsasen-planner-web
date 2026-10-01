import { boot } from "quasar/wrappers";
import { Notify } from "quasar";
import { START_LOCATION } from "vue-router";
import {
  createVersionChecker,
  tryReload,
  isChunkLoadError,
} from "src/shared/appVersion";

const FETCH_TIMEOUT_MS = 5000;

function getSessionStorage() {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

// Oppdager ny deploy og laster appen på nytt, slik at brukere med fanen
// åpen lenge ikke kjører gammel JS eller får feil ved lasting av chunks.
export default boot(({ router }) => {
  if (process.env.DEV) return;

  const checker = createVersionChecker({
    currentVersion: __APP_VERSION__.version,
    // Tidsavbrudd slik at en treg forespørsel ikke holder igjen navigering.
    fetchFn: (url, options) =>
      fetch(url, { ...options, signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) }),
  });

  const reload = (path, reason) =>
    tryReload({
      location: window.location,
      path,
      storage: getSessionStorage(),
      reason,
    });

  // Ved navigering: ny versjon gir full sideinnlasting til målet. Registreres
  // etter auth-guarden i router/index.js, og kjører derfor etter den.
  router.beforeEach(async (to, from) => {
    if (from === START_LOCATION) return true;
    if (await checker.check()) {
      window.location.assign(to.fullPath);
      return false;
    }
    return true;
  });

  // Når fanen blir synlig igjen: vis én vedvarende melding om ny versjon.
  let notified = false;
  document.addEventListener("visibilitychange", async () => {
    if (document.visibilityState !== "visible" || notified) return;
    if (!(await checker.check()) || notified) return;
    notified = true;
    Notify.create({
      message: "Ny versjon tilgjengelig",
      timeout: 0,
      actions: [
        {
          label: "Oppdater",
          color: "white",
          handler: () => window.location.reload(),
        },
        { icon: "close", color: "white", round: true, flat: true, size: "sm" },
      ],
    });
  });

  // Chunk-feil etter deploy (gamle filer finnes ikke lenger).
  // Hvis løkkesperren stopper reload, slipper vi feilen videre som normalt.
  window.addEventListener("vite:preloadError", (event) => {
    if (reload(undefined, "chunk")) event.preventDefault();
  });

  router.onError((error, to) => {
    if (isChunkLoadError(error)) reload(to?.fullPath, "chunk");
  });
});
