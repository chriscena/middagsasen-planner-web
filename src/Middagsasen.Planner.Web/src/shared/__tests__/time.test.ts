import { afterEach, describe, it, expect, vi } from "vitest";
import {
  durationHours,
  formatDate,
  formatDuration,
  formatDateTime,
  formatDayMonth,
  formatShortDate,
  formatTime,
  formatTimeRange,
  formatWeekday,
  formatWeekNumber,
  fromDayKey,
  intervalOn,
  isDayKey,
  isFuture,
  isPast,
  isValidDate,
  isValidTime,
  lastHours,
  nextDay,
  offsetTime,
  parseDateTime,
  parseEventStatusDate,
  parseTime,
  toDayKey,
  toInstantWire,
  toLocalWire,
  toTimeWire,
  toUtcWire,
  today,
} from "@/shared/time";

const HOUR = 60 * 60 * 1000;

afterEach(() => {
  vi.useRealTimers();
});

describe("tidssone i testene", () => {
  it("kjører i Europe/Oslo (CET om vinteren, CEST om sommeren)", () => {
    expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe(
      "Europe/Oslo"
    );
    expect(new Date(2026, 0, 15).getTimezoneOffset()).toBe(-60);
    expect(new Date(2026, 6, 15).getTimezoneOffset()).toBe(-120);
  });
});

describe("visning", () => {
  const date = new Date(2026, 0, 15, 9, 5);

  it("formaterer Date", () => {
    expect(formatTime(date)).toBe("09:05");
    expect(formatDate(date)).toBe("15.01.2026");
    expect(formatDateTime(date)).toBe("15.01.2026 09:05");
  });

  it("tolker ISO-strenger uten sone som lokal tid", () => {
    expect(formatTime("2026-01-15T09:05")).toBe("09:05");
    expect(formatDate("2026-01-15")).toBe("15.01.2026");
    expect(formatDateTime("2026-01-15T09:05:00")).toBe("15.01.2026 09:05");
  });

  it("tolker ISO-strenger med sone og mange desimaler", () => {
    expect(formatTime("2026-01-15T08:05:00Z")).toBe("09:05");
    expect(formatTime("2026-07-15T08:05:00Z")).toBe("10:05");
    expect(formatDateTime("2026-01-15T09:05:00.1234567")).toBe(
      "15.01.2026 09:05"
    );
  });

  it("viser midnatt som 00:00 på riktig dag", () => {
    expect(formatDateTime(new Date(2026, 0, 16, 0, 0))).toBe(
      "16.01.2026 00:00"
    );
    // 23:00 UTC 15. januar er midnatt 16. januar i Oslo.
    expect(formatDate("2026-01-15T23:00:00Z")).toBe("16.01.2026");
  });

  it("gir tom streng for manglende eller ugyldig verdi", () => {
    for (const value of [null, undefined, "", "tull", new Date(NaN)]) {
      expect(formatTime(value)).toBe("");
      expect(formatDate(value)).toBe("");
      expect(formatDateTime(value)).toBe("");
      expect(formatDayMonth(value)).toBe("");
      expect(formatWeekday(value)).toBe("");
      expect(formatWeekNumber(value)).toBe("");
      expect(formatShortDate(value)).toBe("");
    }
  });

  it("formaterer tidsrom", () => {
    expect(formatTimeRange("2026-01-15T22:00", "2026-01-16T02:00")).toBe(
      "22:00-02:00"
    );
  });

  it("formaterer dag/måned, ukedag, ukenummer og kort dato", () => {
    expect(formatDayMonth("2026-10-03")).toBe("03.10");
    expect(formatWeekday("2026-10-03")).toBe("lør");
    // 1. januar 2026 er torsdag i uke 1; 28. desember 2026 er mandag i uke 53.
    expect(formatWeekNumber("2026-01-01")).toBe("1");
    expect(formatWeekNumber("2026-12-28")).toBe("53");
    expect(formatShortDate("2026-10-03T12:00:00")).toBe("lør 3. okt.");
  });

  it("formaterer kort dato på norsk (ukedag og måned)", () => {
    expect(formatShortDate(new Date(2026, 4, 3, 12))).toBe("søn 3. mai");
    expect(formatShortDate("2026-01-15T08:00:00Z")).toBe("tor 15. jan.");
    // 23:30 UTC 31. desember er 00:30 1. januar i Oslo.
    expect(formatShortDate("2026-12-31T23:30:00Z")).toBe("fre 1. jan.");
  });
});

