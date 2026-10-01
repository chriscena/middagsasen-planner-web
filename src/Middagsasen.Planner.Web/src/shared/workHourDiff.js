// Rene hjelpefunksjoner for redigering og godkjenning av timeføringer.

function sameInstant(a, b) {
  if (!a && !b) return true;
  if (!a || !b) return false;
  return new Date(a).getTime() === new Date(b).getTime();
}

function sameText(a, b) {
  return (a ?? "") === (b ?? "");
}

/**
 * Finner hvilke innholdsfelter som er endret mellom opprinnelige og nåværende verdier.
 * Tider sammenlignes på tidspunkt-nivå (ikke strengformat).
 * @param {{ startDateTime, endDateTime, description }} original
 * @param {{ startDateTime, endDateTime, description }} current
 * @returns {{ startTime?, endTime?, description? }}
 */
export function getWorkHourChanges(original, current) {
  const changes = {};
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
export function buildWorkHourPatch(original, current, approvalStatus = null) {
  const patch = getWorkHourChanges(original, current);
  if (approvalStatus === 1 || approvalStatus === 2) {
    patch.approvalStatus = approvalStatus;
  }
  return patch;
}

/**
 * Klassifiserer en axios-feil fra WorkHours-API-et.
 * @returns {"conflict" | "forbidden" | "notFound" | "other"}
 */
export function getWorkHourErrorKind(error) {
  const status = error?.response?.status;
  if (status === 409) return "conflict";
  if (status === 403) return "forbidden";
  if (status === 404) return "notFound";
  return "other";
}

/**
 * Henter serverens feilmelding (`{ error: "<melding>" }`) hvis den finnes,
 * ellers `fallback` (f.eks. ved tom 403, ProblemDetails eller nettverksfeil).
 */
export function getWorkHourErrorMessage(error, fallback) {
  const message = error?.response?.data?.error;
  return typeof message === "string" && message.trim() !== ""
    ? message
    : fallback;
}

/**
 * Lager én oppsummering etter masse-godkjenning/avslag.
 * @param {{ ok: number, alreadyProcessed: number, notFound: number, failed: number }} counts
 * @param {1|2} status
 * @returns {{ type: "positive" | "warning", message: string }}
 */
export function summarizeBulkApproval(counts, status) {
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
