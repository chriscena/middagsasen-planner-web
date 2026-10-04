// Felles hjelpere for å tolke feil fra API-et.
//
// Backend svarer med ProblemDetails (`application/problem+json`) ved feil:
// `{ type, title, status, detail, traceId }`. I tillegg finnes:
// - ValidationProblemDetails (400) fra modellvalidering: `errors: { felt: string[] }`
//   (meldingene er engelske og tekniske og vises aldri til bruker)
// - ren streng som body (f.eks. `BadRequest("Ugyldig telefonnummer")`)
import type { ProblemDetails } from "@/types";

export interface ErrorResponse {
  status?: unknown;
  data?: unknown;
}

// Feil fra catch er ukjent; plukk ut axios-lignende `response` uten å anta typen.
export function getErrorResponse(error: unknown): ErrorResponse | undefined {
  if (typeof error !== "object" || error === null || !("response" in error)) {
    return undefined;
  }
  const response = error.response;
  return typeof response === "object" && response !== null
    ? response
    : undefined;
}

function isOptional(value: unknown, type: "string" | "number"): boolean {
  return value === undefined || value === null || typeof value === type;
}

/**
 * Sjekker om en respons-body ser ut som ProblemDetails (RFC 9457).
 * Krever minst ett av standardfeltene, med riktig type.
 */
export function isProblemDetails(data: unknown): data is ProblemDetails {
  if (typeof data !== "object" || data === null || Array.isArray(data)) {
    return false;
  }
  const record = data as Record<string, unknown>;
  const hasKnownField = ["type", "title", "status", "detail"].some(
    (key) => key in record
  );
  return (
    hasKnownField &&
    isOptional(record.type, "string") &&
    isOptional(record.title, "string") &&
    isOptional(record.status, "number") &&
    isOptional(record.detail, "string")
  );
}

function nonBlank(value: unknown): string | undefined {
  return typeof value === "string" && value.trim() !== "" ? value : undefined;
}

// ValidationProblemDetails (modellvalidering) har et `errors`-objekt. Meldingene
// der er engelske og tekniske, og skal ikke vises til bruker.
function isValidationProblemDetails(data: ProblemDetails): boolean {
  const errors: unknown = (data as Record<string, unknown>).errors;
  return typeof errors === "object" && errors !== null;
}

/**
 * Henter en brukervennlig feilmelding fra en API-feil, i prioritert rekkefølge:
 * 1. status >= 500 gir `fallback` (generisk tekst, proxy-sider o.l.),
 * 2. ValidationProblemDetails gir `fallback`, også om `detail` er satt
 *    (meldingene fra modellvalidering er engelske og tekniske),
 * 3. ProblemDetails `detail` (ikke-tom, norsk melding fra backend),
 * 4. ren streng-body (ikke-tom),
 * 5. ellers `fallback` (f.eks. nettverksfeil eller tom body).
 */
export function getApiErrorMessage(error: unknown, fallback: string): string {
  const response = getErrorResponse(error);
  if (!response) return fallback;

  const { data } = response;
  const status =
    typeof response.status === "number"
      ? response.status
      : isProblemDetails(data)
        ? data.status
        : undefined;
  // 5xx-meldinger (generisk tekst, proxy-sider o.l.) vises aldri til bruker.
  if (typeof status === "number" && status >= 500) return fallback;

  if (isProblemDetails(data)) {
    if (isValidationProblemDetails(data)) return fallback;
    return nonBlank(data.detail) ?? fallback;
  }

  return nonBlank(data) ?? fallback;
}