describe("parsing fra skjema", () => {
  it.each(["10:00", "00:00", "23:59"])("godtar klokkeslett %s", (time) => {
    expect(isValidTime(time)).toBe(true);
  });

  it.each([null, undefined, "", "1", "abc", "25:00", "10:60"])(
    "avviser klokkeslett %s",
    (time) => {
      expect(isValidTime(time)).toBe(false);
    }
  );

  it("validerer dato dd.MM.yyyy", () => {
    expect(isValidDate("15.01.2026")).toBe(true);
    expect(isValidDate("29.02.2028")).toBe(true);
    expect(isValidDate("30.02.2026")).toBe(false);
    expect(isValidDate("2026-01-15")).toBe(false);
    expect(isValidDate("")).toBe(false);
    expect(isValidDate(null)).toBe(false);
  });

  it("parser dato og klokkeslett til lokal tid", () => {
    expect(parseDateTime("15.01.2026", "10:00")).toEqual(
      new Date(2026, 0, 15, 10, 0)
    );
    expect(parseDateTime("15.01.2026", "00:00")).toEqual(
      new Date(2026, 0, 15, 0, 0)
    );
  });

  it("gir Invalid Date for manglende eller ugyldig dato/tid", () => {
    expect(Number.isNaN(parseDateTime(null, "10:00").getTime())).toBe(true);
    expect(Number.isNaN(parseDateTime("15.01.2026", null).getTime())).toBe(
      true
    );
    expect(Number.isNaN(parseDateTime("15.01.2026", "1").getTime())).toBe(true);
  });

  it("legger klokkeslett på en fast referansedato", () => {
    expect(parseTime("10:30")).toEqual(new Date(2000, 0, 1, 10, 30));
    expect(Number.isNaN(parseTime(null).getTime())).toBe(true);
  });

  it("parseTime forskyves ikke når i dag er sommertidsdagen", () => {
    vi.useFakeTimers();
    // 02:00–03:00 finnes ikke 29.03.2026 i Oslo.
    vi.setSystemTime(new Date(2026, 2, 29, 12, 0));
    expect(formatTime(parseTime("02:30"))).toBe("02:30");
    expect(parseTime("02:30")).toEqual(new Date(2000, 0, 1, 2, 30));
    expect(toTimeWire("02:30")).toBe("02:30");
  });
});

