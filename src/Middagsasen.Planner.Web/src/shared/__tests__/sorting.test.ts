import { describe, it, expect } from "vitest";
import { compareNb } from "@/shared/sorting";

describe("compareNb", () => {
  it("sorts Norwegian letters after z, in the order æ ø å", () => {
    const sorted = ["Ørn", "Ås", "Æble", "Zebra", "Berg"].sort(compareNb);
    expect(sorted).toEqual(["Berg", "Zebra", "Æble", "Ørn", "Ås"]);
  });

  it("ignores case differences for ordering", () => {
    expect(["kiosk", "Barneheis", "Storheis"].sort(compareNb)).toEqual([
      "Barneheis",
      "kiosk",
      "Storheis",
    ]);
  });
});
