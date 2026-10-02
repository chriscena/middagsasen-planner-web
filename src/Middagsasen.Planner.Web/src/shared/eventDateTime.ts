// Rene hjelpefunksjoner for å gjøre dato ("dd.MM.yyyy") og klokkeslett ("HH:mm")
// fra vaktliste-skjemaene om til Date (lokal tid).

import { addDays, isBefore, parse } from "date-fns";

/**
 * Lager et tidspunkt av dato og klokkeslett. Er `start` oppgitt og tidspunktet
 * havner før `start`, flyttes det til neste dag (vakt over midnatt).
 * Ugyldig eller manglende dato/tid gir Invalid Date (kaster ikke).
 */
export function toDateTime(
  date: string | null | undefined,
  time: string | null | undefined,
  start?: Date | null
): Date {
  const datetime = parse(
    `${date ?? ""} ${time ?? ""}`,
    "dd.MM.yyyy HH:mm",
    new Date()
  );
  if (start && isBefore(datetime, start)) return addDays(datetime, 1);
  return datetime;
}