describe("intervalOn (midnattsregelen)", () => {
  it("beholder samme dag når slutt er etter start", () => {
    expect(intervalOn("15.01.2026", "10:00", "17:00")).toEqual({
      start: new Date(2026, 0, 15, 10, 0),
      end: new Date(2026, 0, 15, 17, 0),
    });
  });

  it("beholder samme dag når slutt er lik start", () => {
    const { start, end } = intervalOn("15.01.2026", "10:00", "10:00");
    expect(end).toEqual(start);
  });

  it("flytter slutt til neste dag når den er før start", () => {
    expect(intervalOn("15.01.2026", "22:00", "02:00")).toEqual({
      start: new Date(2026, 0, 15, 22, 0),
      end: new Date(2026, 0, 16, 2, 0),
    });
  });

  it("slutt på midnatt (00:00) går til neste dag", () => {
    expect(intervalOn("15.01.2026", "18:00", "00:00").end).toEqual(
      new Date(2026, 0, 16, 0, 0)
    );
  });

  it("start på midnatt beholder samme dag", () => {
    expect(intervalOn("15.01.2026", "00:00", "06:00")).toEqual({
      start: new Date(2026, 0, 15, 0, 0),
      end: new Date(2026, 0, 15, 6, 0),
    });
  });

  it("håndterer måneds- og årsskifte", () => {
    expect(intervalOn("31.12.2026", "20:00", "01:30").end).toEqual(
      new Date(2027, 0, 1, 1, 30)
    );
  });

  it("natt til sommertid (29.03.2026) er én time kortere", () => {
    const { start, end } = intervalOn("28.03.2026", "22:00", "06:00");
    expect(end).toEqual(new Date(2026, 2, 29, 6, 0));
    expect(end.getTime() - start.getTime()).toBe(7 * HOUR);
  });

  it("natt til vintertid (25.10.2026) er én time lengre", () => {
    const { start, end } = intervalOn("24.10.2026", "22:00", "06:00");
    expect(end).toEqual(new Date(2026, 9, 25, 6, 0));
    expect(end.getTime() - start.getTime()).toBe(9 * HOUR);
  });

  it("tidsrom på selve sommertidsdagen", () => {
    const { start, end } = intervalOn("29.03.2026", "01:00", "04:00");
    expect(formatDateTime(start)).toBe("29.03.2026 01:00");
    expect(formatDateTime(end)).toBe("29.03.2026 04:00");
    expect(end.getTime() - start.getTime()).toBe(2 * HOUR);
  });

  it("gir Invalid Date i feltet som er ugyldig", () => {
    const missingEnd = intervalOn("15.01.2026", "10:00", "");
    expect(missingEnd.start).toEqual(new Date(2026, 0, 15, 10, 0));
    expect(Number.isNaN(missingEnd.end.getTime())).toBe(true);
    const missingDate = intervalOn(null, "10:00", "17:00");
    expect(Number.isNaN(missingDate.start.getTime())).toBe(true);
    expect(Number.isNaN(missingDate.end.getTime())).toBe(true);
  });
});

describe("offsetTime", () => {
  it("forskyver klokkeslett", () => {
    expect(offsetTime("10:00", -30)).toBe("09:30");
    expect(offsetTime("17:00", 30)).toBe("17:30");
  });

  it("ruller over midnatt", () => {
    expect(offsetTime("23:50", 30)).toBe("00:20");
    expect(offsetTime("00:10", -30)).toBe("23:40");
  });

  it("er uavhengig av sommertid på dagens dato", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 2, 29, 12, 0));
    expect(offsetTime("01:45", 30)).toBe("02:15");
    vi.setSystemTime(new Date(2026, 9, 25, 12, 0));
    expect(offsetTime("01:45", 30)).toBe("02:15");
  });

  it("gir null for ugyldig klokkeslett", () => {
    expect(offsetTime("1", 30)).toBeNull();
    expect(offsetTime(null, 30)).toBeNull();
  });
});

