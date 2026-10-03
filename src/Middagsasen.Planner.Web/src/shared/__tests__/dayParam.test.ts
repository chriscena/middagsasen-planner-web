import { describe, it, expect } from "vitest";
import { parseDayParam } from "src/shared/dayParam";

describe("parseDayParam", () => {
  it("godtar gyldig yyyy-MM-dd", () => {
    expect(parseDayParam("2026-10-03")).toBe("2026-10-03");
    expect(parseDayParam("2028-02-29")).toBe("2028-02-29");
  });

  it("avviser ugyldige datoer og andre formater", () => {
    expect(parseDayParam("2026-02-30")).toBeNull();
    expect(parseDayParam("2026-13-01")).toBeNull();
    expect(parseDayParam("2026-1-3")).toBeNull();
    expect(parseDayParam("03.10.2026")).toBeNull();
    expect(parseDayParam("tull")).toBeNull();
    expect(parseDayParam("")).toBeNull();
    expect(parseDayParam(undefined)).toBeNull();
  });
});
