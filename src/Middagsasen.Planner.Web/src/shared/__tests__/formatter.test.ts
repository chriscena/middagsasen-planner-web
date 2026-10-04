import { describe, it, expect } from "vitest";
import { formatHours, formatNumber } from "@/shared/formatter";

describe("formatNumber", () => {
  it("formats with comma and one decimal by default", () => {
    expect(formatNumber(1.25)).toBe("1,3");
    expect(formatNumber(7)).toBe("7,0");
    expect(formatNumber(2.5, 2)).toBe("2,50");
  });

  it("returns a dash for missing values instead of throwing", () => {
    expect(formatNumber(null)).toBe("–");
    expect(formatNumber(undefined)).toBe("–");
    expect(formatNumber(NaN)).toBe("–");
  });
});

describe("formatHours", () => {
  it("adds the unit", () => {
    expect(formatHours(7.5)).toBe("7,5 t");
    expect(formatHours(0)).toBe("0,0 t");
  });

  it("returns only a dash when hours are missing", () => {
    expect(formatHours(null)).toBe("–");
    expect(formatHours(undefined)).toBe("–");
  });
});
