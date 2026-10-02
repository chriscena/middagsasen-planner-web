import type { InternalAxiosRequestConfig } from "axios";

// Standard timeout for API-kall som ikke har sitt eget `signal`.
export const DEFAULT_REQUEST_TIMEOUT_MS = 10_000;

/**
 * Legger på standardverdier for alle kall via `api`:
 * - timeout-signal, men bare når kallet ikke har sitt eget `signal`
 *   (f.eks. fil-nedlasting med lengre grense),
 * - Bearer-token når brukeren er innlogget.
 */
export function applyRequestDefaults(
  config: InternalAxiosRequestConfig,
  token: string | null
): InternalAxiosRequestConfig {
  config.signal ??= AbortSignal.timeout(DEFAULT_REQUEST_TIMEOUT_MS);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
}
