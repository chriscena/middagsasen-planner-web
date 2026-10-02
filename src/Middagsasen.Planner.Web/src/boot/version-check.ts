import { defineBoot } from "#q-app/wrappers";
import { Notify } from "quasar";
import { START_LOCATION } from "vue-router";
import {
  createVersionChecker,
  reloadOnce,
  decideNavigation,
  isChunkLoadError,
  type ReloadFn,
} from "src/shared/appVersion";

const FETCH_TIMEOUT_MS = 5000;

function getSessionStorage(): Storage | null {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

// fetch med tidsavbrudd. Bruker AbortController + setTimeout i stedet for
// AbortSignal.timeout, som mangler i eldre Safari.
async function fetchWithTimeout(
  url: string,
  options: RequestInit
): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

// Oppdager ny deploy og laster appen på nytt, slik at brukere med fanen
// åpen lenge ikke kjører gammel JS eller får feil ved lasting av chunks.
export default defineBoot(({ router }) => {
  if (process.env.DEV) return;

  const currentVersion = __APP_VERSION__.version;
  const checker = createVersionChecker({
    currentVersion,
    fetchFn: fetchWithTimeout,
  });

  // Automatisk reload går via reloadOnce: høyst ett forsøk per nøkkel per økt.
  const reload: ReloadFn = ({ key, path }) =>
    reloadOnce({
      location: window.location,
      storage: getSessionStorage(),
      key,
      path,
    });

  const chunkKey = `chunk:${currentVersion}`;

  // Målet for pågående navigering, slik at chunk-feil kan laste riktig rute.
  let pendingPath: string | null = null;

  // Ved navigering: kjent ny versjon gir full sideinnlasting til målet.
  // Venter aldri på nettverket. Registreres etter auth-guarden i
  // router/index.js, og kjører derfor etter den.
  router.beforeEach((to, from) => {
    pendingPath = to.fullPath;
    return decideNavigation({
      to,
      from,
      isStartLocation: from === START_LOCATION,
      checker,
      reload,
    });
  });

  router.afterEach(() => {
    pendingPath = null;
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
          // Bevisst klikk fra brukeren kan ikke gi løkke, så vi laster alltid
          // inn på nytt i stedet for å gå via reloadOnce.
          handler: () => window.location.reload(),
        },
        // Lukk-knappen kommer fra Notify-defaults (boot/notify-defaults.ts).
      ],
    });
  });

  // Chunk-feil etter deploy (gamle filer finnes ikke lenger). Samme nøkkel
  // for begge hendelsene, så de ikke gir to reloads. Hvis sperren stopper
  // reload, slipper vi feilen videre som normalt.
  window.addEventListener("vite:preloadError", (event) => {
    if (reload({ key: chunkKey, path: pendingPath ?? undefined })) {
      event.preventDefault();
    }
  });

  router.onError((error, to) => {
    if (isChunkLoadError(error)) {
      reload({ key: chunkKey, path: to?.fullPath ?? pendingPath ?? undefined });
    }
  });
});
