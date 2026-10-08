import { describe, it, expect } from "vitest";
import {
  areValidCompetencyRequirements,
  competencyOptionsFor,
  competencyRequirementErrors,
  formatFacilityWarning,
  newCompetencyRequirementDraft,
  toCompetencyRequirementDrafts,
  toCompetencyRequirementRequests,
  type CompetencyRequirementDraft,
} from "@/shared/competencyRequirements";
import type { CompetencyResponse } from "@/types";

function draft(
  clientKey: string,
  overrides: Partial<CompetencyRequirementDraft> = {}
): CompetencyRequirementDraft {
  return {
    clientKey,
    competencyId: 1,
    competencyName: null,
    minimumRequired: 1,
    ...overrides,
  };
}

function competency(
  id: number,
  name: string,
  inactive = false
): CompetencyResponse {
  return {
    id,
    name,
    hasExpiry: false,
    inactive,
    resourceTypes: [],
    approvers: [],
  };
}

describe("toCompetencyRequirementDrafts", () => {
  it("mapper svaret til skjemarader med unike nøkler", () => {
    const drafts = toCompetencyRequirementDrafts([
      { competencyId: 3, competencyName: "Snøskuterfører", minimumRequired: 1 },
      { competencyId: 4, competencyName: "Førstehjelp", minimumRequired: 2 },
    ]);
    expect(drafts).toMatchObject([
      { competencyId: 3, competencyName: "Snøskuterfører", minimumRequired: 1 },
      { competencyId: 4, competencyName: "Førstehjelp", minimumRequired: 2 },
    ]);
    expect(new Set(drafts.map((d) => d.clientKey)).size).toBe(2);
  });

  it("gir tom liste uten anleggskrav", () => {
    expect(toCompetencyRequirementDrafts(undefined)).toEqual([]);
    expect(toCompetencyRequirementDrafts(null)).toEqual([]);
  });
});

describe("newCompetencyRequirementDraft", () => {
  it("starter uten kompetanse og med antall 1", () => {
    expect(newCompetencyRequirementDraft()).toMatchObject({
      competencyId: null,
      minimumRequired: 1,
    });
  });
});

describe("competencyRequirementErrors", () => {
  it("gir ingen feil for gyldige anleggskrav", () => {
    const drafts = [draft("a"), draft("b", { competencyId: 2 })];
    expect(competencyRequirementErrors(drafts)).toEqual({});
    expect(areValidCompetencyRequirements(drafts)).toBe(true);
    expect(areValidCompetencyRequirements([])).toBe(true);
  });

  it("krever kompetanse", () => {
    const errors = competencyRequirementErrors([
      draft("a", { competencyId: null }),
    ]);
    expect(errors["a"]?.competency).toBe("Velg kompetanse");
  });

  it("gir feil på andre rad med samme kompetanse", () => {
    const errors = competencyRequirementErrors([draft("a"), draft("b")]);
    expect(errors["a"]).toBeUndefined();
    expect(errors["b"]?.competency).toBe("Kompetansen er allerede valgt");
  });

  it.each([0, -1, 1.5, "", null, "abc"])(
    "avviser antall %s",
    (minimumRequired) => {
      const drafts = [draft("a", { minimumRequired })];
      expect(competencyRequirementErrors(drafts)["a"]?.minimumRequired).toBe(
        "Må være et heltall på minst 1"
      );
      expect(areValidCompetencyRequirements(drafts)).toBe(false);
    }
  );

  it("godtar antall som tekst fra q-input", () => {
    expect(
      areValidCompetencyRequirements([draft("a", { minimumRequired: "2" })])
    ).toBe(true);
  });
});

describe("toCompetencyRequirementRequests", () => {
  it("sender hele lista med antall som tall", () => {
    expect(
      toCompetencyRequirementRequests([
        draft("a", { competencyId: 3, minimumRequired: "2" }),
        draft("b", { competencyId: 4, minimumRequired: 1 }),
      ])
    ).toEqual([
      { competencyId: 3, minimumRequired: 2 },
      { competencyId: 4, minimumRequired: 1 },
    ]);
  });

  it("gir tom liste når alle anleggskrav er fjernet", () => {
    expect(toCompetencyRequirementRequests([])).toEqual([]);
  });
});

describe("competencyOptionsFor", () => {
  const competencies = [
    competency(1, "Snøskuterfører"),
    competency(2, "Førstehjelp"),
    competency(3, "Kiosk", true),
  ];

  it("skjuler inaktive og kompetanser valgt i andre rader", () => {
    const current = draft("a", { competencyId: null });
    const other = draft("b", { competencyId: 2 });
    expect(
      competencyOptionsFor(current, [current, other], competencies)
    ).toEqual([{ id: 1, name: "Snøskuterfører" }]);
  });

  it("viser radens egen kompetanse selv om den er inaktiv", () => {
    const current = draft("a", { competencyId: 3 });
    expect(
      competencyOptionsFor(current, [current], competencies).map((o) => o.id)
    ).toEqual([2, 3, 1]);
  });

  it("bruker navnet fra serveren når kompetansen mangler i lista", () => {
    const current = draft("a", {
      competencyId: 9,
      competencyName: "Heisvakt",
    });
    expect(competencyOptionsFor(current, [current], [])).toEqual([
      { id: 9, name: "Heisvakt" },
    ]);
  });
});

describe("formatFacilityWarning", () => {
  it("viser kompetanse, tidsrom og antall", () => {
    expect(
      formatFacilityWarning({
        competencyName: "Snøskuterfører",
        minimumRequired: 1,
        currentCount: 0,
        startTime: "2026-01-10T19:00",
        endTime: "2026-01-10T21:00",
      })
    ).toBe("Snøskuterfører 19:00-21:00: 0 av 1");
  });
});
