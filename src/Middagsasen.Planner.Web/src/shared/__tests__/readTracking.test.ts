import { describe, it, expect } from "vitest";
import { reachedEnd, requiresScrollToEnd } from "@/shared/readTracking";

describe("requiresScrollToEnd", () => {
  it("krever bla til bunnen for PDF", () => {
    expect(requiresScrollToEnd("application/pdf")).toBe(true);
    expect(requiresScrollToEnd("Application/PDF")).toBe(true);
  });

  it("regner bilder som lest straks de er vist", () => {
    expect(requiresScrollToEnd("image/png")).toBe(false);
    expect(requiresScrollToEnd("image/jpeg")).toBe(false);
    expect(requiresScrollToEnd("")).toBe(false);
  });
});

describe("reachedEnd", () => {
  it("er ikke nådd før sentinelen har vært synlig", () => {
    expect(reachedEnd(false, [])).toBe(false);
    expect(reachedEnd(false, [{ isIntersecting: false }])).toBe(false);
  });

  it("er nådd når sentinelen er synlig", () => {
    expect(
      reachedEnd(false, [{ isIntersecting: false }, { isIntersecting: true }])
    ).toBe(true);
  });

  it("forblir nådd når brukeren blar opp igjen", () => {
    expect(reachedEnd(true, [{ isIntersecting: false }])).toBe(true);
    expect(reachedEnd(true, [])).toBe(true);
  });
});
