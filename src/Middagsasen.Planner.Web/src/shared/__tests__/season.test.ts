import { describe, it, expect } from "vitest";
import { getSeasonStartYear } from "@/shared/season";

describe("getSeasonStartYear", () => {
  it("returns previous season for 30 June 23:30 local time", () => {
    expect(getSeasonStartYear(new Date(2026, 5, 30, 23, 30))).toBe(2025);
  });

  it("returns new season for 1 July 00:30 local time", () => {
    expect(getSeasonStartYear(new Date(2026, 6, 1, 0, 30))).toBe(2026);
  });

  it("handles January and December", () => {
    expect(getSeasonStartYear(new Date(2027, 0, 15))).toBe(2026);
    expect(getSeasonStartYear(new Date(2026, 11, 31, 23, 59))).toBe(2026);
  });

  it("accepts ISO strings", () => {
    const local = new Date(2026, 6, 1, 0, 30);
    expect(getSeasonStartYear(local.toISOString())).toBe(2026);
  });

  it("returns null for missing or invalid values", () => {
    expect(getSeasonStartYear(null)).toBeNull();
    expect(getSeasonStartYear(undefined)).toBeNull();
    expect(getSeasonStartYear("ikke en dato")).toBeNull();
  });
});
