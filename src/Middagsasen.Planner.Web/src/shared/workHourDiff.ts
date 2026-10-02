// Rene hjelpefunksjoner for redigering og godkjenning av timeføringer.

type DateTimeValue = string | Date | null | undefined;

export interface WorkHourValues {
  startDateTime?: DateTimeValue;
  endDateTime?: DateTimeValue;
  description?: string | null | undefined;
}

export interface WorkHourChanges {
  startTime?: DateTimeValue;
  endTime?: DateTimeValue;
  description?: string | null | undefined;
}

export type ApprovalStatus = 1 | 2;

export interface WorkHourPatch extends WorkHourChanges {
  approvalStatus?: ApprovalStatus;
}

export type WorkHourErrorKind = "conflict" | "forbidden" | "notFound" | "other";

export interface BulkApprovalCounts {
  ok?: number;
  alreadyProcessed?: number;
  notFound?: number;
  failed?: number;
}

export interface BulkApprovalSummary {
  type: "positive" | "warning";
  message: string;
}

function sameInstant(a: DateTimeValue, b: DateTimeValue): boolean {
  if (!a && !b) return true;
  if (!a || !b) return false;
  return new Date(a).getTime() === new Date(b).getTime();
}

function sameText(
  a: string | null | undefined,
  b: string | null | undefined
): boolean {
  return (a ?? "") === (b ?? "");
}

// Feil fra catch er ukjent; plukk ut axios-lignende `response` uten å anta typen.
function getErrorResponse(
  error: unknown
): { status?: unknown; data?: unknown } | undefined {
  if (typeof error !== "object" || error === null || !("response" in error)) {
    return undefined;
  }
  const response = error.response;
  return typeof response === "object" && response !== null ? response : undefined;
}

/**
 * Finner hvilke innholdsfelter som er endret mellom opprinnelige og nåværende verdier.
 * Tider sammenlignes på tidspunkt-nivå (ikke strengformat).
 */
export function getWorkHourChanges(
  original: WorkHourValues,
  current: WorkHourValues
): WorkHourChanges {
  const changes: WorkHourChanges = {};
  if (!sameInstant(original.startDateTime, current.startDateTime)) {
    changes.startTime = current.startDateTime;
  }
  if (!sameInstant(original.endDateTime, current.endDateTime)) {
    changes.endTime = current.endDateTime;
  }
  if (!sameText(original.description, current.description)) {
    changes.description = current.description;
  }
  return changes;
}

/**
 * Bygger PATCH-body: kun endrede felter, pluss eventuell approvalStatus (1|2).
 * userId sendes aldri.
 */
export function buildWorkHourPatch(
  original: WorkHourValues,
  current: WorkHourValues,
  approvalStatus: number | null = null
): WorkHourPatch {
  const patch: WorkHourPatch = getWorkHourChanges(original, current);
  if (approvalStatus === 1 || approvalStatus === 2) {
    patch.approvalStatus = approvalStatus;
  }
  return patch;
}

/**
 * Klassifiserer en axios-feil fra WorkHours-API-et.
 */
export function getWorkHourErrorKind(error: unknown): WorkHourErrorKind {
  const status = getErrorResponse(error)?.status;
  if (status === 409) return "conflict";
  if (status === 403) return "forbidden";
  if (status === 404) return "notFound";
  return "other";
}

/**
 * Henter serverens feilmelding (`{ error: "<melding>" }`) hvis den finnes,
 * ellers `fallback` (f.eks. ved tom 403, ProblemDetails eller nettverksfeil).
 */
export function getWorkHourErrorMessage(error: unknown, fallback: string): string {
  const data = getErrorResponse(error)?.data;
  const message =
    typeof data === "object" && data !== null && "error" in data
      ? data.error
      : undefined;
  return typeof message === "string" && message.trim() !== ""
    ? message
    : fallback;
}

/**
 * Lager én oppsummering etter masse-godkjenning/avslag.
 * `status` er 1 (godkjent) eller 2 (avslått).
 */
export function summarizeBulkApproval(
  counts: BulkApprovalCounts,
  status: number
): BulkApprovalSummary {
  const { ok = 0, alreadyProcessed = 0, notFound = 0, failed = 0 } = counts;
  const verb = status === 2 ? "avslått" : "godkjent";
  const parts = [`${ok} ${verb}`];
  if (alreadyProcessed > 0) parts.push(`${alreadyProcessed} var allerede behandlet`);
  if (notFound > 0) parts.push(`${notFound} fantes ikke lenger`);
  if (failed > 0) parts.push(`${failed} feilet`);
  return {
    type:
      alreadyProcessed === 0 && notFound === 0 && failed === 0
        ? "positive"
        : "warning",
    message: parts.join(", "),
  };
}
