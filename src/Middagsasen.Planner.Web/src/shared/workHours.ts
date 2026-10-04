// Felles modul for timeføring i frontend: godkjenningsstatus og visningen av
// den, filteret på godkjenningssiden (inkl. sesongregelen), varighet,
// redigering (diff/PATCH), masse-godkjenning og tolkning av feilsvar.
//
// Hva innlogget bruker kan gjøre med en føring (redigere, slette, godkjenne,
// nullstille status) avgjøres av backend og kommer som flagg i
// `WorkHourResponse` (`canEdit`, `canDelete`, `canApprove`, `canResetStatus`).
// Disse reglene skal ikke gjenskapes her.

import { getApiErrorMessage, getErrorResponse } from "./apiError";
import { durationHours } from "./time";
import type { DateInput } from "./time";
import { ApprovalFilter, ApprovalStatus } from "src/types";

// ---------------------------------------------------------------------------
// Status

/**
 * Godkjenningsstatus slik den kommer fra API-et; null/undefined = ubehandlet.
 * Databasen kan ha eldre rader med andre verdier (f.eks. 0); de behandles
 * også som ubehandlet.
 */
export type ApprovalStatusValue = ApprovalStatus | null | undefined;

/** Om statusen er en kjent behandlet status (godkjent eller avslått). */
function isProcessed(status: ApprovalStatusValue): status is ApprovalStatus {
  return (
    status === ApprovalStatus.Approved || status === ApprovalStatus.Rejected
  );
}

/**
 * Om føringen er åpen (ubehandlet, verken godkjent eller avslått). Ukjente
 * verdier regnes som åpne.
 */
export function isOpen(status: ApprovalStatusValue): boolean {
  return !isProcessed(status);
}

export interface ApprovalStatusDisplay {
  /** Material-ikon. */
  icon: string;
  /** CSS-klasse for ikonfarge (definert i sidene: green-text/red-text/grey-text). */
  iconClass: string;
  /** Quasar-farge for badge (null = ingen badge for ubehandlet). */
  badgeColor: "positive" | "negative" | null;
  /** Tekst, f.eks. til badge: «Godkjent», «Avslått», «Ubehandlet». */
  label: string;
}

const OPEN_DISPLAY: ApprovalStatusDisplay = {
  icon: "radio_button_unchecked",
  iconClass: "grey-text",
  badgeColor: null,
  label: "Ubehandlet",
};

const STATUS_DISPLAY: Record<ApprovalStatus, ApprovalStatusDisplay> = {
  [ApprovalStatus.Approved]: {
    icon: "check_circle",
    iconClass: "green-text",
    badgeColor: "positive",
    label: "Godkjent",
  },
  [ApprovalStatus.Rejected]: {
    icon: "cancel",
    iconClass: "red-text",
    badgeColor: "negative",
    label: "Avslått",
  },
};

/** Ikon, farge og tekst for en godkjenningsstatus. Ukjente verdier vises som ubehandlet. */
export function getApprovalStatusDisplay(
  status: ApprovalStatusValue
): ApprovalStatusDisplay {
  return isProcessed(status) ? STATUS_DISPLAY[status] : OPEN_DISPLAY;
}

export interface ApprovalActionText {
  /** Substantiv, f.eks. «Bekreft godkjenning». */
  noun: string;
  /** Perfektum partisipp, f.eks. «8 godkjent». */
  past: string;
  /** Melding etter vellykket godkjenning/avslag av én føring. */
  done: string;
}

const ACTION_TEXT: Record<ApprovalStatus, ApprovalActionText> = {
  [ApprovalStatus.Approved]: {
    noun: "godkjenning",
    past: "godkjent",
    done: "Timeføring godkjent",
  },
  [ApprovalStatus.Rejected]: {
    noun: "avslag",
    past: "avslått",
    done: "Timeføring avslått",
  },
};

/** Tekster for handlingen å sette statusen (godkjenne eller avslå). */
export function getApprovalActionText(
  status: ApprovalStatus
): ApprovalActionText {
  return ACTION_TEXT[status];
}

export interface ApprovedByValues {
  approvedBy?: number | null | undefined;
  approvedByName?: string | null | undefined;
  approvalStatus?: ApprovalStatusValue;
}

/** «Godkjent av: Navn» / «Avslått av: Navn», eller tom streng for åpne føringer. */
export function getApprovedByText(row: ApprovedByValues): string {
  if (!row.approvedBy || isOpen(row.approvalStatus)) return "";
  const name = row.approvedByName ?? "ukjent";
  return row.approvalStatus === ApprovalStatus.Approved
    ? `Godkjent av: ${name}`
    : `Avslått av: ${name}`;
}

// ---------------------------------------------------------------------------
// Filter på godkjenningssiden

const APPROVAL_FILTERS: readonly number[] = Object.values(ApprovalFilter);

/**
 * Tolker filterverdien fra URL-en (`a`). Ugyldig eller manglende verdi gir
 * `fallback` (Ubehandlet).
 */
export function parseApprovalFilter(
  value: unknown,
  fallback: ApprovalFilter = ApprovalFilter.Pending
): ApprovalFilter {
  const parsed = parseInt(String(value));
  return APPROVAL_FILTERS.includes(parsed)
    ? (parsed as ApprovalFilter)
    : fallback;
}

/**
 * Sesongregelen: ubehandlede føringer vises på tvers av sesonger, så de ikke
 * skjules av sesongfilteret.
 */
export function filterIgnoresSeason(filter: ApprovalFilter): boolean {
  return filter === ApprovalFilter.Pending;
}

