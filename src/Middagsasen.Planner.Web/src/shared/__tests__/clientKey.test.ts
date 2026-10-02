import { describe, it, expect } from "vitest";
import { newClientKey } from "src/shared/clientKey";

describe("newClientKey", () => {
  it("uses the existing id", () => {
    expect(newClientKey(42)).toBe("id-42");
  });

  it("creates unique keys without id", () => {
    const keys = new Set([
      newClientKey(),
      newClientKey(null),
      newClientKey(0),
      newClientKey(undefined),
    ]);
    expect(keys.size).toBe(4);
    expect([...keys].some((k) => k.startsWith("id-"))).toBe(false);
  });
});
