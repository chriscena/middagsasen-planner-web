import { describe, it, expect } from "vitest";
import { toDateTime } from "src/shared/eventDateTime";

describe("toDateTime", () => {
  it("parses date and time without start", () => {
    expect(toDateTime("15.01.2026", "10:00")).toEqual(
      new Date(2026, 0, 15, 10, 0)
    );
  });

  it("keeps the same day when the time is after start", () => {
    const start = new Date(2026, 0, 15, 10, 0);
    expect(toDateTime("15.01.2026", "17:00", start)).toEqual(
      new Date(2026, 0, 15, 17, 0)
    );
  });

  it("keeps the same day when the time equals start", () => {
    const start = new Date(2026, 0, 15, 10, 0);
    expect(toDateTime("15.01.2026", "10:00", start)).toEqual(start);
  });

  it("moves to the next day when the time is before start (over midnight)", () => {
    const start = new Date(2026, 0, 15, 22, 0);
    expect(toDateTime("15.01.2026", "02:00", start)).toEqual(
      new Date(2026, 0, 16, 2, 0)
    );
  });

  it("handles month and year boundaries", () => {
    const start = new Date(2026, 11, 31, 20, 0);
    expect(toDateTime("31.12.2026", "01:30", start)).toEqual(
      new Date(2027, 0, 1, 1, 30)
    );
  });

  it("returns Invalid Date for missing or invalid input", () => {
    expect(Number.isNaN(toDateTime(null, "10:00").getTime())).toBe(true);
    expect(Number.isNaN(toDateTime("15.01.2026", null).getTime())).toBe(true);
    expect(
      Number.isNaN(
        toDateTime("15.01.2026", "", new Date(2026, 0, 15, 10)).getTime()
      )
    ).toBe(true);
  });
});
