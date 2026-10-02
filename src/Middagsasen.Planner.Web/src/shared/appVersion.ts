// Versjonssjekk mot /version.json slik at brukere med en gammel fane
// oppdager at en ny versjon er deployet. All logikk er ren og får
// avhengigheter (fetch, klokke, storage) injisert, slik at den kan testes.

export const VERSION_URL = "/version.json";
export const DEFAULT_THROTTLE_MS = 5 * 60 * 1000;
const RELOAD_KEY_PREFIX = "app-reload:";

export const CHUNK_ERROR_PATTERN =
  /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module/i;

// Minimal del av Response som versjonssjekken bruker.
export interface VersionResponse {
  ok: boolean;
  json(): Promise<unknown>;
}

export type FetchFn = (
  url: string,
  init: RequestInit
) => Promise<VersionResponse | null | undefined>;

export interface VersionCheckerOptions {
  currentVersion: string | null | undefined;
  fetchFn: FetchFn;
  now?: () => number;
  throttleMs?: number;
}

export interface VersionChecker {
  check(): Promise<boolean>;
  readonly updateAvailable: boolean;
  readonly latestVersion: string | null;
}

export interface ReloadLocation {
  assign(url: string): void;
  reload(): void;
}

export type ReloadStorage = Pick<Storage, "getItem" | "setItem">;

export interface ReloadRequest {
  key: string;
  path?: string | undefined;
}

export interface ReloadOnceOptions extends ReloadRequest {
  location: ReloadLocation;
  storage: ReloadStorage | null | undefined;
}

export type ReloadFn = (request: ReloadRequest) => boolean;

interface NavigationLocation {
  path: string;
  fullPath: string;
}

export interface DecideNavigationOptions {
  to: NavigationLocation;
  from: NavigationLocation;
  isStartLocation: boolean;
  checker: VersionChecker;
  reload: ReloadFn;
}

export interface VersionInfo {
  builtAt?: string | undefined;
  sha?: string | undefined;
}

export function isChunkLoadError(error: unknown): boolean {
  const message =
    typeof error === "object" && error !== null && "message" in error
      ? error.message
      : undefined;
  return typeof message === "string" && CHUNK_ERROR_PATTERN.test(message);
}

export function createVersionChecker({
  currentVersion,
  fetchFn,
  now = () => Date.now(),
  throttleMs = DEFAULT_THROTTLE_MS,
}: VersionCheckerOptions): VersionChecker {
  // Remote-versjonen som ble oppdaget, når den avviker fra currentVersion.
  let latestVersion: string | null = null;
  let lastCheckAt: number | null = null;
  let inFlight: Promise<boolean> | null = null;

  async function fetchRemoteVersion(): Promise<string | null> {
    try {
      const response = await fetchFn(`${VERSION_URL}?t=${now()}`, {
        cache: "no-store",
      });
      if (!response?.ok) return null;
      const data = await response.json();
      const version =
        typeof data === "object" && data !== null && "version" in data
          ? data.version
          : undefined;
      return typeof version === "string" ? version : null;
    } catch {
      // Nettverksfeil eller ugyldig JSON: ignorer stille.
      return null;
    }
  }

  async function check(): Promise<boolean> {
    // Når ny versjon først er oppdaget, trenger vi ikke spørre igjen.
    if (latestVersion !== null) return true;
    if (!currentVersion) return false;

    // Samtidige kall deler samme forespørsel.
    if (inFlight) return inFlight;

    const timestamp = now();
    if (lastCheckAt !== null && timestamp - lastCheckAt < throttleMs) {
      return false;
    }
    lastCheckAt = timestamp;

    inFlight = (async () => {
      const remoteVersion = await fetchRemoteVersion();
      if (remoteVersion && remoteVersion !== currentVersion) {
        latestVersion = remoteVersion;
      }
      return latestVersion !== null;
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
      return latestVersion !== null;
    },
    get latestVersion() {
      return latestVersion;
    },
  };
}

function safeGet(
  storage: ReloadStorage | null | undefined,
  key: string
): string | null {
  try {
    return storage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

function safeSet(
  storage: ReloadStorage | null | undefined,
  key: string,
  value: string
): void {
  try {
    storage?.setItem(key, value);
  } catch {
    // Storage kan være utilgjengelig (privat modus o.l.); ignorer.
  }
}

// Løkkesperre: laster inn på nytt høyst én gang per nøkkel per økt
// (sessionStorage). Nøkkelen inneholder versjonen, f.eks. "remote:<versjon>"
// eller "chunk:<versjon>", slik at en CDN/nettleser som serverer gammel HTML
// ikke gir en uendelig løkke. Returnerer true hvis reload ble startet.
//
// Er storage utilgjengelig, reloader vi likevel. Da finnes ingen sperre, men
// det er sjeldent og akseptert risiko.
export function reloadOnce({
  location,
  storage,
  key,
  path,
}: ReloadOnceOptions): boolean {
  const storageKey = RELOAD_KEY_PREFIX + key;
  if (safeGet(storage, storageKey) !== null) return false;

  safeSet(storage, storageKey, "1");
  if (path) location.assign(path);
  else location.reload();
  return true;
}

// Beslutning for router.beforeEach. Venter aldri på nettverket: hvis en ny
// versjon allerede er kjent, lastes målruten på nytt (returnerer false). Ellers
// startes en sjekk i bakgrunnen, og reload skjer ved neste navigering.
// `reload` er reloadOnce med location/storage bundet.
export function decideNavigation({
  to,
  from,
  isStartLocation,
  checker,
  reload,
}: DecideNavigationOptions): boolean {
  if (isStartLocation) return true;

  // Bare query/hash endres (paginering, kalender-bla o.l.): ikke reload.
  const samePath = to.path === from.path;
  if (
    !samePath &&
    checker.updateAvailable &&
    reload({ key: `remote:${checker.latestVersion}`, path: to.fullPath })
  ) {
    return false;
  }

  checker.check().catch(() => {});
  return true;
}

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

// Formaterer versjon for visning, f.eks. "v 2026-10-01 14:32 · 9dd31cd" (lokal tid).
export function formatVersion({ builtAt, sha }: VersionInfo = {}): string {
  const date = builtAt ? new Date(builtAt) : null;
  if (!date || isNaN(date.getTime())) return sha ? `v ${sha}` : "";

  const formatted =
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}`;
  return sha ? `v ${formatted} · ${sha}` : `v ${formatted}`;
}