describe("dager", () => {
  it("lager og tolker dag-nøkler", () => {
    expect(toDayKey(new Date(2026, 9, 3, 23, 59))).toBe("2026-10-03");
    expect(toDayKey("2026-10-03T10:00")).toBe("2026-10-03");
    expect(toDayKey(null)).toBe("");
    expect(fromDayKey("2026-10-03")).toEqual(new Date(2026, 9, 3));
    expect(Number.isNaN(fromDayKey("tull").getTime())).toBe(true);
  });

  it("validerer dag-nøkler strengt", () => {
    expect(isDayKey("2026-10-03")).toBe(true);
    expect(isDayKey("2028-02-29")).toBe(true);
    expect(isDayKey("2026-02-30")).toBe(false);
    expect(isDayKey("2026-13-01")).toBe(false);
    expect(isDayKey("2026-1-3")).toBe(false);
    expect(isDayKey("2026")).toBe(false);
    expect(isDayKey("20261003")).toBe(false);
    expect(isDayKey("tull")).toBe(false);
    expect(isDayKey("03.10.2026")).toBe(false);
    expect(isDayKey("")).toBe(false);
    expect(isDayKey(undefined)).toBe(false);
  });

  it("today() beregnes ved hvert kall og skifter ved lokal midnatt", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 3, 23, 59, 59));
    expect(today()).toBe("2026-10-03");
    vi.advanceTimersByTime(1000);
    expect(today()).toBe("2026-10-04");
  });

  it("today() bruker lokal dato, ikke UTC", () => {
    vi.useFakeTimers();
    // 22:30 UTC 3. oktober er 00:30 4. oktober i Oslo (sommertid).
    vi.setSystemTime(new Date("2026-10-03T22:30:00Z"));
    expect(today()).toBe("2026-10-04");
  });

  it("isPast og isFuture sammenligner med i dag", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 3, 12, 0));
    expect(isPast("2026-10-02")).toBe(true);
    expect(isPast("2026-10-03")).toBe(false);
    expect(isPast("2026-10-04")).toBe(false);
    expect(isFuture("2026-10-02")).toBe(false);
    expect(isFuture("2026-10-03")).toBe(false);
    expect(isFuture("2026-10-04")).toBe(true);
    expect(isPast(new Date(2026, 9, 2, 23, 59))).toBe(true);
    expect(isFuture(new Date(2026, 9, 4, 0, 0))).toBe(true);
  });

  it("isPast skifter ved midnatt", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 9, 3, 23, 59, 59));
    expect(isPast("2026-10-03")).toBe(false);
    vi.advanceTimersByTime(1000);
    expect(isPast("2026-10-03")).toBe(true);
  });

  it("isPast og isFuture er false for ugyldig verdi", () => {
    expect(isPast("tull")).toBe(false);
    expect(isFuture(null)).toBe(false);
  });

  it("nextDay går én kalenderdag frem, også over sommertid", () => {
    expect(nextDay("2026-03-28")).toEqual(new Date(2026, 2, 29));
    expect(nextDay("2026-10-24")).toEqual(new Date(2026, 9, 25));
    expect(nextDay(new Date(2026, 11, 31))).toEqual(new Date(2027, 0, 1));
  });

  it("lastHours gir tidsrommet bakover fra nå", () => {
    const now = new Date("2026-10-25T02:00:00Z");
    const { start, end } = lastHours(2, now);
    expect(end).toBe(now);
    expect(start.toISOString()).toBe("2026-10-25T00:00:00.000Z");
  });
});

