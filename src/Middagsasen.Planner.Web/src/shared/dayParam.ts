// Validering av dato-parameteren i /day/:date.

import { isDayKey } from "src/shared/time";

/**
 * Returnerer datoen hvis `value` er en gyldig dag-nøkkel (yyyy-MM-dd),
 * ellers null. Se `isDayKey` for hvor streng sjekken er.
 */
export function parseDayParam(value: string | null | undefined): string | null {
  return isDayKey(value) ? value : null;
}
