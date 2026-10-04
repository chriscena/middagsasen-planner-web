// Felles modul for dato og tid i frontend: visning, parsing fra skjemaene,
// dag-nøkler («i dag», fortid/fremtid), midnattsregelen og wire-format mot
// API-et. Formatstrengene ligger kun her; kallere bruker de navngitte
// operasjonene. All tid er lokal tid (nettleserens tidssone) med mindre
// annet er sagt.
//
// Wire-format mot API-et (endres ikke her, bare samlet):
// - Arrangementer og maler (POST/PUT /api/events, /api/templates):
//   lokal tid uten tidssone, "yyyy-MM-ddTHH:mm" → `toLocalWire`. Svarene
//   har samme form (uten sone) og kan gis direkte til visningsfunksjonene.
//   For maler bruker backend bare klokkeslettet (TimeOfDay); datodelen er
//   den faste referansedatoen fra `parseTime`.
// - Svar med lokal tid uten sone, "yyyy-MM-ddTHH:mm": arrangementer,
//   ressurser, maler og vakter (`startTime`/`endTime`).
// - Svar med UTC-tidspunkt, "yyyy-MM-ddTHH:mm:ssZ": filer (`created`/
//   `updated`), meldinger (`created`) og opplæring (`confirmed`).
//   Begge formene kan gis direkte til visningsfunksjonene (`parseISO`
//   håndterer sone).
// - Timeføring (/api/workhours): UTC-tidspunkt fra `Date.toISOString()`,
//   "yyyy-MM-ddTHH:mm:ss.sssZ" → `toInstantWire`.
// - Vær (GET /api/weather?start=&end=): UTC uten millisekunder,
//   "yyyy-MM-ddTHH:mm:ssZ" → `toUtcWire`.
// - Vaktlister for en periode (GET /api/events?start=&end=): dato
//   "yyyy-MM-dd" (lokal dag) → `toDayKey`.
// - Status per dag (GET /api/eventstatus): `date` kommer som "yyyy/MM/dd"
//   → `parseEventStatusDate`.
// - Mine vakter (GET /api/me/shifts): `startDate` er "yyyy-MM-dd", tidene er
//   ISO-strenger; begge kan gis direkte til visningsfunksjonene.

import {
  addDays,
  addMinutes,
  format,
  isBefore,
  isValid,
  parse,
  parseISO,
} from "date-fns";
import { nb } from "date-fns/locale";

// Private formatstrenger (date-fns-tokens).
const TIME_FORMAT = "HH:mm";
const DATE_FORMAT = "dd.MM.yyyy";
const DATE_TIME_FORMAT = "dd.MM.yyyy HH:mm";
const DAY_MONTH_FORMAT = "dd.MM";
const WEEKDAY_FORMAT = "EEE";
const WEEK_NUMBER_FORMAT = "w";
const SHORT_DATE_FORMAT = "EEE d. LLL";
const LOCAL_WIRE_FORMAT = "yyyy-MM-dd'T'HH:mm";
const EVENT_STATUS_DATE_FORMAT = "yyyy/MM/dd";

/** Dag-nøkkel "yyyy-MM-dd" (date-fns-tokens); også `model-type` til VueDatePicker. */
export const DAY_KEY_FORMAT = "yyyy-MM-dd";
/** `mask` til QDate for skjemadatoer ("dd.MM.yyyy" i Quasars tokens). */
export const QUASAR_DATE_MASK = "DD.MM.YYYY";
/** `mask` til QTime for skjemaklokkeslett ("HH:mm"). */
export const QUASAR_TIME_MASK = "HH:mm";
/** Visningsformat for klokkeslett på tidsaksen i Chart.js (date-fns-adapter). */
export const CHART_TIME_FORMAT = TIME_FORMAT;

// Fast referansedato uten sommertidsovergang, slik at regning med
// klokkeslett ikke avhenger av hvilken dag det er i dag.
const TIME_REFERENCE_DATE = new Date(2000, 0, 1);

const MS_PER_HOUR = 60 * 60 * 1000;

/** Verdi som kan vises: Date, ISO-streng (med eller uten sone), eller mangler. */
export type DateInput = Date | string | null | undefined;

// Date for gyldig input, ellers null. Strenger tolkes med parseISO, så
// "yyyy-MM-dd" og tider uten sone blir lokal tid.
function toDate(value: DateInput): Date | null {
  if (value === null || value === undefined || value === "") return null;
  const date = value instanceof Date ? value : parseISO(value);
  return isValid(date) ? date : null;
}