describe("wire-format", () => {
  it("toLocalWire gir lokal tid uten sone", () => {
    expect(toLocalWire(new Date(2026, 0, 15, 10, 5))).toBe("2026-01-15T10:05");
    expect(toLocalWire(new Date(2026, 6, 15, 0, 0))).toBe("2026-07-15T00:00");
  });

  it("toLocalWire kaster for Invalid Date", () => {
    expect(() => toLocalWire(new Date(NaN))).toThrow(RangeError);
  });

  it("toTimeWire gir bare klokkeslett med to sifre", () => {
    expect(toTimeWire("10:30")).toBe("10:30");
    expect(toTimeWire("00:00")).toBe("00:00");
    expect(toTimeWire("23:59")).toBe("23:59");
    // Det isValidTime godtar uten utfylling, normaliseres.
    expect(isValidTime("9:05")).toBe(true);
    expect(toTimeWire("9:05")).toBe("09:05");
  });

  it("toTimeWire kaster for ugyldig eller manglende klokkeslett", () => {
    for (const value of ["1", "24:00", "10:60", "", null, undefined]) {
      expect(isValidTime(value)).toBe(false);
      expect(() => toTimeWire(value)).toThrow(RangeError);
    }
  });

  it("toInstantWire gir UTC med millisekunder", () => {
    expect(toInstantWire(new Date(2026, 0, 15, 10, 0))).toBe(
      "2026-01-15T09:00:00.000Z"
    );
    expect(toInstantWire(new Date(2026, 6, 15, 10, 0))).toBe(
      "2026-07-15T08:00:00.000Z"
    );
  });

  it("toInstantWire rundt vintertid (25.10.2026)", () => {
    expect(toInstantWire(new Date(2026, 9, 25, 1, 30))).toBe(
      "2026-10-24T23:30:00.000Z"
    );
    expect(toInstantWire(new Date(2026, 9, 25, 3, 30))).toBe(
      "2026-10-25T02:30:00.000Z"
    );
  });

  it("toInstantWire kaster for Invalid Date", () => {
    expect(() => toInstantWire(new Date(NaN))).toThrow(RangeError);
  });

  it("toUtcWire gir UTC uten millisekunder", () => {
    expect(toUtcWire(new Date("2026-10-03T10:15:30.789Z"))).toBe(
      "2026-10-03T10:15:30Z"
    );
    expect(toUtcWire(new Date(2026, 0, 15, 0, 0))).toBe("2026-01-14T23:00:00Z");
  });

  it("toDayKey gir lokal dato for query-parametre", () => {
    expect(toDayKey("2026-10-03")).toBe("2026-10-03");
    expect(toDayKey(new Date(2026, 9, 3, 23, 30))).toBe("2026-10-03");
    expect(toDayKey(nextDay("2026-10-24"))).toBe("2026-10-25");
    expect(toDayKey(nextDay("2026-03-28"))).toBe("2026-03-29");
  });

  it("parseEventStatusDate tolker yyyy/MM/dd", () => {
    expect(parseEventStatusDate("2026/10/03")).toEqual(new Date(2026, 9, 3));
    expect(toDayKey(parseEventStatusDate("2026/10/03"))).toBe("2026-10-03");
    expect(Number.isNaN(parseEventStatusDate("2026-10-03").getTime())).toBe(
      true
    );
  });
});

describe("varighet", () => {
  it("durationHours gir timer som desimaltall", () => {
    expect(
      durationHours("2026-01-10T08:00:00.000Z", "2026-01-10T09:30:00.000Z")
    ).toBe(1.5);
    expect(
      durationHours(new Date(2026, 0, 10, 8), new Date(2026, 0, 10, 8))
    ).toBe(0);
  });

  it("durationHours gir null for manglende eller ugyldig verdi", () => {
    expect(durationHours(null, "2026-01-10T09:00:00Z")).toBeNull();
    expect(durationHours("2026-01-10T09:00:00Z", undefined)).toBeNull();
    expect(durationHours("tull", "2026-01-10T09:00:00Z")).toBeNull();
  });

  it("durationHours regner faktisk tid over sommertidsskifte", () => {
    // 25.10.2026 02:00–03:00 kommer to ganger: 00:00–04:00 lokal tid er 5 timer.
    expect(
      durationHours(new Date(2026, 9, 25, 0, 0), new Date(2026, 9, 25, 4, 0))
    ).toBe(5);
  });

  it("formatDuration gir H:MM", () => {
    expect(
      formatDuration("2026-01-10T08:00:00.000Z", "2026-01-10T12:05:00.000Z")
    ).toBe("4:05");
    expect(
      formatDuration("2026-01-10T08:00:00.000Z", "2026-01-10T08:00:00.000Z")
    ).toBe("0:00");
    expect(
      formatDuration("2026-01-10T20:00:00.000Z", "2026-01-11T08:30:00.000Z")
    ).toBe("12:30");
  });

  it("formatDuration runder til nærmeste minutt uten å gi :60", () => {
    expect(
      formatDuration("2026-01-10T08:00:00.000Z", "2026-01-10T08:59:45.000Z")
    ).toBe("1:00");
    expect(
      formatDuration("2026-01-10T08:00:00.000Z", "2026-01-10T08:00:20.000Z")
    ).toBe("0:00");
  });

  it("formatDuration gir tom streng for ugyldig eller negativt tidsrom", () => {
    expect(formatDuration(null, "2026-01-10T08:00:00Z")).toBe("");
    expect(
      formatDuration("2026-01-10T09:00:00.000Z", "2026-01-10T08:00:00.000Z")
    ).toBe("");
  });
});
