// Godkjennere i kompetanseskjemaet som ennå ikke er lagret, ligger kun lokalt
// med id 0. De legges til via API-et etter at kompetansen er lagret.
import type { CompetencyApproverResponse } from "src/types";

/** Godkjennere som kun ligger lokalt (id 0) og må legges til via API-et. */
export function localApprovers(
  approvers: CompetencyApproverResponse[]
): CompetencyApproverResponse[] {
  return approvers.filter((a) => !a.id);
}

/**
 * Bytter ut lokale godkjennere med svaret fra API-et der kallet lyktes.
 * Feilede beholdes som lokale (id 0), slik at et nytt «Lagre» prøver igjen.
 * `results[i]` hører til `pending[i]`.
 */
export function applyApproverResults(
  approvers: CompetencyApproverResponse[],
  pending: CompetencyApproverResponse[],
  results: PromiseSettledResult<CompetencyApproverResponse>[]
): { approvers: CompetencyApproverResponse[]; failed: number } {
  const resultFor = new Map(
    pending.map((approver, i) => [approver, results[i]] as const)
  );
  let failed = 0;
  const merged = approvers.map((approver) => {
    const result = resultFor.get(approver);
    if (!result) return approver;
    if (result.status === "fulfilled") return result.value;
    failed += 1;
    return approver;
  });
  return { approvers: merged, failed };
}