function formatOrEmpty(
  value: DateInput,
  pattern: string,
  options?: { locale: typeof nb }
): string {
  const date = toDate(value);
  return date ? format(date, pattern, options) : "";
}

// ---------------------------------------------------------------------------
// Visning. Manglende eller ugyldig verdi gir tom streng (kaster ikke).

/** Klokkeslett "HH:mm". */
export function formatTime(value: DateInput): string {
  return formatOrEmpty(value, TIME_FORMAT);
}

/** Dato "dd.MM.yyyy". */
export function formatDate(value: DateInput): string {
  return formatOrEmpty(value, DATE_FORMAT);
}

/** Dato og klokkeslett "dd.MM.yyyy HH:mm". */
export function formatDateTime(value: DateInput): string {
  return formatOrEmpty(value, DATE_TIME_FORMAT);
}

/** Tidsrom "HH:mm-HH:mm". */
export function formatTimeRange(start: DateInput, end: DateInput): string {
  return `${formatTime(start)}-${formatTime(end)}`;
}

/** Dag og måned "dd.MM". */
export function formatDayMonth(value: DateInput): string {
  return formatOrEmpty(value, DAY_MONTH_FORMAT);
}

/** Kort ukedag på norsk, f.eks. "lør". */
export function formatWeekday(value: DateInput): string {
  return formatOrEmpty(value, WEEKDAY_FORMAT, { locale: nb });
}

/** Ukenummer etter norsk regel (uka starter mandag). */
export function formatWeekNumber(value: DateInput): string {
  return formatOrEmpty(value, WEEK_NUMBER_FORMAT, { locale: nb });
}

/**
 * Kort dato med ukedag og måned på norsk, f.eks. "lør 3. okt." (beskjeder
 * på vakter).
 */
export function formatShortDate(value: DateInput): string {
  return formatOrEmpty(value, SHORT_DATE_FORMAT, { locale: nb });
}

// ---------------------------------------------------------------------------
// Varighet. Regnes som faktisk forløpt tid mellom to tidspunkter (også over
// sommertidsskifte).

/**
 * Varighet i timer (desimaltall) fra `start` til `end`, eller null hvis en
 * av verdiene mangler eller er ugyldig. Slutt før start gir negativt tall.
 */
export function durationHours(start: DateInput, end: DateInput): number | null {
  const startDate = toDate(start);
  const endDate = toDate(end);
  if (!startDate || !endDate) return null;
  return (endDate.getTime() - startDate.getTime()) / MS_PER_HOUR;
}

/**
 * Varighet som "H:MM" (timer uten utfylling, minutter rundet til nærmeste
 * hele), f.eks. "1:30" og "12:05". Tom streng hvis en verdi mangler eller er
 * ugyldig, eller slutt er før start.
 */
export function formatDuration(start: DateInput, end: DateInput): string {
  const hours = durationHours(start, end);
  if (hours === null || hours < 0) return "";
  const totalMinutes = Math.round(hours * 60);
  const minutes = totalMinutes % 60;
  return `${(totalMinutes - minutes) / 60}:${String(minutes).padStart(2, "0")}`;
}

// ---------------------------------------------------------------------------
// Parsing fra skjemaene: dato "dd.MM.yyyy" og klokkeslett "HH:mm".

/** Om klokkeslettet er gyldig "HH:mm". Null og tom streng er ugyldig. */
export function isValidTime(time: string | null | undefined): boolean {
  return isValid(parse(time ?? "", TIME_FORMAT, new Date()));
}

/** Om datoen er gyldig "dd.MM.yyyy". Null og tom streng er ugyldig. */
export function isValidDate(date: string | null | undefined): boolean {
  return isValid(parse(date ?? "", DATE_FORMAT, new Date()));
}

/**
 * Klokkeslett "HH:mm" lagt på en fast referansedato (1. januar 2000, uten
 * sommertidsskifte), slik at klokkeslettet aldri forskyves av sommertid på
 * dagens dato. Brukes for maler, der backend bare leser klokkeslettet.
 * Ugyldig eller manglende klokkeslett gir Invalid Date (kaster ikke).
 */
export function parseTime(time: string | null | undefined): Date {
  return parse(time ?? "", TIME_FORMAT, TIME_REFERENCE_DATE);
}

/**
 * Tidspunkt av dato "dd.MM.yyyy" og klokkeslett "HH:mm". Ugyldig eller
 * manglende dato/tid gir Invalid Date (kaster ikke). Uten midnattsregel;
 * bruk `intervalOn` for et tidsrom.
 */
export function parseDateTime(
  date: string | null | undefined,
  time: string | null | undefined
): Date {
  return parse(`${date ?? ""} ${time ?? ""}`, DATE_TIME_FORMAT, new Date());
}

