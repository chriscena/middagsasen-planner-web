// Stabile klientnøkler for listeelementer som ennå ikke har id fra API-et
// (brukes som `:key` i `v-for`). Nøklene sendes aldri til API-et.

let fallbackCounter = 0;

/**
 * Nøkkel for et element som allerede har id fra API-et, ellers en ny unik nøkkel.
 * `crypto.randomUUID` finnes bare i sikre kontekster (https/localhost), så det
 * faller tilbake til en teller f.eks. ved `quasar dev` over LAN-IP.
 */
export function newClientKey(id?: number | null): string {
  if (id !== null && id !== undefined && id !== 0) return `id-${id}`;
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID();
  }
  fallbackCounter += 1;
  return `key-${Date.now()}-${fallbackCounter}`;
}
