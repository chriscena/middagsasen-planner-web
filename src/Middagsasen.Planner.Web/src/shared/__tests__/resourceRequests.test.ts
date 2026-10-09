import { describe, it, expect } from "vitest";
import {
  findInvalidResource,
  invalidResourceMessage,
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
  it("gir bare oppgaver som ikke er slettet", () => {
    const kept = draft({ id: 2 });
    const unset = draft({ id: 3, isDeleted: undefined });
    expect(visibleResources([draft({ isDeleted: true }), kept, unset])).toEqual(
      [kept, unset]
    );
  });

  it("gir tom liste når alle oppgaver er slettet", () => {
    expect(visibleResources([draft({ isDeleted: true })])).toEqual([]);
  });
});

describe("findInvalidResource", () => {
  it("finner første synlige oppgave med ugyldig tid", () => {
    const invalid = draft({ id: 2, endTime: "1" });
    expect(
      findInvalidResource([draft(), invalid, draft({ startTime: null })])
    ).toBe(invalid);
  });

  it("gir undefined når alle synlige oppgaver er gyldige", () => {
    expect(findInvalidResource([draft(), draft({ id: 2 })])).toBeUndefined();
    expect(findInvalidResource([])).toBeUndefined();
  });

  it("ignorerer slettede oppgaver", () => {
    expect(
      findInvalidResource([draft({ startTime: "1", isDeleted: true })])
    ).toBeUndefined();
  });
});

describe("invalidResourceMessage", () => {
  it("navngir vakttypen og tar med tidene", () => {
    expect(
      invalidResourceMessage({
        resourceType: { name: "Storheis" },
        startTime: "18:00",
        endTime: "1",
      })
    ).toBe("Oppgaven «Storheis» (18:00–1) har ugyldig start- eller sluttid.");
  });

  it("håndterer manglende vakttype og tomme tider", () => {
    expect(
      invalidResourceMessage({
        resourceType: null,
        startTime: " ",
        endTime: null,
      })
    ).toBe("En oppgave (?–?) har ugyldig start- eller sluttid.");
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

  it("sender nye oppgaver med id null og originalShiftCount null", () => {
    const [request] = toResourceRequests([
      draft({ id: undefined, originalShiftCount: 4 }),
    ]);
    expect(request?.id).toBe(null);
    expect(request?.originalShiftCount).toBe(null);
  });

  it("sender originalShiftCount null for eksisterende oppgave uten original", () => {
    const [request] = toResourceRequests([draft({ id: 5 })]);
    expect(request?.originalShiftCount).toBe(null);
  });

  it("sender slettede oppgaver med id, men ikke nye slettede oppgaver", () => {
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

  it("utelater nye slettede oppgaver", () => {
    const requests = toResourceTemplateRequests([
      draft({ id: 1, isDeleted: true }),
      draft({ id: undefined, isDeleted: true }),
    ]);
    expect(requests.map((r) => r.id)).toEqual([1]);
  });
});
