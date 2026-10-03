// Validering av dato-parameteren i /day/:date.

import { format, isValid, parse } from "date-fns";

/**
 * Returnerer datoen hvis `value` er en gyldig dato på formen yyyy-MM-dd,
 * ellers null. Strengere enn `isValid(new Date(value))`, som også godtar
 * f.eks. "2026-02-30" (ruller over) og andre formater.
 */
export function parseDayParam(value: string | null | undefined): string | null {
  if (!value) return null;
  const date = parse(value, "yyyy-MM-dd", new Date());
  if (!isValid(date) || format(date, "yyyy-MM-dd") !== value) return null;
  return value;
}
