import { describe, it, expect } from "vitest";
import { isValidTime, toResourceDateTimes } from "src/shared/timeValidation";
import { toDateTime } from "src/shared/eventDateTime";

describe("isValidTime", () => {
  it.each(["10:00", "00:00", "23:59"])("accepts %s", (time) => {
    expect(isValidTime(time)).toBe(true);
  });

  it.each([null, undefined, "", "1", "abc", "25:00", "10:60"])(
    "rejects %s",
    (time) => {
      expect(isValidTime(time)).toBe(false);
    }
  );
});

describe("toResourceDateTimes", () => {
  it("returns start and end on the event date", () => {
    const result = toResourceDateTimes("01.12.2023", "09:30", "17:30");
    expect(result?.start).toEqual(toDateTime("01.12.2023", "09:30"));
    expect(result?.end).toEqual(toDateTime("01.12.2023", "17:30"));
  });

  it("does not move times over midnight (backend decides the day)", () => {
    const result = toResourceDateTimes("01.12.2023", "22:00", "02:00");
    expect(result?.start).toEqual(new Date(2023, 11, 1, 22, 0));
    expect(result?.end).toEqual(new Date(2023, 11, 1, 2, 0));
  });

  it("returns null for invalid start time", () => {
    expect(toResourceDateTimes("01.12.2023", "1", "17:30")).toBe(null);
  });

  it("returns null for invalid or missing end time", () => {
    expect(toResourceDateTimes("01.12.2023", "09:30", "1")).toBe(null);
    expect(toResourceDateTimes("01.12.2023", "09:30", null)).toBe(null);
  });

  it("returns null for invalid or missing date", () => {
    expect(toResourceDateTimes("1.1", "09:30", "17:30")).toBe(null);
    expect(toResourceDateTimes(null, "09:30", "17:30")).toBe(null);
  });
});
