// Validering av klokkeslett ("HH:mm") og beregnede vakttider fra skjemaene,
// slik at ugyldige verdier (f.eks. «1») stoppes før `format` kaster RangeError.

import { isValid, parse } from "date-fns";
import { toDateTime } from "src/shared/eventDateTime";

/** Om klokkeslettet er gyldig "HH:mm". Null og tom streng er ugyldig. */
export function isValidTime(time: string | null | undefined): boolean {
  return isValid(parse(time ?? "", "HH:mm", new Date()));
}

export interface ResourceDateTimes {
  start: Date;
  end: Date;
}

/**
 * Start- og sluttidspunkt for en vakt i en vaktliste, begge lagt på
 * vaktlistedatoen, eller null hvis dato eller klokkeslett er ugyldig.
 * Backend bruker bare klokkeslettet og bestemmer selv hvilket døgn vakten
 * havner på (nærmest vaktlista, inkl. slutt over midnatt) — se
 * `Services/Events/ResourceTimes.cs` i API-prosjektet.
 */
export function toResourceDateTimes(
  date: string | null | undefined,
  startTime: string | null | undefined,
  endTime: string | null | undefined
): ResourceDateTimes | null {
  const start = toDateTime(date, startTime);
  if (!isValid(start)) return null;
  const end = toDateTime(date, endTime);
  if (!isValid(end)) return null;
  return { start, end };
}
