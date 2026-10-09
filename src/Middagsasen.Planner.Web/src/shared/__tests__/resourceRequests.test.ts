import { describe, it, expect } from "vitest";
import {
  findInvalidResource,
  toResourceRequests,
  toResourceTemplateRequests,
  visibleResources,
  type EventResourceDraft,
} from "@/shared/resourceRequests";

function draft(
  overrides: Partial<EventResourceDraft> = {}
): EventResourceDraft {
  return {
    id: 1,
    resourceType: { id: 7 },
    startTime: "09:30",
    endTime: "17:00",
    shiftCount: 2,
    isDeleted: false,
    ...overrides,
  };
}

describe("visibleResources", () => {
  it("gir bare vakter som ikke er slettet", () => {
    const kept = draft({ id: 2 });
    const unset = draft({ id: 3, isDeleted: undefined });
    expect(visibleResources([draft({ isDeleted: true }), kept, unset])).toEqual(
      [kept, unset]
    );
  });

  it("gir tom liste når alle vakter er slettet", () => {
    expect(visibleResources([draft({ isDeleted: true })])).toEqual([]);
  });
});

describe("findInvalidResource", () => {
  it("finner første synlige vakt med ugyldig tid", () => {
    const invalid = draft({ id: 2, endTime: "1" });
    expect(
      findInvalidResource([draft(), invalid, draft({ startTime: null })])
    ).toBe(invalid);
  });

  it("gir undefined når alle synlige vakter er gyldige", () => {
    expect(findInvalidResource([draft(), draft({ id: 2 })])).toBeUndefined();
    expect(findInvalidResource([])).toBeUndefined();
  });

  it("ignorerer slettede vakter", () => {
    expect(
      findInvalidResource([draft({ startTime: "1", isDeleted: true })])
    ).toBeUndefined();
  });
});

describe("toResourceRequests", () => {
  it("mapper til request med klokkeslett HH:mm", () => {
    expect(
      toResourceRequests([
        draft({
          startTime: "9:05",
          shiftCount: "3",
          originalShiftCount: 2,
        }),
      ])
    ).toEqual([
      {
        id: 1,
        resourceTypeId: 7,
        startTime: "09:05",
        endTime: "17:00",
        shiftCount: 3,
        originalShiftCount: 2,
        isDeleted: false,
      },
    ]);
  });

  it("sender nye vakter med id null og originalShiftCount null", () => {
    const [request] = toResourceRequests([
      draft({ id: undefined, originalShiftCount: 4 }),
    ]);
    expect(request?.id).toBe(null);
    expect(request?.originalShiftCount).toBe(null);
  });

  it("sender originalShiftCount null for eksisterende vakt uten original", () => {
    const [request] = toResourceRequests([draft({ id: 5 })]);
    expect(request?.originalShiftCount).toBe(null);
  });

  it("sender slettede vakter med id, men ikke nye slettede vakter", () => {
    const requests = toResourceRequests([
      draft({ id: 1, isDeleted: true }),
      draft({ id: undefined, isDeleted: true }),
      draft({ id: null, isDeleted: true }),
      draft({ id: 2 }),
    ]);
    expect(requests.map((r) => [r.id, r.isDeleted])).toEqual([
      [1, true],
      [2, false],
    ]);
  });

  it("kaster for ugyldig tid (valider med findInvalidResource først)", () => {
    expect(() => toResourceRequests([draft({ endTime: "1" })])).toThrow(
      RangeError
    );
  });
});

describe("toResourceTemplateRequests", () => {
  it("mapper uten originalShiftCount", () => {
    expect(
      toResourceTemplateRequests([
        draft({ startTime: "9:05", originalShiftCount: 2 }),
      ])
    ).toEqual([
      {
        id: 1,
        resourceTypeId: 7,
        startTime: "09:05",
        endTime: "17:00",
        shiftCount: 2,
        isDeleted: false,
      },
    ]);
    const [request] = toResourceTemplateRequests([draft()]);
    expect(request).not.toHaveProperty("originalShiftCount");
  });

  it("utelater nye slettede vakter", () => {
    const requests = toResourceTemplateRequests([
      draft({ id: 1, isDeleted: true }),
      draft({ id: undefined, isDeleted: true }),
    ]);
    expect(requests.map((r) => r.id)).toEqual([1]);
  });
});
