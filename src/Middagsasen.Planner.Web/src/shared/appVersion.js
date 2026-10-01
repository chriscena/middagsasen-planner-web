// Versjonssjekk mot /version.json slik at brukere med en gammel fane
// oppdager at en ny versjon er deployet. All logikk er ren og får
// avhengigheter (fetch, klokke, storage) injisert, slik at den kan testes.

export const VERSION_URL = "/version.json";
export const DEFAULT_THROTTLE_MS = 5 * 60 * 1000;
export const RELOAD_GUARD_MS = 10 * 1000;
const RELOAD_KEY_PREFIX = "app-reload:";

export const CHUNK_ERROR_PATTERN =
  /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module/i;

export function isChunkLoadError(error) {
  const message = typeof error === "string" ? error : error?.message;
  return typeof message === "string" && CHUNK_ERROR_PATTERN.test(message);
}

export function createVersionChecker({
  currentVersion,
  fetchFn,
  now = () => Date.now(),
  throttleMs = DEFAULT_THROTTLE_MS,
}) {
  let updateAvailable = false;
  let lastCheckAt = null;
  let inFlight = null;

  async function fetchRemoteVersion() {
    try {
      const response = await fetchFn(`${VERSION_URL}?t=${now()}`, {
        cache: "no-store",
      });
      if (!response?.ok) return null;
      const data = await response.json();
      return typeof data?.version === "string" ? data.version : null;
    } catch {
      // Nettverksfeil eller ugyldig JSON: ignorer stille.
      return null;
    }
  }

  async function check({ force = false } = {}) {
    // Når ny versjon først er oppdaget, trenger vi ikke spørre igjen.
    if (updateAvailable) return true;
    if (!currentVersion) return false;

    // Samtidige kall deler samme forespørsel.
    if (inFlight) return inFlight;

    const timestamp = now();
    if (!force && lastCheckAt !== null && timestamp - lastCheckAt < throttleMs) {
      return false;
    }
    lastCheckAt = timestamp;

    inFlight = (async () => {
      const remoteVersion = await fetchRemoteVersion();
      if (remoteVersion && remoteVersion !== currentVersion) {
        updateAvailable = true;
      }
      return updateAvailable;
    })();

    try {
      return await inFlight;
    } finally {
      inFlight = null;
    }
  }

  return {
    check,
    get updateAvailable() {
      return updateAvailable;
    },
  };
}

function safeGet(storage, key) {
  try {
    return storage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

function safeSet(storage, key, value) {
  try {
    storage?.setItem(key, value);
  } catch {
    // Storage kan være utilgjengelig (privat modus o.l.); ignorer.
  }
}

// Løkkesperre: laster bare inn på nytt hvis det ikke ble gjort en reload av
// samme årsak de siste `guardMs` millisekundene. Returnerer true hvis reload
// ble startet.
export function tryReload({
  location,
  path,
  storage,
  now = () => Date.now(),
  reason = "default",
  guardMs = RELOAD_GUARD_MS,
}) {
  const key = RELOAD_KEY_PREFIX + reason;
  const timestamp = now();
  const last = Number(safeGet(storage, key));
  if (last && timestamp - last < guardMs) return false;

  safeSet(storage, key, String(timestamp));
  if (path) location.assign(path);
  else location.reload();
  return true;
}

function pad(n) {
  return String(n).padStart(2, "0");
}

// Formaterer versjon for visning, f.eks. "v 2026-10-01 14:32 · 9dd31cd" (lokal tid).
export function formatVersion({ builtAt, sha } = {}) {
  const date = builtAt ? new Date(builtAt) : null;
  if (!date || isNaN(date.getTime())) return sha ? `v ${sha}` : "";

  const formatted =
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}`;
  return sha ? `v ${formatted} · ${sha}` : `v ${formatted}`;
}
