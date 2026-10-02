// Rene hjelpefunksjoner for å gjøre dato ("dd.MM.yyyy") og klokkeslett ("HH:mm")
// fra vaktliste-skjemaene om til Date (lokal tid).

import { addDays, addHours, isBefore, parse } from "date-fns";

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

/**
 * Starttidspunkt for en vakt (ressurs) i en vaktliste. Vakter starter ofte litt
 * før vaktlista (standard er 30 minutter før), så starttiden legges på datoen
 * med mindre den er mer enn 12 timer før vaktlistas start. Da tolkes den som
 * neste dag (f.eks. vaktliste 22:00, vakt 01:00).
 */
export function toResourceStartDateTime(
  date: string | null | undefined,
  time: string | null | undefined,
  eventStart: Date
): Date {
  return toDateTime(date, time, addHours(eventStart, -12));
}
