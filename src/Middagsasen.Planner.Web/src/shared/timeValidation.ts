// Validering av beregnede vakttider fra skjemaene, slik at ugyldige verdier
// (f.eks. «1») stoppes før `toLocalWire` kaster RangeError.

import { isValidDate, isValidTime, parseDateTime } from "src/shared/time";

export interface ResourceDateTimes {
  start: Date;
  end: Date;
}

/**
 * Start- og sluttidspunkt for en vakt i en vaktliste, begge lagt på
 * vaktlistedatoen, eller null hvis dato eller klokkeslett er ugyldig.
 * Backend bruker bare klokkeslettet og bestemmer selv hvilket døgn vakten
 * havner på (nærmest vaktlista, inkl. slutt over midnatt) — se
 * `Services/Events/ResourceTimes.cs` i API-prosjektet. Derfor brukes ikke
 * midnattsregelen (`intervalOn`) her.
 */
export function toResourceDateTimes(
  date: string | null | undefined,
  startTime: string | null | undefined,
  endTime: string | null | undefined
): ResourceDateTimes | null {
  if (!isValidDate(date) || !isValidTime(startTime) || !isValidTime(endTime))
    return null;
  return {
    start: parseDateTime(date, startTime),
    end: parseDateTime(date, endTime),
  };
}
