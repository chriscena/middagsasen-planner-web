// Felles hjelpere for å tolke feil fra API-et.
//
// Backend svarer med ProblemDetails (`application/problem+json`) ved feil:
// `{ type, title, status, detail, traceId }`. I tillegg finnes:
// - ValidationProblemDetails (400) fra modellvalidering: `errors: { felt: string[] }`
// - ren streng som body (f.eks. `BadRequest("Ugyldig telefonnummer")`)
import type { ProblemDetails } from "src/types";

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

// Første ikke-tomme melding fra ValidationProblemDetails.errors.
function firstValidationError(data: ProblemDetails): string | undefined {
  const errors: unknown = (data as Record<string, unknown>).errors;
  if (typeof errors !== "object" || errors === null) return undefined;
  for (const messages of Object.values(errors)) {
    const list: unknown[] = Array.isArray(messages) ? messages : [messages];
    for (const message of list) {
      const text = nonBlank(message);
      if (text) return text;
    }
  }
  return undefined;
}

/**
 * Henter en brukervennlig feilmelding fra en API-feil, i prioritert rekkefølge:
 * 1. ProblemDetails `detail` (ikke-tom, og ikke ved status >= 500, der den er
 *    en generisk engelsk tekst),
 * 2. første melding fra ValidationProblemDetails `errors`,
 * 3. ren streng-body (ikke-tom),
 * 4. ellers `fallback` (f.eks. nettverksfeil, tom body eller 5xx).
 * Ved status >= 500 brukes alltid `fallback`.
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
    return nonBlank(data.detail) ?? firstValidationError(data) ?? fallback;
  }

  return nonBlank(data) ?? fallback;
}
