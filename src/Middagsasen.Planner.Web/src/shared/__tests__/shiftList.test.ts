import { describe, it, expect } from "vitest";
import { createShiftList } from "@/shared/shiftList";
import type { ShiftResponse } from "@/types";

function shift(id: number): ShiftResponse {
  return {
    id,
    eventResourceId: 1,
    user: { id: id * 10 } as ShiftResponse["user"],
    needsTraining: false,
    isMine: false,
    canEdit: false,
    canWithdraw: false,
    canConfirmTraining: false,
  };
}

describe("createShiftList", () => {
  it("fills up with vacant slots to minimumStaff", () => {
    const list = createShiftList({
      id: 7,
      minimumStaff: 3,
      shifts: [shift(5)],
    });
    expect(list.map((e) => e.key)).toEqual([
      "shift-5",
      "empty-7-0",
      "empty-7-1",
    ]);
    expect(list[1]?.shift).toEqual({ id: 0, user: null, comment: null });
  });

  it("gives unique keys for all rows", () => {
    const list = createShiftList({ id: 2, minimumStaff: 4, shifts: [] });
    expect(new Set(list.map((e) => e.key)).size).toBe(4);
  });

  it("does not add vacant slots when staffed", () => {
    const list = createShiftList({
      id: 1,
      minimumStaff: 1,
      shifts: [shift(1), shift(2)],
    });
    expect(list.map((e) => e.key)).toEqual(["shift-1", "shift-2"]);
  });

  it("keeps the shift object unchanged (no key on it)", () => {
    const s = shift(3);
    const list = createShiftList({ id: 1, minimumStaff: 0, shifts: [s] });
    expect(list[0]?.shift).toBe(s);
  });
});