export interface Interval {
  start: Date;
  end: Date;
}

/**
 * Tidsrom på datoen `date` ("dd.MM.yyyy") fra `startTime` til `endTime`
 * ("HH:mm"). Midnattsregelen: er slutt før start, går tidsrommet over
 * midnatt, og slutt flyttes til neste dag (samme klokkeslett, også over
 * sommertidsskifte). Lik start og slutt gir samme dag. Ugyldige verdier gir
 * Invalid Date i feltet det gjelder.
 */
export function intervalOn(
  date: string | null | undefined,
  startTime: string | null | undefined,
  endTime: string | null | undefined
): Interval {
  const start = parseDateTime(date, startTime);
  const end = parseDateTime(date, endTime);
  return { start, end: isBefore(end, start) ? addDays(end, 1) : end };
}

/**
 * Klokkeslett "HH:mm" forskjøvet med `minutes` (veggklokke, ruller over
 * midnatt), eller null hvis klokkeslettet er ugyldig.
 */
export function offsetTime(
  time: string | null | undefined,
  minutes: number
): string | null {
  const datetime = parse(time ?? "", TIME_FORMAT, TIME_REFERENCE_DATE);
  return isValid(datetime)
    ? format(addMinutes(datetime, minutes), TIME_FORMAT)
    : null;
}

// ---------------------------------------------------------------------------
// Dager. En dag-nøkkel er "yyyy-MM-dd" i lokal tid (URL-er, kalenderen,
// EventStore). Strenge sammenligninger av nøkler er trygge fordi formatet
// sorterer leksikografisk.

/** Dag-nøkkel "yyyy-MM-dd", eller tom streng for manglende/ugyldig verdi. */
export function toDayKey(value: DateInput): string {
  return formatOrEmpty(value, DAY_KEY_FORMAT);
}

/** Lokal midnatt for dag-nøkkelen (Invalid Date hvis den er ugyldig). */
export function fromDayKey(day: string | null | undefined): Date {
  return parse(day ?? "", DAY_KEY_FORMAT, new Date());
}

/**
 * Om verdien er en gyldig dag-nøkkel "yyyy-MM-dd". Strengere enn
 * `isValid(new Date(value))`, som også godtar f.eks. "2026-02-30" (ruller
 * over) og andre formater.
 */
export function isDayKey(value: string | null | undefined): value is string {
  if (!value) return false;
  const date = fromDayKey(value);
  return isValid(date) && format(date, DAY_KEY_FORMAT) === value;
}

/** Dagens dag-nøkkel, beregnet ved hvert kall. */
export function today(): string {
  return format(new Date(), DAY_KEY_FORMAT);
}

/** Dagen etter (lokal kalenderdag, samme klokkeslett). */
export function nextDay(value: Date | string): Date {
  const date = toDate(value);
  return date ? addDays(date, 1) : new Date(NaN);
}

/** Om dagen er før i dag. Ugyldig verdi gir false. */
export function isPast(day: DateInput): boolean {
  const key = toDayKey(day);
  return key !== "" && key < today();
}

/** Om dagen er etter i dag (i dag regnes ikke med). Ugyldig verdi gir false. */
export function isFuture(day: DateInput): boolean {
  const key = toDayKey(day);
  return key !== "" && key > today();
}

/** Tidsrommet fra `hours` timer før `now` til `now`. */
export function lastHours(hours: number, now: Date = new Date()): Interval {
  return { start: addMinutes(now, -hours * 60), end: now };
}

// ---------------------------------------------------------------------------
// Wire-format (se oversikten øverst). Funksjonene som tar en Date kaster
// RangeError for Invalid Date, så kallere må validere først.

/** Lokal tid uten tidssone, "yyyy-MM-ddTHH:mm" (arrangementer og maler). */
export function toLocalWire(date: Date): string {
  return format(date, LOCAL_WIRE_FORMAT);
}

/** UTC-tidspunkt med millisekunder, "yyyy-MM-ddTHH:mm:ss.sssZ" (timeføring). */
export function toInstantWire(date: Date): string {
  return date.toISOString();
}

/** UTC-tidspunkt uten millisekunder, "yyyy-MM-ddTHH:mm:ssZ" (vær). */
export function toUtcWire(date: Date): string {
  return `${date.toISOString().slice(0, 19)}Z`;
}

/** Datoen i status per dag fra API-et ("yyyy/MM/dd"), som lokal midnatt. */
export function parseEventStatusDate(value: string | null | undefined): Date {
  return parse(value ?? "", EVENT_STATUS_DATE_FORMAT, new Date());
}