/** Sesongen (startår) som skal sendes til API-et for filteret, ellers null. */
export function seasonForQuery(
  filter: ApprovalFilter,
  season: number | null
): number | null {
  return filterIgnoresSeason(filter) ? null : season;
}

// ---------------------------------------------------------------------------
// Varighet

/**
 * Om føringen har en varighet som er verdt å lagre: minst ett minutt etter
 * avrunding til nærmeste hele minutt, slik skjemaet viser den
 * (`formatDuration` i `shared/time`). Manglende verdier eller slutt før
 * start gir false.
 */
export function hasDuration(start: DateInput, end: DateInput): boolean {
  const hours = durationHours(start, end);
  return hours !== null && Math.round(hours * 60) > 0;
}

// ---------------------------------------------------------------------------
// Redigering (diff og PATCH)

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

export interface WorkHourPatch extends WorkHourChanges {
  approvalStatus?: ApprovalStatus;
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
 * Bygger PATCH-body: kun endrede felter, pluss eventuell `approvalStatus`
 * (godkjent/avslått). userId sendes aldri.
 */
export function buildWorkHourPatch(
  original: WorkHourValues,
  current: WorkHourValues,
  approvalStatus: ApprovalStatus | null = null
): WorkHourPatch {
  const patch: WorkHourPatch = getWorkHourChanges(original, current);
  if (approvalStatus !== null) {
    patch.approvalStatus = approvalStatus;
  }
  return patch;
}

// ---------------------------------------------------------------------------
// Feilsvar

export type WorkHourErrorKind = "conflict" | "forbidden" | "notFound" | "other";

/** Handlingen som feilet; bestemmer standardteksten. */
export type WorkHourAction =
  | "create"
  | "update"
  | "delete"
  | "approve"
  | "reject"
  | "changeStatus";

export interface WorkHourError {
  /** Melding til bruker. Backendens ProblemDetails `detail` vinner. */
  message: string;
  kind: WorkHourErrorKind;
  /** Føringen er behandlet eller slettet av andre: lukk og last listen på nytt. */
  shouldReload: boolean;
}

const FALLBACK_MESSAGES: Record<WorkHourAction, string> = {
  create: "Klarte ikke å lagre timer",
  update: "Klarte ikke å lagre endringer",
  delete: "Klarte ikke å slette timeføring",
  approve: "Klarte ikke å godkjenne timeføring",
  reject: "Klarte ikke å avslå timeføring",
  changeStatus: "Klarte ikke å oppdatere status",
};

const CONFLICT_MESSAGE =
  "Føringen er allerede behandlet og kan ikke endres lenger";
const STATUS_CONFLICT_MESSAGE =
  "Statusen kunne ikke endres fordi føringen er endret av noen andre";
const NOT_FOUND_MESSAGE = "Føringen finnes ikke lenger";
const FORBIDDEN_MESSAGE = "Du har ikke tilgang til å endre denne føringen";

/** Handlingen for å sette statusen til godkjent eller avslått. */
export function approvalAction(status: ApprovalStatus): "approve" | "reject" {
  return status === ApprovalStatus.Approved ? "approve" : "reject";
}

/**
 * Klassifiserer en axios-feil fra WorkHours-API-et ut fra HTTP-status.
 */
export function getWorkHourErrorKind(error: unknown): WorkHourErrorKind {
  const status = getErrorResponse(error)?.status;
  if (status === 409) return "conflict";
  if (status === 403) return "forbidden";
  if (status === 404) return "notFound";
  return "other";
}

function defaultMessage(kind: WorkHourErrorKind, action: WorkHourAction) {
  switch (kind) {
    case "conflict":
      return action === "changeStatus"
        ? STATUS_CONFLICT_MESSAGE
        : CONFLICT_MESSAGE;
    case "notFound":
      return NOT_FOUND_MESSAGE;
    case "forbidden":
      return FORBIDDEN_MESSAGE;
    default:
      return FALLBACK_MESSAGES[action];
  }
}

/**
 * Tolker en feil fra WorkHours-API-et til melding og videre handling.
 * Ved opprettelse finnes ingen eksisterende føring, så da brukes bare
 * standardteksten og listen lastes ikke på nytt.
 */
export function getWorkHourError(
  error: unknown,
  action: WorkHourAction
): WorkHourError {
  const kind = getWorkHourErrorKind(error);
  const existing = action !== "create";
  const fallback = existing
    ? defaultMessage(kind, action)
    : FALLBACK_MESSAGES[action];
  return {
    message: getApiErrorMessage(error, fallback),
    kind,
    shouldReload: existing && (kind === "conflict" || kind === "notFound"),
  };
}

// ---------------------------------------------------------------------------
// Masse-godkjenning

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

/** Teller en feilet føring i masse-godkjenningen ut fra feiltypen. */
export function countBulkApprovalError(
  counts: Required<BulkApprovalCounts>,
  error: unknown
): void {
  const kind = getWorkHourErrorKind(error);
  if (kind === "conflict") counts.alreadyProcessed++;
  else if (kind === "notFound") counts.notFound++;
  else counts.failed++;
}

/**
 * Lager én oppsummering etter masse-godkjenning/avslag.
 */
export function summarizeBulkApproval(
  counts: BulkApprovalCounts,
  status: ApprovalStatus
): BulkApprovalSummary {
  const { ok = 0, alreadyProcessed = 0, notFound = 0, failed = 0 } = counts;
  const parts = [`${ok} ${getApprovalActionText(status).past}`];
  if (alreadyProcessed > 0)
    parts.push(`${alreadyProcessed} var allerede behandlet`);
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
