import { describe, it, expect } from "vitest";
import { createLatest } from "@/shared/latestRequest";

describe("createLatest", () => {
  it("siste start() er gjeldende", () => {
    const latest = createLatest();
    const token = latest.start();
    expect(latest.isCurrent(token)).toBe(true);
  });

  it("en nyere start() gjør eldre tokens utdaterte", () => {
    const latest = createLatest();
    const first = latest.start();
    const second = latest.start();
    expect(latest.isCurrent(first)).toBe(false);
    expect(latest.isCurrent(second)).toBe(true);
  });

  it("cancel() gjør alle utstedte tokens utdaterte", () => {
    const latest = createLatest();
    const token = latest.start();
    latest.cancel();
    expect(latest.isCurrent(token)).toBe(false);
  });

  it("start() etter cancel() gir et gjeldende token", () => {
    const latest = createLatest();
    latest.start();
    latest.cancel();
    const token = latest.start();
    expect(latest.isCurrent(token)).toBe(true);
  });

  it("hver vakt er uavhengig", () => {
    const a = createLatest();
    const b = createLatest();
    const tokenA = a.start();
    const tokenB = b.start();
    b.cancel();
    expect(a.isCurrent(tokenA)).toBe(true);
    expect(b.isCurrent(tokenB)).toBe(false);
  });
});
